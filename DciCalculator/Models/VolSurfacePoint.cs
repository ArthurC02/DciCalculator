namespace DciCalculator.Models;

/// <summary>
/// 波動率曲面基礎節點
/// 對應特定 (Strike, Tenor) 的隱含波動率資料
/// </summary>
public readonly record struct VolSurfacePoint
{
    /// <summary>
    /// 履約價 Strike (亦可透過換算得 Moneyness)
    /// </summary>
    public double Strike { get; init; }

    /// <summary>
    /// 到期時間 Tenor (年)
    /// </summary>
    public double Tenor { get; init; }

    /// <summary>
    /// 隱含波動率 (Implied Volatility)
    /// </summary>
    public double Volatility { get; init; }

    /// <summary>
    /// Moneyness (衡量與 ATM 的相對位置)
    /// 定義: Moneyness = Strike / Forward
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
                "波動率必須位於 (0, 5.0] 範圍內");

        Strike = strike;
        Tenor = tenor;
        Volatility = volatility;
        Moneyness = null;
    }

    /// <summary>
    /// 建立並計算 Moneyness 的節點
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
    /// 驗證節點資料是否有效
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
/// 波動率微笑/斜率類型
/// </summary>
public enum VolSmileType
{
    /// <summary>
    /// 平坦 (無微笑)
    /// </summary>
    Flat,

    /// <summary>
    /// 微笑形 (兩端較高)
    /// </summary>
    Smile,

    /// <summary>
    /// 負斜率 Skew (Put 波動率高於 Call)
    /// </summary>
    Skew,

    /// <summary>
    /// 正斜率 ReverseSkew (Call 波動率高於 Put)
    /// </summary>
    ReverseSkew
}
