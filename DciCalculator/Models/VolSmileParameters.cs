namespace DciCalculator.Models;

/// <summary>
/// Volatility Smile 參數
/// 採用外匯期權常見的 ATM / Risk Reversal / Butterfly 三參數結構。
/// 
/// 範例（某一 25 Delta 期限）：
/// - ATM Vol: 10.0%
/// - 25D Risk Reversal: 1.5% (Put 隱含波動率高於 Call，表示向下偏斜需求較強)
/// - 25D Butterfly: 0.5% (兩翼高於中間，呈微笑型)
/// </summary>
public sealed record VolSmileParameters
{
    /// <summary>
    /// ATM (At-The-Money) 隱含波動率。
    /// </summary>
    public double ATMVol { get; init; }

    /// <summary>
    /// Risk Reversal (25 Delta Put - 25 Delta Call)。
    /// RR > 0: Put 隱含波動率高於 Call（向下偏斜 / 看跌保護需求）。
    /// RR < 0: Call 隱含波動率高於 Put（向上偏斜）。
    /// </summary>
    public double RiskReversal25D { get; init; }

    /// <summary>
    /// Butterfly = (25D Put + 25D Call) / 2 - ATM。
    /// BF > 0: 兩翼高於中間（微笑 Smile）。
    /// BF = 0: 平坦。
    /// </summary>
    public double Butterfly25D { get; init; }

    /// <summary>
    /// Tenor 到期年數（以年為單位）。
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
                "ATM 隱含波動率必須落在 (0, 5.0] 範圍");

        if (Math.Abs(riskReversal25D) > 0.5)
            throw new ArgumentOutOfRangeException(nameof(riskReversal25D),
                "Risk Reversal 必須落在 [-0.5, 0.5] 範圍");

        if (butterfly25D < 0 || butterfly25D > 0.5)
            throw new ArgumentOutOfRangeException(nameof(butterfly25D),
                "Butterfly 必須落在 [0, 0.5] 範圍");

        if (tenor <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenor));

        ATMVol = atmVol;
        RiskReversal25D = riskReversal25D;
        Butterfly25D = butterfly25D;
        Tenor = tenor;
    }

    /// <summary>
    /// 計算 25 Delta Put 隱含波動率。
    /// Vol_25P = ATM + BF + RR/2
    /// </summary>
    public double Get25DeltaPutVol()
    {
        return ATMVol + Butterfly25D + RiskReversal25D / 2.0;
    }

    /// <summary>
    /// 計算 25 Delta Call 隱含波動率。
    /// Vol_25C = ATM + BF - RR/2
    /// </summary>
    public double Get25DeltaCallVol()
    {
        return ATMVol + Butterfly25D - RiskReversal25D / 2.0;
    }

    /// <summary>
    /// 估算指定 Delta 的隱含波動率（簡易分段線性近似）。
    /// </summary>
    /// <param name="delta">Delta (-1 至 1)，負值代表 Put。</param>
    /// <returns>近似隱含波動率。</returns>
    public double EstimateVolByDelta(double delta)
    {
        if (Math.Abs(delta) > 1.0)
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta 必須落在 [-1, 1] 範圍");

        // ATM
        if (Math.Abs(Math.Abs(delta) - 0.5) < 0.01)
            return ATMVol;

        double absDelta = Math.Abs(delta);

        if (absDelta > 0.5)
        {
            // 超出常見插值區間，簡單外推：加倍 Butterfly 作為翼部增厚
            return ATMVol + Butterfly25D * 2.0;
        }

        if (delta > 0) // Call 方向
        {
            // 在 ATM (0.5) 與 25D Call (0.25) 之間線性插值
            if (absDelta >= 0.25)
            {
                double weight = (0.5 - absDelta) / 0.25;
                return ATMVol * (1 - weight) + Get25DeltaCallVol() * weight;
            }
            else
            {
                // 進一步向翼部靠近：直接取 25D Call 值
                return Get25DeltaCallVol();
            }
        }
        else // Put 方向 (delta < 0)
        {
            // 在 ATM (-0.5) 與 25D Put (-0.25) 之間線性插值
            if (absDelta <= 0.5 && absDelta >= 0.25)
            {
                double weight = (absDelta - 0.25) / 0.25;
                return Get25DeltaPutVol() * (1 - weight) + ATMVol * weight;
            }
            else
            {
                // 更接近 Put 翼：直接取 25D Put 值
                return Get25DeltaPutVol();
            }
        }
    }

    /// <summary>
    /// 建立平坦 Smile（RR=0, BF=0）。
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
