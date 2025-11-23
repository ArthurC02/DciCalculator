namespace DciCalculator.Models;

/// <summary>
/// DCI 報酬結果 (Knock-In 與最終贖回金額資訊)
/// </summary>
public sealed record DciPayoffResult(
    bool IsKnockedIn,          // 是否已 Knock-In (是否觸及障礙/門檻 Strike)
    decimal PayoffForeign,     // 外幣端最終贖回金額 (觸發條件後計算)
    decimal PayoffDomestic,    // 本幣端最終贖回金額 (兌換或保底機制)
    decimal FinalSpot,         // 最終觀察匯率 (到期時 Spot)
    decimal Strike             // 障礙/履約價 Strike
);
