using System.Runtime.CompilerServices;
using DciCalculator.Models;
using DciCalculator.Services.Pricing;

namespace DciCalculator;

/// <summary>
/// Strike 求解器（向後相容）。
/// ⚠️ 請使用 <see cref="StrikeSolverService"/> 類別以支援依賴注入。
/// </summary>
[Obsolete("請使用 StrikeSolverService 類別以支援依賴注入。此靜態類別將在未來版本移除。")]
public static class StrikeSolver
{
    private static readonly StrikeSolverService _solver = new(new DciPricingEngine(new PricingModels.GarmanKohlhagenModel()));

    /// <summary>
    /// 反推達成目標 Coupon 所需的 Strike。
    /// </summary>
    public static decimal SolveStrike(
        DciInput input,
        double targetCoupon,
        decimal? initialGuess = null)
    {
        return _solver.SolveStrike(input, targetCoupon, initialGuess);
    }

    /// <summary>
    /// 產生 Strike 梯形（使用百分比範圍）。
    /// </summary>
    public static IReadOnlyList<(decimal Strike, double Coupon)> GenerateStrikeLadder(
        DciInput input,
        int strikeCount = 10,
        double minStrikeRatio = 0.95,
        double maxStrikeRatio = 1.00)
    {
        double belowSpotPercent = 1.0 - minStrikeRatio;
        double aboveSpotPercent = maxStrikeRatio - 1.0;
        return _solver.GenerateStrikeLadderByPercent(input, strikeCount, belowSpotPercent, aboveSpotPercent);
    }

    /// <summary>
    /// 判斷 Strike 是否合理（相對 Spot 在允許範圍）。
    /// </summary>
    public static bool IsStrikeReasonable(decimal strike, decimal spot)
    {
        return _solver.IsStrikeReasonable(strike, spot);
    }

    /// <summary>
    /// 計算最大可能 Coupon。
    /// </summary>
    public static double CalculateMaxCoupon(DciInput input)
    {
        return _solver.CalculateMaxCoupon(input);
    }

    /// <summary>
    /// 計算最小可能 Coupon。
    /// </summary>
    public static double CalculateMinCoupon(DciInput input)
    {
        return _solver.CalculateMinCoupon(input);
    }
}
