using DciCalculator.Models;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// 情境分析器介面
/// </summary>
public interface IScenarioAnalyzer
{
    /// <summary>
    /// 執行情境分析
    /// </summary>
    /// <param name="baseInput">基準 DCI 輸入</param>
    /// <param name="spotShifts">Spot 變動列表（可為負值）</param>
    /// <param name="volShifts">波動率變動列表（可為負值）</param>
    /// <returns>情境結果列表</returns>
    IReadOnlyList<ScenarioResult> Analyze(
        DciInput baseInput,
        IEnumerable<decimal> spotShifts,
        IEnumerable<double> volShifts);

    /// <summary>
    /// 快速情境分析（使用預設參數）
    /// </summary>
    /// <param name="baseInput">基準 DCI 輸入</param>
    /// <returns>情境結果列表</returns>
    IReadOnlyList<ScenarioResult> QuickAnalyze(DciInput baseInput);

    /// <summary>
    /// 計算敏感度（單步 bump）
    /// </summary>
    /// <param name="baseInput">基準 DCI 輸入</param>
    /// <param name="spotBump">Spot bump 大小（預設 0.01）</param>
    /// <param name="volBump">波動率 bump 大小（預設 0.01）</param>
    /// <returns>敏感度結果</returns>
    SensitivityResult CalculateSensitivities(
        DciInput baseInput,
        decimal spotBump = 0.01m,
        double volBump = 0.01);

    /// <summary>
    /// 生成情境分析報告
    /// </summary>
    /// <param name="results">情境結果列表</param>
    /// <returns>格式化的報告字串</returns>
    string GenerateReport(IReadOnlyList<ScenarioResult> results);

    /// <summary>
    /// 計算 PnL 分佈（Monte Carlo 模擬）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="scenarios">模擬情境數量</param>
    /// <param name="spotVolatility">Spot 波動率（年化）</param>
    /// <param name="seed">隨機種子（可選，用於重現性）</param>
    /// <returns>PnL 分佈統計</returns>
    PnLDistribution CalculatePnLDistribution(
        DciInput input,
        int scenarios = 1000,
        double spotVolatility = 0.10,
        int? seed = null);
}

/// <summary>
/// 敏感度結果
/// </summary>
public record SensitivityResult(
    double DeltaSpot,      // Spot 敏感度
    double DeltaVol);      // 波動率敏感度
