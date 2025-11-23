using System.Runtime.CompilerServices;

namespace DciCalculator.VolSurfaces;

/// <summary>
/// 平坦波動度曲面（Flat Volatility Surface）
/// 所有 Strike 和 Tenor 使用相同波動度
/// 
/// 用途：
/// - 向後相容（替代單一波動度參數）
/// - 簡化場景測試
/// - 流動性較差的市場
/// </summary>
public sealed class FlatVolSurface : IVolSurface
{
    private readonly double _flatVol;

    public string SurfaceName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立平坦波動度曲面
    /// </summary>
    /// <param name="surfaceName">曲面名稱（例如 "USD/TWD"）</param>
    /// <param name="referenceDate">基準日期</param>
    /// <param name="flatVol">固定波動度（年化）</param>
    public FlatVolSurface(string surfaceName, DateTime referenceDate, double flatVol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceName);

        if (flatVol <= 0 || flatVol > 5.0)
            throw new ArgumentOutOfRangeException(nameof(flatVol),
                "波動度必須在 (0, 5.0] 範圍內");

        SurfaceName = surfaceName;
        ReferenceDate = referenceDate;
        _flatVol = flatVol;
    }

    /// <summary>
    /// 建立平坦曲面（使用當前日期）
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
        // Flat surface 適用於所有 Strike 和 Tenor
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
    /// 建立測試用曲面
    /// </summary>
    public static FlatVolSurface CreateMock(string surfaceName = "USD/TWD", double vol = 0.10)
    {
        return new FlatVolSurface(surfaceName, vol);
    }
}
