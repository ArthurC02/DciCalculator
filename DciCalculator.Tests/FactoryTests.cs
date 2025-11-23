using DciCalculator.Curves;
using DciCalculator.DependencyInjection;
using DciCalculator.Factories;
using DciCalculator.Models;
using DciCalculator.VolSurfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Factory 模式整合測試
/// </summary>
public class FactoryTests
{
    private static readonly DateTime ReferenceDate = new(2024, 1, 1);

    #region CurveFactory Tests

    [Fact]
    public void CurveFactory_CreateZeroCurve_WithLinearInterpolation_ShouldReturnLinearCurve()
    {
        // Arrange
        var factory = new CurveFactory();
        var points = new List<CurvePoint>
        {
            new(0.25, 0.02),  // 3M: 2%
            new(1.0, 0.025),  // 1Y: 2.5%
            new(2.0, 0.03)    // 2Y: 3%
        };

        // Act
        var curve = factory.CreateZeroCurve(
            "USD",
            new DateTime(2024, 1, 1),
            points,
            Models.InterpolationMethod.Linear);

        // Assert
        Assert.NotNull(curve);
        Assert.IsType<LinearInterpolatedCurve>(curve);
    }

    [Fact]
    public void CurveFactory_CreateZeroCurve_WithCubicSpline_ShouldReturnCubicSplineCurve()
    {
        // Arrange
        var factory = new CurveFactory();
        var points = new List<CurvePoint>
        {
            new(0.25, 0.02),  // 3M
            new(1.0, 0.025),  // 1Y
            new(2.0, 0.03)    // 2Y
        };

        // Act
        var curve = factory.CreateZeroCurve(
            "USD",
            ReferenceDate,
            points,
            Models.InterpolationMethod.CubicSpline);

        // Assert
        Assert.NotNull(curve);
        Assert.IsType<CubicSplineCurve>(curve);
    }

    [Fact]
    public void CurveFactory_CreateFlatCurve_ShouldReturnFlatZeroCurve()
    {
        // Arrange
        var factory = new CurveFactory();

        // Act
        var curve = factory.CreateFlatCurve("TWD", new DateTime(2024, 1, 1), 0.03);

        // Assert
        Assert.NotNull(curve);
        Assert.IsType<FlatZeroCurve>(curve);
        
        // 驗證 flat rate
        var df1Y = curve.GetDiscountFactor(new DateTime(2025, 1, 1));
        var expectedDf = Math.Exp(-0.03 * 1.0);
        Assert.Equal(expectedDf, df1Y, 3);  // 使用較寬鬆的精確度 (3 位小數)
    }

    [Fact]
    public void CurveFactory_BootstrapCurve_ShouldReturnBootstrappedCurve()
    {
        // Arrange
        var factory = new CurveFactory();
        var instruments = new List<MarketInstrument>
        {
            DepositInstrument.Create(ReferenceDate, "3M", 0.025),
            DepositInstrument.Create(ReferenceDate, "6M", 0.028)
        };

        // Act
        var curve = factory.BootstrapCurve(
            "USD",
            ReferenceDate,
            instruments,
            Models.InterpolationMethod.Linear);

        // Assert
        Assert.NotNull(curve);
    }

    [Fact]
    public void CurveFactory_CreateZeroCurve_WithEmptyPoints_ShouldThrow()
    {
        // Arrange
        var factory = new CurveFactory();
        var emptyPoints = new List<CurvePoint>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateZeroCurve("USD", DateTime.Today, emptyPoints));
        
