using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 三次樣條插值零息曲線（Cubic Spline Interpolation）
/// 使用自然邊界條件的三次樣條進行插值
/// 
/// 優點：
/// - 曲線更平滑（一階和二階導數連續）
/// - 避免線性插值的「折點」
/// - 更符合實務市場曲線形狀
/// 
/// 自然邊界條件：
/// - 曲線兩端的二階導數為 0
/// </summary>
public sealed class CubicSplineCurve : IZeroCurve
{
    private readonly CurvePoint[] _points;
    private readonly double[] _secondDerivatives; // 二階導數（係數）

    public string CurveName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立三次樣條插值曲線
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

        // 排序並驗證節點
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

        // 計算三次樣條係數
        _secondDerivatives = ComputeSecondDerivatives();
    }

    /// <summary>
    /// 計算三次樣條的二階導數（使用 Thomas 演算法求解三對角矩陣）
    /// </summary>
    private double[] ComputeSecondDerivatives()
    {
        int n = _points.Length;
        double[] y2 = new double[n];

        if (n == 2)
        {
            // 只有兩個點，退化為線性
            y2[0] = 0;
            y2[1] = 0;
            return y2;
        }

        // 使用自然邊界條件（端點二階導數 = 0）
        double[] u = new double[n - 1];

        // 設定邊界條件
        y2[0] = 0;
        u[0] = 0;

        // 向前消元
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

        // 設定右邊界條件
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

        // 邊界情況：外推
        if (timeInYears <= _points[0].Tenor)
            return _points[0].ZeroRate;

        if (timeInYears >= _points[^1].Tenor)
            return _points[^1].ZeroRate;

        // 找到插值區間
        int index = FindIntervalIndex(timeInYears);

        // 三次樣條插值
        return InterpolateCubicSpline(timeInYears, index);
    }

    /// <summary>
    /// 三次樣條插值公式
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
            throw new ArgumentException("日期不能早於基準日期", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return GetZeroRate(timeInYears);
    }

    public double GetDiscountFactor(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不能早於基準日期", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return GetDiscountFactor(timeInYears);
    }

    public double GetForwardRate(double startTime, double endTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startTime);
        ArgumentOutOfRangeException.ThrowIfNegative(endTime);

        if (endTime <= startTime)
            throw new ArgumentException("結束時間必須 > 起始時間");

        double r1 = GetZeroRate(startTime);
        double r2 = GetZeroRate(endTime);

        double forwardRate = (r2 * endTime - r1 * startTime) / (endTime - startTime);

        return forwardRate;
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
    /// 建立標準期限曲線（測試用）
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
