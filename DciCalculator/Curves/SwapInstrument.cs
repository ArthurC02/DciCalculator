namespace DciCalculator.Curves;

/// <summary>
/// 利率交換 (Interest Rate Swap, IRS)
/// 
/// 結構：
/// - Fixed Leg：支付固定利率 (各期名義本金 * 固定利率)
/// - Floating Leg：支付浮動利率 (依當期重新設定的零利率)
/// 
/// 現值公式：
/// PV_Fixed = Notional * Fixed_Rate * Σ[ DF(T_i) * Δt_i ]
/// PV_Float = Notional * ( DF(T_0) - DF(T_n) )
/// 
/// Bootstrap：平價(Par)條件下 PV_Fixed = PV_Float，
/// 以此反推末端折現因子 DF(T_n)。
/// </summary>
public sealed class SwapInstrument : MarketInstrument
{
    /// <summary>
    /// 支付頻率 (每年期數，例如 2=半年付、4=季付)
    /// </summary>
    public int PaymentFrequency { get; }

    /// <summary>
    /// Day Count Convention
    /// </summary>
    public DayCountConvention DayCount { get; }

    /// <summary>
    /// 名義本金
    /// </summary>
    public double Notional { get; }

    /// <summary>
    /// 建立 IRS 交換合約
    /// </summary>
    /// <param name="startDate">起始日</param>
    /// <param name="maturityDate">到期日</param>
    /// <param name="swapRate">固定交換利率 (Par 固定率)</param>
    /// <param name="paymentFrequency">支付頻率 (2=半年, 4=季度)</param>
    /// <param name="dayCount">日數計算法</param>
    /// <param name="notional">名義本金</param>
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
    /// PV = PV_Fixed - PV_Float；Par Swap 時 PV=0。
    /// </summary>
    public override double CalculatePresentValue(IZeroCurve curve)
    {
        // 計算固定腿現值
        double pvFixed = CalculateFixedLegPV(curve);

        // 計算浮動腿現值 (簡化：假設與零利率一致)
        double pvFloat = CalculateFloatLegPV(curve);

        // Par Swap: PV_Fixed = PV_Float
        return pvFixed - pvFloat;
    }

    /// <summary>
    /// 計算固定腿 PV
    /// PV_Fixed = Notional * Fixed_Rate * Σ[ DF(T_i) * Δt_i ]
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
    /// 計算浮動腿 PV (簡化公式)
    /// PV_Float = Notional * ( DF(T_0) - DF(T_n) )
    /// </summary>
    private double CalculateFloatLegPV(IZeroCurve curve)
    {
        double df0 = curve.GetDiscountFactor(StartDate);
        double dfn = curve.GetDiscountFactor(MaturityDate);

        return Notional * (df0 - dfn);
    }

    /// <summary>
    /// 產生支付期日與對應年化期間 (Δt_i)
    /// </summary>
    private IEnumerable<(DateTime PaymentDate, double YearFraction)> GeneratePaymentSchedule()
    {
        int totalPeriods = (int)(Tenor * PaymentFrequency);
        _ = 1.0 / PaymentFrequency;

        for (int i = 1; i <= totalPeriods; i++)
        {
            DateTime prevDate = StartDate.AddDays((i - 1) * 365.0 / PaymentFrequency);
            DateTime paymentDate = StartDate.AddDays(i * 365.0 / PaymentFrequency);

            // 調整至營業日
            paymentDate = DayCountCalculator.AdjustToBusinessDay(paymentDate);

            double yearFraction = DayCountCalculator.YearFraction(prevDate, paymentDate, DayCount);

            yield return (paymentDate, yearFraction);
        }
    }

    /// <summary>
    /// 計算年金因子 Annuity = Σ[ DF(T_i) * Δt_i ]
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
    /// 由平價交換利率推導期末折現因子 DF(T_n)
    /// 推導：DF(T_n) = [ DF(T_0) - Swap_Rate * Σ_{i=1}^{n-1}( DF(T_i) * Δt_i ) ] / ( 1 + Swap_Rate * Δt_n )
    /// </summary>
    public double CalculateImpliedDiscountFactor(IZeroCurve existingCurve)
    {
        // 計算已知期次的部分年金因子 (不含最後一期)
        var schedule = GeneratePaymentSchedule().ToList();
        double knownAnnuity = 0.0;

        for (int i = 0; i < schedule.Count - 1; i++)
        {
            var (paymentDate, yearFraction) = schedule[i];
            double df = existingCurve.GetDiscountFactor(paymentDate);
            knownAnnuity += df * yearFraction;
        }

        // 最後一期資訊
        var (_, lastFraction) = schedule[^1];
        double df0 = existingCurve.GetDiscountFactor(StartDate);

        // DF(T_n) = [ DF(T_0) - Swap_Rate * Known_Annuity ] / ( 1 + Swap_Rate * Δt_n )
        double dfn = (df0 - MarketQuote * knownAnnuity) / (1.0 + MarketQuote * lastFraction);

        return dfn;
    }

    /// <summary>
    /// 建立標準化 Swap 合約 (以 tenor 建期)
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
