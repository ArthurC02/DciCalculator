using DciCalculator.Models;

namespace DciCalculator.Validation;

/// <summary>
/// DCI 輸入參數驗證器
/// </summary>
public sealed class DciInputValidator : IValidator<DciInput>
{
    /// <summary>
    /// 驗證 DCI 輸入參數
    /// </summary>
    public ValidationResult Validate(DciInput item)
    {
        if (item == null)
            return ValidationResult.Failure(nameof(item), "DciInput 不能為 null");

        var errors = new List<ValidationError>();

        // 驗證名義本金
        if (item.NotionalForeign <= 0)
            errors.Add(new ValidationError(
                nameof(item.NotionalForeign),
                $"外幣名義本金必須大於 0，實際值: {item.NotionalForeign}"));

        // 驗證 Strike
        if (item.Strike <= 0)
            errors.Add(new ValidationError(
                nameof(item.Strike),
                $"Strike 必須大於 0，實際值: {item.Strike}"));

        // 驗證 Spot
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

        // 驗證波動率
        if (item.Volatility <= 0)
            errors.Add(new ValidationError(
                nameof(item.Volatility),
                $"波動率必須大於 0，實際值: {item.Volatility}"));

        if (item.Volatility > 5.0)
            errors.Add(new ValidationError(
                nameof(item.Volatility),
                $"波動率過高 (>{500:P0})，請檢查單位是否正確（應使用小數，如 0.12 表示 12%），實際值: {item.Volatility}"));

        // 驗證期限
        if (item.TenorInYears <= 0)
            errors.Add(new ValidationError(
                nameof(item.TenorInYears),
                $"期限必須大於 0，實際值: {item.TenorInYears}"));

        if (item.TenorInYears > 30)
            errors.Add(new ValidationError(
                nameof(item.TenorInYears),
                $"期限過長（>{30} 年），可能不合理，實際值: {item.TenorInYears}"));

        // 驗證利率
        if (item.RateDomestic < -0.1 || item.RateDomestic > 1.0)
            errors.Add(new ValidationError(
                nameof(item.RateDomestic),
                $"本幣利率超出合理範圍 [-10%, 100%]，實際值: {item.RateDomestic:P2}"));

        if (item.RateForeign < -0.1 || item.RateForeign > 1.0)
            errors.Add(new ValidationError(
                nameof(item.RateForeign),
                $"外幣利率超出合理範圍 [-10%, 100%]，實際值: {item.RateForeign:P2}"));

        if (item.DepositRateAnnual < 0 || item.DepositRateAnnual > 0.5)
            errors.Add(new ValidationError(
                nameof(item.DepositRateAnnual),
                $"存款利率超出合理範圍 [0%, 50%]，實際值: {item.DepositRateAnnual:P2}"));

        // 業務邏輯驗證：Strike 與 Spot 的關係
        if (item.Strike > 0)
        {
            decimal spotMid = item.SpotQuote.Mid;
            decimal strikeToSpotRatio = item.Strike / spotMid;

            // 警告：Strike 偏離 Spot 過遠（可能不是錯誤，但值得注意）
            if (strikeToSpotRatio < 0.5m || strikeToSpotRatio > 1.5m)
            {
                errors.Add(new ValidationError(
                    nameof(item.Strike),
                    $"Strike ({item.Strike:F4}) 偏離 Spot Mid ({spotMid:F4}) 超過 50%，Strike/Spot = {strikeToSpotRatio:P1}"));
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
