using DciCalculator.Models;
using System.Runtime.CompilerServices;

namespace DciCalculator.Algorithms;

/// <summary>
/// Black-Scholes 歐式期權定價模型（股票/指數）
/// 實現嚴謹的數值穩定性和效能優化
/// 精度：計算結果精確到小數點後第 4 位（適用於匯率）
/// </summary>
public static class BlackScholes
{
    // 數值穩定性常數
    private const double MinVolatility = 1e-6;        // 最小波動度（避免除以零）
    private const double MaxVolatility = 5.0;         // 最大波動度（避免極端值）
    private const double MinTimeToMaturity = 1e-6;    // 最小到期時間（約 30 秒）
    private const double MaxTimeToMaturity = 100.0;   // 最大到期時間（100 年）
    private const double MinRate = -0.20;             // 最小利率（-20%）
    private const double MaxRate = 0.50;              // 最大利率（50%）
    private const double DeepITMThreshold = 20.0;     // Deep ITM/OTM 閾值（d > 20）

    /// <summary>
    /// 標準 Black-Scholes 歐式期權定價（股票/指數）
    /// 
    /// 公式：
    /// Call = S * N(d1) - K * e^(-rT) * N(d2)
    /// Put = K * e^(-rT) * N(-d2) - S * N(-d1)
    /// 
    /// 其中：
    /// d1 = [ln(S/K) + (r + σ²/2)T] / (σ√T)
    /// d2 = d1 - σ√T
    /// </summary>
    /// <param name="spot">現價（必須 > 0）</param>
    /// <param name="strike">履約價（必須 > 0）</param>
    /// <param name="rate">無風險利率（年化，例如 0.05 代表 5%）</param>
    /// <param name="volatility">年化波動度（必須 > 0，建議範圍 0.01 ~ 2.0）</param>
    /// <param name="timeToMaturity">到期時間（年，必須 > 0）</param>
    /// <param name="optionType">期權類型（Call 或 Put）</param>
    /// <returns>期權理論價格</returns>
    /// <exception cref="ArgumentOutOfRangeException">參數超出有效範圍</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Price(
        double spot,
        double strike,
        double rate,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        // === 嚴格參數驗證 ===
        ValidateParameters(spot, strike, rate, volatility, timeToMaturity);

        // === 處理邊界情況 ===

