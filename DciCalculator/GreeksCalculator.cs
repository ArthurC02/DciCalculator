using DciCalculator.Algorithms;
using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// 期權 Greeks 計算器（基於 Garman-Kohlhagen 模型）
/// 提供 Delta、Gamma、Vega、Theta、Rho 等風險指標
/// </summary>
public static class GreeksCalculator
{
    /// <summary>
    /// 計算 FX 期權的所有 Greeks
    /// </summary>
    public static GreeksResult CalculateGreeks(
        double spot,
        double strike,
        double rDomestic,
        double rForeign,
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
                    + ((rDomestic - rForeign + (0.5 * volatility * volatility)) * timeToMaturity))
                    / volSqrtT;

        double d2 = d1 - volSqrtT;

        double dfDomestic = Math.Exp(-rDomestic * timeToMaturity);
        double dfForeign = Math.Exp(-rForeign * timeToMaturity);

        double nd1 = MathFx.NormalCdf(d1);
        double nd2 = MathFx.NormalCdf(d2);
        double npd1 = MathFx.NormalPdf(d1);

        // Delta: 期權價格對即期匯率的敏感度
        double delta = optionType switch
        {
            OptionType.Call => dfForeign * nd1,
            OptionType.Put => -dfForeign * MathFx.NormalCdf(-d1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Gamma: Delta 對即期匯率的敏感度（二階導數）
        double gamma = (dfForeign * npd1) / (spot * volSqrtT);

        // Vega: 期權價格對波動度的敏感度（通常以 1% 波動度變化計）
        double vega = spot * dfForeign * npd1 * sqrtT / 100.0;

        // Theta: 期權價格對時間流逝的敏感度（每日）
        double theta = optionType switch
        {
            OptionType.Call => CalculateCallTheta(
                spot, strike, rDomestic, rForeign, volatility,
                timeToMaturity, dfDomestic, dfForeign, d1, d2, npd1, nd1, nd2),
            OptionType.Put => CalculatePutTheta(
                spot, strike, rDomestic, rForeign, volatility,
                timeToMaturity, dfDomestic, dfForeign, d1, d2, npd1, nd1, nd2),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };
        // 轉換為每日 Theta
        theta /= 365.0;

        // Rho (Domestic): 期權價格對本幣利率的敏感度
        double rhoDomestic = optionType switch
        {
            OptionType.Call => -strike * timeToMaturity * dfDomestic * nd2 / 100.0,
            OptionType.Put => strike * timeToMaturity * dfDomestic * MathFx.NormalCdf(-d2) / 100.0,
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Rho (Foreign): 期權價格對外幣利率的敏感度
        double rhoForeign = optionType switch
        {
            OptionType.Call => -spot * timeToMaturity * dfForeign * nd1 / 100.0,
            OptionType.Put => spot * timeToMaturity * dfForeign * MathFx.NormalCdf(-d1) / 100.0,
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
        double term2 = -rForeign * spot * dfForeign * MathFx.NormalCdf(-d1);
        double term3 = rDomestic * strike * dfDomestic * MathFx.NormalCdf(-d2);

        return term1 + term2 + term3;
    }

    /// <summary>
    /// 計算 DCI 產品的 Greeks（考慮賣出 Put 的風險）
    /// </summary>
    public static GreeksResult CalculateDciGreeks(DciInput input)
    {
        double spotD = (double)input.SpotQuote.Mid;
        double strikeD = (double)input.Strike;

        // DCI 通常是賣出 Put，所以 Greeks 需要取反號
        var putGreeks = CalculateGreeks(
            spot: spotD,
            strike: strikeD,
            rDomestic: input.RateDomestic,
            rForeign: input.RateForeign,
            volatility: input.Volatility,
            timeToMaturity: input.TenorInYears,
            optionType: OptionType.Put
        );

        // 賣出 Put 的 Greeks = -1 * 買入 Put 的 Greeks
        return new GreeksResult(
            Delta: -putGreeks.Delta,
            Gamma: -putGreeks.Gamma,
            Vega: -putGreeks.Vega,
            Theta: -putGreeks.Theta,
            RhoDomestic: -putGreeks.RhoDomestic,
            RhoForeign: -putGreeks.RhoForeign
        );
    }
}
