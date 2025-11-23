using System.Runtime.CompilerServices;

namespace DciCalculator.VolSurfaces;

/// <summary>
/// 平坦波動率曲面 (Flat Volatility Surface)。
/// 所有 Strike 與 Tenor 回傳相同的一致波動率。
/// 
/// 適用情境：
/// - 早期原型／尚無完整 Smile 資料
/// - 快速風險概算
/// - 回測或教學示例
/// </summary>
public sealed class FlatVolSurface : IVolSurface
{
    private readonly double _flatVol;

    public string SurfaceName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立平坦曲面。
    /// </summary>
    /// <param name="surfaceName">曲面名稱（例如 "USD/TWD"）。</param>
    /// <param name="referenceDate">基準日期。</param>
    /// <param name="flatVol">固定年化波動率。</param>
    public FlatVolSurface(string surfaceName, DateTime referenceDate, double flatVol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceName);

        if (flatVol <= 0 || flatVol > 5.0)
            throw new ArgumentOutOfRangeException(nameof(flatVol),
                "波動率必須落在 (0, 5.0] 範圍內");

        SurfaceName = surfaceName;
        ReferenceDate = referenceDate;
        _flatVol = flatVol;
    }

    /// <summary>
    /// 建立平坦曲面（基準日期 = 今日）。
    /// </summary>
    public FlatVolSurface(string surfaceName, double flatVol)
        : this(surfaceName, DateTime.Today, flatVol)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVolatility(double strike, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        return _flatVol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetATMVolatility(double spot, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        return _flatVol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVolatilityByMoneyness(double moneyness, double tenor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(moneyness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenor);

        return _flatVol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double MinStrike, double MaxStrike, double MinTenor, double MaxTenor) GetValidRange()
    {
        // 平坦曲面：視為支援所有正的 Strike 與 Tenor
        return (0.0, double.MaxValue, 0.0, double.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInRange(double strike, double tenor)
    {
        return strike > 0 && tenor > 0;
    }

    public override string ToString()
    {
        return $"FlatVolSurface({SurfaceName}, Vol={_flatVol:P2})";
    }

    /// <summary>
    /// 建立示例曲面。
    /// </summary>
    public static FlatVolSurface CreateMock(string surfaceName = "USD/TWD", double vol = 0.10)
    {
        return new FlatVolSurface(surfaceName, vol);
    }
}
