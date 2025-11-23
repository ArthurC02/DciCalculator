using DciCalculator.Curves;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// 零利率曲線測試：平坦曲線、線性插值、三次樣條、Forward Rate 與折現因子。
/// </summary>
public class ZeroCurveTests
{
    [Fact]
    public void FlatZeroCurve_ReturnsConstantRate()
    {
        // Arrange
        var curve = new FlatZeroCurve("USD", 0.05);

        // Act & Assert
        Assert.Equal(0.05, curve.GetZeroRate(0.25));
        Assert.Equal(0.05, curve.GetZeroRate(1.0));
        Assert.Equal(0.05, curve.GetZeroRate(5.0));
    }

    [Fact]
    public void FlatZeroCurve_DiscountFactor_CalculatesCorrectly()
    {
        // Arrange
        var curve = new FlatZeroCurve("USD", 0.05);

        // Act
        double df = curve.GetDiscountFactor(1.0);

        // Assert: DF = exp(-0.05 * 1) ? 0.9512
        Assert.Equal(Math.Exp(-0.05), df, precision: 6);
    }

    [Fact]
    public void LinearInterpolatedCurve_Interpolates_Correctly()
    {
        // Arrange
        var points = new[]
        {
            new CurvePoint(0.25, 0.01), // 3M: 1%
            new CurvePoint(1.0, 0.02)   // 1Y: 2%
        };
        var curve = new LinearInterpolatedCurve("USD", DateTime.Today, points);

        // Act: 6M 零利率 (T=0.5) 介於 0.25 與 1.0 兩點之間
        double rate6M = curve.GetZeroRate(0.5);

        // Assert: 線性插值驗證
        // weight = (0.5 - 0.25) / (1.0 - 0.25) = 0.25 / 0.75 = 1/3
        // rate = 0.01 + (1/3) * (0.02 - 0.01) = 0.01 + 0.00333... ? 0.01333
        Assert.Equal(0.013333333333333334, rate6M, precision: 6);
    }

    [Fact]
    public void LinearInterpolatedCurve_ForwardRate_CalculatesCorrectly()
    {
        // Arrange
        var points = new[]
        {
            new CurvePoint(0.5, 0.01),  // 6M: 1%
            new CurvePoint(1.0, 0.02)   // 1Y: 2%
        };
        var curve = new LinearInterpolatedCurve("USD", DateTime.Today, points);

        // Act: Forward Rate from 6M to 1Y
        double forwardRate = curve.GetForwardRate(0.5, 1.0);

        // Assert: f(0.5, 1) = [r(1)*1 - r(0.5)*0.5] / (1 - 0.5) = [0.02 - 0.005] / 0.5 = 0.03
        Assert.Equal(0.03, forwardRate, precision: 6);
    }

    [Fact]
    public void CubicSplineCurve_SmoothInterpolation()
    {
        // Arrange
        var points = new[]
        {
            new CurvePoint(0.25, 0.01), // 3M
            new CurvePoint(0.5, 0.015), // 6M
            new CurvePoint(1.0, 0.02),  // 1Y
            new CurvePoint(2.0, 0.025)  // 2Y
        };
        var curve = new CubicSplineCurve("USD", DateTime.Today, points);

        // Act: 插值點 (0.75Y)
        double rate9M = curve.GetZeroRate(0.75);

        // Assert: 結果介於 1.5% ~ 2.0%
        Assert.InRange(rate9M, 0.015, 0.020);
    }

    [Fact]
    public void CurvePoint_FromDiscountFactor_CalculatesCorrectly()
    {
        // Arrange
        double df = 0.95;   // DF = 0.95
        double tenor = 1.0; // 1Y

        // Act
        var point = CurvePoint.FromDiscountFactor(tenor, df);

        // Assert: r = -ln(0.95) / 1 ? 0.05129
        Assert.Equal(-Math.Log(0.95), point.ZeroRate, precision: 6);
    }

    [Fact]
    public void LinearInterpolatedCurve_GetValidRange_ReturnsCorrectRange()
    {
        // Arrange
        var points = new[]
        {
            new CurvePoint(0.25, 0.01),
            new CurvePoint(2.0, 0.025)
        };
        var curve = new LinearInterpolatedCurve("USD", DateTime.Today, points);

        // Act
        var (min, max) = curve.GetValidRange();

        // Assert
        Assert.Equal(0.25, min);
        Assert.Equal(2.0, max);
    }
}
