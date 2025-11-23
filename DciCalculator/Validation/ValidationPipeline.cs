namespace DciCalculator.Validation;

/// <summary>
/// 驗證管線，用於串接多個驗證器
/// </summary>
/// <typeparam name="T">要驗證的物件類型</typeparam>
public sealed class ValidationPipeline<T> : IValidator<T>
{
    private readonly List<IValidator<T>> _validators;

    /// <summary>
    /// 建立空的驗證管線
    /// </summary>
    public ValidationPipeline()
    {
        _validators = new List<IValidator<T>>();
    }

    /// <summary>
    /// 建立包含指定驗證器的管線
    /// </summary>
    public ValidationPipeline(IEnumerable<IValidator<T>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = new List<IValidator<T>>(validators);
    }

    /// <summary>
    /// 新增驗證器到管線
    /// </summary>
    public ValidationPipeline<T> Add(IValidator<T> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validators.Add(validator);
        return this;
    }

    /// <summary>
    /// 執行所有驗證器並合併結果
    /// </summary>
    public ValidationResult Validate(T item)
    {
        if (_validators.Count == 0)
            return ValidationResult.Success();

        var results = _validators
            .Select(v => v.Validate(item))
            .ToArray();

        return ValidationResult.Combine(results);
    }

    /// <summary>
    /// 取得管線中的驗證器數量
    /// </summary>
    public int Count => _validators.Count;

    /// <summary>
    /// 建立包含指定驗證器的新管線（靜態工廠方法）
    /// </summary>
    public static ValidationPipeline<T> Create(params IValidator<T>[] validators)
    {
        return new ValidationPipeline<T>(validators);
    }
}
