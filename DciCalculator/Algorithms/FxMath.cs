using MathNet.Numerics;

namespace DciCalculator.Algorithms;

public static class MathFx
{
    /// <summary>
    /// 標準常態分布累積機率 N(x)
    /// Φ(x) = 0.5 * [1 + erf(x / sqrt(2))]
    /// </summary>
    public static double NormalCdf(double x)
    {
        return 0.5 * (1.0 + SpecialFunctions.Erf(x / Math.Sqrt(2.0)));
    }

    /// <summary>
    /// 標準常態分布機率密度函數 φ(x)
    /// φ(x) = (1 / sqrt(2π)) * exp(-x² / 2)
    /// 用於計算 Greeks（Delta, Vega 等）
    /// </summary>
    public static double NormalPdf(double x)
    {
        const double InvSqrt2Pi = 0.3989422804014327; // 1 / sqrt(2π)
        return InvSqrt2Pi * Math.Exp(-0.5 * x * x);
    }

    /// <summary>
    /// 計算折現因子 Discount Factor
    /// df = exp(-rate * time)
    /// </summary>
    public static double DiscountFactor(double rate, double timeInYears)
    {
        return Math.Exp(-rate * timeInYears);
    }

    /// <summary>
    /// 將 Forward Points (pips) 轉換為完整匯率
    /// 例如：Spot = 30.500, Forward Points = 50 pips (0.050)
    /// → Forward = 30.550
    /// </summary>
    public static decimal ApplyForwardPoints(decimal spot, decimal forwardPoints)
    {
        return spot + forwardPoints;
    }

    /// <summary>
    /// Pip 轉換：將 pips 轉換為匯率差異
    /// USD/TWD: 1 pip = 0.001 TWD
    /// 可根據貨幣對調整
    /// </summary>
    public static decimal PipsToRate(decimal pips, int decimalPlaces = 3)
    {
        return pips * (decimal)Math.Pow(10, -decimalPlaces);
    }

    /// <summary>
    /// 將匯率差異轉換為 Pips
    /// 例如：30.550 - 30.500 = 0.050 → 50 pips
    /// </summary>
    public static decimal RateToPips(decimal rateDifference, int decimalPlaces = 3)
    {
        return rateDifference * (decimal)Math.Pow(10, decimalPlaces);
    }

    /// <summary>
    /// 計算 Forward 匯率（根據利率平價理論）
    /// Forward = Spot * exp((rDomestic - rForeign) * T)
    /// </summary>
    public static decimal CalculateForward(
        decimal spot,
        double rDomestic,
        double rForeign,
        double timeInYears)
    {
        double spotD = (double)spot;
        double forward = spotD * Math.Exp((rDomestic - rForeign) * timeInYears);
        return (decimal)forward;
    }

    /// <summary>
    /// 計算 Forward Points（Forward - Spot）
    /// </summary>
    public static decimal CalculateForwardPoints(
        decimal spot,
        double rDomestic,
        double rForeign,
        double timeInYears)
    {
        decimal forward = CalculateForward(spot, rDomestic, rForeign, timeInYears);
        return forward - spot;
    }
}

