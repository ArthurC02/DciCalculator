using System.Runtime.CompilerServices;
using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// Strike 求解器。
/// 用於反推達成目標 Coupon 所需的行使價 (Strike)。
/// 
/// 使用場景：
/// - 客戶希望達成年化收益 8%
/// - 需透過調整行使價找到對應的結構 Strike（例如得到 29.85）
/// 
/// 方法：Newton-Raphson 數值迭代。
/// </summary>
public static class StrikeSolver
{
    private const double Tolerance = 1e-4;      // Coupon 誤差 0.01%
    private const int MaxIterations = 50;       // 最大迭代次數
    private const double MinStrikeRatio = 0.80; // Strike 不低於 Spot 的 80%
    private const double MaxStrikeRatio = 1.20; // Strike 不高於 Spot 的 120%

    /// <summary>
    /// 反推達成目標 Coupon 所需的 Strike。
    /// 
    /// 說明：賣出 Put 收取期權價格 + 存款利息 → 年化 Coupon。
    /// 調整 Strike 會改變 Put 價格，進而影響總利息與 Coupon。
    /// </summary>
    /// <param name="input">DCI 輸入（Strike 將被動態替換）</param>
    /// <param name="targetCoupon">目標年化 Coupon (例如 0.08 = 8%)</param>
    /// <param name="initialGuess">初始 Strike 估計（預設 Spot * 0.98）</param>
    /// <returns>對應 Strike；若無法收斂會擲出例外</returns>
    public static decimal SolveStrike(
        DciInput input,
        double targetCoupon,
        decimal? initialGuess = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (targetCoupon <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetCoupon),
                "目標 Coupon 必須 > 0");

        // 初始猜測：接近 Spot（DCI 結構為賣出 Put）
        decimal spotMid = input.SpotQuote.Mid;
        decimal strike = initialGuess ?? spotMid * 0.98m;

        // Strike 邊界範圍（風控限制）
        decimal minStrike = spotMid * (decimal)MinStrikeRatio;
        decimal maxStrike = spotMid * (decimal)MaxStrikeRatio;

        for (int i = 0; i < MaxIterations; i++)
        {
            // 1. 計算目前 Strike 的 Coupon
            double currentCoupon = CalculateCoupon(input, strike);
            double diff = currentCoupon - targetCoupon;

            // 2. 誤差檢查
            if (Math.Abs(diff) < Tolerance)
                return Math.Round(strike, 4, MidpointRounding.AwayFromZero);

            // 3. 計算導數 dCoupon / dStrike
            double derivative = CalculateCouponDerivative(input, strike);

            // 低導數防止過度步長
            if (Math.Abs(derivative) < 1e-10)
                break;

            // 4. Newton-Raphson 更新
            double strikeD = (double)strike;
            double strikeNew = strikeD - diff / derivative;

            // 控制單步最大變動（穩定收斂）
            double maxChange = strikeD * 0.1; // 最大 10% 變動
            strikeNew = Math.Clamp(strikeNew, strikeD - maxChange, strikeD + maxChange);
            strikeNew = Math.Clamp(strikeNew, (double)minStrike, (double)maxStrike);

            // 5. 收斂判斷
            decimal strikeNewDecimal = (decimal)strikeNew;
            if (Math.Abs(strikeNewDecimal - strike) < 0.0001m)
                return Math.Round(strikeNewDecimal, 4, MidpointRounding.AwayFromZero);

            strike = strikeNewDecimal;
        }

        // 未收斂：擲出例外（decimal 無 NaN）
        throw new InvalidOperationException(
            $"Strike 求解未收斂。目標 Coupon: {targetCoupon:P2}, 最終 Strike: {strike:F4}, Coupon: {CalculateCoupon(input, strike):P2}");
    }

    /// <summary>
    /// 產生 Strike 梯形 (Strike Ladder)：多個 Strike 與其對應 Coupon。
    /// 用於展示不同行使價對報酬的影響與客戶比較。
    /// </summary>
    /// <param name="input">DCI 輸入</param>
    /// <param name="strikeCount">梯形節點數</param>
    /// <param name="minStrikeRatio">最小 Strike（相對 Spot）</param>
    /// <param name="maxStrikeRatio">最大 Strike（相對 Spot）</param>
    /// <returns>(Strike, Coupon) 列表</returns>
    public static IReadOnlyList<(decimal Strike, double Coupon)> GenerateStrikeLadder(
        DciInput input,
        int strikeCount = 10,
        double minStrikeRatio = 0.95,
        double maxStrikeRatio = 1.00)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (strikeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(strikeCount), "節點數需 > 0");

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
    /// 計算指定 Strike 的 Coupon。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateCoupon(DciInput input, decimal strike)
    {
        // 建立新的輸入（替換 Strike）
        var modifiedInput = input with { Strike = strike };

        // 報價計算
        var quote = DciPricer.Quote(modifiedInput);

        return quote.CouponAnnual;
    }

    /// <summary>
    /// 計算 dCoupon / dStrike（對 Strike 的敏感度）。
    /// 使用中央差分近似：f'(x) ≈ [f(x+h) - f(x-h)] / (2h)
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
    /// 判斷 Strike 是否合理（相對 Spot 在允許範圍）。
    /// </summary>
    /// <param name="strike">Strike</param>
    /// <param name="spot">Spot</param>
    /// <returns>True ���ܦX�z</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStrikeReasonable(decimal strike, decimal spot)
    {
        if (strike <= 0 || spot <= 0)
            return false;

        double ratio = (double)(strike / spot);

        // Strike 位於 Spot 的 80% ~ 120% 之間
        return ratio >= MinStrikeRatio && ratio <= MaxStrikeRatio;
    }

    /// <summary>
    /// 計算最大可能 Coupon（Strike = Spot * 1.2）。用於檢查目標 Coupon 是否可達成。
    /// </summary>
    public static double CalculateMaxCoupon(DciInput input)
    {
        decimal maxStrike = input.SpotQuote.Mid * (decimal)MaxStrikeRatio;
        return CalculateCoupon(input, maxStrike);
    }

    /// <summary>
    /// 計算最小可能 Coupon（Strike = Spot * 0.8）。
    /// </summary>
    public static double CalculateMinCoupon(DciInput input)
    {
        decimal minStrike = input.SpotQuote.Mid * (decimal)MinStrikeRatio;
        return CalculateCoupon(input, minStrike);
    }
}
