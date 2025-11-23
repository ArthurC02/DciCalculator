using DciCalculator.Core.Interfaces;
using DciCalculator.Models;
using DciCalculator.PricingModels;
using DciCalculator.Services.Pricing;

namespace DciCalculator;

/// <summary>
/// 情境分析器（向後相容）。
/// ⚠️ 請使用 <see cref="ScenarioAnalyzerService"/> 類別以支援依賴注入。
/// </summary>
[Obsolete("請使用 ScenarioAnalyzerService 類別以支援依賴注入。此靜態類別將在未來版本移除。")]
public static class ScenarioAnalyzer
{
    private static readonly ScenarioAnalyzerService _analyzer = new(
        new DciPricingEngine(new GarmanKohlhagenModel()),
        new DciPayoffCalculatorService());

    /// <summary>
    /// 執行情境分析。
    /// </summary>
    public static IReadOnlyList<ScenarioResult> Analyze(
        DciInput baseInput,
        IEnumerable<decimal> spotShifts,
        IEnumerable<double> volShifts)
    {
        return _analyzer.Analyze(baseInput, spotShifts, volShifts);
    }

    /// <summary>
    /// 快速預設情境分析。
    /// </summary>
    public static IReadOnlyList<ScenarioResult> QuickAnalyze(DciInput baseInput)
    {
        return _analyzer.QuickAnalyze(baseInput);
    }

    /// <summary>
    /// 計算敏感度（單一步長近似）。
    /// </summary>
    public static (double SpotDelta, double VolVega) CalculateSensitivities(DciInput baseInput)
    {
        var result = _analyzer.CalculateSensitivities(baseInput);
        return (result.DeltaSpot, result.DeltaVol);
    }

    /// <summary>
    /// 計算到期 PnL 分佈（Monte Carlo 模擬）。
    /// </summary>
    public static PnLDistribution CalculatePnLDistribution(
        DciInput baseInput,
        int scenarios = 100,
        double spotVolatility = 0.10)
    {
        return _analyzer.CalculatePnLDistribution(baseInput, scenarios, spotVolatility);
    }

    /// <summary>
    /// 產生情境分析報告（文字格式）。
    /// </summary>
    public static string GenerateReport(IReadOnlyList<ScenarioResult> results)
    {
        return _analyzer.GenerateReport(results);
    }
}

/// <summary>
/// 情境分析結果。
/// </summary>
public sealed record ScenarioResult(
    decimal SpotShift,          // Spot 移動（pips）
    double VolShift,            // Vol 變動（絕對）
    decimal Spot,               // 新 Spot
    double Volatility,          // 新 Vol
    double Coupon,              // 新 Coupon
    double CouponChange,        // Coupon 變化（vs. 基準）
    decimal TotalInterest,      // 總利息（外幣）
    decimal InterestChange      // 利息變化（vs. 基準）
);

/// <summary>
/// PnL 分佈統計。
/// </summary>
public sealed record PnLDistribution(
    int Scenarios,              // 模擬次數
    decimal Mean,               // 平均 PnL
    decimal Median,             // 中位數 PnL
    decimal Percentile5,        // 5% 分位（VaR 95%）
    decimal Percentile95,       // 95% 分位
    decimal Min,                // 最小 PnL
    decimal Max                 // 最大 PnL
);
