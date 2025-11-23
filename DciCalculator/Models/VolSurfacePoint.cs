namespace DciCalculator.Models;

/// <summary>
/// 波動度曲面節點
/// 描述特定 (Strike, Tenor) 的隱含波動度
/// </summary>
public readonly record struct VolSurfacePoint
{
    /// <summary>
    /// Strike（執行價，可以是絕對值或 Moneyness）
    /// </summary>
    public double Strike { get; init; }

    /// <summary>
    /// Tenor（期限，年）
    /// </summary>
    public double Tenor { get; init; }

    /// <summary>
    /// 隱含波動度（年化）
    /// </summary>
    public double Volatility { get; init; }

    /// <summary>
    /// Moneyness（相對於 ATM 的位置）
    /// Moneyness = Strike / Forward
    /// </summary>
    public double? Moneyness { get; init; }

    public VolSurfacePoint(double strike, double tenor, double volatility)
    {
        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), "Strike 必須 > 0");

        if (tenor <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenor), "Tenor 必須 > 0");

        if (volatility <= 0 || volatility > 5.0)
            throw new ArgumentOutOfRangeException(nameof(volatility),
                "波動度必須在 (0, 5.0] 範圍內");

        Strike = strike;
        Tenor = tenor;
        Volatility = volatility;
        Moneyness = null;
    }

    /// <summary>
    /// 建立帶 Moneyness 的節點
    /// </summary>
    public static VolSurfacePoint CreateWithMoneyness(
        double strike,
        double tenor,
        double volatility,
        double forward)
    {
        var point = new VolSurfacePoint(strike, tenor, volatility)
        {
            Moneyness = strike / forward
        };
        return point;
    }

    /// <summary>
    /// 驗證節點是否有效
    /// </summary>
    public bool IsValid()
    {
        return Strike > 0 &&
               Tenor > 0 &&
               Volatility > 0 &&
               Volatility <= 5.0 &&
               !double.IsNaN(Volatility) &&
               !double.IsInfinity(Volatility);
    }

    public override string ToString()
    {
        string mny = Moneyness.HasValue ? $", M={Moneyness.Value:F4}" : "";
        return $"K={Strike:F4}, T={Tenor:F4}Y, Vol={Volatility:P2}{mny}";
    }
}

/// <summary>
/// Volatility Smile 類型
/// </summary>
public enum VolSmileType
{
    /// <summary>
    /// 無 Smile（平坦）
    /// </summary>
    Flat,

    /// <summary>
    /// 標準 Smile（兩端高，中間低）
    /// </summary>
    Smile,

    /// <summary>
    /// Skew（斜偏，Put 端高於 Call 端）
    /// </summary>
    Skew,

    /// <summary>
    /// 反向 Skew
    /// </summary>
    ReverseSkew
}
