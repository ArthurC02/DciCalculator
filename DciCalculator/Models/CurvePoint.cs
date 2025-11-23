namespace DciCalculator.Models;

/// <summary>
/// 曲線節點（Curve Pillar Point）
/// 描述曲線上的一個資料點
/// </summary>
public readonly record struct CurvePoint
{
    /// <summary>
    /// 期限（年）
    /// </summary>
    public double Tenor { get; init; }

    /// <summary>
    /// 零息利率（連續複利）
    /// </summary>
    public double ZeroRate { get; init; }

    /// <summary>
    /// 折現因子（衍生值，可快取）
    /// DF = exp(-ZeroRate * Tenor)
    /// </summary>
    public double DiscountFactor => Math.Exp(-ZeroRate * Tenor);

    public CurvePoint(double tenor, double zeroRate)
    {
        if (tenor < 0)
            throw new ArgumentOutOfRangeException(nameof(tenor), "期限不能為負");

        if (double.IsNaN(zeroRate) || double.IsInfinity(zeroRate))
            throw new ArgumentException("利率必須為有效數值", nameof(zeroRate));

        Tenor = tenor;
        ZeroRate = zeroRate;
    }

    /// <summary>
    /// 從折現因子建立曲線點
    /// r = -ln(DF) / T
    /// </summary>
    public static CurvePoint FromDiscountFactor(double tenor, double discountFactor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tenor, 0.0);
        
        if (discountFactor <= 0 || discountFactor > 1.0)
            throw new ArgumentOutOfRangeException(nameof(discountFactor),
                "折現因子必須在 (0, 1] 範圍內");

        double zeroRate = -Math.Log(discountFactor) / tenor;
        return new CurvePoint(tenor, zeroRate);
    }

    /// <summary>
    /// 驗證曲線點是否合理
    /// </summary>
    public bool IsValid()
    {
        return Tenor >= 0 &&
               !double.IsNaN(ZeroRate) &&
               !double.IsInfinity(ZeroRate) &&
               ZeroRate > -0.20 &&  // 最低 -20%
               ZeroRate < 0.50;     // 最高 50%
    }

    public override string ToString()
    {
        return $"T={Tenor:F4}Y, r={ZeroRate:P4}, DF={DiscountFactor:F6}";
    }
}

/// <summary>
/// 插值方法列舉
/// </summary>
public enum InterpolationMethod
{
    /// <summary>
    /// 線性插值（Zero Rate）
    /// </summary>
    Linear,

    /// <summary>
    /// 對數線性插值（Discount Factor）
    /// </summary>
    LogLinear,

    /// <summary>
    /// 三次樣條插值（自然邊界條件）
    /// </summary>
    CubicSpline,

    /// <summary>
    /// 不插值（使用最近節點）
    /// </summary>
    Flat
}
