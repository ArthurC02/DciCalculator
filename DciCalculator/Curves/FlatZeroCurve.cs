using System.Runtime.CompilerServices;

namespace DciCalculator.Curves;

/// <summary>
/// 平坦零息曲線（Flat Zero Curve）
/// 所有期限使用相同利率
/// 
/// 用途：
/// - 向後相容（替代單一利率參數）
/// - 簡化場景測試
/// - 流動性較差的市場
/// </summary>
public sealed class FlatZeroCurve : IZeroCurve
{
    private readonly double _flatRate;

    public string CurveName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立平坦零息曲線
    /// </summary>
    /// <param name="curveName">曲線名稱（例如 "USD", "TWD"）</param>
    /// <param name="referenceDate">基準日期</param>
    /// <param name="flatRate">固定利率（年化，連續複利）</param>
    public FlatZeroCurve(string curveName, DateTime referenceDate, double flatRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);

        if (flatRate < -0.20 || flatRate > 0.50)
            throw new ArgumentOutOfRangeException(nameof(flatRate),
                "利率必須在 [-0.20, 0.50] 範圍內");

        CurveName = curveName;
        ReferenceDate = referenceDate;
        _flatRate = flatRate;
    }

    /// <summary>
    /// 建立平坦曲線（使用當前日期）
    /// </summary>
    public FlatZeroCurve(string curveName, double flatRate)
        : this(curveName, DateTime.Today, flatRate)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetZeroRate(double timeInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeInYears);
        return _flatRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDiscountFactor(double timeInYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeInYears);
        return Math.Exp(-_flatRate * timeInYears);
    }

    public double GetZeroRate(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不能早於基準日期", nameof(date));

        return _flatRate;
    }

    public double GetDiscountFactor(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不能早於基準日期", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return Math.Exp(-_flatRate * timeInYears);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetForwardRate(double startTime, double endTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startTime);
        ArgumentOutOfRangeException.ThrowIfNegative(endTime);

        if (endTime <= startTime)
            throw new ArgumentException("結束時間必須 > 起始時間");

        // Flat Curve: Forward Rate = Zero Rate
        return _flatRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double MinTenor, double MaxTenor) GetValidRange()
    {
        // Flat curve 適用於所有期限
        return (0.0, double.MaxValue);
    }

    public override string ToString()
    {
        return $"FlatZeroCurve({CurveName}, r={_flatRate:P4})";
    }

    /// <summary>
    /// 建立簡單的測試曲線
    /// </summary>
    public static FlatZeroCurve CreateMock(string curveName = "USD", double rate = 0.05)
    {
        return new FlatZeroCurve(curveName, rate);
    }
}
