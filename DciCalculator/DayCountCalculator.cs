using System.Runtime.CompilerServices;

namespace DciCalculator;

/// <summary>
/// Day Count Convention (日數計算慣例)
/// 用於將實際天數換算成年期比例 (Year Fraction)
/// </summary>
public enum DayCountConvention
{
    /// <summary>
    /// Actual/365 : 實際天數 / 365
    /// 常見用途：GBP、部分 FX 產品
    /// </summary>
    Act365,

    /// <summary>
    /// Actual/360 : 實際天數 / 360
    /// 常見用途：USD、EUR 貨幣市場
    /// </summary>
    Act360,

    /// <summary>
    /// Actual/Actual : 實際天數 / 該年天數 (考慮閏年)
    /// 常見用途：政府債券
    /// </summary>
    ActAct,

    /// <summary>
    /// 30/360 : 每月統一視為 30 天、每年 360 天
    /// 常見用途：公司債、部份結構票據
    /// </summary>
    Thirty360,

    /// <summary>
    /// Business/252 : 工作天 / 252 (年度假設工作天數)
    /// 常見用途：巴西等市場衍生品
    /// </summary>
    Bus252
}

/// <summary>
/// Day Count 計算工具
/// 提供年期比例、到期日、Tenor 解析與工作天相關運算
/// </summary>
public static class DayCountCalculator
{
    /// <summary>
    /// 計算年期比例 (Year Fraction)
    /// </summary>
    /// <param name="startDate">起始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <param name="convention">日數計算慣例</param>
    /// <returns>年期比例 (以年為單位)</returns>
    public static double YearFraction(
        DateTime startDate,
        DateTime endDate,
        DayCountConvention convention = DayCountConvention.Act365)
    {
        if (endDate < startDate)
            throw new ArgumentException("結束日期必須 >= 起始日期");

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
    /// Actual/365 計算邏輯
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateAct365(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        return actualDays / 365.0;
    }

    /// <summary>
    /// Actual/360 計算邏輯
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateAct360(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        return actualDays / 360.0;
    }

    /// <summary>
    /// Actual/Actual 計算 (考慮閏年)
    /// </summary>
    private static double CalculateActAct(DateTime startDate, DateTime endDate)
    {
        int actualDays = (endDate - startDate).Days;
        
        // 邏輯：使用起始年之天數 (Leap Year 366, 否則 365)
        // 若跨年亦可改進為加總各年度天數 (此處簡化)
        int daysInYear = DateTime.IsLeapYear(startDate.Year) ? 366 : 365;
        
        return actualDays / (double)daysInYear;
    }

    /// <summary>
    /// 30/360 計算 (每月固定 30 天)
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
    /// Business/252 計算 (僅計算工作天)
    /// </summary>
    private static double CalculateBus252(DateTime startDate, DateTime endDate)
    {
        int businessDays = CountBusinessDays(startDate, endDate);
        return businessDays / 252.0;
    }

    /// <summary>
    /// 計算工作天 (略過週六、週日，不含假日)
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
    /// 計算到期日 (依據天數 Tenor)
    /// </summary>
    /// <param name="startDate">起始日期</param>
    /// <param name="tenorInDays">天數 Tenor</param>
    /// <param name="adjustForWeekends">是否調整至工作天 (Following)</param>
    /// <returns>到期日期</returns>
    public static DateTime CalculateMaturityDate(
        DateTime startDate,
        int tenorInDays,
        bool adjustForWeekends = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenorInDays);

        DateTime maturityDate = startDate.AddDays(tenorInDays);

        if (adjustForWeekends)
        {
            // Following Business Day Convention：遇週末往後遞延
            while (maturityDate.DayOfWeek == DayOfWeek.Saturday ||
                   maturityDate.DayOfWeek == DayOfWeek.Sunday)
            {
                maturityDate = maturityDate.AddDays(1);
            }
        }

        return maturityDate;
    }

    /// <summary>
    /// 解析 Tenor 字串 (例如 "3M", "90D", "1Y")
    /// </summary>
    /// <param name="tenorString">Tenor 字串</param>
    /// <param name="referenceDate">參考日期 (起始)</param>
    /// <returns>(天數, 年期比例)</returns>
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
            throw new ArgumentException($"無法解析數值: {tenorString}");

        int days = unit switch
        {
            'D' => value,                           // Days
            'W' => value * 7,                       // Weeks
            'M' => ApproximateMonthsToDays(value),  // Months
            'Y' => value * 365,                     // Years
            _ => throw new ArgumentException($"未知單位: {unit}")
        };

        DateTime maturityDate = CalculateMaturityDate(referenceDate, days);
        double yearFraction = YearFraction(referenceDate, maturityDate, DayCountConvention.Act365);

        return (days, yearFraction);
    }

    /// <summary>
    /// 月份近似換算天數 (每月 30 天簡化)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ApproximateMonthsToDays(int months)
    {
        // 假設：每月 30 天
        return months * 30;
    }

    /// <summary>
    /// 是否為工作日 (非週六、週日)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday &&
               date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// 調整至下一個工作日 (Following)
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
        
        // 若跨月則改為使用 Preceding 規則
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
/// Business Day Convention (工作日調整規則)
/// </summary>
public enum BusinessDayConvention
{
    /// <summary>
    /// Following：遇週末/假日往後調整
    /// </summary>
    Following,

    /// <summary>
    /// Modified Following：若跨月則改向前 (Preceding)
    /// </summary>
    ModifiedFollowing,

    /// <summary>
    /// Preceding：遇週末往前調整
    /// </summary>
    Preceding,

    /// <summary>
    /// None：不調整
    /// </summary>
    None
}
