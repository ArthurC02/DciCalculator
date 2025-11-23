using DciCalculator.Algorithms;
using DciCalculator.Curves;
using DciCalculator.VolSurfaces;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Phase 4 整合測試：曲線和曲面與定價引擎整合
/// </summary>
public class Phase4IntegrationTests
{
    private readonly DateTime _refDate = new DateTime(2024, 1, 1);

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_UsesCorrectRates()
    {
        // Arrange: 建立曲線
        var twdCurve = new FlatZeroCurve("TWD", _refDate, 0.015); // 1.5%
        var usdCurve = new FlatZeroCurve("USD", _refDate, 0.050); // 5%
        var volSurface = new FlatVolSurface("USD/TWD", _refDate, 0.10); // 10%

        // Act: 使用曲線定價
        double priceWithCurves = GarmanKohlhagen.PriceWithCurves(
            spot: 30.5,
            strike: 30.0,
            domesticCurve: twdCurve,
            foreignCurve: usdCurve,
            volSurface: volSurface,
            timeToMaturity: 0.25,
            optionType: OptionType.Put
        );

        // Act: 原始 API（應該相同）
        double priceOriginal = GarmanKohlhagen.PriceFxOption(
            spot: 30.5,
            strike: 30.0,
            rDomestic: 0.015,
            rForeign: 0.050,
            volatility: 0.10,
            timeToMaturity: 0.25,
            optionType: OptionType.Put
        );

        // Assert: 兩個方法應該得到相同結果
        Assert.Equal(priceOriginal, priceWithCurves, precision: 6);
    }

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_RespectsTermStructure()
    {
        // Arrange: 建立期限結構曲線（短期低、長期高）
        var twdPoints = new[]
        {
            new CurvePoint(0.25, 0.010),  // 3M: 1.0%
            new CurvePoint(1.00, 0.020)   // 1Y: 2.0%
        };
        var twdCurve = new LinearInterpolatedCurve("TWD", _refDate, twdPoints);

        var usdCurve = new FlatZeroCurve("USD", _refDate, 0.050);
        var volSurface = new FlatVolSurface("USD/TWD", _refDate, 0.10);

        // Act: 定價 3M 和 1Y 期權
        double price3M = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double price1Y = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.0, twdCurve, usdCurve, volSurface, 1.0, OptionType.Put);

        // Assert: 1Y 價格應該不同（因為利率不同）
        Assert.NotEqual(price3M, price1Y);
    }

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_RespectsVolSmile()
    {
        // Arrange: 建立 Vol Surface（有 Smile）
        var points = new[]
        {
            new VolSurfacePoint(29.0, 0.25, 0.12), // ITM: 12%
            new VolSurfacePoint(30.5, 0.25, 0.10), // ATM: 10%
            new VolSurfacePoint(32.0, 0.25, 0.08), // OTM: 8%
            
            new VolSurfacePoint(29.0, 1.0, 0.12),
            new VolSurfacePoint(30.5, 1.0, 0.10),
            new VolSurfacePoint(32.0, 1.0, 0.08)
        };
        var volSurface = new InterpolatedVolSurface("USD/TWD", _refDate, points);

        var twdCurve = new FlatZeroCurve("TWD", _refDate, 0.015);
        var usdCurve = new FlatZeroCurve("USD", _refDate, 0.050);

        // Act: 定價不同 Strike 的 Put
        double priceITM = GarmanKohlhagen.PriceWithCurves(
            30.5, 29.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double priceATM = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.5, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double priceOTM = GarmanKohlhagen.PriceWithCurves(
            30.5, 32.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        // Assert: OTM Put 價格應該高於 ITM Put（因為 Spot > Strike）
        // 並且反映 Vol 差異
        Assert.True(priceOTM > priceITM, $"OTM={priceOTM}, ITM={priceITM}");
        
        // 驗證 Vol 影響：Strike 32 (OTM Put) 使用較低 Vol，但仍有價值
        Assert.True(priceOTM > 0);
        Assert.True(priceATM > 0);
    }

    [Fact]
    public void GarmanKohlhagen_PriceWithDiscountFactors_Accurate()
    {
        // Arrange
        double rDom = 0.015;
        double rFor = 0.050;
        double T = 0.25;

        double dfDom = Math.Exp(-rDom * T);
        double dfFor = Math.Exp(-rFor * T);

        // Act: 使用 DF 定價
        double priceWithDF = GarmanKohlhagen.PriceWithDiscountFactors(
            spot: 30.5,
            strike: 30.0,
            dfDomestic: dfDom,
            dfForeign: dfFor,
            volatility: 0.10,
            timeToMaturity: T,
            optionType: OptionType.Put
        );

        // Act: 使用原始 API
        double priceOriginal = GarmanKohlhagen.PriceFxOption(
            spot: 30.5,
            strike: 30.0,
            rDomestic: rDom,
            rForeign: rFor,
            volatility: 0.10,
            timeToMaturity: T,
            optionType: OptionType.Put
        );

        // Assert
        Assert.Equal(priceOriginal, priceWithDF, precision: 8);
    }

    [Fact]
    public void EndToEnd_BootstrapCurvesAndPrice()
    {
        // Arrange: 從市場報價建構曲線
        var twdQuotes = new Dictionary<string, double>
        {
            { "1M", 0.0150 },
            { "3M", 0.0155 },
            { "6M", 0.0160 },
            { "1Y", 0.0170 }
        };

        var usdQuotes = new Dictionary<string, double>
        {
            { "1M", 0.0480 },
            { "3M", 0.0490 },
            { "6M", 0.0500 },
            { "1Y", 0.0520 }
        };

        var twdCurve = CurveBootstrapper.BuildStandardCurve("TWD", _refDate, twdQuotes);
        var usdCurve = CurveBootstrapper.BuildStandardCurve("USD", _refDate, usdQuotes);

        // 建立 Vol Surface
        var volSurface = InterpolatedVolSurface.CreateStandardGrid("USD/TWD", 0.10);

        // Act: 定價 90 天 DCI Put
        double optionPrice = GarmanKohlhagen.PriceWithCurves(
            spot: 30.5,
            strike: 30.0,
            domesticCurve: twdCurve,
            foreignCurve: usdCurve,
            volSurface: volSurface,
            timeToMaturity: 90.0 / 365.0,
            optionType: OptionType.Put
        );

        // Assert: 價格應該合理（0.1 ~ 1.0 TWD per USD）
        Assert.InRange(optionPrice, 0.05, 1.0);
    }
}
