using DciCalculator.Models;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// Strike 求解器介面
/// </summary>
public interface IStrikeSolver
{
    /// <summary>
    /// 根據目標 Coupon 求解 Strike
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="targetCoupon">目標年化 Coupon（如 0.08 表示 8%）</param>
    /// <param name="initialGuess">初始猜測值（可選）</param>
    /// <param name="maxIterations">最大迭代次數</param>
    /// <param name="tolerance">容差</param>
    /// <returns>求解得到的 Strike</returns>
    decimal SolveStrike(
        DciInput input,
        double targetCoupon,
        decimal? initialGuess = null,
        int maxIterations = 50,
        double tolerance = 1e-6);

    /// <summary>
    /// 生成 Strike 梯形（多個 Strike 與對應的 Coupon）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="strikeCount">Strike 數量</param>
    /// <param name="minStrike">最小 Strike</param>
    /// <param name="maxStrike">最大 Strike</param>
    /// <returns>Strike 與 Coupon 的列表</returns>
    IReadOnlyList<(decimal Strike, double Coupon)> GenerateStrikeLadder(
        DciInput input,
        int strikeCount,
        decimal minStrike,
        decimal maxStrike);

    /// <summary>
    /// 生成 Strike 梯形（使用百分比範圍）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <param name="strikeCount">Strike 數量</param>
    /// <param name="belowSpotPercent">低於現貨的百分比（如 0.10 表示 10%）</param>
    /// <param name="aboveSpotPercent">高於現貨的百分比</param>
    /// <returns>Strike 與 Coupon 的列表</returns>
    IReadOnlyList<(decimal Strike, double Coupon)> GenerateStrikeLadderByPercent(
        DciInput input,
        int strikeCount,
        double belowSpotPercent,
        double aboveSpotPercent);
}
