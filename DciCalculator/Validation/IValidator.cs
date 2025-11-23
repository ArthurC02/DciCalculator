namespace DciCalculator.Validation;

/// <summary>
/// 驗證器介面
/// </summary>
/// <typeparam name="T">要驗證的類型</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// 驗證物件
    /// </summary>
    /// <param name="item">要驗證的物件</param>
    /// <returns>驗證結果</returns>
    ValidationResult Validate(T item);
}
