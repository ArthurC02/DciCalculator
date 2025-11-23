using System.Runtime.CompilerServices;

namespace DciCalculator;

/// <summary>
/// Margin 計算器。
/// 提供以 pips 或百分比對理論值加入保留利潤（Margin）的功能。
/// 
/// 與 DCI 商品相關的主要用途：
/// - 以 pips 下調理論 Strike（賣出 Put 時取得更保守行使價）
/// - 以百分比下調理論期權價格
/// - 將調整後期權價格轉換為額外利息並計算最終 Coupon
/// - 依目標 Coupon 反推所需 Margin 百分比
/// - 套用 Bid/Ask Spread 與合計成本（Margin + Spread）
/// </summary>
public static class MarginCalculator
{
    /// <summary>
    /// 以 pips 形式對理論 Strike 套用 Margin（通常用於賣出 Put 調整行使價）。
    /// 
    /// 範例：
    /// - 理論 Strike = 30.00
    /// - 加入 10 pips（pipSize=0.01）後調整 Strike = 29.90
    /// - 下調 Strike 代表賣出 Put 時的行使價更保守，有助提高安全邊際。
    /// </summary>
    /// <param name="theoreticalStrike">理論 Strike（尚未加入 Margin）</param>
    /// <param name="marginPips">Margin（pips），用來下調 Strike</param>
    /// <param name="pipSize">每 pip 數值大小（預設 0.01，適用 USD/TWD 類型）</param>
    /// <returns>加入 Margin 後的調整 Strike</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ApplyMarginToStrike(
        decimal theoreticalStrike,
        decimal marginPips,
        decimal pipSize = 0.01m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(theoreticalStrike);
        
        // 以 pips 下調 Strike（相當於銀行留取利潤）
        decimal marginAmount = marginPips * pipSize;
        decimal adjustedStrike = theoreticalStrike - marginAmount;

        // 確保 Strike 仍為正數
        if (adjustedStrike <= 0)
            throw new ArgumentException(
                $"調整後 Strike ({adjustedStrike}) 必須 > 0。" +
                $"理論 Strike: {theoreticalStrike}, Margin: {marginPips} pips",
                nameof(marginPips));

        return adjustedStrike;
    }

    /// <summary>
    /// 以百分比形式對理論期權價格套用 Margin。
    /// 
    /// 範例：
    /// - 理論價格 = 0.50 TWD per 1 USD
    /// - 加入 10% Margin 後調整價格 = 0.45 TWD per 1 USD
    /// - 下調後價格反映銀行留取利潤。
    /// </summary>
    /// <param name="theoreticalPrice">理論期權價格</param>
    /// <param name="marginPercent">Margin 百分比（0.10 = 10%）</param>
    /// <returns>套用 Margin 後的期權價格</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ApplyMarginToPrice(
        decimal theoreticalPrice,
        double marginPercent)
    {
        if (theoreticalPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(theoreticalPrice), 
                "理論價格不可為負。");

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 百分比必須落在 [0, 1)。");

        // 套用百分比 Margin 下調價格
        decimal adjustedPrice = theoreticalPrice * (1m - (decimal)marginPercent);

        return adjustedPrice;
    }

    /// <summary>
    /// 計算加入 Margin 後的最終 Coupon。
    /// 流程：
    /// 1. 使用理論期權價格（未含 Margin）
    /// 2. 套用 Margin 下調期權價格
    /// 3. 換算為期內額外利息（以外幣名義計）
    /// 4. 加總存款利息後計算最終年化 Coupon
    /// </summary>
    /// <param name="theoreticalOptionPrice">理論期權價格（不含 Margin，按 1 年化）</param>
    /// <param name="marginPercent">Margin 百分比</param>
    /// <param name="notionalForeign">外幣名義本金</param>
    /// <param name="spot">即期匯率</param>
    /// <param name="depositInterest">存款利息（外幣）</param>
    /// <param name="tenorInYears">期內年數（年期）</param>
    /// <returns>套用 Margin 後的年化 Coupon</returns>
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

        // 1. 套用 Margin 下調期權價格
        double adjustedOptionPrice = theoreticalOptionPrice * (1.0 - marginPercent);

        // 2. 換算為每單位外幣的期權價格
        double optionPricePerForeign = adjustedOptionPrice / (double)spot;

        // 3. 計算期權額外利息（外幣）
        decimal optionInterest = notionalForeign * (decimal)optionPricePerForeign;

        // 4. 加總存款利息
        decimal totalInterest = depositInterest + optionInterest;

        // 5. 換算年化 Coupon
        double coupon = (double)(totalInterest / notionalForeign) / tenorInYears;

        return coupon;
    }

    /// <summary>
    /// 反推達成目標 Coupon 所需的 Margin 百分比（近似線性）。
    /// </summary>
    /// <param name="theoreticalCoupon">理論 Coupon（未含 Margin）</param>
    /// <param name="targetCoupon">目標 Coupon</param>
    /// <returns>所需 Margin 百分比；若無法達成則回傳 NaN</returns>
    public static double SolveMarginForTargetCoupon(
        double theoreticalCoupon,
        double targetCoupon)
    {
        if (targetCoupon >= theoreticalCoupon)
            return 0.0; // 不需任何 Margin

        if (targetCoupon <= 0)
            return double.NaN; // 目標無效

        // 使用線性近似：
        // margin_pct = (理論 Coupon - 目標 Coupon) / 理論 Coupon
        double marginPercent = (theoreticalCoupon - targetCoupon) / theoreticalCoupon;

        // 基本風控上限：不超過 50%
        return Math.Clamp(marginPercent, 0.0, 0.5);
    }

    /// <summary>
    /// 由 Mid 價與總 Spread（pips）計算 Bid/Ask 報價。
    /// </summary>
    /// <param name="mid">Mid 價</param>
    /// <param name="spreadPips">總 Spread（pips）</param>
    /// <param name="pipSize">每 pip 數值大小</param>
    /// <returns>(Bid, Ask) 雙向報價</returns>
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
    /// 套用總成本（Margin + Spread 成本百分比）後的 Coupon。
    /// </summary>
    /// <param name="theoreticalCoupon">理論 Coupon</param>
    /// <param name="marginPercent">Margin 百分比</param>
    /// <param name="spreadCostPercent">Spread 成本百分比</param>
    /// <returns>扣除成本後的 Coupon</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ApplyTotalCost(
        double theoreticalCoupon,
        double marginPercent,
        double spreadCostPercent)
    {
        double totalCost = marginPercent + spreadCostPercent;
        
        if (totalCost >= 1.0)
            throw new ArgumentException("總成本百分比不可 >= 100%");

        return theoreticalCoupon * (1.0 - totalCost);
    }
}
