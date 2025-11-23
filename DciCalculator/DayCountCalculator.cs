using System.Runtime.CompilerServices;

namespace DciCalculator;

/// <summary>
/// Day Count Convention（日期計算慣例）
/// 計算兩日期間的年化期間
/// </summary>
public enum DayCountConvention
{
    /// <summary>
    /// Actual/365：實際天數 / 365
    /// 常用於：GBP、部分 FX 市場
    /// </summary>
    Act365,

    /// <summary>
    /// Actual/360：實際天數 / 360
    /// 常用於：USD、EUR Money Market
    /// </summary>
    Act360,

    /// <summary>
    /// Actual/Actual：實際天數 / 實際年天數
    /// 考慮閏年
    /// 常用於：債券市場
    /// </summary>
    ActAct,

    /// <summary>
    /// 30/360：每月 30 天，每年 360 天
    /// 常用於：公司債、部分衍生品
    /// </summary>
    Thirty360,

    /// <summary>
    /// Business/252：工作日 / 252
    /// 常用於：某些亞洲市場
    /// </summary>
    Bus252
}

/// <summary>
/// Day Count 計算器
/// 精確計算兩日期間的年化期間
/// </summary>
public static class DayCountCalculator
{
    /// <summary>
    /// 計算年化期間（Year Fraction）
    /// </summary>
    /// <param name="startDate">起始日</param>
    /// <param name="endDate">結束日</param>
    /// <param name="convention">Day Count 慣例</param>
    /// <returns>年化期間</returns>
    public static double YearFraction(
        DateTime startDate,
        DateTime endDate,
        DayCountConvention convention = DayCountConvention.Act365)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日必須 >= 起始日");

