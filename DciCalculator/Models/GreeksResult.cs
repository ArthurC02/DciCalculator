namespace DciCalculator.Models;

/// <summary>
/// 期權 Greeks 風險指標結果
/// </summary>
public sealed record GreeksResult(
    double Delta,         // 對即期匯率的敏感度（一階導數）
    double Gamma,         // 對即期匯率的二階敏感度（Delta 的變化率）
    double Vega,          // 對波動度的敏感度（1% 波動度變化）
    double Theta,         // 對時間流逝的敏感度（每日時間衰減）
    double RhoDomestic,   // 對本幣利率的敏感度（1% 利率變化）
    double RhoForeign     // 對外幣利率的敏感度（1% 利率變化）
);
