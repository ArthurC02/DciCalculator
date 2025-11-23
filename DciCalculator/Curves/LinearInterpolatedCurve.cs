using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 線性插值零息曲線
/// 在節點間使用線性插值計算利率
/// 
/// 插值方法：
/// r(t) = r1 + (r2 - r1) * (t - t1) / (t2 - t1)
/// 其中 t1 ? t ? t2
/// </summary>
public sealed class LinearInterpolatedCurve : IZeroCurve
{
    private readonly CurvePoint[] _points;

    public string CurveName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立線性插值曲線
    /// </summary>
    /// <param name="curveName">曲線名稱</param>
    /// <param name="referenceDate">基準日期</param>
    /// <param name="points">曲線節點（必須按 Tenor 排序）</param>
    public LinearInterpolatedCurve(
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

        if (_points.Length < 2)
            throw new ArgumentException("至少需要 2 個曲線節點", nameof(points));

        // 驗證所有節點
        for (int i = 0; i < _points.Length; i++)
        {
            if (!_points[i].IsValid())
                throw new ArgumentException($"節點 {i} 無效: {_points[i]}");

            // 檢查是否有重複 Tenor
            if (i > 0 && Math.Abs(_points[i].Tenor - _points[i - 1].Tenor) < 1e-10)
                throw new ArgumentException($"Tenor 重複: {_points[i].Tenor}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetZeroRate(double timeInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeInYears);

        // 邊界情況
        if (timeInYears <= _points[0].Tenor)
            return _points[0].ZeroRate; // 外推（使用第一個節點）

        if (timeInYears >= _points[^1].Tenor)
            return _points[^1].ZeroRate; // 外推（使用最後一個節點）

        // 二分搜尋找到插值區間
        int index = FindIntervalIndex(timeInYears);

        // 線性插值
        var p1 = _points[index];
        var p2 = _points[index + 1];

        double weight = (timeInYears - p1.Tenor) / (p2.Tenor - p1.Tenor);
        double interpolatedRate = p1.ZeroRate + weight * (p2.ZeroRate - p1.ZeroRate);

        return interpolatedRate;
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

        // Forward Rate 公式：
        // f(t1, t2) = [r(t2)*t2 - r(t1)*t1] / (t2 - t1)
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

    /// <summary>
    /// 二分搜尋找到插值區間
    /// 返回 index 使得 points[index].Tenor ? t < points[index+1].Tenor
    /// </summary>
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

    /// <summary>
    /// 取得所有曲線節點（唯讀）
    /// </summary>
    public IReadOnlyList<CurvePoint> GetPoints() => _points;

    public override string ToString()
    {
        return $"LinearInterpolatedCurve({CurveName}, {_points.Length} points, " +
               $"range: [{_points[0].Tenor:F2}Y - {_points[^1].Tenor:F2}Y])";
    }

    /// <summary>
    /// 建立標準期限曲線（測試用）
    /// </summary>
    public static LinearInterpolatedCurve CreateStandardCurve(
        string curveName,
        double rate1M,
        double rate3M,
        double rate6M,
        double rate1Y)
    {
        var points = new[]
        {
            new CurvePoint(1.0 / 12.0, rate1M),   // 1M
            new CurvePoint(3.0 / 12.0, rate3M),   // 3M
            new CurvePoint(6.0 / 12.0, rate6M),   // 6M
            new CurvePoint(1.0, rate1Y)           // 1Y
        };

        return new LinearInterpolatedCurve(curveName, DateTime.Today, points);
    }
}
