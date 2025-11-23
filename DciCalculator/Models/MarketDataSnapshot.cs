namespace DciCalculator.Models;

/// <summary>
/// 市場資料快照 (Market Data Snapshot)
/// 封裝建立 DCI 所需的核心即時/延遲市場資訊。
/// 
/// 功能用途:
/// - 作為單一來源提供報價與參數
/// - 支援基本資料檢核與時效判斷
/// - 統一時間戳記格式 (UTC)
/// </summary>
public sealed record MarketDataSnapshot
{
    /// <summary>
    /// 建立時間戳 (UTC)
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// 幣別貨幣對 (例如 "USD/TWD")
    /// </summary>
    public string CurrencyPair { get; init; }

    /// <summary>
    /// 現貨報價 (Bid / Ask / Mid)
    /// </summary>
    public FxQuote SpotQuote { get; init; }

    /// <summary>
    /// 本幣年化利率 (Domestic Rate)
    /// </summary>
    public double RateDomestic { get; init; }

    /// <summary>
    /// 外幣年化利率 (Foreign Rate)
    /// </summary>
    public double RateForeign { get; init; }

    /// <summary>
    /// 隱含波動率 (Implied Volatility)
    /// </summary>
    public double Volatility { get; init; }

    /// <summary>
    /// 遠期點數 (Forward Points)，可由利率差推得
    /// </summary>
    public decimal? ForwardPoints { get; init; }

    /// <summary>
    /// 資料來源 (例如 "Bloomberg", "Reuters")
    /// </summary>
    public string? DataSource { get; init; }

    /// <summary>
    /// 是否為即時資料
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
                "波動率必須位於 (0, 5.0] 區間內");

        CurrencyPair = currencyPair;
        SpotQuote = spotQuote;
        RateDomestic = rateDomestic;
        RateForeign = rateForeign;
        Volatility = volatility;
        TimestampUtc = DateTime.UtcNow;
        IsRealTime = true;
    }

    /// <summary>
    /// 判斷資料是否過期 (超過指定秒數)
    /// </summary>
    public bool IsStale(int maxAgeSeconds = 60)
    {
        TimeSpan age = DateTime.UtcNow - TimestampUtc;
        return age.TotalSeconds > maxAgeSeconds;
    }

    /// <summary>
    /// 計算遠期匯率 Forward (根據利率平價關係)
    /// Forward = Spot * exp((r_dom - r_for) * T)
    /// </summary>
    public decimal CalculateForward(double tenorInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenorInYears);

        double spotD = (double)SpotQuote.Mid;
        double forward = spotD * Math.Exp((RateDomestic - RateForeign) * tenorInYears);
        
        return (decimal)forward;
    }

    /// <summary>
    /// 計算遠期點數 (Forward - Spot)
    /// </summary>
    public decimal CalculateForwardPoints(double tenorInYears)
    {
        decimal forward = CalculateForward(tenorInYears);
        return forward - SpotQuote.Mid;
    }

    /// <summary>
    /// 驗證市場資料合理性並回傳錯誤集合
    /// </summary>
    public MarketDataValidationResult Validate()
    {
        var errors = new List<string>();

        // Spot 基本檢核
        if (SpotQuote.Bid <= 0 || SpotQuote.Ask <= 0)
            errors.Add("Spot Bid/Ask 必須 > 0");

        if (SpotQuote.Bid >= SpotQuote.Ask)
            errors.Add($"Spot Bid ({SpotQuote.Bid}) 必須 < Ask ({SpotQuote.Ask})");

        decimal spread = SpotQuote.Ask - SpotQuote.Bid;
        decimal spreadPercent = spread / SpotQuote.Mid;
        if (spreadPercent > 0.01m) // 1% spread 上限
            errors.Add($"Spot Spread 過大: {spreadPercent:P2}");

        // 利率範圍檢核
        if (RateDomestic < -0.20 || RateDomestic > 0.50)
            errors.Add($"本幣利率異常: {RateDomestic:P2}");

        if (RateForeign < -0.20 || RateForeign > 0.50)
            errors.Add($"外幣利率異常: {RateForeign:P2}");

        // 波動率範圍檢核
        if (Volatility < 0.01 || Volatility > 2.0)
            errors.Add($"波動率異常: {Volatility:P2}");

        // 新鮮度/時效檢核
        if (IsStale(maxAgeSeconds: 300)) // 5 分鐘
            errors.Add($"資料過期: {(DateTime.UtcNow - TimestampUtc).TotalMinutes:F1} 分鐘前");

        return new MarketDataValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors
        );
    }

    /// <summary>
    /// 轉換為 DciInput (定價所需輸入結構)
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
    /// 建立模擬/測試用快照
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
/// 市場資料驗證結果
/// </summary>
public sealed record MarketDataValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);
