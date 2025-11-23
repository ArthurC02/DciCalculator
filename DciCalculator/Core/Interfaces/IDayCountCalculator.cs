namespace DciCalculator.Core.Interfaces;

/// <summary>
/// Day Count Calculator 策略介面
/// 提供不同日數計算慣例的統一介面
/// </summary>
public interface IDayCountCalculator
{
    /// <summary>
    /// 計算年期比例 (Year Fraction)
    /// </summary>
    /// <param name="startDate">起始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <returns>年期比例 (以年為單位)</returns>
    double YearFraction(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 日數計算慣例名稱（用於識別）
    /// </summary>
    string ConventionName { get; }
}
