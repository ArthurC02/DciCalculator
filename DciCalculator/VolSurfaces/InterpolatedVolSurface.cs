using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator.VolSurfaces;

/// <summary>
/// 可插值的波動率曲面。
/// 使用雙線性插值 (Bilinear Interpolation) 於 Strike 與 Tenor 二維網格上計算中間點的隱含波動率。
/// 
/// 插值核心概念：
/// 1. Strike 區間：線性權重
/// 2. Tenor 區間：線性權重
/// 3. 組合：雙線性加權
/// </summary>
public sealed class InterpolatedVolSurface : IVolSurface
{
    private readonly VolSurfacePoint[] _points;
    private readonly double[] _uniqueStrikes;
    private readonly double[] _uniqueTenors;

    public string SurfaceName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立插值曲面。
    /// </summary>
    /// <param name="surfaceName">曲面名稱。</param>
    /// <param name="referenceDate">基準日期。</param>
    /// <param name="points">輸入基礎網格點（至少 4 個；需形成 >= 2x2 網格）。</param>
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
            throw new ArgumentException("至少需要 4 個基礎網格點 (2x2)" , nameof(points));

        // 驗證所有輸入網格點有效性
        foreach (var point in _points)
        {
            if (!point.IsValid())
                throw new ArgumentException($"輸入點無效: {point}");
        }

        // 建立唯一 Strike 與 Tenor 陣列
        _uniqueStrikes = _points.Select(p => p.Strike).Distinct().OrderBy(s => s).ToArray();
        _uniqueTenors = _points.Select(p => p.Tenor).Distinct().OrderBy(t => t).ToArray();

        if (_uniqueStrikes.Length < 2 || _uniqueTenors.Length < 2)
            throw new ArgumentException("至少需要 2 個不同 Strike 與 2 個不同 Tenor");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVolatility(double strike, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        // 邊界裁切（Clamp）
        strike = Math.Clamp(strike, _uniqueStrikes[0], _uniqueStrikes[^1]);
        tenor = Math.Clamp(tenor, _uniqueTenors[0], _uniqueTenors[^1]);

        // 找出所在區間索引
        int strikeIndex = FindIntervalIndex(_uniqueStrikes, strike);
        int tenorIndex = FindIntervalIndex(_uniqueTenors, tenor);

        // 執行雙線性插值
        return BilinearInterpolation(strike, tenor, strikeIndex, tenorIndex);
    }

    /// <summary>
    /// 雙線性插值公式說明：
    /// 角落四點：
    /// (K1, T1)=V11  (K2, T1)=V21
    /// (K1, T2)=V12  (K2, T2)=V22
    /// 欲求中點 (K, T)：
    /// V(K,T) = V11*(K2-K)*(T2-T) + V21*(K-K1)*(T2-T)
    ///        + V12*(K2-K)*(T-T1) + V22*(K-K1)*(T-T1)
    ///        / [(K2-K1)*(T2-T1)]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double BilinearInterpolation(double strike, double tenor, int strikeIdx, int tenorIdx)
    {
        double k1 = _uniqueStrikes[strikeIdx];
        double k2 = _uniqueStrikes[strikeIdx + 1];
        double t1 = _uniqueTenors[tenorIdx];
        double t2 = _uniqueTenors[tenorIdx + 1];

        // 取得四個角落點波動率
        double v11 = GetPointVolatility(k1, t1);
        double v21 = GetPointVolatility(k2, t1);
        double v12 = GetPointVolatility(k1, t2);
        double v22 = GetPointVolatility(k2, t2);

        // 計算權重
        double wK = (strike - k1) / (k2 - k1);
        double wT = (tenor - t1) / (t2 - t1);

        // 雙線性組合
        double vol = (1 - wK) * (1 - wT) * v11
                   + wK * (1 - wT) * v21
                   + (1 - wK) * wT * v12
                   + wK * wT * v22;

        return vol;
    }

    /// <summary>
    /// 取得指定 (Strike, Tenor) 波動率（必須是原始網格點）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetPointVolatility(double strike, double tenor)
    {
        var point = _points.FirstOrDefault(p =>
            Math.Abs(p.Strike - strike) < 1e-6 &&
            Math.Abs(p.Tenor - tenor) < 1e-6);

        if (point.Equals(default(VolSurfacePoint)))
            throw new InvalidOperationException($"找不到網格點 K={strike}, T={tenor}");

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

        // 以中間 Strike 做為 ATM 近似
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
    /// 二分搜尋找區間索引
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
    /// 取得全部原始網格點（未排序過）。
    /// </summary>
    public IReadOnlyList<VolSurfacePoint> GetPoints() => _points;

    public override string ToString()
    {
        return $"InterpolatedVolSurface({SurfaceName}, {_points.Length} points, " +
               $"K=[{_uniqueStrikes[0]:F2}-{_uniqueStrikes[^1]:F2}], " +
               $"T=[{_uniqueTenors[0]:F2}Y-{_uniqueTenors[^1]:F2}Y])";
    }

    /// <summary>
    /// 建立標準 2x3 網格（示例用）。
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
