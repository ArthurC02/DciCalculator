using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// DCI 到期 Payoff 計算器
/// 計算 DCI 產品到期時的實際回報（本金 + 利息，以及可能的貨幣轉換）
/// </summary>
public static class DciPayoffCalculator
{
    /// <summary>
    /// 計算 DCI 到期回報結果
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="quoteResult">DCI 報價結果</param>
    /// <param name="spotAtMaturity">到期時的即期匯率</param>
    /// <returns>到期回報計算結果</returns>
    public static DciPayoffResult CalculatePayoff(
        DciInput input,
        DciQuoteResult quoteResult,
        decimal spotAtMaturity)
    {
        // 判斷是否觸及履約價（匯率跌破 Strike）
        bool isKnockedIn = spotAtMaturity <= input.Strike;

        if (isKnockedIn)
        {
            // 匯率跌破履約價 → 被迫轉換成本幣（TWD）
            // 客戶收到：(本金 + 利息) * Strike
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
            // 匯率未跌破履約價 → 領回外幣（USD）+ 高利息
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
    /// 計算盈虧分析（與單純持有外幣定存相比）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="payoffResult">到期回報結果</param>
    /// <returns>盈虧分析結果（以外幣計）</returns>
    public static decimal CalculatePnLVsDeposit(
        DciInput input,
        DciPayoffResult payoffResult)
    {
        // 單純外幣定存的回報
        decimal T = (decimal)input.TenorInYears;
        decimal depositOnlyPayoff = 
            input.NotionalForeign * (1m + (decimal)input.DepositRateAnnual * T);

        if (payoffResult.IsKnockedIn)
        {
            // 被轉換成本幣，需要換算回外幣等值
            decimal dciPayoffInForeign = 
                payoffResult.PayoffDomestic / payoffResult.FinalSpot;
            
            return dciPayoffInForeign - depositOnlyPayoff;
        }
        else
        {
            // 直接收到外幣
            return payoffResult.PayoffForeign - depositOnlyPayoff;
        }
    }
}
