using DciCalculator.Models;
using DciCalculator.Validation;
using Xunit;

namespace DciCalculator.Tests;

public class ValidationTests
{
    #region DciInputValidator Tests

    [Fact]
    public void DciInputValidator_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void DciInputValidator_NullInput_ReturnsFailure()
    {
        // Arrange
        var validator = new DciInputValidator();

        // Act
        var result = validator.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("不能為 null", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_NegativeNotional_ReturnsError()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: -100m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("名義本金必須大於 0", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_NegativeStrike_ReturnsError()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: -31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Strike 必須大於 0", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_InvalidSpotBidAsk_ReturnsError()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.05m, 31.00m), // Ask < Bid
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Ask", result.ErrorMessage);
        Assert.Contains("Bid", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_ExcessiveVolatility_ReturnsError()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 6.0, // > 5.0
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("波動率過高", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_OutOfRangeRates_ReturnsErrors()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 1.5, // > 1.0
            RateForeign: -0.2, // < -0.1
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("本幣利率超出合理範圍", result.ErrorMessage);
        Assert.Contains("外幣利率超出合理範圍", result.ErrorMessage);
    }

    [Fact]
    public void DciInputValidator_StrikeFarFromSpot_ReturnsWarning()
    {
        // Arrange
        var validator = new DciInputValidator();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 50.00m, // Much higher than spot
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = validator.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Strike", result.ErrorMessage);
        Assert.Contains("偏離", result.ErrorMessage);
    }

    #endregion

    #region MarketDataSnapshotValidator Tests

    [Fact]
    public void MarketDataSnapshotValidator_ValidSnapshot_ReturnsSuccess()
    {
        // Arrange
        var validator = new MarketDataSnapshotValidator();
        var snapshot = new MarketDataSnapshot(
            currencyPair: "USD/TWD",
            spotQuote: new FxQuote(31.00m, 31.05m),
            rateDomestic: 0.02,
            rateForeign: 0.05,
            volatility: 0.12
        );

        // Act
        var result = validator.Validate(snapshot);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MarketDataSnapshotValidator_NullSnapshot_ReturnsFailure()
    {
        // Arrange
        var validator = new MarketDataSnapshotValidator();

        // Act
        var result = validator.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("不能為 null", result.ErrorMessage);
    }

    [Fact]
    public void MarketDataSnapshotValidator_EmptyCurrencyPair_ReturnsError()
    {
        // Arrange
        var validator = new MarketDataSnapshotValidator();
        // 使用 init 語法繞過建構子驗證來測試驗證器
        var snapshot = new MarketDataSnapshot(
            currencyPair: "USD/TWD",
            spotQuote: new FxQuote(31.00m, 31.05m),
            rateDomestic: 0.02,
            rateForeign: 0.05,
            volatility: 0.12
        )
        {
            CurrencyPair = "" // 使用 init 設定無效值
        };

        // Act
        var result = validator.Validate(snapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("幣別對不能為空", result.ErrorMessage);
    }

    [Fact]
    public void MarketDataSnapshotValidator_InvalidSpot_ReturnsError()
    {
        // Arrange
        var validator = new MarketDataSnapshotValidator();
        // 使用 init 語法繞過建構子驗證
        var snapshot = new MarketDataSnapshot(
            currencyPair: "USD/TWD",
            spotQuote: new FxQuote(31.00m, 31.05m),
            rateDomestic: 0.02,
            rateForeign: 0.05,
            volatility: 0.12
        )
        {
            SpotQuote = new FxQuote(31.05m, 31.00m) // Ask < Bid
        };

        // Act
        var result = validator.Validate(snapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Ask", result.ErrorMessage);
        Assert.Contains("Bid", result.ErrorMessage);
    }

    [Fact]
    public void MarketDataSnapshotValidator_ExcessiveVolatility_ReturnsError()
    {
        // Arrange
        var validator = new MarketDataSnapshotValidator();
        // 使用 init 語法繞過建構子驗證
        var snapshot = new MarketDataSnapshot(
            currencyPair: "USD/TWD",
            spotQuote: new FxQuote(31.00m, 31.05m),
            rateDomestic: 0.02,
            rateForeign: 0.05,
            volatility: 0.12
        )
        {
            Volatility = 10.0 // 使用 init 設定過高的波動率
        };

        // Act
        var result = validator.Validate(snapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("波動率過高", result.ErrorMessage);
    }

    #endregion

    #region ValidationPipeline Tests

    [Fact]
    public void ValidationPipeline_EmptyPipeline_ReturnsSuccess()
    {
        // Arrange
        var pipeline = new ValidationPipeline<DciInput>();
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = pipeline.Validate(input);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidationPipeline_SingleValidator_Works()
    {
        // Arrange
        var pipeline = new ValidationPipeline<DciInput>();
        pipeline.Add(new DciInputValidator());
        
        var input = new DciInput(
            NotionalForeign: -100m, // Invalid
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = pipeline.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("名義本金必須大於 0", result.ErrorMessage);
    }

    [Fact]
    public void ValidationPipeline_CreateStaticMethod_Works()
    {
        // Arrange
        var pipeline = ValidationPipeline<DciInput>.Create(
            new DciInputValidator()
        );
        
        var input = new DciInput(
            NotionalForeign: 10_000m,
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = pipeline.Validate(input);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1, pipeline.Count);
    }

    [Fact]
    public void ValidationPipeline_CombinesMultipleErrors()
    {
        // Custom validator for testing
        var customValidator = new CustomDciValidator();
        var pipeline = ValidationPipeline<DciInput>.Create(
            new DciInputValidator(),
            customValidator
        );
        
        var input = new DciInput(
            NotionalForeign: -100m, // Fails DciInputValidator
            SpotQuote: new FxQuote(31.00m, 31.05m),
            Strike: 31.50m,
            RateDomestic: 0.02,
            RateForeign: 0.05,
            Volatility: 0.12,
            TenorInYears: 0.25,
            DepositRateAnnual: 0.05
        );

        // Act
        var result = pipeline.Validate(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2); // At least one from each validator
    }

    // Helper validator for testing pipeline
    private class CustomDciValidator : IValidator<DciInput>
    {
        public ValidationResult Validate(DciInput item)
        {
            if (item.Strike > 100m)
                return ValidationResult.Failure(nameof(item.Strike), "Custom: Strike too high");
            if (item.NotionalForeign < 0)
                return ValidationResult.Failure(nameof(item.NotionalForeign), "Custom: Negative notional");
            return ValidationResult.Success();
        }
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Success_HasNoErrors()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.ErrorMessage);
    }

    [Fact]
    public void ValidationResult_SingleFailure_HasError()
    {
        // Act
        var result = ValidationResult.Failure("TestProperty", "Test error message");

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("TestProperty", result.Errors[0].PropertyName);
        Assert.Equal("Test error message", result.Errors[0].Message);
    }

    [Fact]
    public void ValidationResult_MultipleFailures_HasAllErrors()
    {
        // Arrange
        var errors = new[]
        {
            new ValidationError("Prop1", "Error 1"),
            new ValidationError("Prop2", "Error 2")
        };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ValidationResult_Combine_MergesResults()
    {
        // Arrange
        var result1 = ValidationResult.Failure("Prop1", "Error 1");
        var result2 = ValidationResult.Failure("Prop2", "Error 2");
        var result3 = ValidationResult.Success();

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Equal(2, combined.Errors.Count);
    }

    [Fact]
    public void ValidationResult_CombineAllSuccess_ReturnsSuccess()
    {
        // Arrange
        var result1 = ValidationResult.Success();
        var result2 = ValidationResult.Success();

        // Act
        var combined = ValidationResult.Combine(result1, result2);

        // Assert
        Assert.True(combined.IsValid);
        Assert.Empty(combined.Errors);
    }

    #endregion
}
