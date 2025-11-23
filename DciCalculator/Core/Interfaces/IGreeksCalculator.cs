using DciCalculator.Models;

namespace DciCalculator.Core.Interfaces;

/// <summary>
/// Greeks 計算器介面
/// </summary>
public interface IGreeksCalculator
{
    /// <summary>
    /// 計算 DCI 的 Greeks（針對賣出 Put 部位）
    /// </summary>
    /// <param name="input">DCI 輸入參數</param>
    /// <returns>Greeks 計算結果</returns>
    GreeksResult CalculateDciGreeks(DciInput input);

    /// <summary>
    /// 計算單一 FX 期權的 Greeks
    /// </summary>
    /// <param name="spot">現貨價格</param>
    /// <param name="strike">行使價</param>
    /// <param name="domesticRate">本幣利率</param>
    /// <param name="foreignRate">外幣利率</param>
    /// <param name="volatility">波動率</param>
    /// <param name="timeToMaturity">到期時間（年）</param>
    /// <param name="optionType">期權類型</param>
    /// <returns>Greeks 計算結果</returns>
    GreeksResult CalculateGreeks(
        double spot,
        double strike,
        double domesticRate,
        double foreignRate,
        double volatility,
        double timeToMaturity,
        OptionType optionType);
}
