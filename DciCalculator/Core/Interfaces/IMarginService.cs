namespace DciCalculator.Core.Interfaces;

/// <summary>
/// 邊際服務介面
/// </summary>
public interface IMarginService
{
    /// <summary>
    /// 對 Strike 應用邊際調整（pips）
    /// </summary>
    /// <param name="strike">原始 Strike</param>
    /// <param name="marginPips">邊際 pips</param>
    /// <param name="pipSize">Pip 大小（預設 0.01）</param>
    /// <returns>調整後的 Strike</returns>
    decimal ApplyMarginToStrike(decimal strike, decimal marginPips, decimal pipSize = 0.01m);

    /// <summary>
    /// 對期權價格應用邊際調整（百分比）
    /// </summary>
    /// <param name="optionPrice">原始期權價格</param>
    /// <param name="marginPercent">邊際百分比（如 0.10 表示 10%）</param>
    /// <returns>調整後的價格</returns>
    decimal ApplyMarginToPrice(decimal optionPrice, double marginPercent);

    /// <summary>
    /// 根據理論 Coupon 與目標 Coupon 反推所需的邊際
    /// </summary>
    /// <param name="theoreticalCoupon">理論年化 Coupon</param>
    /// <param name="targetCoupon">目標年化 Coupon</param>
    /// <returns>所需的邊際百分比</returns>
    double SolveMarginForTargetCoupon(double theoreticalCoupon, double targetCoupon);

    /// <summary>
    /// 計算邊際調整後的 Coupon
    /// </summary>
    /// <param name="baseCoupon">基礎 Coupon</param>
    /// <param name="marginPercent">邊際百分比</param>
    /// <returns>調整後的 Coupon</returns>
    double CalculateCouponWithMargin(double baseCoupon, double marginPercent);

    /// <summary>
    /// 計算價差（Spread）
    /// </summary>
    /// <param name="bidPrice">買價</param>
    /// <param name="askPrice">賣價</param>
    /// <returns>價差</returns>
    decimal CalculateSpread(decimal bidPrice, decimal askPrice);

    /// <summary>
    /// 計算價差百分比
    /// </summary>
    /// <param name="bidPrice">買價</param>
    /// <param name="askPrice">賣價</param>
    /// <returns>價差百分比</returns>
    double CalculateSpreadPercent(decimal bidPrice, decimal askPrice);

    /// <summary>
    /// 由 Mid 價與總 Spread（pips）計算 Bid/Ask 報價
    /// </summary>
    /// <param name="mid">Mid 價</param>
    /// <param name="spreadPips">總 Spread（pips）</param>
    /// <param name="pipSize">每 pip 數值大小</param>
    /// <returns>(Bid, Ask) 雙向報價</returns>
    (decimal Bid, decimal Ask) ApplySpread(
        decimal mid,
        decimal spreadPips,
        decimal pipSize = 0.01m);

    /// <summary>
    /// 套用總成本（Margin + Spread 成本百分比）後的 Coupon
    /// </summary>
    /// <param name="theoreticalCoupon">理論 Coupon</param>
    /// <param name="marginPercent">Margin 百分比</param>
    /// <param name="spreadCostPercent">Spread 成本百分比</param>
    /// <returns>扣除成本後的 Coupon</returns>
    double ApplyTotalCost(
        double theoreticalCoupon,
        double marginPercent,
        double spreadCostPercent);

    /// <summary>
    /// 計算加入 Margin 後的最終 Coupon（完整版本）
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
    double CalculateCouponWithMarginDetailed(
        double theoreticalOptionPrice,
        double marginPercent,
        decimal notionalForeign,
        decimal spot,
        decimal depositInterest,
        double tenorInYears);
}
