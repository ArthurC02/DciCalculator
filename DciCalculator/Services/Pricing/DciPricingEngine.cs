using DciCalculator.Core.Interfaces;
using DciCalculator.Models;

namespace DciCalculator.Services.Pricing;

/// <summary>
/// DCI 定價引擎服務（實例版本）
/// 實現 IDciPricingEngine 介面，支援依賴注入
/// </summary>
public class DciPricingEngine : IDciPricingEngine
{
    private readonly IPricingModel _pricingModel;

    /// <summary>
    /// 建構函數 - 注入定價模型
    /// </summary>
    /// <param name="pricingModel">定價模型（如 GarmanKohlhagenModel）</param>
    public DciPricingEngine(IPricingModel pricingModel)
    {
        _pricingModel = pricingModel ?? throw new ArgumentNullException(nameof(pricingModel));
    }

    /// <summary>
    /// 計算 DCI 報價（標準版）
    /// </summary>
    public DciQuoteResult Quote(DciInput input)
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

        // 使用注入的定價模型
        double optionPremiumDomesticPer1Foreign = _pricingModel.PriceFxOption(
            spot: spotD,
            strike: strikeD,
            domesticRate: input.RateDomestic,
            foreignRate: input.RateForeign,
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
    public DciQuoteResult QuoteWithMargin(DciInput input, double marginPercent)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 必須在 [0, 1) 範圍內");

        // 1) 計算理論報價
        var theoreticalQuote = Quote(input);

        // 2) 扣除 Margin
        decimal adjustedOptionInterest = 
            theoreticalQuote.InterestFromOption * (1m - (decimal)marginPercent);

        // 3) 重新計算總利息和 Coupon
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
    public DciQuoteResult QuoteFromSnapshot(
        MarketDataSnapshot snapshot,
        decimal notionalForeign,
        decimal strike,
        double tenorInYears,
        double depositRateAnnual)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // 驗證市場數據
        var validation = snapshot.Validate();
        if (!validation.IsValid)
            throw new ArgumentException(
                $"市場數據無效: {string.Join(", ", validation.Errors)}");

        // 建立 DciInput
        var input = snapshot.ToDciInput(
            notionalForeign,
            strike,
            tenorInYears,
            depositRateAnnual
        );

        // 計算報價
        return Quote(input);
    }

    /// <summary>
    /// 批量報價
    /// </summary>
    public IReadOnlyList<DciQuoteResult> QuoteBatch(IEnumerable<DciInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var results = new List<DciQuoteResult>();

        foreach (var input in inputs)
        {
            results.Add(Quote(input));
        }

        return results;
    }

    /// <summary>
    /// 產生報價摘要（格式化輸出）
    /// </summary>
    public string GenerateQuoteSummary(DciInput input, DciQuoteResult quote)
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
