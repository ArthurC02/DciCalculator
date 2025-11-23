using DciCalculator.Core.Interfaces;
using DciCalculator.Curves;
using DciCalculator.Models;
using DciCalculator.VolSurfaces;
using System.Runtime.CompilerServices;

namespace DciCalculator.PricingModels;

/// <summary>
/// Garman-Kohlhagen FX 期權定價模型（實例版本）
/// 實現 IPricingModel 介面，支援依賴注入
/// </summary>
public class GarmanKohlhagenModel : IPricingModel
{
    // 數值穩定性常數
    private const double MinVolatility = 1e-6;
    private const double MaxVolatility = 5.0;
    private const double MinTimeToMaturity = 1e-6;
    private const double MaxTimeToMaturity = 100.0;
    private const double MinRate = -0.20;
    private const double MaxRate = 0.50;
    private const double DeepITMThreshold = 20.0;

    /// <summary>
    /// 計算 FX 期權價格（基本參數版本）
    /// </summary>
    public double PriceFxOption(
        double spot,
        double strike,
        double domesticRate,
        double foreignRate,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        // 嚴格參數驗證
        ValidateParameters(spot, strike, domesticRate, foreignRate, volatility, timeToMaturity);

        // 處理邊界情況
        if (timeToMaturity < MinTimeToMaturity)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        if (volatility < MinVolatility)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // 核心計算
        return CalculatePriceCore(spot, strike, domesticRate, foreignRate, volatility, timeToMaturity, optionType);
    }

    /// <summary>
    /// 使用曲線與波動率曲面計算期權價格
    /// </summary>
    public double PriceWithCurves(
        double spot,
        double strike,
        IZeroCurve domesticCurve,
        IZeroCurve foreignCurve,
        IVolSurface volSurface,
        double timeToMaturity,
        OptionType optionType)
    {
        ArgumentNullException.ThrowIfNull(domesticCurve);
        ArgumentNullException.ThrowIfNull(foreignCurve);
        ArgumentNullException.ThrowIfNull(volSurface);

        // 從曲線取得利率
        double rDomestic = domesticCurve.GetZeroRate(timeToMaturity);
        double rForeign = foreignCurve.GetZeroRate(timeToMaturity);

        // 從曲面取得波動率
        double volatility = volSurface.GetVolatility(strike, timeToMaturity);

        // 使用基本定價函數
        return PriceFxOption(spot, strike, rDomestic, rForeign, volatility, timeToMaturity, optionType);
    }

