using DciCalculator.Curves;
using DciCalculator.Models;

namespace DciCalculator.Factories;

/// <summary>
/// 利率曲線工廠介面
/// </summary>
public interface ICurveFactory
{
    /// <summary>
    /// 創建零息利率曲線
    /// </summary>
    /// <param name="currency">貨幣代碼（如 "USD", "TWD"）</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="points">曲線點位</param>
    /// <param name="interpolationMethod">插值方法</param>
    /// <returns>零息利率曲線</returns>
    IZeroCurve CreateZeroCurve(
        string currency,
        DateTime referenceDate,
        IEnumerable<CurvePoint> points,
        Models.InterpolationMethod interpolationMethod = Models.InterpolationMethod.Linear);

    /// <summary>
    /// 創建平坦利率曲線（固定利率）
    /// </summary>
    /// <param name="currency">貨幣代碼</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="flatRate">固定利率（如 0.05 表示 5%）</param>
    /// <returns>平坦利率曲線</returns>
    IZeroCurve CreateFlatCurve(
        string currency,
        DateTime referenceDate,
        double flatRate);

    /// <summary>
    /// 從市場工具建立曲線（利率曲線自舉）
    /// </summary>
    /// <param name="currency">貨幣代碼</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="instruments">市場工具（存款、利率交換等）</param>
    /// <param name="interpolationMethod">插值方法</param>
    /// <returns>自舉建立的零息利率曲線</returns>
    IZeroCurve BootstrapCurve(
        string currency,
        DateTime referenceDate,
        IEnumerable<MarketInstrument> instruments,
        Models.InterpolationMethod interpolationMethod = Models.InterpolationMethod.Linear);
}
