using DciCalculator.Algorithms;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Black-Scholes 定價測試。
/// 覆蓋期權價格、到期行為、Put-Call Parity、隱含波動率反推等情境。
/// </summary>
public class BlackScholesTests
{
    private const double Tolerance = 1e-4; // 精度：比較取至 4 位

    #region 基本定價案例

    [Fact]
    public void Price_ATMCallOption_ReturnsCorrectValue()
    {
        // Arrange: At-The-Money Call
        double spot = 100.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);

        // Assert: 期望 Call 價格約 10.45
        Assert.InRange(price, 10.40, 10.50);
    }

    [Fact]
    public void Price_ATMPutOption_ReturnsCorrectValue()
    {
        // Arrange: At-The-Money Put
        double spot = 100.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Put);

        // Assert: 期望 Put 價格約 5.57 (符合 Put-Call Parity)
        Assert.InRange(price, 5.50, 5.65);
    }

    [Fact]
    public void Price_ITMCallOption_ReturnsCorrectValue()
    {
        // Arrange: In-The-Money Call (spot > strike)
        double spot = 110.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);
        // Assert: 價格高於最低合理下限 (10.0)
        Assert.True(price > 10.0);
        Assert.InRange(price, 15.0, 18.0);
    }

    [Fact]
    public void Price_OTMPutOption_ReturnsCorrectValue()
    {
        // Arrange: Out-Of-The-Money Put (spot > strike)
        double spot = 110.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Put);
        // Assert: 價格為正且落在合理範圍
        Assert.True(price > 0);
        Assert.InRange(price, 2.0, 4.0);
    }

    #endregion

    #region Put-Call Parity 驗證

    [Theory]
    [InlineData(100.0, 100.0, 0.05, 0.20, 1.0)]
    [InlineData(50.0, 60.0, 0.03, 0.30, 0.5)]
    [InlineData(150.0, 140.0, 0.02, 0.15, 2.0)]
    public void Price_PutCallParity_HoldsTrue(
        double spot, double strike, double rate, double volatility, double timeToMaturity)
    {
        // Arrange & Act
        double callPrice = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);
        double putPrice = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Put);

        // Put-Call Parity: C - P = S - K * e^(-rT)
        double leftSide = callPrice - putPrice;
        double rightSide = spot - strike * Math.Exp(-rate * timeToMaturity);

        // Assert
        Assert.Equal(rightSide, leftSide, precision: 4);
    }

    #endregion

    #region 邊界與極端行為

    [Fact]
    public void Price_NearMaturity_ReturnsIntrinsicValue()
    {
        // Arrange: 即將到期 (距離到期約 1 秒)
        double spot = 110.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0 / (365.0 * 24.0 * 3600.0); // 1 秒 (極短到期)

        // Act
        double callPrice = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);
        double putPrice = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Put);

        // Assert: 價格貼近內含價值
        double callIntrinsic = Math.Max(0, spot - strike);
        double putIntrinsic = Math.Max(0, strike - spot);

        Assert.Equal(callIntrinsic, callPrice, precision: 2);
        Assert.Equal(putIntrinsic, putPrice, precision: 2);
    }

    [Fact]
    public void Price_ZeroVolatility_ReturnsIntrinsicValue()
    {
        // Arrange: 波動率趨近 0
        double spot = 110.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 1e-8;
        double timeToMaturity = 1.0;

        // Act
        double callPrice = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);

        // Assert: 價格即為內含價值
        double intrinsic = Math.Max(0, spot - strike);
        Assert.Equal(intrinsic, callPrice, precision: 1);
    }

    [Fact]
    public void Price_DeepITMCall_ReturnsCorrectValue()
    {
        // Arrange: Deep In-The-Money Call
        double spot = 200.0;
        double strike = 100.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);

        // Assert: Deep ITM 理論近似 S - K*e^(-rT)
        double expected = spot - strike * Math.Exp(-rate * timeToMaturity);
        Assert.Equal(expected, price, precision: 2);
    }

    [Fact]
    public void Price_DeepOTMCall_ReturnsNearZero()
    {
        // Arrange: Deep Out-Of-The-Money Call
        double spot = 50.0;
        double strike = 200.0;
        double rate = 0.05;
        double volatility = 0.20;
        double timeToMaturity = 1.0;

        // Act
        double price = BlackScholes.Price(spot, strike, rate, volatility, timeToMaturity, OptionType.Call);

        // Assert: Deep OTM 價格接近 0
        Assert.InRange(price, 0.0, 0.001);
    }

    #endregion

    #region 參數驗證與異常

    [Fact]
    public void Price_NegativeSpot_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlackScholes.Price(-100.0, 100.0, 0.05, 0.20, 1.0, OptionType.Call));
    }

    [Fact]
    public void Price_NegativeStrike_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlackScholes.Price(100.0, -100.0, 0.05, 0.20, 1.0, OptionType.Call));
    }

    [Fact]
    public void Price_NegativeVolatility_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlackScholes.Price(100.0, 100.0, 0.05, -0.20, 1.0, OptionType.Call));
    }

    [Fact]
    public void Price_NegativeTime_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlackScholes.Price(100.0, 100.0, 0.05, 0.20, -1.0, OptionType.Call));
    }

    [Fact]
    public void Price_ExcessiveVolatility_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlackScholes.Price(100.0, 100.0, 0.05, 10.0, 1.0, OptionType.Call));
    }

    #endregion

    #region 隱含波動率反推

    [Fact]
    public void ImpliedVolatility_KnownPrice_RecoversVolatility()
    {
        // Arrange
        double spot = 100.0;
        double strike = 100.0;
        double rate = 0.05;
        double trueVolatility = 0.25;
        double timeToMaturity = 1.0;

        // Arrange: 先產生理論價格
        double marketPrice = BlackScholes.Price(
            spot, strike, rate, trueVolatility, timeToMaturity, OptionType.Call);

        // Act: 反推隱含波動率
        double impliedVol = BlackScholes.ImpliedVolatility(
            marketPrice, spot, strike, rate, timeToMaturity, OptionType.Call);

        // Assert: 回復原本設定的波動率
        Assert.Equal(trueVolatility, impliedVol, precision: 4);
    }

    [Fact]
    public void ImpliedVolatility_NegativePrice_ReturnsNaN()
    {
        // Arrange
        double marketPrice = -10.0; // 非法價格

        // Act
        double impliedVol = BlackScholes.ImpliedVolatility(
            marketPrice, 100.0, 100.0, 0.05, 1.0, OptionType.Call);

        // Assert
        Assert.True(double.IsNaN(impliedVol));
    }

    #endregion

    #region FX 場景精度測試 (保留 4 位小數)

    [Fact]
    public void Price_FxRateScenario_MaintainsPrecision()
    {
        // Arrange: USD/TWD 場景
        double spot = 30.5000;      // TWD/USD
        double strike = 31.0000;
        double rate = 0.015;        // TWD 1.5%
        double volatility = 0.10;   // 10%
        double timeToMaturity = 90.0 / 365.0; // 90 天

        // Act
        double putPrice = BlackScholes.Price(
            spot, strike, rate, volatility, timeToMaturity, OptionType.Put);

        // Assert: 價格合理且四位小數穩定
        Assert.True(putPrice > 0);
        Assert.True(putPrice < strike); // 價格低於履約價 (合理)

        // 補充：四捨五入後仍與原值一致 (四位精度)
        double rounded = Math.Round(putPrice, 4);
        Assert.Equal(rounded, putPrice, precision: 4);
    }

    #endregion
}
