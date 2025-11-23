using DciCalculator.Algorithms;
using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// 外匯選擇權 Greeks 計算器（採用 Garman-Kohlhagen 模型）
/// 提供 Delta、Gamma、Vega、Theta、Rho 等風險敏感度計算。
/// </summary>
public static class GreeksCalculator
{
    /// <summary>
    /// 計算單一 FX 選擇權全部 Greeks（使用 Garman-Kohlhagen）。
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

        // Delta: 價格對標的即期匯率微小變動的敏感度
        double delta = optionType switch
        {
            OptionType.Call => dfForeign * nd1,
            OptionType.Put => -dfForeign * MathFx.NormalCdf(-d1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Gamma: Delta 對即期匯率再度微小變動的二階敏感度
        double gamma = (dfForeign * npd1) / (spot * volSqrtT);

        // Vega: 價格對隱含波動率變動的敏感度（波動率上升 1% 時的近似價格改變）
        double vega = spot * dfForeign * npd1 * sqrtT / 100.0;

        // Theta: 價格對剩餘時間流逝的敏感度（每日時間價值耗損）
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
        // 換算為每日 Theta
        theta /= 365.0;

        // Rho (Domestic): 價格對本國利率變動的敏感度
        double rhoDomestic = optionType switch
        {
            OptionType.Call => -strike * timeToMaturity * dfDomestic * nd2 / 100.0,
            OptionType.Put => strike * timeToMaturity * dfDomestic * MathFx.NormalCdf(-d2) / 100.0,
            _ => throw new ArgumentOutOfRangeException(nameof(optionType))
        };

        // Rho (Foreign): 價格對外國利率變動的敏感度
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
    /// 計算 DCI 商品之 Greeks（考量賣出 Put 的部位方向）。
    /// </summary>
    public static GreeksResult CalculateDciGreeks(DciInput input)
    {
        double spotD = (double)input.SpotQuote.Mid;
        double strikeD = (double)input.Strike;

        // DCI 結構屬賣出 Put，Greeks 需調整為反向
        var putGreeks = CalculateGreeks(
            spot: spotD,
            strike: strikeD,
            rDomestic: input.RateDomestic,
            rForeign: input.RateForeign,
            volatility: input.Volatility,
            timeToMaturity: input.TenorInYears,
            optionType: OptionType.Put
        );

        // 賣出 Put 之 Greeks = -1 * 買入 Put 之 Greeks
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
