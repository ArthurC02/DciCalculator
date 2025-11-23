using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 曲線建構器（Curve Bootstrapper）
/// 從市場工具建構零息利率曲線
/// 
/// Bootstrap 演算法：
/// 1. 按期限排序工具
/// 2. 從短期到長期依序求解
/// 3. Deposit → 直接計算 Zero Rate
/// 4. Swap → 迭代求解（使用已知的短期曲線）
/// </summary>
public sealed class CurveBootstrapper
{
    private readonly string _curveName;
    private readonly DateTime _referenceDate;

    public CurveBootstrapper(string curveName, DateTime referenceDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);
        _curveName = curveName;
        _referenceDate = referenceDate;
    }

    /// <summary>
    /// Bootstrap 主方法
    /// </summary>
    /// <param name="instruments">市場工具集合</param>
    /// <param name="interpolationMethod">插值方法</param>
    /// <returns>建構完成的零息曲線</returns>
    public IZeroCurve Bootstrap(
        IEnumerable<MarketInstrument> instruments,
        InterpolationMethod interpolationMethod = InterpolationMethod.Linear)
    {
        ArgumentNullException.ThrowIfNull(instruments);

        var instrumentList = instruments.OrderBy(i => i.Tenor).ToList();

        if (instrumentList.Count == 0)
            throw new ArgumentException("至少需要一個市場工具", nameof(instruments));

        // 驗證工具
        ValidateInstruments(instrumentList);

        // 建構曲線節點
        var curvePoints = new List<CurvePoint>();

        // 逐步 Bootstrap
        for (int i = 0; i < instrumentList.Count; i++)
        {
            var instrument = instrumentList[i];

            // 根據工具類型選擇方法
            CurvePoint point = instrument.InstrumentType switch
            {
                MarketInstrumentType.Deposit => BootstrapDeposit((DepositInstrument)instrument),
                MarketInstrumentType.Swap => BootstrapSwap(
                    (SwapInstrument)instrument,
                    CreateIntermediateCurve(curvePoints, interpolationMethod)
                ),
                _ => throw new NotSupportedException($"不支援的工具類型: {instrument.InstrumentType}")
            };

            curvePoints.Add(point);
        }

        // 建立最終曲線
        return CreateFinalCurve(curvePoints, interpolationMethod);
    }

    /// <summary>
    /// Bootstrap Deposit（直接計算）
    /// </summary>
    private CurvePoint BootstrapDeposit(DepositInstrument deposit)
    {
        double tenor = deposit.Tenor;
        double zeroRate = deposit.CalculateZeroRate();

        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// Bootstrap Swap（迭代求解）
    /// </summary>
    private CurvePoint BootstrapSwap(SwapInstrument swap, IZeroCurve existingCurve)
    {
        double tenor = swap.Tenor;

        // 方法 1：直接計算隱含 DF（簡化，假設已知所有短期點）
        try
        {
            double impliedDF = swap.CalculateImpliedDiscountFactor(existingCurve);
            
            // 從 DF 反推 Zero Rate
            var point = CurvePoint.FromDiscountFactor(tenor, impliedDF);
            
            return point;
        }
        catch
        {
            // 方法 2：Newton-Raphson 迭代
            return BootstrapSwapIterative(swap, existingCurve);
        }
    }

    /// <summary>
    /// 使用 Newton-Raphson 迭代求解 Swap
    /// </summary>
    private CurvePoint BootstrapSwapIterative(SwapInstrument swap, IZeroCurve existingCurve)
    {
        double tenor = swap.Tenor;
        
        // 初始猜測：使用 Swap Rate 作為 Zero Rate
        double zeroRate = swap.MarketQuote;

        const int maxIterations = 20;
        const double tolerance = 1e-8;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 建立測試曲線（簡化：在最後一期加上新節點）
            var testPoints = existingCurve is LinearInterpolatedCurve linCurve
                ? linCurve.GetPoints().ToList()
                : new List<CurvePoint>();

            testPoints.Add(new CurvePoint(tenor, zeroRate));

            var testCurve = new LinearInterpolatedCurve(_curveName, _referenceDate, testPoints);

            // 計算 PV（應該接近 0）
            double pv = swap.CalculatePresentValue(testCurve);

            if (Math.Abs(pv) < tolerance)
            {
                return new CurvePoint(tenor, zeroRate);
            }

            // 計算 Jacobian
            double jacobian = swap.CalculateJacobian(testCurve, tenor);

            if (Math.Abs(jacobian) < 1e-12)
                break;

            // Newton-Raphson 更新
            zeroRate -= pv / jacobian;

            // 限制更新幅度
            zeroRate = Math.Clamp(zeroRate, -0.10, 0.50);
        }

        // 未收斂，使用最後的估計值
        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// 建立中間曲線（用於迭代）
    /// </summary>
    private IZeroCurve CreateIntermediateCurve(
        List<CurvePoint> points,
        InterpolationMethod method)
    {
        if (points.Count == 0)
        {
            // 預設平坦曲線
            return new FlatZeroCurve(_curveName, _referenceDate, 0.01);
        }

        if (points.Count == 1)
        {
            return new FlatZeroCurve(_curveName, _referenceDate, points[0].ZeroRate);
        }

        return method switch
        {
            InterpolationMethod.Linear => new LinearInterpolatedCurve(_curveName, _referenceDate, points),
            InterpolationMethod.CubicSpline when points.Count >= 3 => 
                new CubicSplineCurve(_curveName, _referenceDate, points),
            _ => new LinearInterpolatedCurve(_curveName, _referenceDate, points)
        };
    }

    /// <summary>
    /// 建立最終曲線
    /// </summary>
    private IZeroCurve CreateFinalCurve(
        List<CurvePoint> points,
        InterpolationMethod method)
    {
        return method switch
        {
            InterpolationMethod.Linear => new LinearInterpolatedCurve(_curveName, _referenceDate, points),
            InterpolationMethod.CubicSpline when points.Count >= 3 => 
                new CubicSplineCurve(_curveName, _referenceDate, points),
            InterpolationMethod.Flat => new FlatZeroCurve(_curveName, _referenceDate, points[0].ZeroRate),
            _ => new LinearInterpolatedCurve(_curveName, _referenceDate, points)
        };
    }

    /// <summary>
    /// 驗證工具集合
    /// </summary>
    private void ValidateInstruments(List<MarketInstrument> instruments)
    {
        for (int i = 0; i < instruments.Count; i++)
        {
            var instrument = instruments[i];

            if (instrument.StartDate != _referenceDate)
                throw new ArgumentException($"工具 {i} 起始日不等於基準日");

            if (i > 0 && instrument.Tenor <= instruments[i - 1].Tenor)
                throw new ArgumentException($"工具 {i} 期限不遞增");
        }
    }

    /// <summary>
    /// 建立標準市場曲線（快速方法）
    /// </summary>
    public static IZeroCurve BuildStandardCurve(
        string curveName,
        DateTime referenceDate,
        Dictionary<string, double> marketQuotes)
    {
        var bootstrapper = new CurveBootstrapper(curveName, referenceDate);
        var instruments = new List<MarketInstrument>();

        foreach (var (tenor, rate) in marketQuotes.OrderBy(kv => ParseTenorToYears(kv.Key)))
        {
            double years = ParseTenorToYears(tenor);

            if (years <= 1.0)
            {
                // 短期：使用 Deposit
                instruments.Add(DepositInstrument.Create(referenceDate, tenor, rate));
            }
            else
            {
                // 長期：使用 Swap
                instruments.Add(SwapInstrument.Create(referenceDate, tenor, rate));
            }
        }

        return bootstrapper.Bootstrap(instruments);
    }

    private static double ParseTenorToYears(string tenor)
    {
        char unit = tenor[^1];
        int value = int.Parse(tenor[..^1]);

        return unit switch
        {
            'M' => value / 12.0,
            'Y' => value,
            _ => throw new ArgumentException($"無效的 Tenor: {tenor}")
        };
    }
}
