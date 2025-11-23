using DciCalculator.Curves;
using DciCalculator.Models;
using DciCalculator.VolSurfaces;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// 定價模型介面，支援期權定價計算
/// </summary>
public interface IPricingModel
{
    /// <summary>
    /// 計算 FX 期權價格（基本參數版本）
    /// </summary>
    /// <param name="spot">現貨價格</param>
    /// <param name="strike">行使價</param>
    /// <param name="domesticRate">本幣利率（連續複利）</param>
    /// <param name="foreignRate">外幣利率（連續複利）</param>
    /// <param name="volatility">波動率</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型（Call/Put）</param>
    /// <returns>期權價格（本幣計價）</returns>
    double PriceFxOption(
        double spot,
        double strike,
        double domesticRate,
        double foreignRate,
        double volatility,
        double timeToMaturity,
        OptionType optionType);

    /// <summary>
    /// 使用曲線與波動率曲面計算期權價格
    /// </summary>
    /// <param name="spot">現貨價格</param>
    /// <param name="strike">行使價</param>
    /// <param name="domesticCurve">本幣零利率曲線</param>
    /// <param name="foreignCurve">外幣零利率曲線</param>
    /// <param name="volSurface">波動率曲面</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型（Call/Put）</param>
    /// <returns>期權價格（本幣計價）</returns>
    double PriceWithCurves(
        double spot,
        double strike,
        IZeroCurve domesticCurve,
        IZeroCurve foreignCurve,
        IVolSurface volSurface,
        double timeToMaturity,
        OptionType optionType);

    /// <summary>
    /// 使用折現因子計算期權價格
    /// </summary>
    /// <param name="spot">現貨價格</param>
    /// <param name="strike">行使價</param>
    /// <param name="domesticDiscountFactor">本幣折現因子</param>
    /// <param name="foreignDiscountFactor">外幣折現因子</param>
    /// <param name="volatility">波動率</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型（Call/Put）</param>
    /// <returns>期權價格（本幣計價）</returns>
    double PriceWithDiscountFactors(
        double spot,
        double strike,
        double domesticDiscountFactor,
        double foreignDiscountFactor,
        double volatility,
        double timeToMaturity,
        OptionType optionType);
}
