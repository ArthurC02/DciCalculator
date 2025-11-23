using DciCalculator.Core.Interfaces;
using DciCalculator.Models;
using DciCalculator.Services.Pricing;

namespace DciCalculator;

/// <summary>
/// DCI 報酬計算工具 (靜態版本 - 已廢棄)
/// </summary>
[Obsolete("請使用 DciPayoffCalculatorService 類別以支援依賴注入。此靜態類別將在未來版本移除。")]
public static class DciPayoffCalculator
{
    private static readonly IDciPayoffCalculator _service = new DciPayoffCalculatorService();


    /// <summary>
    /// 計算 DCI 最終贖回結果
    /// </summary>
    public static DciPayoffResult CalculatePayoff(
        DciInput input,
        DciQuoteResult quoteResult,
        decimal spotAtMaturity)
    {
        return _service.CalculatePayoff(input, quoteResult, spotAtMaturity);
    }

    /// <summary>
    /// 與純外幣定存比較的超額損益 (PnL)
    /// </summary>
    public static decimal CalculatePnLVsDeposit(
        DciInput input,
        DciPayoffResult payoffResult)
    {
        return _service.CalculatePnLVsDeposit(input, payoffResult);
    }
}
