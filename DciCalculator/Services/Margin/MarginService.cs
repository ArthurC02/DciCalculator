using DciCalculator.Core.Interfaces;

namespace DciCalculator.Services.Margin;

/// <summary>
/// 邊際服務（實例版本）
/// 實現 IMarginService 介面，支援依賴注入
/// </summary>
public class MarginService : IMarginService
{
    /// <summary>
    /// 對 Strike 應用邊際調整（pips）
    /// </summary>
    public decimal ApplyMarginToStrike(decimal strike, decimal marginPips, decimal pipSize = 0.01m)
    {
        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), "Strike 必須大於 0");

        if (pipSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pipSize), "Pip size 必須大於 0");

        return strike + (marginPips * pipSize);
    }

    /// <summary>
    /// 對期權價格應用邊際調整（百分比）
    /// </summary>
    public decimal ApplyMarginToPrice(decimal optionPrice, double marginPercent)
    {
        if (optionPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(optionPrice), "期權價格不能為負值");

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 必須在 [0, 1) 範圍內");

        return optionPrice * (1m - (decimal)marginPercent);
    }

    /// <summary>
    /// 根據理論 Coupon 與目標 Coupon 反推所需的邊際
    /// </summary>
    public double SolveMarginForTargetCoupon(double theoreticalCoupon, double targetCoupon)
    {
        if (theoreticalCoupon <= 0)
            throw new ArgumentOutOfRangeException(nameof(theoreticalCoupon),
                "理論 Coupon 必須大於 0");

        if (targetCoupon < 0)
            throw new ArgumentOutOfRangeException(nameof(targetCoupon),
                "目標 Coupon 不能為負值");

        if (targetCoupon > theoreticalCoupon)
            throw new ArgumentException(
                $"目標 Coupon ({targetCoupon:P2}) 不能大於理論 Coupon ({theoreticalCoupon:P2})");

        // margin = 1 - (target / theoretical)
        return 1.0 - (targetCoupon / theoreticalCoupon);
    }

    /// <summary>
    /// 計算邊際調整後的 Coupon
    /// </summary>
    public double CalculateCouponWithMargin(double baseCoupon, double marginPercent)
    {
        if (baseCoupon < 0)
            throw new ArgumentOutOfRangeException(nameof(baseCoupon),
                "基礎 Coupon 不能為負值");

        if (marginPercent < 0 || marginPercent >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(marginPercent),
                "Margin 必須在 [0, 1) 範圍內");

        return baseCoupon * (1.0 - marginPercent);
    }

    /// <summary>
    /// 計算價差（Spread）
    /// </summary>
    public decimal CalculateSpread(decimal bidPrice, decimal askPrice)
    {
        if (bidPrice < 0 || askPrice < 0)
            throw new ArgumentOutOfRangeException("價格不能為負值");

        if (askPrice < bidPrice)
            throw new ArgumentException("Ask 價格必須大於等於 Bid 價格");

        return askPrice - bidPrice;
    }

    /// <summary>
    /// 計算價差百分比
    /// </summary>
    public double CalculateSpreadPercent(decimal bidPrice, decimal askPrice)
    {
        decimal spread = CalculateSpread(bidPrice, askPrice);
        
        if (bidPrice == 0 && askPrice == 0)
            return 0.0;

        decimal midPrice = (bidPrice + askPrice) / 2m;
        
        if (midPrice == 0)
            return 0.0;

        return (double)(spread / midPrice);
    }

    /// <summary>
    /// 由 Mid 價與總 Spread（pips）計算 Bid/Ask 報價
    /// </summary>
    public (decimal Bid, decimal Ask) ApplySpread(
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
    /// 套用總成本（Margin + Spread 成本百分比）後的 Coupon
    /// </summary>
    public double ApplyTotalCost(
        double theoreticalCoupon,
        double marginPercent,
        double spreadCostPercent)
    {
        double totalCost = marginPercent + spreadCostPercent;
        
        if (totalCost >= 1.0)
            throw new ArgumentException("總成本百分比不可 >= 100%");

        return theoreticalCoupon * (1.0 - totalCost);
    }

    /// <summary>
    /// 計算加入 Margin 後的最終 Coupon（完整版本）
    /// </summary>
    public double CalculateCouponWithMarginDetailed(
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
}
