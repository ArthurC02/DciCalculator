namespace DciCalculator.VolSurfaces;

/// <summary>
/// 波動度曲面介面
/// 提供不同 Strike 和 Tenor 的隱含波動度
/// 
/// 用途：
/// - FX 期權定價（考慮 Smile/Skew）
/// - Greeks 計算
/// - 風險管理
/// </summary>
public interface IVolSurface
{
    /// <summary>
    /// 曲面名稱（例如 "USD/TWD"）
    /// </summary>
    string SurfaceName { get; }

    /// <summary>
    /// 基準日期
    /// </summary>
    DateTime ReferenceDate { get; }

    /// <summary>
    /// 取得指定 Strike 和 Tenor 的波動度
    /// </summary>
    /// <param name="strike">執行價（絕對值）</param>
    /// <param name="tenor">期限（年）</param>
    /// <returns>隱含波動度（年化）</returns>
    double GetVolatility(double strike, double tenor);

    /// <summary>
    /// 取得 ATM（At-The-Money）波動度
    /// </summary>
    /// <param name="spot">即期價格</param>
    /// <param name="tenor">期限（年）</param>
    /// <returns>ATM 波動度</returns>
    double GetATMVolatility(double spot, double tenor);

    /// <summary>
    /// 取得指定 Moneyness 和 Tenor 的波動度
    /// Moneyness = Strike / Forward
    /// </summary>
    /// <param name="moneyness">Moneyness（例如 0.95 = 5% OTM）</param>
    /// <param name="tenor">期限（年）</param>
    /// <returns>隱含波動度</returns>
    double GetVolatilityByMoneyness(double moneyness, double tenor);

    /// <summary>
    /// 取得曲面的有效範圍
    /// </summary>
    /// <returns>(最小 Strike, 最大 Strike, 最小 Tenor, 最大 Tenor)</returns>
    (double MinStrike, double MaxStrike, double MinTenor, double MaxTenor) GetValidRange();

    /// <summary>
    /// 檢查指定點是否在曲面範圍內
    /// </summary>
    bool IsInRange(double strike, double tenor);
}
