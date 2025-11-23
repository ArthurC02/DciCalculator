using DciCalculator.Models;

namespace DciCalculator.Builders;

/// <summary>
/// 市場數據快照建構器
/// 使用 Fluent API 建立 MarketDataSnapshot 物件
/// </summary>
public sealed class MarketDataSnapshotBuilder
{
    private string _currencyPair = "USD/TWD";
    private FxQuote _spotQuote = new(31.0m, 31.05m);
    private double _rateDomestic = 0.015;
    private double _rateForeign = 0.05;
    private double _volatility = 0.12;
    private DateTime _timestampUtc = DateTime.UtcNow;
    private decimal? _forwardPoints = null;
    private string? _dataSource = null;
    private bool _isRealTime = true;

    /// <summary>
    /// 設定幣別對
    /// </summary>
    public MarketDataSnapshotBuilder WithCurrencyPair(string currencyPair)
    {
        _currencyPair = currencyPair;
        return this;
    }

    /// <summary>
    /// 設定即期匯率報價
    /// </summary>
    public MarketDataSnapshotBuilder WithSpotQuote(FxQuote spotQuote)
    {
        _spotQuote = spotQuote;
        return this;
    }

    /// <summary>
    /// 設定即期匯率報價 (Bid/Ask)
    /// </summary>
    public MarketDataSnapshotBuilder WithSpotQuote(decimal bid, decimal ask)
    {
        _spotQuote = new FxQuote(bid, ask);
        return this;
    }

    /// <summary>
    /// 設定本幣利率
    /// </summary>
    public MarketDataSnapshotBuilder WithRateDomestic(double rateDomestic)
    {
        _rateDomestic = rateDomestic;
        return this;
    }

    /// <summary>
    /// 設定外幣利率
    /// </summary>
    public MarketDataSnapshotBuilder WithRateForeign(double rateForeign)
    {
        _rateForeign = rateForeign;
        return this;
    }

    /// <summary>
    /// 設定波動率
    /// </summary>
    public MarketDataSnapshotBuilder WithVolatility(double volatility)
    {
        _volatility = volatility;
        return this;
    }

    /// <summary>
    /// 設定時間戳（UTC）
    /// </summary>
    public MarketDataSnapshotBuilder WithTimestampUtc(DateTime timestampUtc)
    {
        _timestampUtc = timestampUtc;
        return this;
    }

    /// <summary>
    /// 設定遠期點數
    /// </summary>
    public MarketDataSnapshotBuilder WithForwardPoints(decimal? forwardPoints)
    {
        _forwardPoints = forwardPoints;
        return this;
    }

    /// <summary>
    /// 設定資料來源
    /// </summary>
    public MarketDataSnapshotBuilder WithDataSource(string? dataSource)
    {
        _dataSource = dataSource;
        return this;
    }

    /// <summary>
    /// 設定是否為即時資料
    /// </summary>
    public MarketDataSnapshotBuilder WithIsRealTime(bool isRealTime)
    {
        _isRealTime = isRealTime;
        return this;
    }

    /// <summary>
    /// 建立 MarketDataSnapshot 實例
    /// </summary>
    public MarketDataSnapshot Build()
    {
        var snapshot = new MarketDataSnapshot(
            currencyPair: _currencyPair,
            spotQuote: _spotQuote,
            rateDomestic: _rateDomestic,
            rateForeign: _rateForeign,
            volatility: _volatility
        )
        {
            TimestampUtc = _timestampUtc,
            ForwardPoints = _forwardPoints,
            DataSource = _dataSource,
            IsRealTime = _isRealTime
        };

        return snapshot;
    }

    /// <summary>
    /// 重置為預設值
    /// </summary>
    public MarketDataSnapshotBuilder Reset()
    {
        _currencyPair = "USD/TWD";
        _spotQuote = new(31.0m, 31.05m);
        _rateDomestic = 0.015;
        _rateForeign = 0.05;
        _volatility = 0.12;
        _timestampUtc = DateTime.UtcNow;
        _forwardPoints = null;
        _dataSource = null;
        _isRealTime = true;
        return this;
    }

    /// <summary>
    /// 從現有 MarketDataSnapshot 建立 Builder（用於修改）
    /// </summary>
    public static MarketDataSnapshotBuilder From(MarketDataSnapshot snapshot)
    {
        return new MarketDataSnapshotBuilder
        {
            _currencyPair = snapshot.CurrencyPair,
            _spotQuote = snapshot.SpotQuote,
            _rateDomestic = snapshot.RateDomestic,
            _rateForeign = snapshot.RateForeign,
            _volatility = snapshot.Volatility,
            _timestampUtc = snapshot.TimestampUtc,
            _forwardPoints = snapshot.ForwardPoints,
            _dataSource = snapshot.DataSource,
            _isRealTime = snapshot.IsRealTime
        };
    }

    /// <summary>
    /// 建立典型的 USD/TWD 市場數據
    /// </summary>
    public static MarketDataSnapshotBuilder CreateTypicalUsdTwd()
    {
        return new MarketDataSnapshotBuilder()
            .WithCurrencyPair("USD/TWD")
            .WithSpotQuote(31.0m, 31.05m)
            .WithRateDomestic(0.015)
            .WithRateForeign(0.05)
            .WithVolatility(0.12)
            .WithDataSource("Mock")
            .WithIsRealTime(true);
    }

    /// <summary>
    /// 建立典型的 EUR/USD 市場數據
    /// </summary>
    public static MarketDataSnapshotBuilder CreateTypicalEurUsd()
    {
        return new MarketDataSnapshotBuilder()
            .WithCurrencyPair("EUR/USD")
            .WithSpotQuote(1.0800m, 1.0805m)
            .WithRateDomestic(0.05)
            .WithRateForeign(0.035)
            .WithVolatility(0.10)
            .WithDataSource("Mock")
            .WithIsRealTime(true);
    }
}