        // 即將到期（時間價值趨近於零）
        if (timeToMaturity < MinTimeToMaturity)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // 波動度極低（近似無波動）
        if (volatility < MinVolatility)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        // === 核心計算（優化版本）===
        return CalculatePriceCore(spot, strike, rate, volatility, timeToMaturity, optionType);
    }

    /// <summary>
    /// 核心定價計算（已優化：消除重複計算、快取中間結果）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculatePriceCore(
        double spot,
        double strike,
        double rate,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        // 預先計算常用項（避免重複計算）
        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;
        double volSquaredHalf = 0.5 * volatility * volatility;

        // 計算 ln(S/K)（單次計算）
        double lnMoneyness = Math.Log(spot / strike);

        // 計算 d1 和 d2
        double d1 = (lnMoneyness + (rate + volSquaredHalf) * timeToMaturity) / volSqrtT;
        double d2 = d1 - volSqrtT;

        // 數值穩定性檢查：處理 deep ITM/OTM
        if (HandleDeepOptions(spot, strike, rate, timeToMaturity, d1, d2, optionType, out double earlyPrice))
        {
            return earlyPrice;
        }

        // 折現因子（單次計算）
        double discountFactor = Math.Exp(-rate * timeToMaturity);

        // 計算 CDF 值
        double nd1 = MathFx.NormalCdf(d1);
        double nd2 = MathFx.NormalCdf(d2);

        // 根據期權類型計算價格
        return optionType switch
        {
            OptionType.Call => spot * nd1 - strike * discountFactor * nd2,
            OptionType.Put => strike * discountFactor * (1.0 - nd2) - spot * (1.0 - nd1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
    }

    /// <summary>
    /// 處理 Deep ITM/OTM 期權（數值穩定性優化）
    /// 當 |d1| 或 |d2| > 20 時，N(d) 約等於 0 或 1，直接使用內含價值
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HandleDeepOptions(
        double spot,
        double strike,
        double rate,
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
            // Deep OTM：價值趨近於零
            price = 0.0;
            return true;
        }

        if (isDeepITM)
        {
            // Deep ITM：價值約等於 內含價值折現
            double discountFactor = Math.Exp(-rate * timeToMaturity);
            price = optionType switch
            {
                OptionType.Call => spot - strike * discountFactor,
                OptionType.Put => strike * discountFactor - spot,
                _ => 0.0
            };

            // 確保非負
            price = Math.Max(0.0, price);
            return true;
        }

        price = 0.0;
        return false;
    }

    /// <summary>
    /// 計算內含價值（Intrinsic Value）
    /// IV = max(0, S - K) for Call
    /// IV = max(0, K - S) for Put
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
        double rate,
        double volatility,
        double timeToMaturity)
    {
        if (spot <= 0)
            throw new ArgumentOutOfRangeException(nameof(spot), spot,
                "現價必須大於 0");

        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), strike,
                "履約價必須大於 0");

        if (double.IsNaN(spot) || double.IsInfinity(spot))
            throw new ArgumentOutOfRangeException(nameof(spot), spot,
                "現價必須為有效數值");

        if (double.IsNaN(strike) || double.IsInfinity(strike))
            throw new ArgumentOutOfRangeException(nameof(strike), strike,
                "履約價必須為有效數值");

        if (volatility <= 0 || volatility > MaxVolatility)
            throw new ArgumentOutOfRangeException(nameof(volatility), volatility,
                $"波動度必須在 (0, {MaxVolatility}] 範圍內");

        if (timeToMaturity <= 0 || timeToMaturity > MaxTimeToMaturity)
            throw new ArgumentOutOfRangeException(nameof(timeToMaturity), timeToMaturity,
                $"到期時間必須在 (0, {MaxTimeToMaturity}] 範圍內");

        if (rate < MinRate || rate > MaxRate)
            throw new ArgumentOutOfRangeException(nameof(rate), rate,
                $"利率必須在 [{MinRate}, {MaxRate}] 範圍內");
    }

    /// <summary>
    /// 計算隱含波動度（Implied Volatility）使用 Newton-Raphson 方法
    /// 效能優化版本：最多迭代 100 次，精度 0.0001
    /// </summary>
    /// <param name="marketPrice">市場價格</param>
    /// <param name="spot">現價</param>
    /// <param name="strike">履約價</param>
    /// <param name="rate">無風險利率</param>
    /// <param name="timeToMaturity">到期時間</param>
    /// <param name="optionType">期權類型</param>
    /// <param name="initialGuess">初始波動度猜測（預設 0.3）</param>
    /// <returns>隱含波動度，若無法收斂則返回 NaN</returns>
    public static double ImpliedVolatility(
        double marketPrice,
        double spot,
        double strike,
        double rate,
        double timeToMaturity,
        OptionType optionType,
        double initialGuess = 0.3)
    {
        const double tolerance = 1e-4;  // 精度：0.01% (0.0001)
        const int maxIterations = 100;
        const double minVol = 0.001;    // 最小波動度 0.1%
        const double maxVol = 3.0;      // 最大波動度 300%

        if (marketPrice <= 0)
            return double.NaN;

        double vol = Math.Clamp(initialGuess, minVol, maxVol);

        for (int i = 0; i < maxIterations; i++)
        {
            double price = Price(spot, strike, rate, vol, timeToMaturity, optionType);
            double diff = price - marketPrice;

            // 收斂檢查
            if (Math.Abs(diff) < tolerance)
                return vol;

            // 計算 Vega (dPrice/dVol)
            double vega = CalculateVega(spot, strike, rate, vol, timeToMaturity);

            // 避免 Vega 過小導致數值不穩定
            if (Math.Abs(vega) < 1e-10)
                break;

            // Newton-Raphson 更新
            double volNew = vol - diff / vega;

            // 限制更新幅度（防止震盪)
            double maxChange = 0.5 * vol;
            volNew = Math.Clamp(volNew, vol - maxChange, vol + maxChange);
            volNew = Math.Clamp(volNew, minVol, maxVol);

            // 檢查是否收斂
            if (Math.Abs(volNew - vol) < tolerance)
                return volNew;

            vol = volNew;
        }

        // 未收斂
        return double.NaN;
    }

    /// <summary>
    /// 計算 Vega（用於隱含波動度計算）
    /// Vega = S * N'(d1) * √T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateVega(
        double spot,
        double strike,
        double rate,
        double volatility,
        double timeToMaturity)
    {
        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;
        double volSquaredHalf = 0.5 * volatility * volatility;

        double lnMoneyness = Math.Log(spot / strike);
        double d1 = (lnMoneyness + (rate + volSquaredHalf) * timeToMaturity) / volSqrtT;

        double npd1 = MathFx.NormalPdf(d1);

        return spot * npd1 * sqrtT;
    }
}

