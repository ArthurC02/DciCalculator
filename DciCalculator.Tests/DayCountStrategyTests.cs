using DciCalculator.DayCount;
using Xunit;

namespace DciCalculator.Tests;

public class DayCountStrategyTests
{
    #region Act365Calculator Tests

    [Fact]
    public void Act365Calculator_SameDay_ReturnsZero()
    {
        // Arrange
        var calculator = new Act365Calculator();
        var date = new DateTime(2024, 1, 15);

        // Act
        var result = calculator.YearFraction(date, date);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Act365Calculator_OneYear_ReturnsOne()
    {
        // Arrange
        var calculator = new Act365Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2025, 1, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(366.0 / 365.0, result, precision: 10); // 2024 is leap year
    }

    [Fact]
    public void Act365Calculator_90Days_ReturnsCorrect()
    {
        // Arrange
        var calculator = new Act365Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 31);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(90.0 / 365.0, result, precision: 10);
    }

    [Fact]
    public void Act365Calculator_EndBeforeStart_ThrowsException()
    {
        // Arrange
        var calculator = new Act365Calculator();
        var start = new DateTime(2024, 12, 31);
        var end = new DateTime(2024, 1, 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => calculator.YearFraction(start, end));
    }

    #endregion

    #region Act360Calculator Tests

