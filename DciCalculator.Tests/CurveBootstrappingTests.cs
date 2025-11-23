using DciCalculator.Curves;
using DciCalculator.Models;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// Curve Bootstrapping 單元測試
/// </summary>
public class CurveBootstrappingTests
{
    private readonly DateTime _referenceDate = new DateTime(2024, 1, 1);

    [Fact]
    public void DepositInstrument_CalculatesZeroRate()
    {
        // Arrange: 3M Deposit @ 1.5%
        var deposit = DepositInstrument.Create(_referenceDate, "3M", 0.015);

        // Act
        double zeroRate = deposit.CalculateZeroRate();

        // Assert: Zero Rate 應接近 1.5%
        Assert.InRange(zeroRate, 0.014, 0.016);
    }

    [Fact]
    public void DepositInstrument_CalculatesDiscountFactor()
    {
        // Arrange
        var deposit = DepositInstrument.Create(_referenceDate, "1Y", 0.02);

        // Act
        double df = deposit.CalculateDiscountFactor();

        // Assert: DF = 1 / (1 + r * T)
        // 實際計算考慮 Act/360：T ? 365/360 = 1.0139
        // DF = 1 / (1 + 0.02 * 1.0139) ? 0.9801
        Assert.Equal(0.9801, df, precision: 3);
    }

    [Fact]
    public void SwapInstrument_CalculatesPV()
    {
        // Arrange: 1Y Swap @ 2%
        var swap = SwapInstrument.Create(_referenceDate, "1Y", 0.02);
        var flatCurve = new FlatZeroCurve("USD", _referenceDate, 0.02);

        // Act: Par Swap 的 PV 應該接近 0
        double pv = swap.CalculatePresentValue(flatCurve);

        // Assert
        Assert.InRange(Math.Abs(pv), 0.0, 0.01); // 接近 0
    }

    [Fact]
    public void CurveBootstrapper_BootstrapDeposits()
    {
        // Arrange: 3 個 Deposits
        var instruments = new List<MarketInstrument>
        {
            DepositInstrument.Create(_referenceDate, "3M", 0.015),
            DepositInstrument.Create(_referenceDate, "6M", 0.016),
            DepositInstrument.Create(_referenceDate, "1Y", 0.017)
        };

        var bootstrapper = new CurveBootstrapper("USD", _referenceDate);

        // Act
        var curve = bootstrapper.Bootstrap(instruments);

        // Assert
        double rate3M = curve.GetZeroRate(0.25);
        double rate6M = curve.GetZeroRate(0.5);
        double rate1Y = curve.GetZeroRate(1.0);

        Assert.InRange(rate3M, 0.014, 0.016);
        Assert.InRange(rate6M, 0.015, 0.017);
        Assert.InRange(rate1Y, 0.016, 0.018);

        // 期限結構：短期 < 長期
        Assert.True(rate6M >= rate3M);
        Assert.True(rate1Y >= rate6M);
    }

    [Fact]
    public void CurveBootstrapper_BuildStandardCurve()
    {
        // Arrange: 市場報價
        var marketQuotes = new Dictionary<string, double>
        {
            { "1M", 0.0150 },
            { "3M", 0.0155 },
            { "6M", 0.0160 },
            { "1Y", 0.0170 },
            { "2Y", 0.0180 }
        };

        // Act
        var curve = CurveBootstrapper.BuildStandardCurve("USD", _referenceDate, marketQuotes);

        // Assert
        double rate1M = curve.GetZeroRate(1.0 / 12.0);
        double rate1Y = curve.GetZeroRate(1.0);
        double rate2Y = curve.GetZeroRate(2.0);

        Assert.InRange(rate1M, 0.014, 0.016);
        Assert.InRange(rate1Y, 0.016, 0.018);
        Assert.InRange(rate2Y, 0.017, 0.019);
    }

    [Fact]
    public void CurveBootstrapper_InterpolatesBetweenPoints()
    {
        // Arrange
        var instruments = new List<MarketInstrument>
        {
            DepositInstrument.Create(_referenceDate, "3M", 0.015),
            DepositInstrument.Create(_referenceDate, "6M", 0.017)
        };

        var bootstrapper = new CurveBootstrapper("USD", _referenceDate);
        var curve = bootstrapper.Bootstrap(instruments);

        // Act: 插值 4.5M (0.375Y)
        double rate4_5M = curve.GetZeroRate(0.375);

        // Assert: 應該在 1.5% ~ 1.7% 之間
        Assert.InRange(rate4_5M, 0.015, 0.017);
    }

    [Fact]
    public void CurveBootstrapper_ForwardRateCalculation()
    {
        // Arrange
        var marketQuotes = new Dictionary<string, double>
        {
            { "6M", 0.015 },
            { "1Y", 0.020 }
        };

        var curve = CurveBootstrapper.BuildStandardCurve("USD", _referenceDate, marketQuotes);

        // Act: 計算 6M-1Y Forward Rate
        double forwardRate = curve.GetForwardRate(0.5, 1.0);

        // Assert: Forward Rate 應該 > Spot Rate（正常期限結構）
        Assert.True(forwardRate > 0.015);
    }
}
