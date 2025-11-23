using DciCalculator.Algorithms;
using DciCalculator.Curves;
using DciCalculator.VolSurfaces;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Phase 4 整合測試：利率曲線、波動率曲面與 FX 期權定價流程整合驗證。
/// </summary>
public class Phase4IntegrationTests
{
    private readonly DateTime _refDate = new DateTime(2024, 1, 1);

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_UsesCorrectRates()
    {
        // Arrange: 建立基礎利率曲線與波動率曲面
        var twdCurve = new FlatZeroCurve("TWD", _refDate, 0.015); // 1.5%
        var usdCurve = new FlatZeroCurve("USD", _refDate, 0.050); // 5%
        var volSurface = new FlatVolSurface("USD/TWD", _refDate, 0.10); // 10%

        // Act: 使用曲線 API 定價
        double priceWithCurves = GarmanKohlhagen.PriceWithCurves(
            spot: 30.5,
            strike: 30.0,
            domesticCurve: twdCurve,
            foreignCurve: usdCurve,
            volSurface: volSurface,
            timeToMaturity: 0.25,
            optionType: OptionType.Put
        );

        // Act: 原始 API（直接使用年化利率）
        double priceOriginal = GarmanKohlhagen.PriceFxOption(
            spot: 30.5,
            strike: 30.0,
            rDomestic: 0.015,
            rForeign: 0.050,
            volatility: 0.10,
            timeToMaturity: 0.25,
            optionType: OptionType.Put
        );

        // Assert: 兩種方法結果一致
        Assert.Equal(priceOriginal, priceWithCurves, precision: 6);
    }

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_RespectsTermStructure()
    {
        // Arrange: 建立具期限結構的利率曲線 (短天期 vs 長天期)
        var twdPoints = new[]
        {
            new CurvePoint(0.25, 0.010),  // 3M: 1.0%
            new CurvePoint(1.00, 0.020)   // 1Y: 2.0%
        };
        var twdCurve = new LinearInterpolatedCurve("TWD", _refDate, twdPoints);

        var usdCurve = new FlatZeroCurve("USD", _refDate, 0.050);
        var volSurface = new FlatVolSurface("USD/TWD", _refDate, 0.10);

        // Act: 定價 3M 與 1Y 期權
        double price3M = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double price1Y = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.0, twdCurve, usdCurve, volSurface, 1.0, OptionType.Put);

        // Assert: 1Y 價格應不同（期限結構影響）
        Assert.NotEqual(price3M, price1Y);
    }

    [Fact]
    public void GarmanKohlhagen_PriceWithCurves_RespectsVolSmile()
    {
        // Arrange: 建立帶 Smile 的波動率曲面
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

        // Act: 計算不同 Strike 的 Put 價格
        double priceITM = GarmanKohlhagen.PriceWithCurves(
            30.5, 29.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double priceATM = GarmanKohlhagen.PriceWithCurves(
            30.5, 30.5, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        double priceOTM = GarmanKohlhagen.PriceWithCurves(
            30.5, 32.0, twdCurve, usdCurve, volSurface, 0.25, OptionType.Put);

        // Assert: 較高 Strike (較 ITM) 之 Put 價格應較高
        Assert.True(priceITM < priceATM);
        Assert.True(priceATM < priceOTM);
        
        // 價格皆應為正值（風險中性定價）
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

        // Act: 使用折現因子定價
        double priceWithDF = GarmanKohlhagen.PriceWithDiscountFactors(
            spot: 30.5,
            strike: 30.0,
            dfDomestic: dfDom,
            dfForeign: dfFor,
            volatility: 0.10,
            timeToMaturity: T,
            optionType: OptionType.Put
        );

        // Act: 使用原始年化利率定價
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
        // Arrange: 以市場報價建立標準曲線
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

        // 建立標準波動率曲面
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

        // Assert: 合理價格介於 0.05 ~ 1.0 TWD per USD
        Assert.InRange(optionPrice, 0.05, 1.0);
    }
}
