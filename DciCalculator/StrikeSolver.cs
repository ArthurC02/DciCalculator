using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// Strike 求解器
/// 反推達到目標 Coupon 所需的 Strike
/// 
/// 使用場景：
/// - 客戶要求年化收益率 8%
/// - 系統自動計算需要設定 Strike = 29.85
/// 
/// 方法：Newton-Raphson 迭代求解
/// </summary>
public static class StrikeSolver
{
    private const double Tolerance = 1e-4;      // Coupon 精度 0.01%
    private const int MaxIterations = 50;       // 最大迭代次數
    private const double MinStrikeRatio = 0.80; // Strike 最小為 Spot 的 80%
    private const double MaxStrikeRatio = 1.20; // Strike 最大為 Spot 的 120%

    /// <summary>
    /// 求解達到目標 Coupon 所需的 Strike
    /// 
    /// 原理：
    /// Strike ↓ → Put 期權價值 ↓ → 期權利息 ↓ → Coupon ↓
    /// Strike ↑ → Put 期權價值 ↑ → 期權利息 ↑ → Coupon ↑
    /// </summary>
    /// <param name="input">DCI 輸入（Strike 值將被忽略）</param>
    /// <param name="targetCoupon">目標年化收益率（例如 0.08 = 8%）</param>
    /// <param name="initialGuess">Strike 初始猜測（預設使用 Spot * 0.98）</param>
    /// <returns>達到目標 Coupon 的 Strike，若無法收斂則返回 NaN</returns>
    public static decimal SolveStrike(
        DciInput input,
        double targetCoupon,
        decimal? initialGuess = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (targetCoupon <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetCoupon),
                "目標 Coupon 必須 > 0");

        // 初始猜測：略低於 Spot（DCI 慣例）
        decimal spotMid = input.SpotQuote.Mid;
        decimal strike = initialGuess ?? spotMid * 0.98m;

        // Strike 範圍限制
        decimal minStrike = spotMid * (decimal)MinStrikeRatio;
        decimal maxStrike = spotMid * (decimal)MaxStrikeRatio;

        for (int i = 0; i < MaxIterations; i++)
        {
            // 1. 計算當前 Strike 的 Coupon
            double currentCoupon = CalculateCoupon(input, strike);
            double diff = currentCoupon - targetCoupon;

            // 2. 收斂檢查
            if (Math.Abs(diff) < Tolerance)
                return Math.Round(strike, 4, MidpointRounding.AwayFromZero);

            // 3. 計算導數（dCoupon / dStrike）
            double derivative = CalculateCouponDerivative(input, strike);

            // 避免導數過小
            if (Math.Abs(derivative) < 1e-10)
                break;

            // 4. Newton-Raphson 更新
            double strikeD = (double)strike;
            double strikeNew = strikeD - diff / derivative;

            // 限制更新幅度（防止震盪）
            double maxChange = strikeD * 0.1; // 最多變動 10%
            strikeNew = Math.Clamp(strikeNew, strikeD - maxChange, strikeD + maxChange);
            strikeNew = Math.Clamp(strikeNew, (double)minStrike, (double)maxStrike);

            // 5. 檢查是否收斂
            decimal strikeNewDecimal = (decimal)strikeNew;
            if (Math.Abs(strikeNewDecimal - strike) < 0.0001m)
                return Math.Round(strikeNewDecimal, 4, MidpointRounding.AwayFromZero);

            strike = strikeNewDecimal;
        }

        // 未收斂：返回 NaN（轉成 decimal 會拋例外，改用特殊值）
        throw new InvalidOperationException(
            $"Strike 求解未收斂。目標 Coupon: {targetCoupon:P2}, " +
            $"最後嘗試 Strike: {strike:F4}, Coupon: {CalculateCoupon(input, strike):P2}");
    }

    /// <summary>
    /// 產生 Strike Ladder（一系列 Strike 及對應 Coupon）
    /// 用於展示給客戶選擇
    /// </summary>
    /// <param name="input">DCI 輸入</param>
    /// <param name="strikeCount">Strike 數量</param>
    /// <param name="minStrikeRatio">最小 Strike（相對 Spot）</param>
    /// <param name="maxStrikeRatio">最大 Strike（相對 Spot）</param>
    /// <returns>Strike 及對應 Coupon 的列表</returns>
    public static IReadOnlyList<(decimal Strike, double Coupon)> GenerateStrikeLadder(
        DciInput input,
        int strikeCount = 10,
        double minStrikeRatio = 0.95,
        double maxStrikeRatio = 1.00)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (strikeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(strikeCount), "數量必須 > 0");

        decimal spotMid = input.SpotQuote.Mid;
        decimal minStrike = spotMid * (decimal)minStrikeRatio;
        decimal maxStrike = spotMid * (decimal)maxStrikeRatio;
        decimal stepSize = (maxStrike - minStrike) / (strikeCount - 1);

        var ladder = new List<(decimal Strike, double Coupon)>(strikeCount);

        for (int i = 0; i < strikeCount; i++)
        {
            decimal strike = minStrike + stepSize * i;
            strike = Math.Round(strike, 4, MidpointRounding.AwayFromZero);

            double coupon = CalculateCoupon(input, strike);

            ladder.Add((strike, coupon));
        }

        return ladder;
    }

    /// <summary>
    /// 計算給定 Strike 的 Coupon
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateCoupon(DciInput input, decimal strike)
    {
        // 建立新的 input，替換 Strike
        var modifiedInput = input with { Strike = strike };

        // 計算報價
        var quote = DciPricer.Quote(modifiedInput);

        return quote.CouponAnnual;
    }

    /// <summary>
    /// 計算 dCoupon / dStrike（數值導數）
    /// 使用中央差分法：f'(x) ? [f(x+h) - f(x-h)] / (2h)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateCouponDerivative(DciInput input, decimal strike)
    {
        const decimal h = 0.01m; // 1 pip

        decimal strikePlus = strike + h;
        decimal strikeMinus = strike - h;

        double couponPlus = CalculateCoupon(input, strikePlus);
        double couponMinus = CalculateCoupon(input, strikeMinus);

        double derivative = (couponPlus - couponMinus) / (2.0 * (double)h);

        return derivative;
    }

    /// <summary>
    /// 驗證 Strike 是否合理
    /// </summary>
    /// <param name="strike">Strike</param>
    /// <param name="spot">Spot</param>
    /// <returns>True 表示合理</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStrikeReasonable(decimal strike, decimal spot)
    {
        if (strike <= 0 || spot <= 0)
            return false;

        double ratio = (double)(strike / spot);

        // Strike 應該在 Spot 的 80% ~ 120% 之間
        return ratio >= MinStrikeRatio && ratio <= MaxStrikeRatio;
    }

    /// <summary>
    /// 計算最大可達 Coupon（Strike = Spot * 1.2）
    /// 用於檢查目標 Coupon 是否可達
    /// </summary>
    public static double CalculateMaxCoupon(DciInput input)
    {
        decimal maxStrike = input.SpotQuote.Mid * (decimal)MaxStrikeRatio;
        return CalculateCoupon(input, maxStrike);
    }

    /// <summary>
    /// 計算最小 Coupon（Strike = Spot * 0.8）
    /// </summary>
    public static double CalculateMinCoupon(DciInput input)
    {
        decimal minStrike = input.SpotQuote.Mid * (decimal)MinStrikeRatio;
        return CalculateCoupon(input, minStrike);
    }
}
