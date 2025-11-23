using DciCalculator.Models;
using DciCalculator.Curves;
using DciCalculator.VolSurfaces;
using DciCalculator.PricingModels;
using System.Runtime.CompilerServices;

namespace DciCalculator.Algorithms;

/// <summary>
/// Garman-Kohlhagen FX 期權定價模型（歐式）- 靜態包裝類別
/// 
/// [已棄用] 此靜態類別保留用於向後兼容。
/// 新代碼請使用 <see cref="GarmanKohlhagenModel"/> 以支援依賴注入和更好的測試性。
/// 
/// Black-Scholes 模型的 FX 版本，考慮本幣和外幣利率
/// 實現嚴謹的數值穩定性和效能優化
/// 精度：計算結果精確到小數點後第 4 位（適用於匯率）
/// 
/// v2.0 新增：支援利率曲線和波動度曲面
/// v2.1 重構：內部委託給 GarmanKohlhagenModel 實例
/// </summary>
[Obsolete("請使用 GarmanKohlhagenModel 類別以支援依賴注入。此靜態類別將在未來版本移除。", false)]
public static class GarmanKohlhagen
{
    // 內部使用的模型實例（單例模式用於靜態類別）
    private static readonly GarmanKohlhagenModel _model = new();

    #region Original API (向後相容 - 委託給新模型)

    /// <summary>
    /// Garman-Kohlhagen FX 期權定價（歐式）- 原始 API
    /// </summary>
    /// <param name="spot">即期匯率（本幣每 1 外幣，例如 TWD/USD = 30.5）</param>
    /// <param name="strike">履約價（本幣每 1 外幣）</param>
    /// <param name="rDomestic">本幣利率（年化，例如 0.015 代表 1.5%）</param>
    /// <param name="rForeign">外幣利率（年化，例如 0.05 代表 5%）</param>
    /// <param name="volatility">年化波動度（例如 0.15 代表 15%）</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型（Call 或 Put）</param>
    /// <returns>期權價格（本幣單位）</returns>
    /// <exception cref="ArgumentOutOfRangeException">參數超出有效範圍</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PriceFxOption(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        return _model.PriceFxOption(spot, strike, rDomestic, rForeign, volatility, timeToMaturity, optionType);
    }

    #endregion

    #region Advanced API with Curves and Surfaces (委託給新模型)

    /// <summary>
    /// Garman-Kohlhagen FX 期權定價（使用利率曲線和波動度曲面）
    /// 
    /// 優勢：
    /// - 精確的期限結構定價
    /// - 考慮 Volatility Smile/Skew
    /// - 更貼近實務市場
    /// </summary>
    /// <param name="spot">即期匯率</param>
    /// <param name="strike">履約價</param>
    /// <param name="domesticCurve">本幣利率曲線</param>
    /// <param name="foreignCurve">外幣利率曲線</param>
    /// <param name="volSurface">波動度曲面</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型</param>
    /// <returns>期權價格（本幣單位）</returns>
    public static double PriceWithCurves(
        double spot,
        double strike,
        IZeroCurve domesticCurve,
        IZeroCurve foreignCurve,
        IVolSurface volSurface,
        double timeToMaturity,
        OptionType optionType)
    {
        return _model.PriceWithCurves(spot, strike, domesticCurve, foreignCurve, volSurface, timeToMaturity, optionType);
    }

    /// <summary>
    /// 使用折現因子直接定價（最精確）
    /// </summary>
    public static double PriceWithDiscountFactors(
        double spot,
        double strike,
        double dfDomestic,
        double dfForeign,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        return _model.PriceWithDiscountFactors(spot, strike, dfDomestic, dfForeign, volatility, timeToMaturity, optionType);
    }

    #endregion

    #region Implied Volatility (委託給新模型)

    /// <summary>
    /// 計算隱含波動度（Implied Volatility）使用 Newton-Raphson 方法
    /// FX 期權專用版本
    /// </summary>
    /// <param name="marketPrice">市場價格（本幣）</param>
    /// <param name="spot">即期匯率</param>
    /// <param name="strike">履約價</param>
    /// <param name="rDomestic">本幣利率</param>
    /// <param name="rForeign">外幣利率</param>
    /// <param name="timeToMaturity">到期時間</param>
    /// <param name="optionType">期權類型</param>
    /// <param name="initialGuess">初始波動度猜測（預設 0.15）</param>
    /// <returns>隱含波動度，若無法收斂則返回 NaN</returns>
    public static double ImpliedVolatility(
        double marketPrice,
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double timeToMaturity,
        OptionType optionType,
        double initialGuess = 0.15)
    {
        return _model.ImpliedVolatility(marketPrice, spot, strike, rDomestic, rForeign, timeToMaturity, optionType, initialGuess);
    }

    #endregion
}

