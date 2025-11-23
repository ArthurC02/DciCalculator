using DciCalculator;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// MarginCalculator ³æ¤¸´ú¸Õ
/// </summary>
public class MarginCalculatorTests
{
    [Fact]
    public void ApplyMarginToStrike_WithPositiveMargin_LowersStrike()
    {
        // Arrange
        decimal theoreticalStrike = 30.00m;
        decimal marginPips = 10m; // 10 pips = 0.10

        // Act
        decimal adjustedStrike = MarginCalculator.ApplyMarginToStrike(
            theoreticalStrike, marginPips, pipSize: 0.01m);

        // Assert
        Assert.Equal(29.90m, adjustedStrike);
    }

    [Fact]
    public void ApplyMarginToPrice_With10Percent_Reduces10Percent()
    {
        // Arrange
        decimal theoreticalPrice = 0.50m;
        double marginPercent = 0.10;

        // Act
        decimal adjustedPrice = MarginCalculator.ApplyMarginToPrice(
            theoreticalPrice, marginPercent);

        // Assert
        Assert.Equal(0.45m, adjustedPrice);
    }

    [Fact]
    public void SolveMarginForTargetCoupon_ReturnsCorrectMargin()
    {
        // Arrange
        double theoreticalCoupon = 0.10; // 10%
        double targetCoupon = 0.08;      // 8%

        // Act
        double margin = MarginCalculator.SolveMarginForTargetCoupon(
            theoreticalCoupon, targetCoupon);

        // Assert: margin ? 20%
        Assert.InRange(margin, 0.15, 0.25);
    }

    [Fact]
    public void ApplySpread_CalculatesCorrectBidAsk()
    {
        // Arrange
        decimal mid = 30.50m;
        decimal spreadPips = 2m; // 2 pips

        // Act
        var (bid, ask) = MarginCalculator.ApplySpread(mid, spreadPips, pipSize: 0.01m);

        // Assert
        Assert.Equal(30.49m, bid);
        Assert.Equal(30.51m, ask);
    }
}
