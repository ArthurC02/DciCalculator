namespace DciCalculator.Models;

/// <summary>
/// 選擇權敏感度結果 (Greeks)
/// 包含對底層價格、波動率、時間與利率的主要一階/二階敏感度。
/// </summary>
public sealed record GreeksResult(
    double Delta,         // Delta: 價格對底層資產價格變動的一階敏感度
    double Gamma,         // Gamma: Delta 對底層價格的敏感度 (二階導數)
    double Vega,          // Vega: 價格對隱含波動率變動的敏感度 (每 1% vol 變化)
    double Theta,         // Theta: 價格對時間流逝的敏感度 (每天時間推進的價值衰減)
    double RhoDomestic,   // RhoDomestic: 價格對本幣利率變動 (1% 利率改變) 的敏感度
    double RhoForeign     // RhoForeign: 價格對外幣利率變動 (1% 利率改變) 的敏感度
);
