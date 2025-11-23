using DciCalculator.Core.Interfaces;
using DciCalculator.Models;

namespace DciCalculator.Services.Pricing;

/// <summary>
/// DCI 報酬計算服務實作
/// 計算結構式存款到期後的最終贖回金額 (Knock-In 本幣兌換 或 未觸發保留外幣本金+利息)
/// </summary>
public sealed class DciPayoffCalculatorService : IDciPayoffCalculator
{
    /// <summary>
    /// 計算 DCI 最終贖回結果
    /// Knock-In 條件：到期 Spot <= Strike (以本幣贖回)
    /// 未 Knock-In：保留外幣本金 + 外幣利息
    /// </summary>
    public DciPayoffResult CalculatePayoff(
        DciInput input,
        DciQuoteResult quoteResult,
        decimal spotAtMaturity)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(quoteResult);

        // 判斷是否觸及 Knock-In 障礙 (Spot <= Strike)
        bool isKnockedIn = spotAtMaturity <= input.Strike;

        if (isKnockedIn)
        {
            // 已 Knock-In：外幣本金+利息 以 Strike 兌換成本幣
            // 本幣贖回金額 = (外幣本金 + 外幣總利息) * Strike
            decimal payoffDomestic = 
                (input.NotionalForeign + quoteResult.TotalInterestForeign) * input.Strike;

            return new DciPayoffResult(
                IsKnockedIn: true,
                PayoffForeign: 0m,
                PayoffDomestic: payoffDomestic,
                FinalSpot: spotAtMaturity,
                Strike: input.Strike
            );
        }
        else
        {
            // 未 Knock-In：贖回外幣本金 + 外幣總利息
            decimal payoffForeign = input.NotionalForeign + quoteResult.TotalInterestForeign;

            return new DciPayoffResult(
                IsKnockedIn: false,
                PayoffForeign: payoffForeign,
                PayoffDomestic: 0m,
                FinalSpot: spotAtMaturity,
                Strike: input.Strike
            );
        }
    }

    /// <summary>
    /// 計算與純外幣定存比較的超額損益 (PnL)
    /// 若 Knock-In 則需將本幣贖回金額換算回外幣再比較
    /// </summary>
    public decimal CalculatePnLVsDeposit(
        DciInput input,
        DciPayoffResult payoffResult)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(payoffResult);

        // 外幣單純定存的到期金額
        decimal T = (decimal)input.TenorInYears;
        decimal depositOnlyPayoff = 
            input.NotionalForeign * (1m + (decimal)input.DepositRateAnnual * T);

        if (payoffResult.IsKnockedIn)
        {
            // Knock-In：需將本幣贖回金額除以到期 Spot 還原成外幣
            decimal dciPayoffInForeign = 
                payoffResult.PayoffDomestic / payoffResult.FinalSpot;
            
            return dciPayoffInForeign - depositOnlyPayoff;
        }
        else
        {
            // 未 Knock-In：直接使用外幣贖回金額
            return payoffResult.PayoffForeign - depositOnlyPayoff;
        }
    }
}
