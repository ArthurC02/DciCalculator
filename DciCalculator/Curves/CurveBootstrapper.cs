using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 曲線展期/抽取工具 (Curve Bootstrapper)
/// 由市場工具逐步構建零利率曲線。
/// 
/// 流程：
/// 1. 收集並排序輸入工具 (Tenor 由小至大)
/// 2. 驗證資料與基準日一致
/// 3. Deposit：閉式計算 Zero Rate
/// 4. Swap：平價條件反推末端 DF，必要時改用 Newton-Raphson 迭代
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
    /// 執行展期主程序
    /// </summary>
    /// <param name="instruments">市場工具集合</param>
    /// <param name="interpolationMethod">插值方法</param>
    /// <returns>零利率曲線</returns>
    public IZeroCurve Bootstrap(
        IEnumerable<MarketInstrument> instruments,
        InterpolationMethod interpolationMethod = InterpolationMethod.Linear)
    {
        ArgumentNullException.ThrowIfNull(instruments);

        var instrumentList = instruments.OrderBy(i => i.Tenor).ToList();

        if (instrumentList.Count == 0)
            throw new ArgumentException("至少需要一個市場工具", nameof(instruments));

        // 驗證輸入工具
        ValidateInstruments(instrumentList);

        // 累積生成的節點
        var curvePoints = new List<CurvePoint>();

        // 逐步展期/抽取
        for (int i = 0; i < instrumentList.Count; i++)
        {
            var instrument = instrumentList[i];

            // 依工具類型選擇展期方式
            CurvePoint point = instrument.InstrumentType switch
            {
                MarketInstrumentType.Deposit => BootstrapDeposit((DepositInstrument)instrument),
                MarketInstrumentType.Swap => BootstrapSwap(
                    (SwapInstrument)instrument,
                    CreateIntermediateCurve(curvePoints, interpolationMethod)
                ),
                _ => throw new NotSupportedException($"未知市場工具類型: {instrument.InstrumentType}")
            };

            curvePoints.Add(point);
        }

        // �إ̲߳צ��u
        return CreateFinalCurve(curvePoints, interpolationMethod);
    }

    /// <summary>
    /// 展期 Deposit (閉式 Zero Rate)
    /// </summary>
    private CurvePoint BootstrapDeposit(DepositInstrument deposit)
    {
        double tenor = deposit.Tenor;
        double zeroRate = deposit.CalculateZeroRate();

        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// 展期 Swap (閉式折現因子推導，若失敗則迭代)
    /// </summary>
    private CurvePoint BootstrapSwap(SwapInstrument swap, IZeroCurve existingCurve)
    {
        double tenor = swap.Tenor;

        // 方法一：閉式推導末期 DF
        try
        {
            double impliedDF = swap.CalculateImpliedDiscountFactor(existingCurve);
            
            // 折現因子轉換成 Zero Rate
            var point = CurvePoint.FromDiscountFactor(tenor, impliedDF);
            
            return point;
        }
        catch
        {
            // 方法二：Newton-Raphson 迭代
            return BootstrapSwapIterative(swap, existingCurve);
        }
    }

    /// <summary>
    /// 使用 Newton-Raphson 迭代展期 Swap
    /// </summary>
    private CurvePoint BootstrapSwapIterative(SwapInstrument swap, IZeroCurve existingCurve)
    {
        double tenor = swap.Tenor;
        
        // 初始猜測：使用 Swap Rate 當零利率
        double zeroRate = swap.MarketQuote;

        const int maxIterations = 20;
        const double tolerance = 1e-8;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 暫時線性曲線：加入末端節點
            var testPoints = existingCurve is LinearInterpolatedCurve linCurve
                ? linCurve.GetPoints().ToList()
                : new List<CurvePoint>();

            testPoints.Add(new CurvePoint(tenor, zeroRate));

            var testCurve = new LinearInterpolatedCurve(_curveName, _referenceDate, testPoints);

            // 計算現值 (目標近 0)
            double pv = swap.CalculatePresentValue(testCurve);

            if (Math.Abs(pv) < tolerance)
            {
                return new CurvePoint(tenor, zeroRate);
            }

            // 計算敏感度 Jacobian
            double jacobian = swap.CalculateJacobian(testCurve, tenor);

            if (Math.Abs(jacobian) < 1e-12)
                break;

            // Newton-Raphson 更新
            zeroRate -= pv / jacobian;

            // 限制更新範圍避免發散
            zeroRate = Math.Clamp(zeroRate, -0.10, 0.50);
        }

        // 迭代失敗：回傳最後近似值
        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// 建立中間過渡曲線 (供後續展期引用)
    /// </summary>
    private IZeroCurve CreateIntermediateCurve(
        List<CurvePoint> points,
        InterpolationMethod method)
    {
        if (points.Count == 0)
        {
            // 無節點：回傳預設平坦曲線
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
    /// 依插值方法建立最終曲線
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
    /// 驗證市場工具集合
    /// </summary>
    private void ValidateInstruments(List<MarketInstrument> instruments)
    {
        for (int i = 0; i < instruments.Count; i++)
        {
            var instrument = instruments[i];

            if (instrument.StartDate != _referenceDate)
                throw new ArgumentException($"工具 {i} 起始日不等於基準日");

            if (i > 0 && instrument.Tenor <= instruments[i - 1].Tenor)
                throw new ArgumentException($"工具 {i} Tenor 未遞增");
        }
    }

    /// <summary>
    /// 建立標準示例曲線 (依市場報價)
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
                // 短天期：使用 Deposit
                instruments.Add(DepositInstrument.Create(referenceDate, tenor, rate));
            }
            else
            {
                // 較長天期：使用 Swap
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
            _ => throw new ArgumentException($"未知 Tenor 單位: {tenor}")
        };
    }
}