        Assert.Contains("曲線點位不能為空", exception.Message);
    }

    #endregion

    #region VolSurfaceFactory Tests

    [Fact]
    public void VolSurfaceFactory_CreateInterpolatedVolSurface_ShouldReturnInterpolatedSurface()
    {
        // Arrange
        var factory = new VolSurfaceFactory();
        var points = new List<VolSurfacePoint>
        {
            new() { Strike = 95, Tenor = 0.25, Volatility = 0.12 },
            new() { Strike = 100, Tenor = 0.25, Volatility = 0.10 },
            new() { Strike = 105, Tenor = 0.25, Volatility = 0.11 },
            new() { Strike = 95, Tenor = 0.5, Volatility = 0.13 },
            new() { Strike = 100, Tenor = 0.5, Volatility = 0.11 },
            new() { Strike = 105, Tenor = 0.5, Volatility = 0.12 }
        };

        // Act
        var surface = factory.CreateInterpolatedVolSurface(
            "USDTWD",
            new DateTime(2024, 1, 1),
            points);

        // Assert
        Assert.NotNull(surface);
        Assert.IsType<InterpolatedVolSurface>(surface);
    }

    [Fact]
    public void VolSurfaceFactory_CreateFlatVolSurface_ShouldReturnFlatSurface()
    {
        // Arrange
        var factory = new VolSurfaceFactory();

        // Act
        var surface = factory.CreateFlatVolSurface("EURUSD", new DateTime(2024, 1, 1), 0.15);

        // Assert
        Assert.NotNull(surface);
        Assert.IsType<FlatVolSurface>(surface);
        
        // 驗證 flat vol
        var vol = surface.GetVolatility(100, 1.0);
        Assert.Equal(0.15, vol, 5);
    }

    [Fact]
    public void VolSurfaceFactory_CreateFlatVolSurface_WithNegativeVol_ShouldThrow()
    {
        // Arrange
        var factory = new VolSurfaceFactory();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateFlatVolSurface("USDTWD", DateTime.Today, -0.1));
        
        Assert.Contains("波動率不能為負值", exception.Message);
    }

    [Fact]
    public void VolSurfaceFactory_CreateVolSurfaceFromSmile_ShouldGeneratePoints()
    {
        // Arrange
        var factory = new VolSurfaceFactory();
        var smileParams = new VolSmileParameters(
            atmVol: 0.12,
            riskReversal25D: 0.01,
            butterfly25D: 0.005,
            tenor: 1.0);

        // Act
        var surface = factory.CreateVolSurfaceFromSmile(
            "USDTWD",
            new DateTime(2024, 1, 1),
            0.12,
            smileParams);

        // Assert
        Assert.NotNull(surface);
        Assert.IsType<InterpolatedVolSurface>(surface);
    }

    #endregion

    #region DI Integration Tests

    [Fact]
    public void DI_ShouldRegisterFactories_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDciServices();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var curveFactory = serviceProvider.GetService<ICurveFactory>();
        var volSurfaceFactory = serviceProvider.GetService<IVolSurfaceFactory>();

        // Assert
        Assert.NotNull(curveFactory);
        Assert.NotNull(volSurfaceFactory);
        Assert.IsType<CurveFactory>(curveFactory);
        Assert.IsType<VolSurfaceFactory>(volSurfaceFactory);
    }

    [Fact]
    public void DI_FactoriesShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDciServices();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var curveFactory1 = serviceProvider.GetService<ICurveFactory>();
        var curveFactory2 = serviceProvider.GetService<ICurveFactory>();

        var volFactory1 = serviceProvider.GetService<IVolSurfaceFactory>();
        var volFactory2 = serviceProvider.GetService<IVolSurfaceFactory>();

        // Assert
        Assert.Same(curveFactory1, curveFactory2);
        Assert.Same(volFactory1, volFactory2);
    }

    [Fact]
    public void DI_EndToEnd_UsingFactoriesInPricingWorkflow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDciServices();
        var serviceProvider = services.BuildServiceProvider();

        var curveFactory = serviceProvider.GetRequiredService<ICurveFactory>();
        var volSurfaceFactory = serviceProvider.GetRequiredService<IVolSurfaceFactory>();

        // 建立市場數據
        var referenceDate = new DateTime(2024, 1, 1);
        var domesticCurve = curveFactory.CreateFlatCurve("TWD", referenceDate, 0.02);
        var foreignCurve = curveFactory.CreateFlatCurve("USD", referenceDate, 0.03);
        var volSurface = volSurfaceFactory.CreateFlatVolSurface("USDTWD", referenceDate, 0.12);

        // Act - 驗證曲線和曲面可用
        var df = domesticCurve.GetDiscountFactor(referenceDate.AddYears(1));
        var vol = volSurface.GetVolatility(30, 1.0);

        // Assert
        Assert.True(df > 0 && df <= 1);
        Assert.True(vol > 0);
    }

    #endregion
}
