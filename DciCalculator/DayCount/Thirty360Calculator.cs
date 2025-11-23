using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// 30/360 日數計算器
/// 每月統一視為 30 天、每年 360 天
/// 常見用途：公司債、部份結構票據
/// </summary>
public sealed class Thirty360Calculator : IDayCountCalculator
{
    /// <inheritdoc/>
    public string ConventionName => "30/360";

    /// <inheritdoc/>
    public double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期", nameof(endDate));

        int y1 = startDate.Year;
        int m1 = startDate.Month;
        int d1 = startDate.Day;

        int y2 = endDate.Year;
        int m2 = endDate.Month;
        int d2 = endDate.Day;

        // 30/360 規則：
        // 如果 d1 = 31，則 d1 = 30
        // 如果 d2 = 31 且 d1 >= 30，則 d2 = 30
        if (d1 == 31)
            d1 = 30;

        if (d2 == 31 && d1 >= 30)
            d2 = 30;

        int days = 360 * (y2 - y1) + 30 * (m2 - m1) + (d2 - d1);
        return days / 360.0;
    }
}
