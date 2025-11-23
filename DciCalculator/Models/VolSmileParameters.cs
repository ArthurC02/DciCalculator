namespace DciCalculator.Models;

/// <summary>
/// Volatility Smile 參數
/// 使用市場慣用的 ATM / Risk Reversal / Butterfly 報價格式
/// 
/// 市場報價範例（25 Delta）：
/// - ATM Vol: 10.0%
/// - 25D Risk Reversal: 1.5% (Put端高於Call端)
/// - 25D Butterfly: 0.5% (兩端高於中間)
/// </summary>
public sealed record VolSmileParameters
{
    /// <summary>
    /// ATM（At-The-Money）波動度
    /// </summary>
    public double ATMVol { get; init; }

    /// <summary>
    /// Risk Reversal（25 Delta Put - 25 Delta Call）
    /// RR > 0: Put 端波動度高於 Call 端（下行風險高）
    /// RR < 0: Call 端波動度高於 Put 端
    /// </summary>
    public double RiskReversal25D { get; init; }

    /// <summary>
    /// Butterfly（(25D Put + 25D Call) / 2 - ATM）
    /// BF > 0: 兩端波動度高於中間（Smile 形狀）
    /// BF = 0: 平坦
    /// </summary>
    public double Butterfly25D { get; init; }

    /// <summary>
    /// Tenor（期限，年）
    /// </summary>
    public double Tenor { get; init; }

    public VolSmileParameters(
        double atmVol,
        double riskReversal25D,
        double butterfly25D,
        double tenor)
    {
        if (atmVol <= 0 || atmVol > 5.0)
            throw new ArgumentOutOfRangeException(nameof(atmVol),
                "ATM 波動度必須在 (0, 5.0] 範圍內");

        if (Math.Abs(riskReversal25D) > 0.5)
            throw new ArgumentOutOfRangeException(nameof(riskReversal25D),
                "Risk Reversal 必須在 [-0.5, 0.5] 範圍內");

        if (butterfly25D < 0 || butterfly25D > 0.5)
            throw new ArgumentOutOfRangeException(nameof(butterfly25D),
                "Butterfly 必須在 [0, 0.5] 範圍內");

        if (tenor <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenor));

        ATMVol = atmVol;
        RiskReversal25D = riskReversal25D;
        Butterfly25D = butterfly25D;
        Tenor = tenor;
    }

    /// <summary>
    /// 計算 25 Delta Put 波動度
    /// Vol_25P = ATM + BF + RR/2
    /// </summary>
    public double Get25DeltaPutVol()
    {
        return ATMVol + Butterfly25D + RiskReversal25D / 2.0;
    }

    /// <summary>
    /// 計算 25 Delta Call 波動度
    /// Vol_25C = ATM + BF - RR/2
    /// </summary>
    public double Get25DeltaCallVol()
    {
        return ATMVol + Butterfly25D - RiskReversal25D / 2.0;
    }

    /// <summary>
    /// 估算指定 Delta 的波動度（線性插值）
    /// </summary>
    /// <param name="delta">Delta（-1 到 1，負數為 Put）</param>
    /// <returns>隱含波動度</returns>
    public double EstimateVolByDelta(double delta)
    {
        if (Math.Abs(delta) > 1.0)
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta 必須在 [-1, 1] 範圍內");

        // ATM
        if (Math.Abs(Math.Abs(delta) - 0.5) < 0.01)
            return ATMVol;

        double absDelta = Math.Abs(delta);

        if (absDelta > 0.5)
        {
            // 外推至 10D 或更遠
            return ATMVol + Butterfly25D * 2.0;
        }

        if (delta > 0) // Call 端
        {
            // 從 ATM (0.5) 插值到 25D Call (0.25)
            if (absDelta >= 0.25)
            {
                double weight = (0.5 - absDelta) / 0.25;
                return ATMVol * (1 - weight) + Get25DeltaCallVol() * weight;
            }
            else
            {
                // 外推
                return Get25DeltaCallVol();
            }
        }
        else // Put 端 (delta < 0)
        {
            // 從 ATM (-0.5) 插值到 25D Put (-0.25)
            if (absDelta <= 0.5 && absDelta >= 0.25)
            {
                double weight = (absDelta - 0.25) / 0.25;
                return Get25DeltaPutVol() * (1 - weight) + ATMVol * weight;
            }
            else
            {
                // 外推
                return Get25DeltaPutVol();
            }
        }
    }

    /// <summary>
    /// 建立平坦 Smile（無 RR 和 BF）
    /// </summary>
    public static VolSmileParameters CreateFlat(double atmVol, double tenor)
    {
        return new VolSmileParameters(atmVol, 0.0, 0.0, tenor);
    }

    public override string ToString()
    {
        return $"T={Tenor:F4}Y, ATM={ATMVol:P2}, RR={RiskReversal25D:+0.00%;-0.00%;0.00%}, BF={Butterfly25D:P2}";
    }
}
