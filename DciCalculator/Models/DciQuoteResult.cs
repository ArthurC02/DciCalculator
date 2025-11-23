namespace DciCalculator.Models;

public sealed record DciQuoteResult(
    decimal NotionalForeign,
    decimal InterestFromDeposit,   // 外幣定存利息
    decimal InterestFromOption,    // 由期權費折算的外幣利息
    decimal TotalInterestForeign,  // 總外幣利息
    double CouponAnnual           // 總年化收益率（以外幣計）
);

