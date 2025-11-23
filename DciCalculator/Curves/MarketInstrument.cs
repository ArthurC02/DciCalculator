namespace DciCalculator.Curves;

/// <summary>
/// 市場工具類型
/// </summary>
public enum MarketInstrumentType
{
    /// <summary>
    /// 存款 (Deposit)
    /// </summary>
    Deposit,

    /// <summary>
    /// 遠期利率協議 (Forward Rate Agreement)
    /// </summary>
    FRA,

    /// <summary>
    /// 利率交換 (Interest Rate Swap)
    /// </summary>
    Swap,

    /// <summary>
    /// 期貨 (Futures)
    /// </summary>
    Futures
}

/// <summary>
/// 市場工具基底類別，供曲線展期 (Bootstrap) 使用。
/// 成員：Maturity 到期日；Quote 市場報價；PV() 計算現值。
/// </summary>
public abstract class MarketInstrument
{
    /// <summary>
    /// 工具類型
    /// </summary>
    public MarketInstrumentType InstrumentType { get; }

    /// <summary>
    /// 起始日
    /// </summary>
    public DateTime StartDate { get; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime MaturityDate { get; }

    /// <summary>
    /// 市場報價 (年化利率或其他值)
    /// </summary>
    public double MarketQuote { get; }

    /// <summary>
    /// 期限 (年)
    /// </summary>
    public double Tenor => (MaturityDate - StartDate).Days / 365.0;

    protected MarketInstrument(
        MarketInstrumentType instrumentType,
        DateTime startDate,
        DateTime maturityDate,
        double marketQuote)
    {
        if (maturityDate <= startDate)
            throw new ArgumentException("到期日必須晚於起始日");

        InstrumentType = instrumentType;
        StartDate = startDate;
        MaturityDate = maturityDate;
        MarketQuote = marketQuote;
    }

    /// <summary>
    /// 計算工具現值 (Present Value)
    /// </summary>
    /// <param name="curve">零利率曲線</param>
    /// <returns>現值</returns>
    public abstract double CalculatePresentValue(IZeroCurve curve);

    /// <summary>
    /// 計算現值對指定 Tenor 零利率敏感度 (Jacobian)，用於展期迭代。
    /// </summary>
    /// <param name="curve">零利率曲線</param>
    /// <param name="pillarTenor">節點 Tenor</param>
    /// <returns>dPV/dr</returns>
    public virtual double CalculateJacobian(IZeroCurve curve, double pillarTenor)
    {
        // 有限差分 (Finite Difference)
        const double h = 1e-6; // 1bp / 100

        double pv0 = CalculatePresentValue(curve);

        // 平坦曲線微擾近似 pillar 利率
        double r0 = curve.GetZeroRate(pillarTenor);
        var perturbedCurve = new FlatZeroCurve("Perturbed", curve.ReferenceDate, r0 + h);

        double pv1 = CalculatePresentValue(perturbedCurve);

        return (pv1 - pv0) / h;
    }

    public override string ToString()
    {
        return $"{InstrumentType} {Tenor:F4}Y @ {MarketQuote:P4}";
    }
}
