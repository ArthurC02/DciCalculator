namespace DciCalculator.Curves;

/// <summary>
/// 零利率曲線介面 (Zero Rate Curve)
/// 描述各期限對應之年化零利率。
/// 
/// 功能：
/// - 取得零利率與折現因子
/// - 計算遠期利率 (Forward Rate)
/// - 提供定價/風控使用
/// </summary>
public interface IZeroCurve
{
    /// <summary>
    /// 曲線名稱 (例："USD")
    /// </summary>
    string CurveName { get; }

    /// <summary>
    /// 基準日 (資料參考日)
    /// </summary>
    DateTime ReferenceDate { get; }

    /// <summary>
    /// 取得指定年期零利率
    /// </summary>
    /// <param name="timeInYears">年期 (年)</param>
    /// <returns>零利率 (e.g. 0.015 = 1.5%)</returns>
    double GetZeroRate(double timeInYears);

    /// <summary>
    /// 取得指定年期折現因子 DF(T) = exp(-r(T)*T)
    /// </summary>
    /// <param name="timeInYears">年期</param>
    /// <returns>折現因子</returns>
    double GetDiscountFactor(double timeInYears);

    /// <summary>
    /// 取得指定日期零利率
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>零利率</returns>
    double GetZeroRate(DateTime date);

    /// <summary>
    /// 取得指定日期折現因子
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>折現因子</returns>
    double GetDiscountFactor(DateTime date);

    /// <summary>
    /// 計算遠期利率 f(T1,T2) = [r(T2)*T2 - r(T1)*T1] / (T2 - T1)
    /// </summary>
    /// <param name="startTime">起始年期</param>
    /// <param name="endTime">結束年期</param>
    /// <returns>遠期利率</returns>
    double GetForwardRate(double startTime, double endTime);

    /// <summary>
    /// 取得支援年期範圍 (最小, 最大)
    /// </summary>
    /// <returns>(最小年期, 最大年期)</returns>
    (double MinTenor, double MaxTenor) GetValidRange();
}
