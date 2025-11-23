using DciCalculator.Algorithms;
using DciCalculator.Models;
using DciCalculator.PricingModels;
using DciCalculator.Services.Pricing;

namespace DciCalculator;

/// <summary>
/// DCI 報價引擎 - 靜態包裝類別
/// 
/// [已棄用] 此靜態類別保留用於向後兼容。
/// 新代碼請使用 <see cref="DciPricingEngine"/> 以支援依賴注入和更好的測試性。
/// 
/// 計算 DCI 產品的報價、利息、Coupon
/// 支援 Margin 加成和市場數據快照
/// 
/// v2.1 重構：內部委託給 DciPricingEngine 實例
/// </summary>
[Obsolete("請使用 DciPricingEngine 類別以支援依賴注入。此靜態類別將在未來版本移除。", false)]
public static class DciPricer
{
    // 內部使用的引擎實例（單例模式用於靜態類別）
    private static readonly DciPricingEngine _engine = new(new GarmanKohlhagenModel());

    /// <summary>
    /// 計算 DCI 報價（標準版）
    /// </summary>
    public static DciQuoteResult Quote(DciInput input)
    {
        return _engine.Quote(input);
    }

    /// <summary>
    /// 計算 DCI 報價（加上 Margin）
    /// </summary>
    public static DciQuoteResult QuoteWithMargin(DciInput input, double marginPercent)
    {
        return _engine.QuoteWithMargin(input, marginPercent);
    }

    /// <summary>
    /// 從市場數據快照計算報價
    /// </summary>
    public static DciQuoteResult QuoteFromMarketData(
        MarketDataSnapshot marketData,
        decimal notionalForeign,
        decimal strike,
        double tenorInYears,
        double depositRateAnnual,
        double marginPercent = 0.0)
    {
        var result = _engine.QuoteFromSnapshot(marketData, notionalForeign, strike, tenorInYears, depositRateAnnual);
        
        return marginPercent > 0
            ? _engine.QuoteWithMargin(
                marketData.ToDciInput(notionalForeign, strike, tenorInYears, depositRateAnnual),
                marginPercent)
            : result;
    }

    /// <summary>
    /// 計算完整報價（包含 Greeks）
    /// </summary>
    public static (DciQuoteResult Quote, GreeksResult Greeks) QuoteWithGreeks(DciInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var quote = _engine.Quote(input);
        var greeks = GreeksCalculator.CalculateDciGreeks(input);

        return (quote, greeks);
    }

    /// <summary>
    /// 批量報價（多個 Strike）
    /// </summary>
    public static IReadOnlyList<(decimal Strike, DciQuoteResult Quote)> QuoteBatch(
        DciInput input,
        IEnumerable<decimal> strikes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(strikes);

        var results = new List<(decimal Strike, DciQuoteResult Quote)>();

        foreach (decimal strike in strikes)
        {
            var modifiedInput = input with { Strike = strike };
            var quote = _engine.Quote(modifiedInput);
            results.Add((strike, quote));
        }

        return results;
    }

    /// <summary>
    /// 產生報價摘要（格式化輸出）
    /// </summary>
    public static string GenerateQuoteSummary(DciInput input, DciQuoteResult quote)
    {
        return _engine.GenerateQuoteSummary(input, quote);
    }
}

