using DciCalculator.Models;
using System.Runtime.CompilerServices;

namespace DciCalculator.Algorithms;

/// <summary>
/// Garman-Kohlhagen FX 期權定價模型（歐式）
/// Black-Scholes 模型的 FX 版本，考慮本幣和外幣利率
/// 實現嚴謹的數值穩定性和效能優化
/// 精度：計算結果精確到小數點後第 4 位（適用於匯率）
/// </summary>
public static class GarmanKohlhagen
{
    // 數值穩定性常數（與 BlackScholes 一致）
    private const double MinVolatility = 1e-6;
    private const double MaxVolatility = 5.0;
    private const double MinTimeToMaturity = 1e-6;
    private const double MaxTimeToMaturity = 100.0;
    private const double MinRate = -0.20;
    private const double MaxRate = 0.50;
    private const double DeepITMThreshold = 20.0;

    /// <summary>
    /// Garman-Kohlhagen FX 期權定價（歐式）
    /// 
    /// 公式：
    /// Call = S * e^(-r_f*T) * N(d1) - K * e^(-r_d*T) * N(d2)
    /// Put = K * e^(-r_d*T) * N(-d2) - S * e^(-r_f*T) * N(-d1)
    /// 
    /// 其中：
    /// d1 = [ln(S/K) + (r_d - r_f + σ²/2)T] / (σ√T)
    /// d2 = d1 - σ√T
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
        // === 嚴格參數驗證 ===
        ValidateParameters(spot, strike, rDomestic, rForeign, volatility, timeToMaturity);

        // === 處理邊界情況 ===

        // 即將到期
        if (timeToMaturity < MinTimeToMaturity)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // 波動度極低
        if (volatility < MinVolatility)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // === 核心計算 ===
        return CalculatePriceCore(spot, strike, rDomestic, rForeign, volatility, timeToMaturity, optionType);
    }

    /// <summary>
    /// 核心定價計算（已優化）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculatePriceCore(
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
        double nd1 = MathFx.NormalCdf(d1);
        double nd2 = MathFx.NormalCdf(d2);

        // 根據期權類型計算價格（優化版本，利用 1-N(x) = N(-x)）
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
    private static bool HandleDeepOptions(
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
            throw new ArgumentOutOfRangeException(nameof(spot), spot,
                "即期匯率必須大於 0");

        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), strike,
                "履約價必須大於 0");

        if (double.IsNaN(spot) || double.IsInfinity(spot))
            throw new ArgumentOutOfRangeException(nameof(spot), spot,
                "即期匯率必須為有效數值");

        if (double.IsNaN(strike) || double.IsInfinity(strike))
            throw new ArgumentOutOfRangeException(nameof(strike), strike,
                "履約價必須為有效數值");

        if (volatility <= 0 || volatility > MaxVolatility)
            throw new ArgumentOutOfRangeException(nameof(volatility), volatility,
                $"波動度必須在 (0, {MaxVolatility}] 範圍內");

        if (timeToMaturity <= 0 || timeToMaturity > MaxTimeToMaturity)
            throw new ArgumentOutOfRangeException(nameof(timeToMaturity), timeToMaturity,
                $"到期時間必須在 (0, {MaxTimeToMaturity}] 範圍內");

        if (rDomestic < MinRate || rDomestic > MaxRate)
            throw new ArgumentOutOfRangeException(nameof(rDomestic), rDomestic,
                $"本幣利率必須在 [{MinRate}, {MaxRate}] 範圍內");

        if (rForeign < MinRate || rForeign > MaxRate)
            throw new ArgumentOutOfRangeException(nameof(rForeign), rForeign,
                $"外幣利率必須在 [{MinRate}, {MaxRate}] 範圍內");
    }

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
        const double tolerance = 1e-4;
        const int maxIterations = 100;
        const double minVol = 0.001;
        const double maxVol = 3.0;

        if (marketPrice <= 0)
            return double.NaN;

        double vol = Math.Clamp(initialGuess, minVol, maxVol);

        for (int i = 0; i < maxIterations; i++)
        {
            double price = PriceFxOption(spot, strike, rDomestic, rForeign, vol, timeToMaturity, optionType);
            double diff = price - marketPrice;

            if (Math.Abs(diff) < tolerance)
                return vol;

            // 計算 Vega
            double vega = CalculateVega(spot, strike, rDomestic, rForeign, vol, timeToMaturity);

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

    /// <summary>
    /// 計算 Vega（FX 期權版本）
    /// Vega = S * e^(-r_f*T) * N'(d1) * √T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateVega(
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

        double npd1 = MathFx.NormalPdf(d1);
        double dfForeign = Math.Exp(-rForeign * timeToMaturity);

        return spot * dfForeign * npd1 * sqrtT;
    }
}

