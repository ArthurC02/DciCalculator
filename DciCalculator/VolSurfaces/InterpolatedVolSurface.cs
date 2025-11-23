using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.VolSurfaces;

/// <summary>
/// 插值波動度曲面
/// 使用雙線性插值（Bilinear Interpolation）在 Strike 和 Tenor 兩個維度插值
/// 
/// 插值邏輯：
/// 1. Strike 維度：線性插值
/// 2. Tenor 維度：線性插值
/// 3. 組合：雙線性插值
/// </summary>
public sealed class InterpolatedVolSurface : IVolSurface
{
    private readonly VolSurfacePoint[] _points;
    private readonly double[] _uniqueStrikes;
    private readonly double[] _uniqueTenors;

    public string SurfaceName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立插值波動度曲面
    /// </summary>
    /// <param name="surfaceName">曲面名稱</param>
    /// <param name="referenceDate">基準日期</param>
    /// <param name="points">曲面節點（至少 4 個點：2x2 網格）</param>
    public InterpolatedVolSurface(
        string surfaceName,
        DateTime referenceDate,
        IEnumerable<VolSurfacePoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceName);
        ArgumentNullException.ThrowIfNull(points);

        SurfaceName = surfaceName;
        ReferenceDate = referenceDate;

        _points = points.ToArray();

        if (_points.Length < 4)
            throw new ArgumentException("至少需要 4 個節點（2x2 網格）", nameof(points));

        // 驗證所有節點
        foreach (var point in _points)
        {
            if (!point.IsValid())
                throw new ArgumentException($"節點無效: {point}");
        }

        // 提取唯一的 Strike 和 Tenor
        _uniqueStrikes = _points.Select(p => p.Strike).Distinct().OrderBy(s => s).ToArray();
        _uniqueTenors = _points.Select(p => p.Tenor).Distinct().OrderBy(t => t).ToArray();

        if (_uniqueStrikes.Length < 2 || _uniqueTenors.Length < 2)
            throw new ArgumentException("至少需要 2 個不同的 Strike 和 2 個不同的 Tenor");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVolatility(double strike, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        // 邊界外推
        strike = Math.Clamp(strike, _uniqueStrikes[0], _uniqueStrikes[^1]);
        tenor = Math.Clamp(tenor, _uniqueTenors[0], _uniqueTenors[^1]);

        // 找到插值區間
        int strikeIndex = FindIntervalIndex(_uniqueStrikes, strike);
        int tenorIndex = FindIntervalIndex(_uniqueTenors, tenor);

        // 雙線性插值
        return BilinearInterpolation(strike, tenor, strikeIndex, tenorIndex);
    }

    /// <summary>
    /// 雙線性插值
    /// 
    /// 給定四個角點：
    /// (K1, T1) → V11    (K2, T1) → V21
    /// (K1, T2) → V12    (K2, T2) → V22
    /// 
    /// 插值點 (K, T) 的值：
    /// V(K, T) = V11 * (K2-K)*(T2-T) + V21 * (K-K1)*(T2-T)
    ///         + V12 * (K2-K)*(T-T1) + V22 * (K-K1)*(T-T1)
    ///         ÷ [(K2-K1) * (T2-T1)]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double BilinearInterpolation(double strike, double tenor, int strikeIdx, int tenorIdx)
    {
        double k1 = _uniqueStrikes[strikeIdx];
        double k2 = _uniqueStrikes[strikeIdx + 1];
        double t1 = _uniqueTenors[tenorIdx];
        double t2 = _uniqueTenors[tenorIdx + 1];

        // 找到四個角點的波動度
        double v11 = GetPointVolatility(k1, t1);
        double v21 = GetPointVolatility(k2, t1);
        double v12 = GetPointVolatility(k1, t2);
        double v22 = GetPointVolatility(k2, t2);

        // 計算權重
        double wK = (strike - k1) / (k2 - k1);
        double wT = (tenor - t1) / (t2 - t1);

        // 雙線性插值
        double vol = (1 - wK) * (1 - wT) * v11
                   + wK * (1 - wT) * v21
                   + (1 - wK) * wT * v12
                   + wK * wT * v22;

        return vol;
    }

