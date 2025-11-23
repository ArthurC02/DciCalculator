using DciCalculator.Models;

namespace DciCalculator.Curves;

/// <summary>
/// 利率交換工具（Interest Rate Swap, IRS）
/// 
/// 定義：
/// - Fixed Leg：固定利率支付（每期支付固定利率）
/// - Float Leg：浮動利率支付（假設與 Zero Rate 一致）
/// 
/// 現值公式：
/// PV_Fixed = Notional * Fixed_Rate * Σ[DF(Ti) * τi]
/// PV_Float = Notional * [DF(T0) - DF(Tn)]
/// 
/// Bootstrap：
/// PV_Fixed = PV_Float (Par Swap)
/// 求解 DF(Tn)
/// </summary>
public sealed class SwapInstrument : MarketInstrument
{
    /// <summary>
    /// 付款頻率（每年幾次）
    /// </summary>
    public int PaymentFrequency { get; }

    /// <summary>
    /// Day Count Convention
    /// </summary>
    public DayCountConvention DayCount { get; }

    /// <summary>
    /// 本金
    /// </summary>
    public double Notional { get; }

    /// <summary>
    /// 建立 IRS 工具
    /// </summary>
    /// <param name="startDate">起始日</param>
    /// <param name="maturityDate">到期日</param>
    /// <param name="swapRate">Swap Fixed Rate（年化）</param>
    /// <param name="paymentFrequency">付款頻率（2=半年付，4=季付）</param>
    /// <param name="dayCount">Day Count Convention</param>
    /// <param name="notional">本金</param>
    public SwapInstrument(
        DateTime startDate,
        DateTime maturityDate,
        double swapRate,
        int paymentFrequency = 2,
        DayCountConvention dayCount = DayCountConvention.Act365,
        double notional = 1.0)
        : base(MarketInstrumentType.Swap, startDate, maturityDate, swapRate)
    {
        if (paymentFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentFrequency));

        PaymentFrequency = paymentFrequency;
        DayCount = dayCount;
        Notional = notional;
    }

    /// <summary>
    /// 計算 Swap 現值
    /// 
    /// PV = PV_Fixed - PV_Float
    /// 
    /// Par Swap: PV = 0
    /// </summary>
    public override double CalculatePresentValue(IZeroCurve curve)
    {
        // 計算 Fixed Leg
        double pvFixed = CalculateFixedLegPV(curve);

        // 計算 Float Leg（簡化：假設與 Zero Rate 一致）
        double pvFloat = CalculateFloatLegPV(curve);

        // Par Swap: PV_Fixed = PV_Float
        return pvFixed - pvFloat;
    }

    /// <summary>
    /// 計算 Fixed Leg 現值
    /// 
    /// PV_Fixed = Notional * Fixed_Rate * Σ[DF(Ti) * τi]
    /// </summary>
    private double CalculateFixedLegPV(IZeroCurve curve)
    {
        double pvFixed = 0.0;

        foreach (var (paymentDate, yearFraction) in GeneratePaymentSchedule())
        {
            double discountFactor = curve.GetDiscountFactor(paymentDate);
            double payment = Notional * MarketQuote * yearFraction;

            pvFixed += payment * discountFactor;
        }

        return pvFixed;
    }

    /// <summary>
    /// 計算 Float Leg 現值（簡化）
    /// 
    /// PV_Float = Notional * [DF(T0) - DF(Tn)]
    /// </summary>
    private double CalculateFloatLegPV(IZeroCurve curve)
    {
        double df0 = curve.GetDiscountFactor(StartDate);
        double dfn = curve.GetDiscountFactor(MaturityDate);

        return Notional * (df0 - dfn);
    }

    /// <summary>
    /// 產生付款時間表
    /// </summary>
    private IEnumerable<(DateTime PaymentDate, double YearFraction)> GeneratePaymentSchedule()
    {
        int totalPeriods = (int)(Tenor * PaymentFrequency);
        double periodLength = 1.0 / PaymentFrequency;

        for (int i = 1; i <= totalPeriods; i++)
        {
            DateTime prevDate = StartDate.AddDays((i - 1) * 365.0 / PaymentFrequency);
            DateTime paymentDate = StartDate.AddDays(i * 365.0 / PaymentFrequency);

            // 調整至工作日
            paymentDate = DayCountCalculator.AdjustToBusinessDay(paymentDate);

            double yearFraction = DayCountCalculator.YearFraction(prevDate, paymentDate, DayCount);

            yield return (paymentDate, yearFraction);
        }
    }

    /// <summary>
    /// 計算 Annuity（年金因子）
    /// 
    /// Annuity = Σ[DF(Ti) * τi]
    /// </summary>
    public double CalculateAnnuity(IZeroCurve curve)
    {
        double annuity = 0.0;

        foreach (var (paymentDate, yearFraction) in GeneratePaymentSchedule())
        {
            double discountFactor = curve.GetDiscountFactor(paymentDate);
            annuity += discountFactor * yearFraction;
        }

        return annuity;
    }

    /// <summary>
    /// 從 Par Swap Rate 反推最後一期的 DF
    /// 
    /// 公式：
    /// DF(Tn) = [DF(T0) - Swap_Rate * Σ(DF(Ti) * τi)] / (1 + Swap_Rate * τn)
    /// </summary>
    public double CalculateImpliedDiscountFactor(IZeroCurve existingCurve)
    {
        // 計算已知期限的 Annuity（排除最後一期）
        var schedule = GeneratePaymentSchedule().ToList();
        double knownAnnuity = 0.0;

        for (int i = 0; i < schedule.Count - 1; i++)
        {
            var (paymentDate, yearFraction) = schedule[i];
            double df = existingCurve.GetDiscountFactor(paymentDate);
            knownAnnuity += df * yearFraction;
        }

        // 最後一期
        var (lastDate, lastFraction) = schedule[^1];
        double df0 = existingCurve.GetDiscountFactor(StartDate);

        // DF(Tn) = [DF(T0) - Swap_Rate * Known_Annuity] / (1 + Swap_Rate * τn)
        double dfn = (df0 - MarketQuote * knownAnnuity) / (1.0 + MarketQuote * lastFraction);

        return dfn;
    }

    /// <summary>
    /// 建立標準 Swap
    /// </summary>
    public static SwapInstrument Create(
        DateTime startDate,
        string tenor,
        double swapRate,
        int paymentFrequency = 2)
    {
        var (days, _) = DayCountCalculator.ParseTenor(tenor, startDate);
        DateTime maturityDate = DayCountCalculator.CalculateMaturityDate(startDate, days);

        return new SwapInstrument(startDate, maturityDate, swapRate, paymentFrequency);
    }

    public override string ToString()
    {
        return $"Swap {Tenor:F4}Y @ {MarketQuote:P4} ({PaymentFrequency}/yr, {DayCount})";
    }
}
