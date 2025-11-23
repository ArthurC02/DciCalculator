using DciCalculator.Models;

namespace DciCalculator.Validation;

/// <summary>
/// 市場數據快照驗證器
/// </summary>
public sealed class MarketDataSnapshotValidator : IValidator<MarketDataSnapshot>
{
    /// <summary>
    /// 驗證市場數據快照
    /// </summary>
    public ValidationResult Validate(MarketDataSnapshot item)
    {
        if (item == null)
            return ValidationResult.Failure(nameof(item), "MarketDataSnapshot 不能為 null");

        var errors = new List<ValidationError>();

        // 驗證幣別對
        if (string.IsNullOrWhiteSpace(item.CurrencyPair))
            errors.Add(new ValidationError(
                nameof(item.CurrencyPair),
                "幣別對不能為空"));

        // 驗證 SpotQuote
        if (item.SpotQuote.Bid <= 0)
            errors.Add(new ValidationError(
                $"{nameof(item.SpotQuote)}.{nameof(item.SpotQuote.Bid)}",
                $"Spot Bid 必須大於 0，實際值: {item.SpotQuote.Bid}"));

        if (item.SpotQuote.Ask <= 0)
            errors.Add(new ValidationError(
                $"{nameof(item.SpotQuote)}.{nameof(item.SpotQuote.Ask)}",
                $"Spot Ask 必須大於 0，實際值: {item.SpotQuote.Ask}"));

        if (item.SpotQuote.Ask < item.SpotQuote.Bid)
            errors.Add(new ValidationError(
                nameof(item.SpotQuote),
                $"Spot Ask ({item.SpotQuote.Ask}) 必須大於等於 Bid ({item.SpotQuote.Bid})"));

        // 驗證本幣利率
        if (item.RateDomestic < -0.1 || item.RateDomestic > 1.0)
            errors.Add(new ValidationError(
                nameof(item.RateDomestic),
                $"本幣利率超出合理範圍 [-10%, 100%]，實際值: {item.RateDomestic:P2}"));

        // 驗證外幣利率
        if (item.RateForeign < -0.1 || item.RateForeign > 1.0)
            errors.Add(new ValidationError(
                nameof(item.RateForeign),
                $"外幣利率超出合理範圍 [-10%, 100%]，實際值: {item.RateForeign:P2}"));

        // 驗證波動率
        if (item.Volatility <= 0)
            errors.Add(new ValidationError(
                nameof(item.Volatility),
                $"波動率必須大於 0，實際值: {item.Volatility}"));

        if (item.Volatility > 5.0)
            errors.Add(new ValidationError(
                nameof(item.Volatility),
                $"波動率過高 (>{500:P0})，請檢查單位是否正確（應使用小數，如 0.12 表示 12%），實際值: {item.Volatility}"));

        // 驗證時間戳
        if (item.TimestampUtc == default(DateTime))
            errors.Add(new ValidationError(
                nameof(item.TimestampUtc),
                "時間戳不能為預設值"));

        // 驗證時間戳不是未來時間
        if (item.TimestampUtc > DateTime.UtcNow.AddMinutes(5))
            errors.Add(new ValidationError(
                nameof(item.TimestampUtc),
                $"時間戳不能是未來時間（允許 5 分鐘誤差），實際值: {item.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC"));

        // 驗證資料時效性（僅在 IsRealTime 為 true 時）
        if (item.IsRealTime && item.IsStale(maxAgeSeconds: 300)) // 5分鐘
        {
            TimeSpan age = DateTime.UtcNow - item.TimestampUtc;
            errors.Add(new ValidationError(
                nameof(item.TimestampUtc),
                $"即時市場數據已過期（>{300} 秒），資料年齡: {age.TotalSeconds:F0} 秒"));
        }

        // 驗證遠期點數（如果有提供）
        if (item.ForwardPoints.HasValue)
        {
            // 遠期點數的合理性檢查：一般不應超過 spot 的 ±50%
            decimal maxPoints = Math.Abs(item.SpotQuote.Mid * 0.5m);
            if (Math.Abs(item.ForwardPoints.Value) > maxPoints)
                errors.Add(new ValidationError(
                    nameof(item.ForwardPoints),
                    $"遠期點數 ({item.ForwardPoints.Value:F4}) 超出合理範圍（±{maxPoints:F4}），可能有誤"));
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