    /// <summary>
    /// 取得指定 (Strike, Tenor) 的波動度（精確匹配）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetPointVolatility(double strike, double tenor)
    {
        var point = _points.FirstOrDefault(p =>
            Math.Abs(p.Strike - strike) < 1e-6 &&
            Math.Abs(p.Tenor - tenor) < 1e-6);

        if (point.Equals(default(VolSurfacePoint)))
            throw new InvalidOperationException($"找不到節點 K={strike}, T={tenor}");

        return point.Volatility;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetATMVolatility(double spot, double tenor)
    {
        // ATM Strike = Spot
        return GetVolatility(spot, tenor);
    }

    public double GetVolatilityByMoneyness(double moneyness, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(moneyness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        // 假設 ATM Strike 在中間
        double atmStrike = (_uniqueStrikes[0] + _uniqueStrikes[^1]) / 2.0;
        double strike = moneyness * atmStrike;

        return GetVolatility(strike, tenor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double MinStrike, double MaxStrike, double MinTenor, double MaxTenor) GetValidRange()
    {
        return (_uniqueStrikes[0], _uniqueStrikes[^1], _uniqueTenors[0], _uniqueTenors[^1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInRange(double strike, double tenor)
    {
        return strike >= _uniqueStrikes[0] &&
               strike <= _uniqueStrikes[^1] &&
               tenor >= _uniqueTenors[0] &&
               tenor <= _uniqueTenors[^1];
    }

    /// <summary>
    /// 二分搜尋找到區間
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindIntervalIndex(double[] sortedArray, double value)
    {
        int left = 0;
        int right = sortedArray.Length - 2;

        while (left < right)
        {
            int mid = (left + right) / 2;

            if (value < sortedArray[mid + 1])
                right = mid;
            else
                left = mid + 1;
        }

        return left;
    }

    /// <summary>
    /// 取得所有節點（唯讀）
    /// </summary>
    public IReadOnlyList<VolSurfacePoint> GetPoints() => _points;

    public override string ToString()
    {
        return $"InterpolatedVolSurface({SurfaceName}, {_points.Length} points, " +
               $"K=[{_uniqueStrikes[0]:F2}-{_uniqueStrikes[^1]:F2}], " +
               $"T=[{_uniqueTenors[0]:F2}Y-{_uniqueTenors[^1]:F2}Y])";
    }

    /// <summary>
    /// 建立標準 2x3 網格（測試用）
    /// </summary>
    public static InterpolatedVolSurface CreateStandardGrid(
        string surfaceName,
        double atmVol,
        double volSkew = 0.02)
    {
        var points = new[]
        {
            // 3M Tenor
            new VolSurfacePoint(29.0, 0.25, atmVol + volSkew),   // ITM Put
            new VolSurfacePoint(30.5, 0.25, atmVol),             // ATM
            new VolSurfacePoint(32.0, 0.25, atmVol - volSkew),   // OTM Put

            // 6M Tenor
            new VolSurfacePoint(29.0, 0.5, atmVol + volSkew + 0.01),
            new VolSurfacePoint(30.5, 0.5, atmVol + 0.01),
            new VolSurfacePoint(32.0, 0.5, atmVol - volSkew + 0.01),

            // 1Y Tenor
            new VolSurfacePoint(29.0, 1.0, atmVol + volSkew + 0.02),
            new VolSurfacePoint(30.5, 1.0, atmVol + 0.02),
            new VolSurfacePoint(32.0, 1.0, atmVol - volSkew + 0.02)
        };

        return new InterpolatedVolSurface(surfaceName, DateTime.Today, points);
    }
}
