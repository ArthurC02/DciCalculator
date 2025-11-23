using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// Actual/365 日數計算器
/// 實際天數除以 365 天
/// 常見用途：GBP、部分 FX 產品
/// </summary>
public sealed class Act365Calculator : IDayCountCalculator
{
    /// <inheritdoc/>
    public string ConventionName => "Actual/365";

    /// <inheritdoc/>
    public double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期", nameof(endDate));

        int actualDays = (endDate - startDate).Days;
        return actualDays / 365.0;
    }
}
