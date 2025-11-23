namespace DciCalculator.Models;

/// <summary>
/// 市場數據快照
/// 整合所有 DCI 定價所需的市場資訊
/// 
/// 用途：
/// - 統一管理市場數據來源
/// - 驗證數據完整性和新鮮度
/// - 提供快照時間戳記
/// </summary>
public sealed record MarketDataSnapshot
{
    /// <summary>
    /// 快照時間戳記（UTC）
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// 貨幣對（例如 "USD/TWD"）
    /// </summary>
    public string CurrencyPair { get; init; }

    /// <summary>
    /// Spot 匯率報價（Bid/Ask/Mid）
    /// </summary>
    public FxQuote SpotQuote { get; init; }

    /// <summary>
    /// 本幣利率（年化）
    /// </summary>
    public double RateDomestic { get; init; }

    /// <summary>
    /// 外幣利率（年化）
    /// </summary>
    public double RateForeign { get; init; }

    /// <summary>
    /// FX 波動度（年化）
    /// </summary>
    public double Volatility { get; init; }

    /// <summary>
    /// Forward Points（選用，可從利率推算）
    /// </summary>
    public decimal? ForwardPoints { get; init; }

    /// <summary>
    /// 數據來源（例如 "Bloomberg", "Reuters"）
    /// </summary>
    public string? DataSource { get; init; }

    /// <summary>
    /// 是否為即時數據
    /// </summary>
    public bool IsRealTime { get; init; }

    public MarketDataSnapshot(
        string currencyPair,
        FxQuote spotQuote,
        double rateDomestic,
        double rateForeign,
        double volatility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyPair);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spotQuote.Mid);
        
        if (volatility <= 0 || volatility > 5.0)
            throw new ArgumentOutOfRangeException(nameof(volatility),
                "波動度必須在 (0, 5.0] 範圍內");

        CurrencyPair = currencyPair;
        SpotQuote = spotQuote;
        RateDomestic = rateDomestic;
        RateForeign = rateForeign;
        Volatility = volatility;
        TimestampUtc = DateTime.UtcNow;
        IsRealTime = true;
    }

    /// <summary>
    /// 檢查數據是否過期（超過指定秒數）
    /// </summary>
    public bool IsStale(int maxAgeSeconds = 60)
    {
        TimeSpan age = DateTime.UtcNow - TimestampUtc;
        return age.TotalSeconds > maxAgeSeconds;
    }

    /// <summary>
    /// 計算 Forward 匯率（基於利率平價）
    /// </summary>
    public decimal CalculateForward(double tenorInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenorInYears);

        double spotD = (double)SpotQuote.Mid;
        double forward = spotD * Math.Exp((RateDomestic - RateForeign) * tenorInYears);
        
        return (decimal)forward;
    }

    /// <summary>
    /// 計算 Forward Points
    /// </summary>
    public decimal CalculateForwardPoints(double tenorInYears)
    {
        decimal forward = CalculateForward(tenorInYears);
        return forward - SpotQuote.Mid;
    }

    /// <summary>
    /// 驗證數據完整性
    /// </summary>
    public MarketDataValidationResult Validate()
    {
        var errors = new List<string>();

        // Spot 檢查
        if (SpotQuote.Bid <= 0 || SpotQuote.Ask <= 0)
            errors.Add("Spot Bid/Ask 必須 > 0");

        if (SpotQuote.Bid >= SpotQuote.Ask)
            errors.Add($"Spot Bid ({SpotQuote.Bid}) 必須 < Ask ({SpotQuote.Ask})");

        decimal spread = SpotQuote.Ask - SpotQuote.Bid;
        decimal spreadPercent = spread / SpotQuote.Mid;
        if (spreadPercent > 0.01m) // 1% spread 警告
            errors.Add($"Spot Spread 過大: {spreadPercent:P2}");

        // 利率檢查
        if (RateDomestic < -0.20 || RateDomestic > 0.50)
            errors.Add($"本幣利率異常: {RateDomestic:P2}");

        if (RateForeign < -0.20 || RateForeign > 0.50)
            errors.Add($"外幣利率異常: {RateForeign:P2}");

        // 波動度檢查
        if (Volatility < 0.01 || Volatility > 2.0)
            errors.Add($"波動度異常: {Volatility:P2}");

        // 時效性檢查
        if (IsStale(maxAgeSeconds: 300)) // 5 分鐘
            errors.Add($"數據過期: {(DateTime.UtcNow - TimestampUtc).TotalMinutes:F1} 分鐘前");

        return new MarketDataValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors
        );
    }

    /// <summary>
    /// 建立 DciInput（方便轉換）
    /// </summary>
    public DciInput ToDciInput(
        decimal notionalForeign,
        decimal strike,
        double tenorInYears,
        double depositRateAnnual)
    {
        return new DciInput(
            NotionalForeign: notionalForeign,
            SpotQuote: SpotQuote,
            Strike: strike,
            RateDomestic: RateDomestic,
            RateForeign: RateForeign,
            Volatility: Volatility,
            TenorInYears: tenorInYears,
            DepositRateAnnual: depositRateAnnual
        );
    }

    /// <summary>
    /// 建立測試/模擬數據
    /// </summary>
    public static MarketDataSnapshot CreateMock(
        string currencyPair = "USD/TWD",
        decimal spotMid = 30.50m,
        decimal spreadPips = 2m,
        double rateDomestic = 0.015,
        double rateForeign = 0.05,
        double volatility = 0.10)
    {
        decimal halfSpread = spreadPips * 0.01m / 2m;
        var spotQuote = new FxQuote(
            Bid: spotMid - halfSpread,
            Ask: spotMid + halfSpread
        );

        return new MarketDataSnapshot(
            currencyPair,
            spotQuote,
            rateDomestic,
            rateForeign,
            volatility)
        {
            DataSource = "Mock",
            IsRealTime = false
        };
    }
}

/// <summary>
/// 市場數據驗證結果
/// </summary>
public sealed record MarketDataValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);
