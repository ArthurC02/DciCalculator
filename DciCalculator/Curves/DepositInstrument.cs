using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 存款工具（Deposit Instrument）
/// 
/// 定義：
/// - 起始日投入本金
/// - 到期日收回本金 + 利息
/// - 利率：簡單利息（Simple Interest）
/// 
/// 現值公式：
/// PV = (1 + r_deposit * T) * DF(T)
/// 
/// Bootstrap：
/// DF(T) = 1 / (1 + r_deposit * T)
/// r_zero = -ln(DF) / T
/// </summary>
public sealed class DepositInstrument : MarketInstrument
{
    /// <summary>
    /// Day Count Convention
    /// </summary>
    public DayCountConvention DayCount { get; }

    /// <summary>
    /// 本金（名義金額）
    /// </summary>
    public double Notional { get; }

    /// <summary>
    /// 建立存款工具
    /// </summary>
    /// <param name="startDate">起始日</param>
    /// <param name="maturityDate">到期日</param>
    /// <param name="depositRate">存款利率（年化，簡單利息）</param>
    /// <param name="dayCount">Day Count Convention</param>
    /// <param name="notional">本金（預設 1.0）</param>
    public DepositInstrument(
        DateTime startDate,
        DateTime maturityDate,
        double depositRate,
        DayCountConvention dayCount = DayCountConvention.Act360,
        double notional = 1.0)
        : base(MarketInstrumentType.Deposit, startDate, maturityDate, depositRate)
    {
        DayCount = dayCount;
        Notional = notional;
    }

    /// <summary>
    /// 計算存款的現值
    /// 
    /// PV = Notional * (1 + r * T) * DF(T)
    /// 
    /// Bootstrap 時應該 PV = Notional（無套利）
    /// </summary>
    public override double CalculatePresentValue(IZeroCurve curve)
    {
        // 計算實際期間
        double yearFraction = DayCountCalculator.YearFraction(StartDate, MaturityDate, DayCount);

        // 到期金額（本金 + 利息）
        double maturityValue = Notional * (1.0 + MarketQuote * yearFraction);

        // 折現回現在
        double discountFactor = curve.GetDiscountFactor(MaturityDate);

        return maturityValue * discountFactor;
    }

    /// <summary>
    /// 直接計算 Zero Rate（無需迭代）
    /// 
    /// DF = 1 / (1 + r_deposit * T)
    /// r_zero = -ln(DF) / T
    /// </summary>
    public double CalculateZeroRate()
    {
        double yearFraction = DayCountCalculator.YearFraction(StartDate, MaturityDate, DayCount);

        // 折現因子
        double discountFactor = 1.0 / (1.0 + MarketQuote * yearFraction);

        // 零息利率（連續複利）
        double zeroRate = -Math.Log(discountFactor) / yearFraction;

        return zeroRate;
    }

    /// <summary>
    /// 計算折現因子
    /// </summary>
    public double CalculateDiscountFactor()
    {
        double yearFraction = DayCountCalculator.YearFraction(StartDate, MaturityDate, DayCount);
        return 1.0 / (1.0 + MarketQuote * yearFraction);
    }

    /// <summary>
    /// 建立標準 Deposit（從 Tenor 字串）
    /// </summary>
    public static DepositInstrument Create(
        DateTime startDate,
        string tenor,
        double depositRate,
        DayCountConvention dayCount = DayCountConvention.Act360)
    {
        var (days, _) = DayCountCalculator.ParseTenor(tenor, startDate);
        DateTime maturityDate = DayCountCalculator.CalculateMaturityDate(startDate, days);

        return new DepositInstrument(startDate, maturityDate, depositRate, dayCount);
    }

    public override string ToString()
    {
        return $"Deposit {Tenor:F4}Y @ {MarketQuote:P4} ({DayCount})";
    }
}
