using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// Business/252 日數計算器
/// 工作天除以 252（年度假設工作天數）
/// 常見用途：巴西等市場衍生品
/// </summary>
public sealed class Bus252Calculator : IDayCountCalculator
{
    /// <inheritdoc/>
    public string ConventionName => "Business/252";

    /// <inheritdoc/>
    public double YearFraction(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期", nameof(endDate));

        // 計算工作天數（排除週六日）
        int businessDays = 0;
        DateTime current = startDate;

        while (current < endDate)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && 
                current.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
            current = current.AddDays(1);
        }

        return businessDays / 252.0;
    }
}
