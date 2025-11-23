using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 線性插值零利率曲線
/// 於節點間以線性方式估算中間期限零利率。
/// 
/// 公式：r(t) = r1 + (r2 - r1) * (t - t1) / (t2 - t1), t1 ≤ t ≤ t2
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
    /// <param name="referenceDate">基準日</param>
    /// <param name="points">節點集合 (Tenor / ZeroRate)</param>
    public LinearInterpolatedCurve(
        string curveName,
        DateTime referenceDate,
        IEnumerable<CurvePoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);
        ArgumentNullException.ThrowIfNull(points);

        CurveName = curveName;
        ReferenceDate = referenceDate;

        // 節點排序
        _points = points.OrderBy(p => p.Tenor).ToArray();

        if (_points.Length < 2)
            throw new ArgumentException("至少需要 2 個節點", nameof(points));

        // 驗證節點
        for (int i = 0; i < _points.Length; i++)
        {
            if (!_points[i].IsValid())
                throw new ArgumentException($"節點 {i} 無效: {_points[i]}");

            // 檢查 Tenor 重複
            if (i > 0 && Math.Abs(_points[i].Tenor - _points[i - 1].Tenor) < 1e-10)
                throw new ArgumentException($"Tenor 重複: {_points[i].Tenor}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetZeroRate(double timeInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeInYears);

        // 左側外推：使用首節點利率
        if (timeInYears <= _points[0].Tenor)
            return _points[0].ZeroRate; // 左側界外直接取首節點利率

        if (timeInYears >= _points[^1].Tenor)
            return _points[^1].ZeroRate; // 右側界外取末節點利率

        // 二分搜尋區間索引
        int index = FindIntervalIndex(timeInYears);

        // 線性插值計算
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

        // 遠期利率公式：f(t1, t2) = [r(t2)*t2 - r(t1)*t1] / (t2 - t1)
        // 來源：連續複利零利率的無套利關係 (forward extraction)
        // r(t) 為年化零利率，結果 forwardRate 為區間年化遠期利率
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
    /// 二分搜尋區間 index, 使 points[index].Tenor ≤ t < points[index+1].Tenor
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
    /// 取得所有節點 (唯讀)
    /// </summary>
    public IReadOnlyList<CurvePoint> GetPoints() => _points;

    public override string ToString()
    {
        return $"LinearInterpolatedCurve({CurveName}, {_points.Length} points, " +
               $"range: [{_points[0].Tenor:F2}Y - {_points[^1].Tenor:F2}Y])";
    }

    /// <summary>
    /// 建立標準示例線性曲線
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
