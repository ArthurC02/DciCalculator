using DciCalculator.Core.Interfaces;
using DciCalculator.Models;

namespace DciCalculator.Services.Pricing;

/// <summary>
/// Greeks 計算器服務（實例版本）
/// 實現 IGreeksCalculator 介面，支援依賴注入
/// </summary>
public class GreeksCalculatorService : IGreeksCalculator
{
    private readonly IPricingModel _pricingModel;

    /// <summary>
    /// 建構函數 - 注入定價模型
    /// </summary>
    public GreeksCalculatorService(IPricingModel pricingModel)
    {
        _pricingModel = pricingModel ?? throw new ArgumentNullException(nameof(pricingModel));
    }

    /// <summary>
    /// 計算 DCI 的 Greeks（針對賣出 Put 部位）
    /// </summary>
    public GreeksResult CalculateDciGreeks(DciInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // DCI 是賣出 Put，Greeks 需要翻轉符號
        var putGreeks = CalculateGreeks(
            spot: (double)input.SpotQuote.Mid,
            strike: (double)input.Strike,
            domesticRate: input.RateDomestic,
            foreignRate: input.RateForeign,
            volatility: input.Volatility,
            timeToMaturity: input.TenorInYears,
            optionType: OptionType.Put
        );

        // 賣出 Put：翻轉所有 Greeks 符號
        return new GreeksResult(
            Delta: -putGreeks.Delta,
            Gamma: -putGreeks.Gamma,
            Vega: -putGreeks.Vega,
            Theta: -putGreeks.Theta,
            RhoDomestic: -putGreeks.RhoDomestic,
            RhoForeign: -putGreeks.RhoForeign
        );
    }

    /// <summary>
    /// 計算單一 FX 期權的 Greeks
    /// </summary>
    public GreeksResult CalculateGreeks(
        double spot,
        double strike,
        double domesticRate,
        double foreignRate,
        double volatility,
        double timeToMaturity,
        OptionType optionType)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(volatility);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeToMaturity);

        double sqrtT = Math.Sqrt(timeToMaturity);
        double volSqrtT = volatility * sqrtT;

        double d1 = (Math.Log(spot / strike)
                    + ((domesticRate - foreignRate + (0.5 * volatility * volatility)) * timeToMaturity))
                    / volSqrtT;

        double d2 = d1 - volSqrtT;

        double dfDomestic = Math.Exp(-domesticRate * timeToMaturity);
        double dfForeign = Math.Exp(-foreignRate * timeToMaturity);

        double nd1 = Algorithms.MathFx.NormalCdf(d1);
        double nd2 = Algorithms.MathFx.NormalCdf(d2);
        double npd1 = Algorithms.MathFx.NormalPdf(d1);

        // Delta
        double delta = optionType switch
        {
            OptionType.Call => dfForeign * nd1,
            OptionType.Put => -dfForeign * Algorithms.MathFx.NormalCdf(-d1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Gamma
        double gamma = (dfForeign * npd1) / (spot * volSqrtT);

        // Vega (1% change)
        double vega = spot * dfForeign * npd1 * sqrtT / 100.0;

        // Theta
        double theta = optionType switch
        {
            OptionType.Call => CalculateCallTheta(
                spot, strike, domesticRate, foreignRate, volatility,
                timeToMaturity, dfDomestic, dfForeign, d1, d2, npd1, nd1, nd2),
            OptionType.Put => CalculatePutTheta(
                spot, strike, domesticRate, foreignRate, volatility,
                timeToMaturity, dfDomestic, dfForeign, d1, d2, npd1, nd1, nd2),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
        theta /= 365.0; // 每日 Theta

        // Rho (Domestic)
        double rhoDomestic = optionType switch
        {
            OptionType.Call => -strike * timeToMaturity * dfDomestic * nd2 / 100.0,
            OptionType.Put => strike * timeToMaturity * dfDomestic * Algorithms.MathFx.NormalCdf(-d2) / 100.0,
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Rho (Foreign)
        double rhoForeign = optionType switch
        {
            OptionType.Call => -spot * timeToMaturity * dfForeign * nd1 / 100.0,
            OptionType.Put => spot * timeToMaturity * dfForeign * Algorithms.MathFx.NormalCdf(-d1) / 100.0,
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        return new GreeksResult(
            Delta: delta,
            Gamma: gamma,
            Vega: vega,
            Theta: theta,
            RhoDomestic: rhoDomestic,
            RhoForeign: rhoForeign
        );
    }

    private static double CalculateCallTheta(
        double spot, double strike, double rDomestic, double rForeign,
        double volatility, double timeToMaturity,
        double dfDomestic, double dfForeign,
        double d1, double d2, double npd1, double nd1, double nd2)
    {
        double term1 = -(spot * dfForeign * npd1 * volatility) / (2.0 * Math.Sqrt(timeToMaturity));
        double term2 = rForeign * spot * dfForeign * nd1;
        double term3 = -rDomestic * strike * dfDomestic * nd2;
        return term1 + term2 + term3;
    }

    private static double CalculatePutTheta(
        double spot, double strike, double rDomestic, double rForeign,
        double volatility, double timeToMaturity,
        double dfDomestic, double dfForeign,
        double d1, double d2, double npd1, double nd1, double nd2)
    {
        double term1 = -(spot * dfForeign * npd1 * volatility) / (2.0 * Math.Sqrt(timeToMaturity));
        double term2 = -rForeign * spot * dfForeign * Algorithms.MathFx.NormalCdf(-d1);
        double term3 = rDomestic * strike * dfDomestic * Algorithms.MathFx.NormalCdf(-d2);
        return term1 + term2 + term3;
    }
}
