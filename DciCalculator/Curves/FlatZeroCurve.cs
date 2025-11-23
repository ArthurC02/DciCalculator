using System.Runtime.CompilerServices;

namespace DciCalculator.Curves;

/// <summary>
/// 平坦零利率曲線 (Flat Zero Curve)
/// 所有期限共用同一零利率。
/// 
/// 特性：
/// - 初始化快速 (僅一個利率參數)
/// - 適合測試或資料稀疏情境
/// - 不反映期限結構斜率
/// </summary>
public sealed class FlatZeroCurve : IZeroCurve
{
    private readonly double _flatRate;

    public string CurveName { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>
    /// 建立平坦零利率曲線
    /// </summary>
    /// <param name="curveName">曲線名稱 (如 "USD")</param>
    /// <param name="referenceDate">基準日</param>
    /// <param name="flatRate">固定零利率 (年化)</param>
    public FlatZeroCurve(string curveName, DateTime referenceDate, double flatRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);

        if (flatRate < -0.20 || flatRate > 0.50)
            throw new ArgumentOutOfRangeException(nameof(flatRate),
                "利率必須位於 [-0.20, 0.50] 範圍內");

        CurveName = curveName;
        ReferenceDate = referenceDate;
        _flatRate = flatRate;
    }

    /// <summary>
    /// 今日為基準的平坦曲線建構
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
            throw new ArgumentException("日期不可早於基準日", nameof(date));

        return _flatRate;
    }

    public double GetDiscountFactor(DateTime date)
    {
        if (date < ReferenceDate)
            throw new ArgumentException("日期不可早於基準日", nameof(date));

        double timeInYears = (date - ReferenceDate).Days / 365.0;
        return Math.Exp(-_flatRate * timeInYears);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetForwardRate(double startTime, double endTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startTime);
        ArgumentOutOfRangeException.ThrowIfNegative(endTime);

        if (endTime <= startTime)
            throw new ArgumentException("結束時間必須大於起始時間");

        // Flat Curve: Forward Rate = Zero Rate
        return _flatRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (double MinTenor, double MaxTenor) GetValidRange()
    {
        // 平坦曲線視為支援所有期限
        return (0.0, double.MaxValue);
    }

    public override string ToString()
    {
        return $"FlatZeroCurve({CurveName}, r={_flatRate:P4})";
    }

    /// <summary>
    /// 建立測試用平坦曲線
    /// </summary>
    public static FlatZeroCurve CreateMock(string curveName = "USD", double rate = 0.05)
    {
        return new FlatZeroCurve(curveName, rate);
    }
}
