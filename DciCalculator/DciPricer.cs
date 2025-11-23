using DciCalculator.Algorithms;
using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// DCI 報價引擎
/// 計算 DCI 產品的報價、利息、Coupon
/// 支援 Margin 加成和市場數據快照
/// </summary>
public static class DciPricer
{
    /// <summary>
    /// 計算 DCI 報價（標準版）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <returns>DCI 報價結果</returns>
    public static DciQuoteResult Quote(DciInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // 1) 期間（decimal）
        decimal T = (decimal)input.TenorInYears;

        // 2) 外幣定存利息
        decimal interestDeposit =
            input.NotionalForeign
          * (decimal)input.DepositRateAnnual
          * T;

        // 3) 使用 Mid 價做 FX 期權定價（本幣每 1 外幣 Premium）
        decimal spotMid = input.SpotQuote.Mid;
        double spotD = (double)spotMid;
        double strikeD = (double)input.Strike;

        double optionPremiumDomesticPer1Foreign = GarmanKohlhagen.PriceFxOption(
            spot: spotD,
            strike: strikeD,
            rDomestic: input.RateDomestic,
            rForeign: input.RateForeign,
            volatility: input.Volatility,
            timeToMaturity: input.TenorInYears,
            optionType: OptionType.Put  // DCI 多半為賣出 Put 結構
        );

        // 4) 將本幣 Premium 換算成「等值外幣」金額（每 1 外幣）
        double optionPremiumForeignPer1 =
            optionPremiumDomesticPer1Foreign / spotD;

        decimal optionInterestForeign =
            input.NotionalForeign * (decimal)optionPremiumForeignPer1;

        // 5) 總外幣利息
        decimal totalInterestForeign = interestDeposit + optionInterestForeign;

        // 6) 計算年化收益率（Coupon）
        double couponAnnual =
            (double)(totalInterestForeign / input.NotionalForeign)
            / input.TenorInYears;

        // 7) 進行四捨五入（位數可依實務需求調整）
        return new DciQuoteResult(
            NotionalForeign: input.NotionalForeign,
            InterestFromDeposit: decimal.Round(interestDeposit, 4, MidpointRounding.AwayFromZero),
            InterestFromOption: decimal.Round(optionInterestForeign, 4, MidpointRounding.AwayFromZero),
            TotalInterestForeign: decimal.Round(totalInterestForeign, 4, MidpointRounding.AwayFromZero),
            CouponAnnual: couponAnnual
        );
    }

    /// <summary>
    /// 計算 DCI 報價（加上 Margin）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="marginPercent">Margin 百分比（例如 0.10 = 10%）</param>
    /// <returns>DCI 報價結果（扣除 Margin 後）</returns>
    public static DciQuoteResult QuoteWithMargin(
        DciInput input,
        double marginPercent)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 必須在 [0, 1) 範圍內");

        // 1) 計算理論報價
        var theoreticalQuote = Quote(input);

        // 2) 期間
        _ = (decimal)input.TenorInYears;

        // 3) 扣除 Margin
        decimal adjustedOptionInterest = 
            theoreticalQuote.InterestFromOption * (1m - (decimal)marginPercent);

        // 4) 重新計算總利息和 Coupon
        decimal totalInterest = theoreticalQuote.InterestFromDeposit + adjustedOptionInterest;
        double coupon = (double)(totalInterest / input.NotionalForeign) / input.TenorInYears;

        return new DciQuoteResult(
            NotionalForeign: input.NotionalForeign,
            InterestFromDeposit: theoreticalQuote.InterestFromDeposit,
            InterestFromOption: decimal.Round(adjustedOptionInterest, 4, MidpointRounding.AwayFromZero),
            TotalInterestForeign: decimal.Round(totalInterest, 4, MidpointRounding.AwayFromZero),
            CouponAnnual: coupon
        );
    }

    /// <summary>
    /// 從市場數據快照計算報價
    /// </summary>
    /// <param name="marketData">市場數據快照</param>
    /// <param name="notionalForeign">外幣本金</param>
    /// <param name="strike">履約價</param>
    /// <param name="tenorInYears">期限（年）</param>
    /// <param name="depositRateAnnual">定存年利率</param>
    /// <param name="marginPercent">Margin 百分比（可選）</param>
    /// <returns>DCI 報價結果</returns>
    public static DciQuoteResult QuoteFromMarketData(
        MarketDataSnapshot marketData,
        decimal notionalForeign,
        decimal strike,
        double tenorInYears,
        double depositRateAnnual,
        double marginPercent = 0.0)
    {
        ArgumentNullException.ThrowIfNull(marketData);

        // 驗證市場數據
        var validation = marketData.Validate();
        if (!validation.IsValid)
            throw new ArgumentException(
                $"市場數據無效: {string.Join(", ", validation.Errors)}");

        // 建立 DciInput
        var input = marketData.ToDciInput(
            notionalForeign,
            strike,
            tenorInYears,
            depositRateAnnual
        );

        // 計算報價
        return marginPercent > 0
            ? QuoteWithMargin(input, marginPercent)
            : Quote(input);
    }

    /// <summary>
    /// 計算完整報價（包含 Greeks）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <returns>(報價結果, Greeks)</returns>
    public static (DciQuoteResult Quote, GreeksResult Greeks) QuoteWithGreeks(DciInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var quote = Quote(input);
        var greeks = GreeksCalculator.CalculateDciGreeks(input);

        return (quote, greeks);
    }

    /// <summary>
    /// 批量報價（多個 Strike）
    /// </summary>
    /// <param name="input">基準 DCI 輸入</param>
    /// <param name="strikes">Strike 列表</param>
    /// <returns>每個 Strike 的報價結果</returns>
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
            var quote = Quote(modifiedInput);
            results.Add((strike, quote));
        }

        return results;
    }

    /// <summary>
    /// 產生報價摘要（格式化輸出）
    /// </summary>
    public static string GenerateQuoteSummary(DciInput input, DciQuoteResult quote)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(quote);

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== DCI 報價摘要 ===");
        summary.AppendLine();
        summary.AppendLine($"本金: {quote.NotionalForeign:N2} (外幣)");
        summary.AppendLine($"Spot: {input.SpotQuote.Mid:F4}");
        summary.AppendLine($"Strike: {input.Strike:F4}");
        summary.AppendLine($"期限: {input.TenorInYears * 365:N0} 天");
        summary.AppendLine();
        summary.AppendLine($"定存利息: {quote.InterestFromDeposit:N4}");
        summary.AppendLine($"期權利息: {quote.InterestFromOption:N4}");
        summary.AppendLine($"總利息: {quote.TotalInterestForeign:N4}");
        summary.AppendLine();
        summary.AppendLine($"年化收益率: {quote.CouponAnnual:P2}");

        return summary.ToString();
    }
}