    /// <summary>
    /// 使用折現因子計算期權價格
    /// </summary>
    public double PriceWithDiscountFactors(
        double spot,
        double strike,
        double domesticDiscountFactor,
        double foreignDiscountFactor,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        if (domesticDiscountFactor <= 0 || domesticDiscountFactor > 1.0)
            throw new ArgumentOutOfRangeException(nameof(domesticDiscountFactor));

        if (foreignDiscountFactor <= 0 || foreignDiscountFactor > 1.0)
            throw new ArgumentOutOfRangeException(nameof(foreignDiscountFactor));

        if (timeToMaturity < MinTimeToMaturity)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        if (volatility < MinVolatility)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // 計算 d1 和 d2
        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;
        double volSquaredHalf = 0.5 * volatility * volatility;
        
        // 從 DF 反推利率（用於 d1/d2 計算）
        double rDomestic = -Math.Log(domesticDiscountFactor) / timeToMaturity;
        double rForeign = -Math.Log(foreignDiscountFactor) / timeToMaturity;

        double lnMoneyness = Math.Log(spot / strike);
        double d1 = (lnMoneyness + (rDomestic - rForeign + volSquaredHalf) * timeToMaturity) / volSqrtT;
        double d2 = d1 - volSqrtT;

        // 直接使用 DF
        double nd1 = Algorithms.MathFx.NormalCdf(d1);
        double nd2 = Algorithms.MathFx.NormalCdf(d2);

        return optionType switch
        {
            OptionType.Call => spot * foreignDiscountFactor * nd1 - strike * domesticDiscountFactor * nd2,
            OptionType.Put => strike * domesticDiscountFactor * (1.0 - nd2) - spot * foreignDiscountFactor * (1.0 - nd1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
    }

    /// <summary>
    /// 計算隱含波動率
    /// </summary>
    public double ImpliedVolatility(
        double marketPrice,
        double spot,
        double strike,
        double domesticRate,
        double foreignRate,
        double timeToMaturity,
        OptionType optionType,
        double initialGuess = 0.15)
    {
        const double tolerance = 1e-4;
        const int maxIterations = 100;
        const double minVol = 0.001;
        const double maxVol = 3.0;

        if (marketPrice <= 0)
            return double.NaN;

        double vol = Math.Clamp(initialGuess, minVol, maxVol);

        for (int i = 0; i < maxIterations; i++)
        {
            double price = PriceFxOption(spot, strike, domesticRate, foreignRate, vol, timeToMaturity, optionType);
            double diff = price - marketPrice;

            if (Math.Abs(diff) < tolerance)
                return vol;

            // 計算 Vega
            double vega = CalculateVega(spot, strike, domesticRate, foreignRate, vol, timeToMaturity);

            if (Math.Abs(vega) < 1e-10)
                break;

            // Newton-Raphson 更新
            double volNew = vol - diff / vega;

            // 限制更新幅度
            double maxChange = 0.5 * vol;
            volNew = Math.Clamp(volNew, vol - maxChange, vol + maxChange);
            volNew = Math.Clamp(volNew, minVol, maxVol);

            if (Math.Abs(volNew - vol) < tolerance)
                return volNew;

            vol = volNew;
        }

        return double.NaN;
    }

    #region Private Helper Methods

    /// <summary>
    /// 核心定價計算（已優化）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculatePriceCore(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        // 預先計算常用項
        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;
        double volSquaredHalf = 0.5 * volatility * volatility;

        // 計算 ln(S/K)
        double lnMoneyness = Math.Log(spot / strike);

        // 計算 d1 和 d2
        double d1 = (lnMoneyness + (rDomestic - rForeign + volSquaredHalf) * timeToMaturity) / volSqrtT;
        double d2 = d1 - volSqrtT;

        // 數值穩定性檢查：處理 deep ITM/OTM
        if (HandleDeepOptions(spot, strike, rDomestic, rForeign, timeToMaturity, d1, d2, optionType, out double earlyPrice))
        {
            return earlyPrice;
        }

        // 折現因子（單次計算）
        double dfDomestic = Math.Exp(-rDomestic * timeToMaturity);
        double dfForeign = Math.Exp(-rForeign * timeToMaturity);

        // 計算 CDF 值
        double nd1 = Algorithms.MathFx.NormalCdf(d1);
        double nd2 = Algorithms.MathFx.NormalCdf(d2);

        // 根據期權類型計算價格
        return optionType switch
        {
            OptionType.Call => spot * dfForeign * nd1 - strike * dfDomestic * nd2,
            OptionType.Put => strike * dfDomestic * (1.0 - nd2) - spot * dfForeign * (1.0 - nd1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
    }

    /// <summary>
    /// 處理 Deep ITM/OTM 期權（數值穩定性優化）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HandleDeepOptions(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double timeToMaturity,
        double d1,
        double d2,
        OptionType optionType,
        out double price)
    {
        bool isDeepITM = (optionType == OptionType.Call && d1 > DeepITMThreshold) ||
                         (optionType == OptionType.Put && d1 < -DeepITMThreshold);

        bool isDeepOTM = (optionType == OptionType.Call && d1 < -DeepITMThreshold) ||
                         (optionType == OptionType.Put && d1 > DeepITMThreshold);

        if (isDeepOTM)
        {
            price = 0.0;
            return true;
        }

        if (isDeepITM)
        {
            double dfDomestic = Math.Exp(-rDomestic * timeToMaturity);
            double dfForeign = Math.Exp(-rForeign * timeToMaturity);

            price = optionType switch
            {
                OptionType.Call => spot * dfForeign - strike * dfDomestic,
                OptionType.Put => strike * dfDomestic - spot * dfForeign,
                _ => 0.0
            };

            price = Math.Max(0.0, price);
            return true;
        }

        price = 0.0;
        return false;
    }

    /// <summary>
    /// 計算內含價值
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateIntrinsicValue(double spot, double strike, OptionType optionType)
    {
        return optionType switch
        {
            OptionType.Call => Math.Max(0.0, spot - strike),
            OptionType.Put => Math.Max(0.0, strike - spot),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
    }

    /// <summary>
    /// 嚴格參數驗證
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateParameters(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double volatility,
        double timeToMaturity)
    {
        if (spot <= 0)
            throw new ArgumentOutOfRangeException(nameof(spot), spot, "即期匯率必須大於 0");

        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), strike, "履約價必須大於 0");

        if (double.IsNaN(spot) || double.IsInfinity(spot))
            throw new ArgumentOutOfRangeException(nameof(spot), spot, "即期匯率必須為有效數值");

        if (double.IsNaN(strike) || double.IsInfinity(strike))
            throw new ArgumentOutOfRangeException(nameof(strike), strike, "履約價必須為有效數值");

        if (volatility <= 0 || volatility > MaxVolatility)
            throw new ArgumentOutOfRangeException(nameof(volatility), volatility, $"波動率必須在 (0, {MaxVolatility}] 範圍內");

        if (timeToMaturity <= 0 || timeToMaturity > MaxTimeToMaturity)
            throw new ArgumentOutOfRangeException(nameof(timeToMaturity), timeToMaturity, $"到期時間必須在 (0, {MaxTimeToMaturity}] 範圍內");

        if (rDomestic < MinRate || rDomestic > MaxRate)
            throw new ArgumentOutOfRangeException(nameof(rDomestic), rDomestic, $"本幣利率必須在 [{MinRate}, {MaxRate}] 範圍內");

        if (rForeign < MinRate || rForeign > MaxRate)
            throw new ArgumentOutOfRangeException(nameof(rForeign), rForeign, $"外幣利率必須在 [{MinRate}, {MaxRate}] 範圍內");
    }

    /// <summary>
    /// 計算 Vega
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateVega(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
        double volatility,
        double timeToMaturity)
    {
        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;
        double volSquaredHalf = 0.5 * volatility * volatility;

        double lnMoneyness = Math.Log(spot / strike);
        double d1 = (lnMoneyness + (rDomestic - rForeign + volSquaredHalf) * timeToMaturity) / volSqrtT;

        double npd1 = Algorithms.MathFx.NormalPdf(d1);
        double dfForeign = Math.Exp(-rForeign * timeToMaturity);

        return spot * dfForeign * npd1 * sqrtT;
    }

    #endregion
}
