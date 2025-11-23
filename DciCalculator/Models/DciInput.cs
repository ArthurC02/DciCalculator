namespace DciCalculator.Models;

public sealed record DciInput(
    decimal NotionalForeign,   // 客戶投資外幣本金（例如 10_000 USD）
    FxQuote SpotQuote,         // 即期匯率 Bid/Ask/Mid
    decimal Strike,            // 履約價（例如 32.00 TWD/USD）
    double RateDomestic,      // 本幣利率（例：TWD 1.5% = 0.015）
    double RateForeign,       // 外幣利率（例：USD 5% = 0.05）
    double Volatility,        // FX 年化波動度（例如 0.15）
    double TenorInYears,      // 期限（年），例：30/365d
    double DepositRateAnnual  // 外幣定存年利率（可等於 RateForeign 或銀行自訂）
);

