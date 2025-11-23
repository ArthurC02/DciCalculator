using DciCalculator.Models;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// DCI 報酬計算服務介面
/// </summary>
public interface IDciPayoffCalculator
{
    /// <summary>
    /// 計算 DCI 最終贖回結果
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="quoteResult">估價結果 (包含外幣利息)</param>
    /// <param name="spotAtMaturity">到期現貨匯率</param>
    /// <returns>最終贖回結果結構</returns>
    DciPayoffResult CalculatePayoff(
        DciInput input,
        DciQuoteResult quoteResult,
        decimal spotAtMaturity);

    /// <summary>
    /// 計算與純外幣定存比較的超額損益 (PnL)
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="payoffResult">最終贖回結果</param>
    /// <returns>相對單純外幣定存的超額 PnL (以外幣計)</returns>
    decimal CalculatePnLVsDeposit(
        DciInput input,
        DciPayoffResult payoffResult);
}
