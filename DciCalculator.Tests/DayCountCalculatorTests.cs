using DciCalculator;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// DayCountCalculator 單元測試
/// </summary>
public class DayCountCalculatorTests
{
    [Fact]
    public void YearFraction_Act365_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 4, 1); // 91 天

        // Act
        double yearFraction = DayCountCalculator.YearFraction(
            startDate, endDate, DayCountConvention.Act365);

        // Assert: 91 / 365 ? 0.2493
        Assert.Equal(91.0 / 365.0, yearFraction, precision: 6);
    }

    [Fact]
    public void YearFraction_Act360_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 4, 1); // 91 天

        // Act
        double yearFraction = DayCountCalculator.YearFraction(
            startDate, endDate, DayCountConvention.Act360);

        // Assert: 91 / 360 ? 0.2528
        Assert.Equal(91.0 / 360.0, yearFraction, precision: 6);
    }

    [Fact]
    public void ParseTenor_3M_Returns90Days()
    {
        // Arrange
        var referenceDate = new DateTime(2024, 1, 1);

        // Act
        var (days, yearFraction) = DayCountCalculator.ParseTenor("3M", referenceDate);

        // Assert
        Assert.Equal(90, days); // 3 * 30
        Assert.InRange(yearFraction, 0.24, 0.26);
    }

    [Fact]
    public void ParseTenor_90D_Returns90Days()
    {
        // Arrange
        var referenceDate = new DateTime(2024, 1, 1);

        // Act
        var (days, yearFraction) = DayCountCalculator.ParseTenor("90D", referenceDate);

        // Assert
        Assert.Equal(90, days);
    }

    [Fact]
    public void CalculateMaturityDate_90Days_AdjustsForWeekend()
    {
        // Arrange: 2024-01-01 是星期一
        var startDate = new DateTime(2024, 1, 1);

        // Act
        var maturityDate = DayCountCalculator.CalculateMaturityDate(
            startDate, tenorInDays: 90, adjustForWeekends: true);

        // Assert: 不應該落在週末
        Assert.NotEqual(DayOfWeek.Saturday, maturityDate.DayOfWeek);
        Assert.NotEqual(DayOfWeek.Sunday, maturityDate.DayOfWeek);
    }

    [Fact]
    public void IsBusinessDay_Monday_ReturnsTrue()
    {
        // Arrange: 2024-01-01 是星期一
        var date = new DateTime(2024, 1, 1);

        // Act
        bool isBusinessDay = DayCountCalculator.IsBusinessDay(date);

        // Assert
        Assert.True(isBusinessDay);
    }

    [Fact]
    public void IsBusinessDay_Saturday_ReturnsFalse()
    {
        // Arrange: 2024-01-06 是星期六
        var date = new DateTime(2024, 1, 6);

        // Act
        bool isBusinessDay = DayCountCalculator.IsBusinessDay(date);

        // Assert
        Assert.False(isBusinessDay);
    }
}
