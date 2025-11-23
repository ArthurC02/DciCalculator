namespace DciCalculator.VolSurfaces;

/// <summary>
/// 波動率曲面介面。
/// 提供依指定 Strike 與 Tenor 取得期權隱含波動率的功能。
/// 
/// 功能涵蓋：
/// - FX 期權報價（可考慮 Smile / Skew）
/// - Greeks 計算
/// - 風險管理支援
/// </summary>
public interface IVolSurface
{
    /// <summary>
    /// 曲面名稱（例如 "USD/TWD"）。
    /// </summary>
    string SurfaceName { get; }

    /// <summary>
    /// 基準日期。
    /// </summary>
    DateTime ReferenceDate { get; }

    /// <summary>
    /// 取得指定 Strike 與 Tenor 的隱含波動率。
    /// </summary>
    /// <param name="strike">履約價（正數）。</param>
    /// <param name="tenor">期限（以年表示）。</param>
    /// <returns>隱含波動率（年化）。</returns>
    double GetVolatility(double strike, double tenor);

    /// <summary>
    /// 取得 ATM (At-The-Money) 隱含波動率。
    /// </summary>
    /// <param name="spot">現貨價格。</param>
    /// <param name="tenor">期限（年）。</param>
    /// <returns>ATM 隱含波動率。</returns>
    double GetATMVolatility(double spot, double tenor);

    /// <summary>
    /// 依指定 Moneyness 與 Tenor 取得隱含波動率。
    /// Moneyness = Strike / Forward。
    /// </summary>
    /// <param name="moneyness">貨幣性（例如 0.95 代表 5% OTM）。</param>
    /// <param name="tenor">期限（年）。</param>
    /// <returns>隱含波動率。</returns>
    double GetVolatilityByMoneyness(double moneyness, double tenor);

    /// <summary>
    /// 取得目前曲面有效查詢範圍。
    /// </summary>
    /// <returns>(最小 Strike, 最大 Strike, 最小 Tenor, 最大 Tenor)</returns>
    (double MinStrike, double MaxStrike, double MinTenor, double MaxTenor) GetValidRange();

    /// <summary>
    /// 檢查指定 Strike / Tenor 是否落在有效範圍內。
    /// </summary>
    bool IsInRange(double strike, double tenor);
}
