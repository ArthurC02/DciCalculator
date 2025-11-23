namespace DciCalculator.Models;

/// <summary>
/// DCI 到期回報計算結果
/// </summary>
public sealed record DciPayoffResult(
    bool IsKnockedIn,          // 是否被履約（匯率跌破 Strike）
    decimal PayoffForeign,     // 外幣回報（若未被履約）
    decimal PayoffDomestic,    // 本幣回報（若被履約）
    decimal FinalSpot,         // 到期時的即期匯率
    decimal Strike             // 履約價
);
