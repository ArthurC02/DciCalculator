using DciCalculator.Models;
using DciCalculator.VolSurfaces;

namespace DciCalculator.Factories;

/// <summary>
/// 波動率曲面工廠介面
/// </summary>
public interface IVolSurfaceFactory
{
    /// <summary>
    /// 創建插值波動率曲面
    /// </summary>
    /// <param name="currencyPair">貨幣對（如 "USDTWD"）</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="points">波動率曲面點位</param>
    /// <returns>插值波動率曲面</returns>
    IVolSurface CreateInterpolatedVolSurface(
        string currencyPair,
        DateTime referenceDate,
        IEnumerable<VolSurfacePoint> points);

    /// <summary>
    /// 創建平坦波動率曲面（固定波動率）
    /// </summary>
    /// <param name="currencyPair">貨幣對</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="flatVol">固定波動率（如 0.12 表示 12%）</param>
    /// <returns>平坦波動率曲面</returns>
    IVolSurface CreateFlatVolSurface(
        string currencyPair,
        DateTime referenceDate,
        double flatVol);

    /// <summary>
    /// 從市場報價創建波動率曲面（使用 Smile 參數）
    /// </summary>
    /// <param name="currencyPair">貨幣對</param>
    /// <param name="referenceDate">參考日期</param>
    /// <param name="atmVol">ATM 波動率</param>
    /// <param name="smileParameters">波動率微笑參數</param>
    /// <returns>波動率曲面</returns>
    IVolSurface CreateVolSurfaceFromSmile(
        string currencyPair,
        DateTime referenceDate,
        double atmVol,
        VolSmileParameters smileParameters);
}
