using DciCalculator.Models;

namespace DciCalculator.Builders;

/// <summary>
/// DCI 輸入參數建構器
/// 使用 Fluent API 建立 DciInput 物件
/// </summary>
public sealed class DciInputBuilder
{
    private decimal _notionalForeign = 10_000m;
    private FxQuote _spotQuote = new(30.0m, 30.1m);
    private decimal _strike = 30.5m;
    private double _rateDomestic = 0.02;
    private double _rateForeign = 0.05;
    private double _volatility = 0.12;
    private double _tenorInYears = 0.25;
    private double _depositRateAnnual = 0.05;

    /// <summary>
    /// 設定外幣名義本金
    /// </summary>
    public DciInputBuilder WithNotionalForeign(decimal notionalForeign)
    {
        _notionalForeign = notionalForeign;
        return this;
    }

    /// <summary>
    /// 設定即期匯率報價
    /// </summary>
    public DciInputBuilder WithSpotQuote(FxQuote spotQuote)
    {
        _spotQuote = spotQuote;
        return this;
    }

    /// <summary>
    /// 設定即期匯率報價 (Bid/Ask)
    /// </summary>
    public DciInputBuilder WithSpotQuote(decimal bid, decimal ask)
    {
        _spotQuote = new FxQuote(bid, ask);
        return this;
    }

    /// <summary>
    /// 設定履約價
    /// </summary>
    public DciInputBuilder WithStrike(decimal strike)
    {
        _strike = strike;
        return this;
    }

    /// <summary>
    /// 設定本幣利率
    /// </summary>
    public DciInputBuilder WithRateDomestic(double rateDomestic)
    {
        _rateDomestic = rateDomestic;
        return this;
    }

    /// <summary>
    /// 設定外幣利率
    /// </summary>
    public DciInputBuilder WithRateForeign(double rateForeign)
    {
        _rateForeign = rateForeign;
        return this;
    }

    /// <summary>
    /// 設定波動率
    /// </summary>
    public DciInputBuilder WithVolatility(double volatility)
    {
        _volatility = volatility;
        return this;
    }

    /// <summary>
    /// 設定期限（年）
    /// </summary>
    public DciInputBuilder WithTenorInYears(double tenorInYears)
    {
        _tenorInYears = tenorInYears;
        return this;
    }

    /// <summary>
    /// 設定存款年利率
    /// </summary>
    public DciInputBuilder WithDepositRateAnnual(double depositRateAnnual)
    {
        _depositRateAnnual = depositRateAnnual;
        return this;
    }

    /// <summary>
    /// 建立 DciInput 實例
    /// </summary>
    public DciInput Build()
    {
        return new DciInput(
            NotionalForeign: _notionalForeign,
            SpotQuote: _spotQuote,
            Strike: _strike,
            RateDomestic: _rateDomestic,
            RateForeign: _rateForeign,
            Volatility: _volatility,
            TenorInYears: _tenorInYears,
            DepositRateAnnual: _depositRateAnnual
        );
    }

    /// <summary>
    /// 重置為預設值
    /// </summary>
    public DciInputBuilder Reset()
    {
        _notionalForeign = 10_000m;
        _spotQuote = new(30.0m, 30.1m);
        _strike = 30.5m;
        _rateDomestic = 0.02;
        _rateForeign = 0.05;
        _volatility = 0.12;
        _tenorInYears = 0.25;
        _depositRateAnnual = 0.05;
        return this;
    }

    /// <summary>
    /// 從現有 DciInput 建立 Builder（用於修改）
    /// </summary>
    public static DciInputBuilder From(DciInput input)
    {
        return new DciInputBuilder
        {
            _notionalForeign = input.NotionalForeign,
            _spotQuote = input.SpotQuote,
            _strike = input.Strike,
            _rateDomestic = input.RateDomestic,
            _rateForeign = input.RateForeign,
            _volatility = input.Volatility,
            _tenorInYears = input.TenorInYears,
            _depositRateAnnual = input.DepositRateAnnual
        };
    }

    /// <summary>
    /// 建立典型的 USD/TWD DCI 設定
    /// </summary>
    public static DciInputBuilder CreateTypicalUsdTwd()
    {
        return new DciInputBuilder()
            .WithNotionalForeign(10_000m)
            .WithSpotQuote(31.0m, 31.05m)
            .WithStrike(31.5m)
            .WithRateDomestic(0.015)
            .WithRateForeign(0.05)
            .WithVolatility(0.12)
            .WithTenorInYears(0.25)
            .WithDepositRateAnnual(0.05);
    }
}