        return convention switch
        {
            DayCountConvention.Act365 => CalculateAct365(startDate, endDate),
            DayCountConvention.Act360 => CalculateAct360(startDate, endDate),
            DayCountConvention.ActAct => CalculateActAct(startDate, endDate),
            DayCountConvention.Thirty360 => CalculateThirty360(startDate, endDate),
            DayCountConvention.Bus252 => CalculateBus252(startDate, endDate),
            _ => throw new ArgumentOutOfRangeException(nameof(convention))
        };
    }

    /// <summary>
    /// Actual/365：實際天數 / 365
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateAct365(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        return actualDays / 365.0;
    }

    /// <summary>
    /// Actual/360：實際天數 / 360
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateAct360(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        return actualDays / 360.0;
    }

    /// <summary>
    /// Actual/Actual：考慮閏年
    /// </summary>
    private static double CalculateActAct(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        
        // 簡化版：使用起始年的天數
        // 完整版需要逐年計算
        int daysInYear = DateTime.IsLeapYear(startDate.Year) ? 366 : 365;
        
        return actualDays / (double)daysInYear;
    }

    /// <summary>
    /// 30/360：每月 30 天
    /// </summary>
    private static double CalculateThirty360(DateTime startDate, DateTime endDate)
    {
        int d1 = Math.Min(startDate.Day, 30);
        int d2 = Math.Min(endDate.Day, 30);
        
        int m1 = startDate.Month;
        int m2 = endDate.Month;
        
        int y1 = startDate.Year;
        int y2 = endDate.Year;

        int days = 360 * (y2 - y1) + 30 * (m2 - m1) + (d2 - d1);
        
        return days / 360.0;
    }

    /// <summary>
    /// Business/252：工作日計算（簡化版，未考慮假日）
    /// </summary>
    private static double CalculateBus252(DateTime startDate, DateTime endDate)
    {
        int businessDays = CountBusinessDays(startDate, endDate);
        return businessDays / 252.0;
    }

    /// <summary>
    /// 計算工作日數（週一至週五，不含假日）
    /// 簡化版：僅排除週末
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountBusinessDays(DateTime startDate, DateTime endDate)
    {
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

        return businessDays;
    }

    /// <summary>
    /// 計算到期日（根據 Tenor）
    /// </summary>
    /// <param name="startDate">起始日</param>
    /// <param name="tenorInDays">期限（天數）</param>
    /// <param name="adjustForWeekends">是否調整至工作日</param>
    /// <returns>到期日</returns>
    public static DateTime CalculateMaturityDate(
        DateTime startDate,
        int tenorInDays,
        bool adjustForWeekends = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenorInDays);

        DateTime maturityDate = startDate.AddDays(tenorInDays);

        if (adjustForWeekends)
        {
            // Following Business Day Convention
            while (maturityDate.DayOfWeek == DayOfWeek.Saturday ||
                   maturityDate.DayOfWeek == DayOfWeek.Sunday)
            {
                maturityDate = maturityDate.AddDays(1);
            }
        }

        return maturityDate;
    }

    /// <summary>
    /// 解析 Tenor 字串（例如 "3M", "90D", "1Y"）
    /// </summary>
    /// <param name="tenorString">Tenor 字串</param>
    /// <param name="referenceDate">參考日期</param>
    /// <returns>(期限天數, 年化期間)</returns>
    public static (int Days, double YearFraction) ParseTenor(
        string tenorString,
        DateTime referenceDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenorString);

        tenorString = tenorString.Trim().ToUpperInvariant();

        if (tenorString.Length < 2)
            throw new ArgumentException($"無效的 Tenor 格式: {tenorString}");

        char unit = tenorString[^1];
        if (!int.TryParse(tenorString[..^1], out int value))
            throw new ArgumentException($"無法解析數字: {tenorString}");

        int days = unit switch
        {
            'D' => value,                           // Days
            'W' => value * 7,                       // Weeks
            'M' => ApproximateMonthsToDays(value),  // Months
            'Y' => value * 365,                     // Years
            _ => throw new ArgumentException($"未知的單位: {unit}")
        };

        DateTime maturityDate = CalculateMaturityDate(referenceDate, days);
        double yearFraction = YearFraction(referenceDate, maturityDate, DayCountConvention.Act365);

        return (days, yearFraction);
    }

    /// <summary>
    /// 近似月份轉天數（簡化版）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ApproximateMonthsToDays(int months)
    {
        // 簡化：每月 30 天
        return months * 30;
    }

    /// <summary>
    /// 檢查是否為工作日
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday &&
               date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// 調整至下一個工作日
    /// </summary>
    public static DateTime AdjustToBusinessDay(
        DateTime date,
        BusinessDayConvention convention = BusinessDayConvention.Following)
    {
        return convention switch
        {
            BusinessDayConvention.Following => AdjustFollowing(date),
            BusinessDayConvention.ModifiedFollowing => AdjustModifiedFollowing(date),
            BusinessDayConvention.Preceding => AdjustPreceding(date),
            _ => date
        };
    }

    private static DateTime AdjustFollowing(DateTime date)
    {
        while (!IsBusinessDay(date))
            date = date.AddDays(1);
        return date;
    }

    private static DateTime AdjustModifiedFollowing(DateTime date)
    {
        DateTime adjusted = AdjustFollowing(date);
        
        // 若調整後跨月，改用 Preceding
        if (adjusted.Month != date.Month)
            return AdjustPreceding(date);
        
        return adjusted;
    }

    private static DateTime AdjustPreceding(DateTime date)
    {
        while (!IsBusinessDay(date))
            date = date.AddDays(-1);
        return date;
    }
}

/// <summary>
/// Business Day Convention（工作日調整慣例）
/// </summary>
public enum BusinessDayConvention
{
    /// <summary>
    /// Following：調整至下一個工作日
    /// </summary>
    Following,

    /// <summary>
    /// Modified Following：調整至下一個工作日，但不跨月
    /// </summary>
    ModifiedFollowing,

    /// <summary>
    /// Preceding：調整至前一個工作日
    /// </summary>
    Preceding,

    /// <summary>
    /// 不調整
    /// </summary>
    None
}
