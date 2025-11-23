using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// Actual/360 日數計算器
/// 實際天數除以 360 天
/// 常見用途：USD、EUR 貨幣市場
/// </summary>
public sealed class Act360Calculator : IDayCountCalculator
{
    /// <inheritdoc/>
    public string ConventionName => "Actual/360";

    /// <inheritdoc/>
    public double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期", nameof(endDate));

        int actualDays = (endDate - startDate).Days;
        return actualDays / 360.0;
    }
}
