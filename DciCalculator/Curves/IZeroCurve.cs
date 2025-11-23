namespace DciCalculator.Curves;

/// <summary>
/// 零息利率曲線介面
/// Zero Rate Curve：描述不同期限的無風險利率
/// 
/// 用途：
/// - 精確折現不同期限的現金流
/// - 計算 Forward Rate
/// - DCI 定價中的利率期限結構
/// </summary>
public interface IZeroCurve
{
    /// <summary>
    /// 曲線名稱（例如 "USD", "TWD"）
    /// </summary>
    string CurveName { get; }

    /// <summary>
    /// 基準日期（曲線建構日期）
    /// </summary>
    DateTime ReferenceDate { get; }

    /// <summary>
    /// 取得指定期限的零息利率（連續複利）
    /// </summary>
    /// <param name="timeInYears">期限（年）</param>
    /// <returns>零息利率（年化，例如 0.015 = 1.5%）</returns>
    double GetZeroRate(double timeInYears);

    /// <summary>
    /// 取得指定期限的折現因子
    /// DF(T) = exp(-r(T) * T)
    /// </summary>
    /// <param name="timeInYears">期限（年）</param>
    /// <returns>折現因子</returns>
    double GetDiscountFactor(double timeInYears);

    /// <summary>
    /// 取得指定日期的零息利率
    /// </summary>
    /// <param name="date">目標日期</param>
    /// <returns>零息利率</returns>
    double GetZeroRate(DateTime date);

    /// <summary>
    /// 取得指定日期的折現因子
    /// </summary>
    /// <param name="date">目標日期</param>
    /// <returns>折現因子</returns>
    double GetDiscountFactor(DateTime date);

    /// <summary>
    /// 計算 Forward Rate
    /// Forward Rate from T1 to T2 = [r(T2)*T2 - r(T1)*T1] / (T2 - T1)
    /// </summary>
    /// <param name="startTime">起始時間（年）</param>
    /// <param name="endTime">結束時間（年）</param>
    /// <returns>Forward Rate（年化）</returns>
    double GetForwardRate(double startTime, double endTime);

    /// <summary>
    /// 取得曲線的有效範圍（最小和最大期限）
    /// </summary>
    /// <returns>(最小期限, 最大期限)</returns>
    (double MinTenor, double MaxTenor) GetValidRange();
}
