using System.Runtime.CompilerServices;

namespace DciCalculator;

/// <summary>
/// Margin 計算器
/// 處理銀行利潤加成（Pips 或百分比）
/// 
/// Margin 對 DCI 的影響：
/// - Margin 降低期權價值
/// - 降低期權利息
/// - 降低總 Coupon（客戶看到的收益率）
/// </summary>
public static class MarginCalculator
{
    /// <summary>
    /// 以 Pips 方式加上 Margin
    /// 
    /// 原理：
    /// - 理論 Strike = 30.00
    /// - 加上 10 pips Margin → 實際 Strike = 29.90
    /// - Strike 降低 → Put 期權價值降低 → 客戶收益降低
    /// </summary>
    /// <param name="theoreticalStrike">理論 Strike（不含 Margin）</param>
    /// <param name="marginPips">Margin（pips），正值表示降低 Strike</param>
    /// <param name="pipSize">Pip 大小（預設 0.01，適用 USD/TWD）</param>
    /// <returns>加上 Margin 後的 Strike</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ApplyMarginToStrike(
        decimal theoreticalStrike,
        decimal marginPips,
        decimal pipSize = 0.01m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(theoreticalStrike);
        
        // Margin 降低 Strike（對銀行有利）
        decimal marginAmount = marginPips * pipSize;
        decimal adjustedStrike = theoreticalStrike - marginAmount;

        // 確保 Strike 為正
        if (adjustedStrike <= 0)
            throw new ArgumentException(
                $"調整後 Strike ({adjustedStrike}) 必須 > 0。" +
                $"理論 Strike: {theoreticalStrike}, Margin: {marginPips} pips",
                nameof(marginPips));

        return adjustedStrike;
    }

    /// <summary>
    /// 以百分比方式加上 Margin
    /// 
    /// 原理：
    /// - 理論期權價格 = 0.50 TWD per 1 USD
    /// - 加上 10% Margin → 實際價格 = 0.45 TWD per 1 USD
    /// - 期權價格降低 → 客戶收益降低
    /// </summary>
    /// <param name="theoreticalPrice">理論期權價格</param>
    /// <param name="marginPercent">Margin 百分比（0.10 = 10%）</param>
    /// <returns>扣除 Margin 後的期權價格</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ApplyMarginToPrice(
        decimal theoreticalPrice,
        double marginPercent)
    {
        if (theoreticalPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(theoreticalPrice), 
                "期權價格不能為負");

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 百分比必須在 [0, 1) 範圍內");

        // 扣除 Margin
        decimal adjustedPrice = theoreticalPrice * (1m - (decimal)marginPercent);

        return adjustedPrice;
    }

    /// <summary>
    /// 計算加上 Margin 後的 Coupon
    /// 
    /// 完整邏輯：
    /// 1. 計算理論期權價格（無 Margin）
    /// 2. 扣除 Margin
    /// 3. 重新計算期權利息
    /// 4. 計算新的總 Coupon
    /// </summary>
    /// <param name="theoreticalOptionPrice">理論期權價格（本幣每 1 外幣）</param>
    /// <param name="marginPercent">Margin 百分比</param>
    /// <param name="notionalForeign">外幣本金</param>
    /// <param name="spot">即期匯率</param>
    /// <param name="depositInterest">定存利息（外幣）</param>
    /// <param name="tenorInYears">期限（年）</param>
    /// <returns>加上 Margin 後的年化 Coupon</returns>
    public static double CalculateCouponWithMargin(
        double theoreticalOptionPrice,
        double marginPercent,
        decimal notionalForeign,
        decimal spot,
        decimal depositInterest,
        double tenorInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notionalForeign);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenorInYears);

        // 1. 扣除 Margin
        double adjustedOptionPrice = theoreticalOptionPrice * (1.0 - marginPercent);

        // 2. 轉換成外幣等值
        double optionPricePerForeign = adjustedOptionPrice / (double)spot;

        // 3. 計算期權利息（外幣）
        decimal optionInterest = notionalForeign * (decimal)optionPricePerForeign;

        // 4. 總利息
        decimal totalInterest = depositInterest + optionInterest;

        // 5. 年化 Coupon
        double coupon = (double)(totalInterest / notionalForeign) / tenorInYears;

        return coupon;
    }

    /// <summary>
    /// 計算達到目標 Coupon 所需的 Margin
    /// 使用二分搜尋法
    /// </summary>
    /// <param name="theoreticalCoupon">理論 Coupon（無 Margin）</param>
    /// <param name="targetCoupon">目標 Coupon</param>
    /// <returns>所需 Margin 百分比，若無法達成則返回 NaN</returns>
    public static double SolveMarginForTargetCoupon(
        double theoreticalCoupon,
        double targetCoupon)
    {
        if (targetCoupon >= theoreticalCoupon)
            return 0.0; // 無需 Margin

        if (targetCoupon <= 0)
            return double.NaN; // 無效目標

        // 使用線性近似
        // Coupon 與期權價格成正比
        // margin_pct = (理論 Coupon - 目標 Coupon) / 理論 Coupon
        double marginPercent = (theoreticalCoupon - targetCoupon) / theoreticalCoupon;

        // 限制範圍 [0, 0.5]（最多扣除 50%）
        return Math.Clamp(marginPercent, 0.0, 0.5);
    }

    /// <summary>
    /// 計算 Bid/Ask Spread（買賣價差）
    /// 用於從 Mid 價推算對客報價
    /// </summary>
    /// <param name="mid">Mid 價格</param>
    /// <param name="spreadPips">Spread（pips）</param>
    /// <param name="pipSize">Pip 大小</param>
    /// <returns>(Bid, Ask)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (decimal Bid, decimal Ask) ApplySpread(
        decimal mid,
        decimal spreadPips,
        decimal pipSize = 0.01m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mid);
        ArgumentOutOfRangeException.ThrowIfNegative(spreadPips);

        decimal halfSpread = spreadPips * pipSize / 2m;
        decimal bid = mid - halfSpread;
        decimal ask = mid + halfSpread;

        return (bid, ask);
    }

    /// <summary>
    /// 計算總成本（Margin + Spread）對 Coupon 的影響
    /// </summary>
    /// <param name="theoreticalCoupon">理論 Coupon</param>
    /// <param name="marginPercent">Margin 百分比</param>
    /// <param name="spreadCostPercent">Spread 成本百分比</param>
    /// <returns>實際 Coupon</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ApplyTotalCost(
        double theoreticalCoupon,
        double marginPercent,
        double spreadCostPercent)
    {
        double totalCost = marginPercent + spreadCostPercent;
        
        if (totalCost >= 1.0)
            throw new ArgumentException("總成本不能 >= 100%");

        return theoreticalCoupon * (1.0 - totalCost);
    }
}