    [Fact]
    public void Act360Calculator_90Days_ReturnsCorrect()
    {
        // Arrange
        var calculator = new Act360Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 31);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(90.0 / 360.0, result, precision: 10);
        Assert.Equal(0.25, result, precision: 2);
    }

    [Fact]
    public void Act360Calculator_OneYear_ReturnsCorrect()
    {
        // Arrange
        var calculator = new Act360Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2025, 1, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert (366 days in 2024 leap year)
        Assert.Equal(366.0 / 360.0, result, precision: 10);
    }

    #endregion

    #region ActActCalculator Tests

    [Fact]
    public void ActActCalculator_SameYear_NonLeap_ReturnsCorrect()
    {
        // Arrange
        var calculator = new ActActCalculator();
        var start = new DateTime(2023, 1, 1);
        var end = new DateTime(2023, 7, 1); // 181 days

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(181.0 / 365.0, result, precision: 10);
    }

    [Fact]
    public void ActActCalculator_SameYear_Leap_ReturnsCorrect()
    {
        // Arrange
        var calculator = new ActActCalculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 7, 1); // 182 days in leap year

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(182.0 / 366.0, result, precision: 10);
    }

    [Fact]
    public void ActActCalculator_SpanningYears_ReturnsCorrect()
    {
        // Arrange
        var calculator = new ActActCalculator();
        var start = new DateTime(2023, 7, 1);
        var end = new DateTime(2024, 7, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert - should be close to 1.0
        Assert.InRange(result, 0.99, 1.01);
    }

    #endregion

    #region Thirty360Calculator Tests

    [Fact]
    public void Thirty360Calculator_OneMonth_Returns30Over360()
    {
        // Arrange
        var calculator = new Thirty360Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(30.0 / 360.0, result, precision: 10);
    }

    [Fact]
    public void Thirty360Calculator_OneYear_ReturnsOne()
    {
        // Arrange
        var calculator = new Thirty360Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2025, 1, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        Assert.Equal(1.0, result, precision: 10);
    }

    [Fact]
    public void Thirty360Calculator_Day31Adjustment_WorksCorrectly()
    {
        // Arrange
        var calculator = new Thirty360Calculator();
        var start = new DateTime(2024, 1, 31);
        var end = new DateTime(2024, 2, 29); // Leap year

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert
        // d1 = 31 -> 30, d2 = 29 (no adjustment), (29 - 30) + 30*(2-1) = 29 days
        Assert.Equal(29.0 / 360.0, result, precision: 10);
    }

    #endregion

    #region Bus252Calculator Tests

    [Fact]
    public void Bus252Calculator_OneWeek_Returns5BusinessDays()
    {
        // Arrange
        var calculator = new Bus252Calculator();
        var start = new DateTime(2024, 1, 1); // Monday
        var end = new DateTime(2024, 1, 8); // Next Monday

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert - 5 business days (Mon-Fri)
        Assert.Equal(5.0 / 252.0, result, precision: 10);
    }

    [Fact]
    public void Bus252Calculator_Weekend_ReturnsZero()
    {
        // Arrange
        var calculator = new Bus252Calculator();
        var start = new DateTime(2024, 1, 6); // Saturday
        var end = new DateTime(2024, 1, 8); // Monday

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert - 0 business days
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Bus252Calculator_OneMonth_ReturnsApproximately21Days()
    {
        // Arrange
        var calculator = new Bus252Calculator();
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        // Act
        var result = calculator.YearFraction(start, end);

        // Assert - approximately 21-23 business days in a month
        Assert.InRange(result, 20.0 / 252.0, 24.0 / 252.0);
    }

    #endregion

    #region DayCountCalculatorFactory Tests

    [Fact]
    public void Factory_GetCalculator_ReturnsCorrectType()
    {
        // Arrange
        var factory = new DayCountCalculatorFactory();

        // Act
        var act365 = factory.GetCalculator(DayCountConvention.Act365);
        var act360 = factory.GetCalculator(DayCountConvention.Act360);
        var actAct = factory.GetCalculator(DayCountConvention.ActAct);
        var thirty360 = factory.GetCalculator(DayCountConvention.Thirty360);
        var bus252 = factory.GetCalculator(DayCountConvention.Bus252);

        // Assert
        Assert.IsType<Act365Calculator>(act365);
        Assert.IsType<Act360Calculator>(act360);
        Assert.IsType<ActActCalculator>(actAct);
        Assert.IsType<Thirty360Calculator>(thirty360);
        Assert.IsType<Bus252Calculator>(bus252);
    }

    [Fact]
    public void Factory_GetSupportedConventions_ReturnsAll()
    {
        // Arrange
        var factory = new DayCountCalculatorFactory();

        // Act
        var conventions = factory.GetSupportedConventions().ToList();

        // Assert
        Assert.Equal(5, conventions.Count);
        Assert.Contains(DayCountConvention.Act365, conventions);
        Assert.Contains(DayCountConvention.Act360, conventions);
        Assert.Contains(DayCountConvention.ActAct, conventions);
        Assert.Contains(DayCountConvention.Thirty360, conventions);
        Assert.Contains(DayCountConvention.Bus252, conventions);
    }

    [Fact]
    public void Factory_ReturnsSameInstanceForSameConvention()
    {
        // Arrange
        var factory = new DayCountCalculatorFactory();

        // Act
        var calc1 = factory.GetCalculator(DayCountConvention.Act365);
        var calc2 = factory.GetCalculator(DayCountConvention.Act365);

        // Assert
        Assert.Same(calc1, calc2);
    }

    #endregion

    #region Static DayCountCalculator Compatibility Tests

    [Fact]
    public void StaticDayCountCalculator_Act365_StillWorks()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 31);

        // Act
        var result = DayCountCalculator.YearFraction(start, end, DayCountConvention.Act365);

        // Assert
        Assert.Equal(90.0 / 365.0, result, precision: 10);
    }

    [Fact]
    public void StaticDayCountCalculator_Act360_StillWorks()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 31);

        // Act
        var result = DayCountCalculator.YearFraction(start, end, DayCountConvention.Act360);

        // Assert
        Assert.Equal(90.0 / 360.0, result, precision: 10);
    }

    [Fact]
    public void StaticDayCountCalculator_DefaultConvention_IsAct365()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 31);

        // Act
        var result = DayCountCalculator.YearFraction(start, end);

        // Assert
        Assert.Equal(90.0 / 365.0, result, precision: 10);
    }

    #endregion
}
