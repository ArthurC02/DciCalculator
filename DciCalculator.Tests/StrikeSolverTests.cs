using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// StrikeSolver 測試：驗證為達成目標年化 Coupon 之 Strike 反求與階梯生成合理性。
/// </summary>
public class StrikeSolverTests
{
    [Fact]
    public void SolveStrike_ForTargetCoupon_Converges()
    {
        // Arrange: 建立 DCI 輸入
        var spotQuote = new FxQuote(Bid: 30.48m, Ask: 30.52m);
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: spotQuote,
            Strike: 30.00m, // 初始猜測值
            RateDomestic: 0.015,
            RateForeign: 0.05,
            Volatility: 0.10,
            TenorInYears: 90.0 / 365.0,
            DepositRateAnnual: 0.03
        );

        double targetCoupon = 0.08; // 8%

        // Act
        decimal strike = StrikeSolver.SolveStrike(input, targetCoupon);

        // Assert: 正常求得 Strike 並使 Coupon 接近目標
        Assert.True(strike > 0);
        Assert.True(StrikeSolver.IsStrikeReasonable(strike, spotQuote.Mid));

        // 再次估價驗證 Coupon
        var verifyInput = input with { Strike = strike };
        var verifyQuote = DciPricer.Quote(verifyInput);
        Assert.Equal(targetCoupon, verifyQuote.CouponAnnual, precision: 3);
    }

    [Fact]
    public void GenerateStrikeLadder_Returns10Strikes()
    {
        // Arrange
        var spotQuote = new FxQuote(Bid: 30.48m, Ask: 30.52m);
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: spotQuote,
            Strike: 30.00m,
            RateDomestic: 0.015,
            RateForeign: 0.05,
            Volatility: 0.10,
            TenorInYears: 90.0 / 365.0,
            DepositRateAnnual: 0.03
        );

        // Act
        var ladder = StrikeSolver.GenerateStrikeLadder(
            input, strikeCount: 10, minStrikeRatio: 0.95, maxStrikeRatio: 1.00);

        // Assert
        Assert.Equal(10, ladder.Count);

        // Coupon 應隨 Strike 上升而上升
        for (int i = 1; i < ladder.Count; i++)
        {
            Assert.True(ladder[i].Strike > ladder[i - 1].Strike);
            Assert.True(ladder[i].Coupon > ladder[i - 1].Coupon);
        }
    }

    [Fact]
    public void IsStrikeReasonable_WithValidStrike_ReturnsTrue()
    {
        // Arrange
        decimal strike = 29.50m;
        decimal spot = 30.50m;

        // Act
        bool isReasonable = StrikeSolver.IsStrikeReasonable(strike, spot);

        // Assert
        Assert.True(isReasonable);
    }

    [Fact]
    public void IsStrikeReasonable_WithTooLowStrike_ReturnsFalse()
    {
        // Arrange: Strike < Spot * 0.8
        decimal strike = 20.00m;
        decimal spot = 30.50m;

        // Act
        bool isReasonable = StrikeSolver.IsStrikeReasonable(strike, spot);

        // Assert
        Assert.False(isReasonable);
    }
}
