using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// Actual/Actual 日數計算器
/// 考慮閏年的精確日數計算
/// 常見用途：政府債券
/// </summary>
public sealed class ActActCalculator : IDayCountCalculator
{
    /// <inheritdoc/>
    public string ConventionName => "Actual/Actual";

    /// <inheritdoc/>
    public double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期", nameof(endDate));

        int actualDays = (endDate - startDate).Days;
        
        // 計算期間內的平均年天數（考慮閏年）
        int years = endDate.Year - startDate.Year;
        if (years == 0)
        {
            // 同一年內
            int daysInYear = DateTime.IsLeapYear(startDate.Year) ? 366 : 365;
            return actualDays / (double)daysInYear;
        }
        else
        {
            // 跨年計算
            double totalDays = 0;
            int totalYearDays = 0;

            for (int year = startDate.Year; year <= endDate.Year; year++)
            {
                DateTime yearStart = new DateTime(year, 1, 1);
                DateTime yearEnd = new DateTime(year, 12, 31);

                DateTime periodStart = year == startDate.Year ? startDate : yearStart;
                DateTime periodEnd = year == endDate.Year ? endDate : yearEnd.AddDays(1);

                if (periodEnd > periodStart)
                {
                    int daysInThisYear = (periodEnd - periodStart).Days;
                    int yearDays = DateTime.IsLeapYear(year) ? 366 : 365;

                    totalDays += daysInThisYear;
                    totalYearDays += yearDays;
                }
            }

            return totalDays / (totalYearDays / (double)(endDate.Year - startDate.Year + 1));
        }
    }
}
