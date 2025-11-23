using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 三次樣條插值利率曲線 (Cubic Spline Interpolation)
/// 使用自然邊界條件的三次樣條平滑利率節點。
/// 
/// 特性：
/// - 連續一階與二階導數 (平滑)
/// - 可避免過度震盪的插值行為
/// - 自然邊界：兩端第二導數 = 0
/// 
/// 目的：
/// 在給定離散 Tenor/ZeroRate 節點下，生成平滑且可微的零利率曲線，方便取得中間期限利率、折現因子與遠期利率。
/// </summary>
public sealed class CubicSplineCurve : IZeroCurve
{
    private readonly CurvePoint[] _points;
    private readonly double[] _secondDerivatives; // 第二導數 (樣條節點處的二階導數值)

    public string CurveName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立三次樣條利率曲線
    /// </summary>
    public CubicSplineCurve(
        string curveName,
        DateTime referenceDate,
        IEnumerable<CurvePoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);
        ArgumentNullException.ThrowIfNull(points);

        CurveName = curveName;
        ReferenceDate = referenceDate;

        // 排序節點 (依 Tenor 由小到大)
        _points = points.OrderBy(p => p.Tenor).ToArray();

        if (_points.Length < 3)
            throw new ArgumentException("三次樣條至少需要 3 個節點", nameof(points));

        // 驗證所有節點
        for (int i = 0; i < _points.Length; i++)
        {
            if (!_points[i].IsValid())
                throw new ArgumentException($"節點 {i} 無效: {_points[i]}");

            if (i > 0 && Math.Abs(_points[i].Tenor - _points[i - 1].Tenor) < 1e-10)
                throw new ArgumentException($"Tenor 重複: {_points[i].Tenor}");
        }

        // 計算三次樣條第二導數
        _secondDerivatives = ComputeSecondDerivatives();
    }

    /// <summary>
    /// 計算樣條節點第二導數陣列 (採自然樣條邊界條件, 兩端二階導數=0)
    /// 使用解三對角線系統的 Thomas 法。
    /// </summary>
    private double[] ComputeSecondDerivatives()
    {
        int n = _points.Length;
        double[] y2 = new double[n];

        if (n == 2)
        {
            // 若僅兩節點則退化為線性插值 (二階導數=0)
            y2[0] = 0;
            y2[1] = 0;
            return y2;
        }

        // 自然邊界條件：端點二階導數 = 0
        double[] u = new double[n - 1];

        // 初始化首端
        y2[0] = 0;
        u[0] = 0;

        // 前行消去
        for (int i = 1; i < n - 1; i++)
        {
            double sig = (_points[i].Tenor - _points[i - 1].Tenor) /
                        (_points[i + 1].Tenor - _points[i - 1].Tenor);

            double p = sig * y2[i - 1] + 2.0;

            y2[i] = (sig - 1.0) / p;

            double dy1 = (_points[i + 1].ZeroRate - _points[i].ZeroRate) /
                        (_points[i + 1].Tenor - _points[i].Tenor);
            double dy0 = (_points[i].ZeroRate - _points[i - 1].ZeroRate) /
                        (_points[i].Tenor - _points[i - 1].Tenor);

            u[i] = (6.0 * (dy1 - dy0) / (_points[i + 1].Tenor - _points[i - 1].Tenor) - sig * u[i - 1]) / p;
        }

        // 設定尾端二階導數 (自然邊界)
        y2[n - 1] = 0;

        // 回代求解
        for (int i = n - 2; i >= 0; i--)
        {
            y2[i] = y2[i] * y2[i + 1] + u[i];
        }

        return y2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetZeroRate(double timeInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeInYears);

        // 左側外插: 若查詢期限 < 最小 Tenor，回傳第一節點利率
        if (timeInYears <= _points[0].Tenor)
            return _points[0].ZeroRate;

        if (timeInYears >= _points[^1].Tenor)
            return _points[^1].ZeroRate;

        // 二分搜尋找到所在區間索引
        int index = FindIntervalIndex(timeInYears);

        // 三次樣條插值
        return InterpolateCubicSpline(timeInYears, index);
    }

    /// <summary>
    /// 三次樣條插值核心計算
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InterpolateCubicSpline(double t, int index)
    {
        double t0 = _points[index].Tenor;
        double t1 = _points[index + 1].Tenor;
        double r0 = _points[index].ZeroRate;
        double r1 = _points[index + 1].ZeroRate;
        double y20 = _secondDerivatives[index];
        double y21 = _secondDerivatives[index + 1];

        double h = t1 - t0;
        double a = (t1 - t) / h;
        double b = (t - t0) / h;

        double result = a * r0 + b * r1 +
                       ((a * a * a - a) * y20 + (b * b * b - b) * y21) * (h * h) / 6.0;

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDiscountFactor(double timeInYears)
    {
        double zeroRate = GetZeroRate(timeInYears);
        return Math.Exp(-zeroRate * timeInYears);
    }

    public double GetZeroRate(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不可早於基準日", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return GetZeroRate(timeInYears);
    }

    public double GetDiscountFactor(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不可早於基準日", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return GetDiscountFactor(timeInYears);
    }

    public double GetForwardRate(double startTime, double endTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startTime);
        ArgumentOutOfRangeException.ThrowIfNegative(endTime);

        if (endTime <= startTime)
            throw new ArgumentException("結束時間必須大於起始時間");

        // 遠期利率公式：f(T1,T2) = [r(T2)*T2 - r(T1)*T1] / (T2 - T1)
        // 來源：連續複利零利率之無套利關係
        // r(T) 為年化零利率 (continuous compounding)
        double r1 = GetZeroRate(startTime); // 起始點零利率 r(T1)
        double r2 = GetZeroRate(endTime);   // 結束點零利率 r(T2)

        double forwardRate = (r2 * endTime - r1 * startTime) / (endTime - startTime);
        return forwardRate; // 返回年化遠期利率
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double MinTenor, double MaxTenor) GetValidRange()
    {
        return (_points[0].Tenor, _points[^1].Tenor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindIntervalIndex(double timeInYears)
    {
        int left = 0;
        int right = _points.Length - 2;

        while (left < right)
        {
            int mid = (left + right) / 2;

            if (timeInYears < _points[mid + 1].Tenor)
                right = mid;
            else
                left = mid + 1;
        }

        return left;
    }

    public IReadOnlyList<CurvePoint> GetPoints() => _points;

    public override string ToString()
    {
        return $"CubicSplineCurve({CurveName}, {_points.Length} points, " +
               $"range: [{_points[0].Tenor:F2}Y - {_points[^1].Tenor:F2}Y])";
    }

    /// <summary>
    /// 建立標準化樣條曲線 (示例用預設節點)
    /// </summary>
    public static CubicSplineCurve CreateStandardCurve(
        string curveName,
        double rate1M,
        double rate3M,
        double rate6M,
        double rate1Y,
        double rate2Y)
    {
        var points = new[]
        {
            new CurvePoint(1.0 / 12.0, rate1M),   // 1M
            new CurvePoint(3.0 / 12.0, rate3M),   // 3M
            new CurvePoint(6.0 / 12.0, rate6M),   // 6M
            new CurvePoint(1.0, rate1Y),          // 1Y
            new CurvePoint(2.0, rate2Y)           // 2Y
        };

        return new CubicSplineCurve(curveName, DateTime.Today, points);
    }
}
