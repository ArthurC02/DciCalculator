using DciCalculator.Models;
using DciCalculator.VolSurfaces;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// 波動率曲面相關測試：平坦曲面、插值曲面、Smile 參數與 Delta 估算。
/// </summary>
public class VolSurfaceTests
{
    [Fact]
    public void FlatVolSurface_ReturnsConstantVol()
    {
        // Arrange
        var surface = new FlatVolSurface("USD/TWD", 0.10);

        // Act & Assert
        Assert.Equal(0.10, surface.GetVolatility(29.0, 0.25));
        Assert.Equal(0.10, surface.GetVolatility(30.5, 0.50));
        Assert.Equal(0.10, surface.GetVolatility(32.0, 1.00));
    }

    [Fact]
    public void FlatVolSurface_ATMVol_ReturnsConstant()
    {
        // Arrange
        var surface = new FlatVolSurface("USD/TWD", 0.10);

        // Act
        double atmVol = surface.GetATMVolatility(30.5, 0.25);

        // Assert
        Assert.Equal(0.10, atmVol);
    }

    [Fact]
    public void VolSmileParameters_CalculatesDeltaVols()
    {
        // Arrange: ATM=10%, RR=1.5%, BF=0.5%
        var smile = new VolSmileParameters(
            atmVol: 0.10,
            riskReversal25D: 0.015,
            butterfly25D: 0.005,
            tenor: 0.25
        );

        // Act
        double put25Vol = smile.Get25DeltaPutVol();
        double call25Vol = smile.Get25DeltaCallVol();

        // Assert
        // Put: 10% + 0.5% + 1.5%/2 = 11.25%
        Assert.Equal(0.1125, put25Vol, precision: 4);
        
        // Call: 10% + 0.5% - 1.5%/2 = 9.75%
        Assert.Equal(0.0975, call25Vol, precision: 4);
    }

    [Fact]
    public void InterpolatedVolSurface_BilinearInterpolation()
    {
        // Arrange: 2x2 網格
        var points = new[]
        {
            new VolSurfacePoint(29.0, 0.25, 0.12), // K1, T1
            new VolSurfacePoint(32.0, 0.25, 0.08), // K2, T1
            new VolSurfacePoint(29.0, 1.00, 0.13), // K1, T2
            new VolSurfacePoint(32.0, 1.00, 0.09)  // K2, T2
        };

        var surface = new InterpolatedVolSurface("USD/TWD", DateTime.Today, points);

        // Act: 插值位置 (K=30.5, T=0.625)
        double vol = surface.GetVolatility(30.5, 0.625);

        // Assert: 結果介於 8% ~ 13%
        Assert.InRange(vol, 0.08, 0.13);
    }

    [Fact]
    public void InterpolatedVolSurface_ATMVol_Interpolates()
    {
        // Arrange
        var surface = InterpolatedVolSurface.CreateStandardGrid("USD/TWD", atmVol: 0.10);

        // Act: ATM (K=30.5) 不同期限
        double vol3M = surface.GetATMVolatility(30.5, 0.25);
        double vol6M = surface.GetATMVolatility(30.5, 0.50);
        double vol1Y = surface.GetATMVolatility(30.5, 1.00);

        // Assert: 期限結構：較長期限波動率不低於較短期限
        Assert.True(vol6M >= vol3M);
        Assert.True(vol1Y >= vol6M);
    }

    [Fact]
    public void InterpolatedVolSurface_GetValidRange_ReturnsCorrectRange()
    {
        // Arrange
        var surface = InterpolatedVolSurface.CreateStandardGrid("USD/TWD", 0.10);

        // Act
        var (minK, maxK, minT, maxT) = surface.GetValidRange();

        // Assert
        Assert.Equal(29.0, minK);
        Assert.Equal(32.0, maxK);
        Assert.Equal(0.25, minT);
        Assert.Equal(1.0, maxT);
    }

    [Fact]
    public void InterpolatedVolSurface_IsInRange_ChecksBoundaries()
    {
        // Arrange
        var surface = InterpolatedVolSurface.CreateStandardGrid("USD/TWD", 0.10);

        // Act & Assert
        Assert.True(surface.IsInRange(30.5, 0.50));   // 有效
        Assert.False(surface.IsInRange(28.0, 0.50));  // Strike 超出範圍
        Assert.False(surface.IsInRange(30.5, 2.00));  // Tenor 超出範圍
    }

    [Fact]
    public void VolSurfacePoint_IsValid_ValidatesCorrectly()
    {
        // Arrange & Act
        var validPoint = new VolSurfacePoint(30.0, 0.25, 0.10);

        // Assert
        Assert.True(validPoint.IsValid());
    }

    [Fact]
    public void VolSmileParameters_EstimateVolByDelta()
    {
        // Arrange
        var smile = new VolSmileParameters(0.10, 0.015, 0.005, 0.25);

        // Act
        double volDelta50 = smile.EstimateVolByDelta(0.50);  // ATM
        double volDelta25C = smile.EstimateVolByDelta(0.25); // 25D Call
        double volDeltaNeg25P = smile.EstimateVolByDelta(-0.25); // 25D Put

        // Assert
        Assert.Equal(0.10, volDelta50, precision: 2); // ATM
        
        // 25D Call 預期低於 ATM；RR>0 時 Put 會較高
        Assert.InRange(volDelta25C, 0.08, 0.11);
        
        // 25D Put 預期高於 ATM
        Assert.InRange(volDeltaNeg25P, 0.10, 0.13);
    }
}
