using DciCalculator.Builders;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

public class BuilderTests
{
    #region DciInputBuilder Tests

    [Fact]
    public void DciInputBuilder_DefaultValues_CreatesValidObject()
    {
        // Arrange & Act
        var builder = new DciInputBuilder();
        var input = builder.Build();

        // Assert
        Assert.Equal(10_000m, input.NotionalForeign);
        Assert.Equal(30.0m, input.SpotQuote.Bid);
        Assert.Equal(30.1m, input.SpotQuote.Ask);
        Assert.Equal(30.5m, input.Strike);
        Assert.Equal(0.02, input.RateDomestic);
        Assert.Equal(0.05, input.RateForeign);
        Assert.Equal(0.12, input.Volatility);
        Assert.Equal(0.25, input.TenorInYears);
        Assert.Equal(0.05, input.DepositRateAnnual);
    }

    [Fact]
    public void DciInputBuilder_FluentApi_SetsAllProperties()
    {
        // Arrange & Act
        var input = new DciInputBuilder()
            .WithNotionalForeign(50_000m)
            .WithSpotQuote(31.0m, 31.05m)
            .WithStrike(32.0m)
            .WithRateDomestic(0.015)
            .WithRateForeign(0.045)
            .WithVolatility(0.15)
            .WithTenorInYears(0.5)
            .WithDepositRateAnnual(0.045)
            .Build();

        // Assert
        Assert.Equal(50_000m, input.NotionalForeign);
        Assert.Equal(31.0m, input.SpotQuote.Bid);
        Assert.Equal(31.05m, input.SpotQuote.Ask);
        Assert.Equal(32.0m, input.Strike);
        Assert.Equal(0.015, input.RateDomestic);
        Assert.Equal(0.045, input.RateForeign);
        Assert.Equal(0.15, input.Volatility);
        Assert.Equal(0.5, input.TenorInYears);
        Assert.Equal(0.045, input.DepositRateAnnual);
    }

    [Fact]
    public void DciInputBuilder_WithSpotQuoteObject_SetsQuote()
    {
        // Arrange
        var quote = new FxQuote(32.0m, 32.1m);

        // Act
        var input = new DciInputBuilder()
            .WithSpotQuote(quote)
            .Build();

        // Assert
        Assert.Equal(32.0m, input.SpotQuote.Bid);
        Assert.Equal(32.1m, input.SpotQuote.Ask);
    }

    [Fact]
    public void DciInputBuilder_Reset_RestoresToDefaults()
    {
        // Arrange
        var builder = new DciInputBuilder()
            .WithNotionalForeign(100_000m)
            .WithStrike(35.0m);

        // Act
        builder.Reset();
        var input = builder.Build();

        // Assert
        Assert.Equal(10_000m, input.NotionalForeign);
        Assert.Equal(30.5m, input.Strike);
    }

    [Fact]
    public void DciInputBuilder_From_CopiesAllProperties()
    {
        // Arrange
        var original = new DciInput(
            NotionalForeign: 20_000m,
            SpotQuote: new FxQuote(31.5m, 31.6m),
            Strike: 32.5m,
            RateDomestic: 0.025,
            RateForeign: 0.055,
            Volatility: 0.14,
            TenorInYears: 0.75,
            DepositRateAnnual: 0.055
        );

        // Act
        var modified = DciInputBuilder.From(original)
            .WithStrike(33.0m)
            .Build();

        // Assert
        Assert.Equal(20_000m, modified.NotionalForeign);
        Assert.Equal(31.5m, modified.SpotQuote.Bid);
        Assert.Equal(33.0m, modified.Strike); // Changed
        Assert.Equal(0.025, modified.RateDomestic);
        Assert.Equal(0.055, modified.RateForeign);
        Assert.Equal(0.14, modified.Volatility);
    }

