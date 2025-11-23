using DciCalculator.Core.Interfaces;
using DciCalculator.DependencyInjection;
using DciCalculator.Models;
using DciCalculator.PricingModels;
using DciCalculator.Services.Pricing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DciCalculator.Tests;

/// <summary>
/// 依賴注入整合測試
/// 展示如何使用 DI 容器與新的服務架構
/// </summary>
public class DependencyInjectionTests
{
    private readonly ServiceProvider _serviceProvider;

    public DependencyInjectionTests()
    {
        // 建立 DI 容器
        var services = new ServiceCollection();
        services.AddDciServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void AddDciServices_ShouldRegisterAllCoreServices()
    {
        // Assert - 驗證所有核心服務都已註冊
        Assert.NotNull(_serviceProvider.GetService<IPricingModel>());
        Assert.NotNull(_serviceProvider.GetService<IDciPricingEngine>());
        Assert.NotNull(_serviceProvider.GetService<IGreeksCalculator>());
        Assert.NotNull(_serviceProvider.GetService<IMarginService>());
        Assert.NotNull(_serviceProvider.GetService<IStrikeSolver>());
        Assert.NotNull(_serviceProvider.GetService<IScenarioAnalyzer>());
    }

    [Fact]
    public void AddDciServices_ShouldRegisterSingletonServices()
    {
        // Arrange & Act
        var engine1 = _serviceProvider.GetRequiredService<IDciPricingEngine>();
        var engine2 = _serviceProvider.GetRequiredService<IDciPricingEngine>();

        // Assert - 驗證是同一個實例 (Singleton)
        Assert.Same(engine1, engine2);
    }

    [Fact]
    public void DciPricingEngine_ShouldWorkWithInjectedDependencies()
    {
        // Arrange
        var engine = _serviceProvider.GetRequiredService<IDciPricingEngine>();
        var input = CreateTestInput();

        // Act
        var quote = engine.Quote(input);

        // Assert
        Assert.True(quote.CouponAnnual > 0);
        Assert.True(quote.TotalInterestForeign > 0);
        Assert.True(quote.InterestFromOption > 0);
    }

    [Fact]
    public void GreeksCalculator_ShouldWorkWithInjectedPricingModel()
    {
        // Arrange
        var calculator = _serviceProvider.GetRequiredService<IGreeksCalculator>();
        var engine = _serviceProvider.GetRequiredService<IDciPricingEngine>();
        var input = CreateTestInput();
        var quote = engine.Quote(input);

        // Act
        var greeks = calculator.CalculateDciGreeks(input);

        // Assert
        // DCI 賣出 Put，Greeks 會翻轉符號
        Assert.NotEqual(0, greeks.Delta);
        Assert.NotEqual(0, greeks.Vega);  // Vega 應該有值（可能為負，因為 short position）
    }

    [Fact]
    public void StrikeSolver_ShouldWorkWithInjectedPricingEngine()
    {
        // Arrange
        var solver = _serviceProvider.GetRequiredService<IStrikeSolver>();
        var input = CreateTestInput();
        double targetCoupon = 0.06; // 6%

        // Act
        var strike = solver.SolveStrike(input, targetCoupon);

        // Assert
        Assert.True(strike > 0);
        Assert.True(strike < input.SpotQuote.Mid);
    }

    [Fact]
    public void ScenarioAnalyzer_ShouldWorkWithInjectedPricingEngine()
    {
        // Arrange
        var analyzer = _serviceProvider.GetRequiredService<IScenarioAnalyzer>();
        var input = CreateTestInput();

        // Act
        var results = analyzer.QuickAnalyze(input);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(15, results.Count); // 5 spot shifts × 3 vol shifts
    }

    [Fact]
    public void CustomPricingModel_CanBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDciServices<GarmanKohlhagenModel>(); // 明確指定模型
        var provider = services.BuildServiceProvider();

        // Act
        var model = provider.GetRequiredService<IPricingModel>();
        var engine = provider.GetRequiredService<IDciPricingEngine>();

        // Assert
        Assert.IsType<GarmanKohlhagenModel>(model);
        Assert.NotNull(engine);
    }

    [Fact]
    public void EndToEnd_CompleteWorkflow_WithDI()
    {
        // Arrange
        var input = CreateTestInput();
        var engine = _serviceProvider.GetRequiredService<IDciPricingEngine>();
        var greeksCalc = _serviceProvider.GetRequiredService<IGreeksCalculator>();
        var solver = _serviceProvider.GetRequiredService<IStrikeSolver>();
        var analyzer = _serviceProvider.GetRequiredService<IScenarioAnalyzer>();

        // Act - 完整流程
        // 1. 報價
        var quote = engine.Quote(input);
        
        // 2. 計算 Greeks
        var greeks = greeksCalc.CalculateDciGreeks(input);
        
        // 3. 求解 Strike
        var targetCoupon = 0.05;
        var solvedStrike = solver.SolveStrike(input, targetCoupon);
        
        // 4. 情境分析
        var scenarios = analyzer.QuickAnalyze(input);

        // Assert
        Assert.True(quote.CouponAnnual > 0);
        Assert.NotEqual(0, greeks.Delta);
        Assert.True(solvedStrike > 0);
        Assert.NotEmpty(scenarios);
    }

    private static DciInput CreateTestInput()
    {
        return new DciInput(
            NotionalForeign: 10_000m,      // 10,000 USD
            SpotQuote: new FxQuote(Bid: 30.49m, Ask: 30.51m),
            Strike: 29.85m,
            RateDomestic: 0.015,           // TWD 1.5%
            RateForeign: 0.055,            // USD 5.5%
            Volatility: 0.08,              // 8%
            TenorInYears: 0.5,             // 6 個月
            DepositRateAnnual: 0.055       // 存款利率 5.5%
        );
    }
}
