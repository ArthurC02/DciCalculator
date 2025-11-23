namespace DciCalculator.Curves;

/// <summary>
/// 市場工具類型
/// </summary>
public enum MarketInstrumentType
{
    /// <summary>
    /// 存款（Deposit）
    /// </summary>
    Deposit,

    /// <summary>
    /// 遠期利率協議（Forward Rate Agreement）
    /// </summary>
    FRA,

    /// <summary>
    /// 利率交換（Interest Rate Swap）
    /// </summary>
    Swap,

    /// <summary>
    /// 期貨（Futures）
    /// </summary>
    Futures
}

/// <summary>
/// 市場工具抽象類別
/// 用於 Curve Bootstrapping
/// 
/// 工具定義：
/// - Maturity：到期日
/// - Quote：市場報價
/// - PV()：計算現值（給定曲線）
/// </summary>
public abstract class MarketInstrument
{
    /// <summary>
    /// 工具類型
    /// </summary>
    public MarketInstrumentType InstrumentType { get; }

    /// <summary>
    /// 起始日期
    /// </summary>
    public DateTime StartDate { get; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateTime MaturityDate { get; }

    /// <summary>
    /// 市場報價（年化利率或價格）
    /// </summary>
    public double MarketQuote { get; }

    /// <summary>
    /// 期限（年）
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
    /// 計算工具的現值（Present Value）
    /// 給定零息曲線，計算工具理論價值
    /// </summary>
    /// <param name="curve">零息利率曲線</param>
    /// <returns>現值</returns>
    public abstract double CalculatePresentValue(IZeroCurve curve);

    /// <summary>
    /// 計算工具對零息利率的敏感度（Jacobian）
    /// 用於 Newton-Raphson 迭代
    /// </summary>
    /// <param name="curve">零息利率曲線</param>
    /// <param name="pillarTenor">節點期限</param>
    /// <returns>dPV/dr</returns>
    public virtual double CalculateJacobian(IZeroCurve curve, double pillarTenor)
    {
        // 數值導數（Finite Difference）
        const double h = 1e-6; // 1bp / 100

        double pv0 = CalculatePresentValue(curve);

        // 擾動曲線（簡化：假設 Flat Curve）
        // 實務應該擾動特定節點
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
