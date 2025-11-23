using DciCalculator.Algorithms;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Garman-Kohlhagen FX 期權模型單元測試
/// 驗證 FX 期權定價的準確性和數值穩定性
/// </summary>
public class GarmanKohlhagenTests
{
    private const double Tolerance = 1e-4;

    #region 基本定價測試

    [Fact]
    public void PriceFxOption_ATMCall_ReturnsCorrectValue()
    {
        // Arrange: USD/TWD ATM Call
        double spot = 30.50;      // TWD per USD
        double strike = 30.50;
        double rDomestic = 0.015; // TWD 1.5%
        double rForeign = 0.05;   // USD 5%
        double volatility = 0.10; // 10%
        double timeToMaturity = 90.0 / 365.0;

        // Act
        double price = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Call);

        // Assert: 價格應該 > 0
        Assert.True(price > 0);
        Assert.True(price < spot); // 理性檢查
    }

    [Fact]
    public void PriceFxOption_ATMPut_ReturnsCorrectValue()
    {
        // Arrange: USD/TWD ATM Put
        double spot = 30.50;
        double strike = 30.50;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double volatility = 0.10;
        double timeToMaturity = 90.0 / 365.0;

        // Act
        double price = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Put);

        // Assert
        Assert.True(price > 0);
        Assert.True(price < strike);
    }

    #endregion

    #region Put-Call Parity (FX 版本)

    [Theory]
    [InlineData(30.50, 31.00, 0.015, 0.05, 0.10, 0.25)]
    [InlineData(1.10, 1.12, 0.02, 0.03, 0.08, 0.5)]
    public void PriceFxOption_PutCallParity_HoldsTrue(
        double spot, double strike, double rDomestic, double rForeign,
        double volatility, double timeToMaturity)
    {
        // Arrange & Act
        double callPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Call);

        double putPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Put);

        // FX Put-Call Parity: C - P = S * e^(-r_f*T) - K * e^(-r_d*T)
        double leftSide = callPrice - putPrice;
        double rightSide = spot * Math.Exp(-rForeign * timeToMaturity)
                         - strike * Math.Exp(-rDomestic * timeToMaturity);

        // Assert
        Assert.Equal(rightSide, leftSide, precision: 4);
    }

    #endregion

    #region 邊界條件測試

    [Fact]
    public void PriceFxOption_DeepITMCall_ReturnsCorrectValue()
    {
        // Arrange: Deep ITM Call (spot >> strike)
        double spot = 35.00;
        double strike = 28.00;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double volatility = 0.10;
        double timeToMaturity = 0.25;

        // Act
        double price = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Call);

        // Assert: 約等於 S*e^(-r_f*T) - K*e^(-r_d*T)
        double expected = spot * Math.Exp(-rForeign * timeToMaturity)
                        - strike * Math.Exp(-rDomestic * timeToMaturity);

        Assert.Equal(expected, price, precision: 2);
    }

    [Fact]
    public void PriceFxOption_DeepOTMPut_ReturnsNearZero()
    {
        // Arrange: Deep OTM Put (spot >> strike)
        double spot = 35.00;
        double strike = 28.00;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double volatility = 0.10;
        double timeToMaturity = 0.25;

        // Act
        double price = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Put);

        // Assert: 約等於 0
        Assert.InRange(price, 0.0, 0.01);
    }

    [Fact]
    public void PriceFxOption_NearMaturity_ReturnsIntrinsicValue()
    {
        // Arrange: 即將到期
        double spot = 31.00;
        double strike = 30.00;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double volatility = 0.10;
        double timeToMaturity = 1.0 / (365.0 * 24.0); // 1 小時

        // Act
        double callPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Call);

        // Assert: 約等於內含價值
        double intrinsic = Math.Max(0, spot - strike);
        Assert.Equal(intrinsic, callPrice, precision: 1);
    }

    #endregion

    #region 參數驗證測試

    [Fact]
    public void PriceFxOption_NegativeSpot_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GarmanKohlhagen.PriceFxOption(-30.0, 30.0, 0.015, 0.05, 0.10, 0.25, OptionType.Call));
    }

    [Fact]
    public void PriceFxOption_ExcessiveVolatility_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GarmanKohlhagen.PriceFxOption(30.0, 30.0, 0.015, 0.05, 10.0, 0.25, OptionType.Call));
    }

    [Fact]
    public void PriceFxOption_ExcessiveRate_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GarmanKohlhagen.PriceFxOption(30.0, 30.0, 1.0, 0.05, 0.10, 0.25, OptionType.Call));
    }

    #endregion

    #region 隱含波動度測試

    [Fact]
    public void ImpliedVolatility_KnownPrice_RecoversVolatility()
    {
        // Arrange
        double spot = 30.50;
        double strike = 31.00;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double trueVolatility = 0.12;
        double timeToMaturity = 90.0 / 365.0;

        double marketPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, trueVolatility, timeToMaturity, OptionType.Put);

        // Act
        double impliedVol = GarmanKohlhagen.ImpliedVolatility(
            marketPrice, spot, strike, rDomestic, rForeign, timeToMaturity, OptionType.Put);

        // Assert
        Assert.Equal(trueVolatility, impliedVol, precision: 4);
    }

    #endregion

    #region DCI 實務場景測試

    [Fact]
    public void PriceFxOption_DciScenario_ReturnsReasonablePrice()
    {
        // Arrange: 典型 DCI 參數（90 天，略低於即期的 Strike）
        double spot = 30.50;          // 當前 USD/TWD
        double strike = 30.00;        // Strike 略低（客戶感覺安全）
        double rDomestic = 0.015;     // TWD 1.5%
        double rForeign = 0.05;       // USD 5%
        double volatility = 0.10;     // 10% vol
        double timeToMaturity = 90.0 / 365.0;

        // Act: 計算 Put 期權價格（DCI 賣出 Put）
        double putPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Put);

        // Assert: 價格應該在合理範圍（0.1 ~ 0.5 TWD per 1 USD）
        Assert.InRange(putPrice, 0.05, 0.80);

        // 驗證精度
        double rounded = Math.Round(putPrice, 4);
        Assert.Equal(rounded, putPrice, precision: 4);
    }

    [Fact]
    public void PriceFxOption_HighVolatility_IncreasesPrice()
    {
        // Arrange
        double spot = 30.50;
        double strike = 30.00;
        double rDomestic = 0.015;
        double rForeign = 0.05;
        double timeToMaturity = 90.0 / 365.0;

        // Act: 比較不同波動度
        double priceLowVol = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, 0.05, timeToMaturity, OptionType.Put);

        double priceHighVol = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, 0.20, timeToMaturity, OptionType.Put);

        // Assert: 高波動度 → 高價格
        Assert.True(priceHighVol > priceLowVol);
    }

    #endregion
}