    [Fact]
    public void DciInputBuilder_CreateTypicalUsdTwd_CreatesRealisticScenario()
    {
        // Act
        var input = DciInputBuilder.CreateTypicalUsdTwd().Build();

        // Assert
        Assert.Equal(10_000m, input.NotionalForeign);
        Assert.Equal(31.0m, input.SpotQuote.Bid);
        Assert.Equal(31.05m, input.SpotQuote.Ask);
        Assert.Equal(31.5m, input.Strike);
        Assert.Equal(0.015, input.RateDomestic);
        Assert.Equal(0.05, input.RateForeign);
        Assert.Equal(0.12, input.Volatility);
        Assert.Equal(0.25, input.TenorInYears);
    }

    [Fact]
    public void DciInputBuilder_MultipleBuilds_AreIndependent()
    {
        // Arrange
        var builder = new DciInputBuilder().WithStrike(35.0m);

        // Act
        var first = builder.Build();
        builder.WithStrike(36.0m);
        var second = builder.Build();

        // Assert
        Assert.Equal(35.0m, first.Strike);
        Assert.Equal(36.0m, second.Strike);
    }

    #endregion

    #region MarketDataSnapshotBuilder Tests

    [Fact]
    public void MarketDataSnapshotBuilder_DefaultValues_CreatesValidObject()
    {
        // Arrange & Act
        var builder = new MarketDataSnapshotBuilder();
        var snapshot = builder.Build();

        // Assert
        Assert.Equal("USD/TWD", snapshot.CurrencyPair);
        Assert.Equal(31.0m, snapshot.SpotQuote.Bid);
        Assert.Equal(31.05m, snapshot.SpotQuote.Ask);
        Assert.Equal(0.015, snapshot.RateDomestic);
        Assert.Equal(0.05, snapshot.RateForeign);
        Assert.Equal(0.12, snapshot.Volatility);
        Assert.True(snapshot.IsRealTime);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_FluentApi_SetsAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var snapshot = new MarketDataSnapshotBuilder()
            .WithCurrencyPair("EUR/USD")
            .WithSpotQuote(1.0800m, 1.0805m)
            .WithRateDomestic(0.05)
            .WithRateForeign(0.035)
            .WithVolatility(0.10)
            .WithTimestampUtc(timestamp)
            .WithForwardPoints(25.5m)
            .WithDataSource("Bloomberg")
            .WithIsRealTime(false)
            .Build();

        // Assert
        Assert.Equal("EUR/USD", snapshot.CurrencyPair);
        Assert.Equal(1.0800m, snapshot.SpotQuote.Bid);
        Assert.Equal(1.0805m, snapshot.SpotQuote.Ask);
        Assert.Equal(0.05, snapshot.RateDomestic);
        Assert.Equal(0.035, snapshot.RateForeign);
        Assert.Equal(0.10, snapshot.Volatility);
        Assert.Equal(timestamp, snapshot.TimestampUtc);
        Assert.Equal(25.5m, snapshot.ForwardPoints);
        Assert.Equal("Bloomberg", snapshot.DataSource);
        Assert.False(snapshot.IsRealTime);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_WithSpotQuoteObject_SetsQuote()
    {
        // Arrange
        var quote = new FxQuote(1.1000m, 1.1005m);

        // Act
        var snapshot = new MarketDataSnapshotBuilder()
            .WithSpotQuote(quote)
            .Build();

        // Assert
        Assert.Equal(1.1000m, snapshot.SpotQuote.Bid);
        Assert.Equal(1.1005m, snapshot.SpotQuote.Ask);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_Reset_RestoresToDefaults()
    {
        // Arrange
        var builder = new MarketDataSnapshotBuilder()
            .WithCurrencyPair("EUR/USD")
            .WithVolatility(0.20);

        // Act
        builder.Reset();
        var snapshot = builder.Build();

        // Assert
        Assert.Equal("USD/TWD", snapshot.CurrencyPair);
        Assert.Equal(0.12, snapshot.Volatility);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_From_CopiesAllProperties()
    {
        // Arrange
        var original = new MarketDataSnapshot(
            currencyPair: "GBP/USD",
            spotQuote: new FxQuote(1.2500m, 1.2505m),
            rateDomestic: 0.045,
            rateForeign: 0.055,
            volatility: 0.11
        )
        {
            ForwardPoints = 10.5m,
            DataSource = "Reuters",
            IsRealTime = true
        };

        // Act
        var modified = MarketDataSnapshotBuilder.From(original)
            .WithVolatility(0.13)
            .Build();

        // Assert
        Assert.Equal("GBP/USD", modified.CurrencyPair);
        Assert.Equal(1.2500m, modified.SpotQuote.Bid);
        Assert.Equal(0.13, modified.Volatility); // Changed
        Assert.Equal(10.5m, modified.ForwardPoints);
        Assert.Equal("Reuters", modified.DataSource);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_CreateTypicalUsdTwd_CreatesRealisticScenario()
    {
        // Act
        var snapshot = MarketDataSnapshotBuilder.CreateTypicalUsdTwd().Build();

        // Assert
        Assert.Equal("USD/TWD", snapshot.CurrencyPair);
        Assert.Equal(31.0m, snapshot.SpotQuote.Bid);
        Assert.Equal(31.05m, snapshot.SpotQuote.Ask);
        Assert.Equal(0.015, snapshot.RateDomestic);
        Assert.Equal(0.05, snapshot.RateForeign);
        Assert.Equal(0.12, snapshot.Volatility);
        Assert.Equal("Mock", snapshot.DataSource);
        Assert.True(snapshot.IsRealTime);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_CreateTypicalEurUsd_CreatesRealisticScenario()
    {
        // Act
        var snapshot = MarketDataSnapshotBuilder.CreateTypicalEurUsd().Build();

        // Assert
        Assert.Equal("EUR/USD", snapshot.CurrencyPair);
        Assert.Equal(1.0800m, snapshot.SpotQuote.Bid);
        Assert.Equal(1.0805m, snapshot.SpotQuote.Ask);
        Assert.Equal(0.05, snapshot.RateDomestic);
        Assert.Equal(0.035, snapshot.RateForeign);
        Assert.Equal(0.10, snapshot.Volatility);
        Assert.Equal("Mock", snapshot.DataSource);
    }

    [Fact]
    public void MarketDataSnapshotBuilder_MultipleBuilds_AreIndependent()
    {
        // Arrange
        var builder = new MarketDataSnapshotBuilder().WithVolatility(0.15);

        // Act
        var first = builder.Build();
        builder.WithVolatility(0.18);
        var second = builder.Build();

        // Assert
        Assert.Equal(0.15, first.Volatility);
        Assert.Equal(0.18, second.Volatility);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Builders_WorkTogetherInRealisticScenario()
    {
        // Arrange - 建立市場數據
        var marketData = MarketDataSnapshotBuilder.CreateTypicalUsdTwd()
            .WithVolatility(0.15)
            .Build();

        // Act - 使用市場數據建立 DCI 輸入
        var dciInput = new DciInputBuilder()
            .WithNotionalForeign(50_000m)
            .WithSpotQuote(marketData.SpotQuote)
            .WithStrike(31.5m)
            .WithRateDomestic(marketData.RateDomestic)
            .WithRateForeign(marketData.RateForeign)
            .WithVolatility(marketData.Volatility)
            .WithTenorInYears(0.25)
            .WithDepositRateAnnual(0.05)
            .Build();

        // Assert
        Assert.Equal(50_000m, dciInput.NotionalForeign);
        Assert.Equal(marketData.SpotQuote.Mid, dciInput.SpotQuote.Mid);
        Assert.Equal(0.15, dciInput.Volatility);
        Assert.Equal(marketData.RateDomestic, dciInput.RateDomestic);
    }

    #endregion
}
