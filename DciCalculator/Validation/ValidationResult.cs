namespace DciCalculator.Validation;

/// <summary>
/// 驗證結果
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// 驗證錯誤集合
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// 是否驗證成功
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// 驗證失敗的錯誤訊息（單一字串）
    /// </summary>
    public string ErrorMessage => string.Join("; ", Errors.Select(e => e.Message));

    private ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors ?? Array.Empty<ValidationError>();
    }

    /// <summary>
    /// 創建成功的驗證結果
    /// </summary>
    public static ValidationResult Success() => new(Array.Empty<ValidationError>());

    /// <summary>
    /// 創建失敗的驗證結果（單一錯誤）
    /// </summary>
    public static ValidationResult Failure(string propertyName, string message)
    {
        return new ValidationResult(new[] { new ValidationError(propertyName, message) });
    }

    /// <summary>
    /// 創建失敗的驗證結果（多個錯誤）
    /// </summary>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors)
    {
        return new ValidationResult(errors.ToList());
    }

    /// <summary>
    /// 合併多個驗證結果
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var allErrors = results.SelectMany(r => r.Errors).ToList();
        return allErrors.Count == 0 ? Success() : Failure(allErrors);
    }
}

/// <summary>
/// 驗證錯誤
/// </summary>
public sealed record ValidationError(string PropertyName, string Message)
{
    /// <summary>
    /// 錯誤的完整描述
    /// </summary>
    public override string ToString() => $"{PropertyName}: {Message}";
}
