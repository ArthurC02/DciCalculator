using System.Runtime.CompilerServices;
using DciCalculator.Core.Interfaces;
using DciCalculator.Services.Margin;

namespace DciCalculator;

/// <summary>
/// Margin 計算器 (靜態版本 - 已廢棄)
/// </summary>
[Obsolete("請使用 MarginService 類別以支援依賴注入。此靜態類別的剩餘方法將在未來版本移除。")]
public static class MarginCalculator
{
    private static readonly IMarginService _service = new MarginService();


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
    /// 計算加入 Margin 後的最終 Coupon（完整版本）
    /// </summary>
    public static double CalculateCouponWithMargin(
        double theoreticalOptionPrice,
        double marginPercent,
        decimal notionalForeign,
        decimal spot,
        decimal depositInterest,
        double tenorInYears)
    {
        return _service.CalculateCouponWithMarginDetailed(
            theoreticalOptionPrice,
            marginPercent,
            notionalForeign,
            spot,
            depositInterest,
            tenorInYears);
    }

    /// <summary>
    /// 反推達成目標 Coupon 所需的 Margin 百分比（近似線性）
    /// </summary>
    public static double SolveMarginForTargetCoupon(
        double theoreticalCoupon,
        double targetCoupon)
    {
        if (targetCoupon >= theoreticalCoupon)
            return 0.0;

        if (targetCoupon <= 0)
            return double.NaN;

        try
        {
            return _service.SolveMarginForTargetCoupon(theoreticalCoupon, targetCoupon);
        }
        catch
        {
            // 使用線性近似（舊邏輯）
            double marginPercent = (theoreticalCoupon - targetCoupon) / theoreticalCoupon;
            return Math.Clamp(marginPercent, 0.0, 0.5);
        }
    }

    /// <summary>
    /// 由 Mid 價與總 Spread（pips）計算 Bid/Ask 報價
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (decimal Bid, decimal Ask) ApplySpread(
        decimal mid,
        decimal spreadPips,
        decimal pipSize = 0.01m)
    {
        return _service.ApplySpread(mid, spreadPips, pipSize);
    }

    /// <summary>
    /// 套用總成本（Margin + Spread 成本百分比）後的 Coupon
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ApplyTotalCost(
        double theoreticalCoupon,
        double marginPercent,
        double spreadCostPercent)
    {
        return _service.ApplyTotalCost(theoreticalCoupon, marginPercent, spreadCostPercent);
    }
}
