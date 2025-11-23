using DciCalculator.Models;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// DCI 定價引擎介面
/// </summary>
public interface IDciPricingEngine
{
    /// <summary>
    /// 計算 DCI 報價
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <returns>DCI 報價結果</returns>
    DciQuoteResult Quote(DciInput input);

    /// <summary>
    /// 計算帶邊際調整的 DCI 報價
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="marginPercent">邊際百分比（如 0.10 表示 10%）</param>
    /// <returns>DCI 報價結果</returns>
    DciQuoteResult QuoteWithMargin(DciInput input, double marginPercent);

    /// <summary>
    /// 使用市場數據快照計算 DCI 報價
    /// </summary>
    /// <param name="snapshot">市場數據快照</param>
    /// <param name="notionalForeign">外幣本金</param>
    /// <param name="strike">行使價</param>
    /// <param name="tenorInYears">期限（年）</param>
    /// <param name="depositRateAnnual">存款年利率</param>
    /// <returns>DCI 報價結果</returns>
    DciQuoteResult QuoteFromSnapshot(
        MarketDataSnapshot snapshot,
        decimal notionalForeign,
        decimal strike,
        double tenorInYears,
        double depositRateAnnual);

    /// <summary>
    /// 批次計算多個 DCI 報價
    /// </summary>
    /// <param name="inputs">DCI 輸入參數列表</param>
    /// <returns>DCI 報價結果列表</returns>
    IReadOnlyList<DciQuoteResult> QuoteBatch(IEnumerable<DciInput> inputs);
}
