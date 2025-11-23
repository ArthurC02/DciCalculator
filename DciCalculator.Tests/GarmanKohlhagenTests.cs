using DciCalculator.Algorithms;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Garman-Kohlhagen FX 期權定價測試。
/// 驗證公式於貼現與波動率輸入的正確性與多種情境行為。
/// </summary>
public class GarmanKohlhagenTests
{
    private const double Tolerance = 1e-4;

    #region 基本定價案例

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

        // Assert: 合理價格 > 0
        Assert.True(price > 0);
        Assert.True(price < spot); // 不超過現貨
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

    #region Put-Call Parity (FX 驗證)

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

    #region 邊界與極端情境

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

        // Assert: Deep ITM 理論近似 S*e^(-r_f*T) - K*e^(-r_d*T)
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

        // Assert: Deep OTM 接近 0
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

        // Assert: 價格貼近內含價值
        double intrinsic = Math.Max(0, spot - strike);
        Assert.Equal(intrinsic, callPrice, precision: 1);
    }

    #endregion

    #region 參數驗證與異常

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

    #region 隱含波動率反推

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

    #region DCI 場景驗證

    [Fact]
    public void PriceFxOption_DciScenario_ReturnsReasonablePrice()
    {
        // Arrange: DCI 典型輸入 (90 天、Strike 接近現貨)
        double spot = 30.50;          // 現貨 USD/TWD
        double strike = 30.00;        // Strike 接近現貨
        double rDomestic = 0.015;     // TWD 1.5%
        double rForeign = 0.05;       // USD 5%
        double volatility = 0.10;     // 10% vol
        double timeToMaturity = 90.0 / 365.0;

        // Act: 計算 Put 價格 (DCI 結構使用 Put)
        double putPrice = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, volatility, timeToMaturity, OptionType.Put);

        // Assert: 合理價格介於 0.05 ~ 0.80 TWD per USD
        Assert.InRange(putPrice, 0.05, 0.80);

        // 四捨五入驗證
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

        // Act: 比較不同波動率
        double priceLowVol = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, 0.05, timeToMaturity, OptionType.Put);

        double priceHighVol = GarmanKohlhagen.PriceFxOption(
            spot, strike, rDomestic, rForeign, 0.20, timeToMaturity, OptionType.Put);

        // Assert: 高波動率價格 > 低波動率價格
        Assert.True(priceHighVol > priceLowVol);
    }

    #endregion
}
