namespace DciCalculator.Models;

/// <summary>
/// 曲線支柱點 (Curve Pillar Point)
/// 表示收益率曲線上一個到期節點
/// </summary>
public readonly record struct CurvePoint
{
    /// <summary>
    /// 到期時間 (年)
    /// </summary>
    public double Tenor { get; init; }

    /// <summary>
    /// 零利率 (連續複利率)
    /// </summary>
    public double ZeroRate { get; init; }

    /// <summary>
    /// 折現因子 (可由零利率推導)
    /// DF = exp(-ZeroRate * Tenor)
    /// </summary>
    public double DiscountFactor => Math.Exp(-ZeroRate * Tenor);

    public CurvePoint(double tenor, double zeroRate)
    {
        if (tenor < 0)
            throw new ArgumentOutOfRangeException(nameof(tenor), "Tenor 不可為負值");

        if (double.IsNaN(zeroRate) || double.IsInfinity(zeroRate))
            throw new ArgumentException("ZeroRate 非法 (NaN 或 Infinity)", nameof(zeroRate));

        Tenor = tenor;
        ZeroRate = zeroRate;
    }

    /// <summary>
    /// 由折現因子建立 CurvePoint
    /// r = -ln(DF) / T
    /// </summary>
    public static CurvePoint FromDiscountFactor(double tenor, double discountFactor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tenor, 0.0);
        
        if (discountFactor <= 0 || discountFactor > 1.0)
            throw new ArgumentOutOfRangeException(nameof(discountFactor),
                "DiscountFactor 必須位於 (0, 1] 區間");

        double zeroRate = -Math.Log(discountFactor) / tenor;
        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// 驗證當前節點資料是否有效
    /// </summary>
    public bool IsValid()
    {
        return Tenor >= 0 &&
               !double.IsNaN(ZeroRate) &&
               !double.IsInfinity(ZeroRate) &&
               ZeroRate > -0.20 &&  // 下限 -20%
               ZeroRate < 0.50;     // 上限 50%
    }

    public override string ToString()
    {
        return $"T={Tenor:F4}Y, r={ZeroRate:P4}, DF={DiscountFactor:F6}";
    }
}

/// <summary>
/// 曲線插值方法
/// </summary>
public enum InterpolationMethod
{
    /// <summary>
    /// 對零利率做線性插值 (Zero Rate)
    /// </summary>
    Linear,

    /// <summary>
    /// 對折現因子做對數線性插值 (Discount Factor)
    /// </summary>
    LogLinear,

    /// <summary>
    /// 三次樣條插值 (平滑曲線)
    /// </summary>
    CubicSpline,

    /// <summary>
    /// 平坦外推 (使用最近節點值)
    /// </summary>
    Flat
}
