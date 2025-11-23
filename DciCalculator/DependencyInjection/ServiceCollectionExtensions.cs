using DciCalculator.Core.Interfaces;
using DciCalculator.DayCount;
using DciCalculator.Factories;
using DciCalculator.PricingModels;
using DciCalculator.Services.Margin;
using DciCalculator.Services.Pricing;
using DciCalculator.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DciCalculator.DependencyInjection;

/// <summary>
/// DCI Calculator 服務註冊擴展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊所有 DCI Calculator 服務到 DI 容器
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <returns>服務集合（支援鏈式調用）</returns>
    public static IServiceCollection AddDciServices(this IServiceCollection services)
    {
        // 註冊定價模型（Singleton - 無狀態，可重用）
        services.TryAddSingleton<IPricingModel, GarmanKohlhagenModel>();

        // 註冊核心服務（Singleton - 無狀態，可重用）
        services.TryAddSingleton<IDciPricingEngine, DciPricingEngine>();
        services.TryAddSingleton<IGreeksCalculator, GreeksCalculatorService>();
        services.TryAddSingleton<IMarginService, MarginService>();
        services.TryAddSingleton<IStrikeSolver, StrikeSolverService>();
        services.TryAddSingleton<IScenarioAnalyzer, ScenarioAnalyzerService>();
        services.TryAddSingleton<IDciPayoffCalculator, DciPayoffCalculatorService>();

        // 註冊工廠（Singleton - 無狀態，可重用）
        services.TryAddSingleton<ICurveFactory, CurveFactory>();
        services.TryAddSingleton<IVolSurfaceFactory, VolSurfaceFactory>();

        // 註冊驗證器（Singleton - 無狀態，可重用）
        services.TryAddSingleton<IValidator<Models.DciInput>, DciInputValidator>();
        services.TryAddSingleton<IValidator<Models.MarketDataSnapshot>, MarketDataSnapshotValidator>();

        // 註冊 Day Count Calculators（Singleton - 無狀態，可重用）
        services.TryAddSingleton<DayCountCalculatorFactory>();
        services.TryAddSingleton<IDayCountCalculator>(sp => 
            sp.GetRequiredService<DayCountCalculatorFactory>().GetCalculator(DayCountConvention.Act365));

        return services;
    }

    /// <summary>
    /// 註冊 DCI Calculator 服務並允許自訂定價模型
    /// </summary>
    /// <typeparam name="TPricingModel">定價模型類型</typeparam>
    /// <param name="services">服務集合</param>
    /// <returns>服務集合（支援鏈式調用）</returns>
    public static IServiceCollection AddDciServices<TPricingModel>(this IServiceCollection services)
        where TPricingModel : class, IPricingModel
    {
        // 註冊自訂定價模型
        services.TryAddSingleton<IPricingModel, TPricingModel>();

        // 註冊核心服務
        services.TryAddSingleton<IDciPricingEngine, DciPricingEngine>();
        services.TryAddSingleton<IGreeksCalculator, GreeksCalculatorService>();
        services.TryAddSingleton<IMarginService, MarginService>();
        services.TryAddSingleton<IStrikeSolver, StrikeSolverService>();
        services.TryAddSingleton<IScenarioAnalyzer, ScenarioAnalyzerService>();
        services.TryAddSingleton<IDciPayoffCalculator, DciPayoffCalculatorService>();

        // 註冊工廠
        services.TryAddSingleton<ICurveFactory, CurveFactory>();
        services.TryAddSingleton<IVolSurfaceFactory, VolSurfaceFactory>();

        // 註冊驗證器
        services.TryAddSingleton<IValidator<Models.DciInput>, DciInputValidator>();
        services.TryAddSingleton<IValidator<Models.MarketDataSnapshot>, MarketDataSnapshotValidator>();

        // 註冊 Day Count Calculators
        services.TryAddSingleton<DayCountCalculatorFactory>();
        services.TryAddSingleton<IDayCountCalculator>(sp => 
            sp.GetRequiredService<DayCountCalculatorFactory>().GetCalculator(DayCountConvention.Act365));

        return services;
    }

    /// <summary>
    /// 註冊 DCI Calculator 服務並使用自訂實例
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configurator">服務配置器</param>
    /// <returns>服務集合（支援鏈式調用）</returns>
    public static IServiceCollection AddDciServices(
        this IServiceCollection services,
        Action<DciServicesConfigurator> configurator)
    {
        var config = new DciServicesConfigurator(services);
        configurator(config);
        return services;
    }
}

/// <summary>
/// DCI 服務配置器
/// </summary>
public class DciServicesConfigurator
{
    private readonly IServiceCollection _services;

    internal DciServicesConfigurator(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// 使用自訂定價模型
    /// </summary>
    public DciServicesConfigurator WithPricingModel<TPricingModel>()
        where TPricingModel : class, IPricingModel
    {
        _services.AddSingleton<IPricingModel, TPricingModel>();
        return this;
    }

    /// <summary>
    /// 使用自訂定價模型實例
    /// </summary>
    public DciServicesConfigurator WithPricingModel(IPricingModel pricingModel)
    {
        _services.AddSingleton(pricingModel);
        return this;
    }

    /// <summary>
    /// 使用自訂 Greeks 計算器
    /// </summary>
    public DciServicesConfigurator WithGreeksCalculator<TGreeksCalculator>()
        where TGreeksCalculator : class, IGreeksCalculator
    {
        _services.AddSingleton<IGreeksCalculator, TGreeksCalculator>();
        return this;
    }

    /// <summary>
    /// 使用自訂邊際服務
    /// </summary>
    public DciServicesConfigurator WithMarginService<TMarginService>()
        where TMarginService : class, IMarginService
    {
        _services.AddSingleton<IMarginService, TMarginService>();
        return this;
    }

    /// <summary>
    /// 使用自訂曲線工廠
    /// </summary>
    public DciServicesConfigurator WithCurveFactory<TCurveFactory>()
        where TCurveFactory : class, ICurveFactory
    {
        _services.AddSingleton<ICurveFactory, TCurveFactory>();
        return this;
    }

    /// <summary>
    /// 使用自訂波動率曲面工廠
    /// </summary>
    public DciServicesConfigurator WithVolSurfaceFactory<TVolSurfaceFactory>()
        where TVolSurfaceFactory : class, IVolSurfaceFactory
    {
        _services.AddSingleton<IVolSurfaceFactory, TVolSurfaceFactory>();
        return this;
    }

    /// <summary>
    /// 註冊自訂驗證器
    /// </summary>
    public DciServicesConfigurator WithValidator<T, TValidator>()
        where TValidator : class, IValidator<T>
    {
        _services.AddSingleton<IValidator<T>, TValidator>();
        return this;
    }

    /// <summary>
    /// 使用特定的 Day Count 慣例作為預設計算器
    /// </summary>
    public DciServicesConfigurator WithDayCountConvention(DayCountConvention convention)
    {
        _services.AddSingleton<IDayCountCalculator>(sp => 
            sp.GetRequiredService<DayCountCalculatorFactory>().GetCalculator(convention));
        return this;
    }
}
