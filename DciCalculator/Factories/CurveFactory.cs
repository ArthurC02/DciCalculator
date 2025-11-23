using DciCalculator.Curves;
using DciCalculator.Models;

namespace DciCalculator.Factories;

/// <summary>
/// 利率曲線工廠實作
/// </summary>
public class CurveFactory : ICurveFactory
{
    /// <summary>
    /// 創建零息利率曲線
    /// </summary>
    public IZeroCurve CreateZeroCurve(
        string currency,
        DateTime referenceDate,
        IEnumerable<CurvePoint> points,
        Models.InterpolationMethod interpolationMethod = Models.InterpolationMethod.Linear)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(points);

        var pointsList = points.ToList();
        
        if (pointsList.Count == 0)
            throw new ArgumentException("曲線點位不能為空", nameof(points));

        return interpolationMethod switch
        {
            Models.InterpolationMethod.Linear => new LinearInterpolatedCurve(currency, referenceDate, pointsList),
            Models.InterpolationMethod.CubicSpline => new CubicSplineCurve(currency, referenceDate, pointsList),
            Models.InterpolationMethod.Flat => throw new NotImplementedException("Flat 插值方法尚未實作，請使用 CreateFlatCurve"),
            Models.InterpolationMethod.LogLinear => throw new NotImplementedException("LogLinear 插值方法尚未實作"),
            _ => throw new ArgumentException($"不支援的插值方法: {interpolationMethod}", nameof(interpolationMethod))
        };
    }

    /// <summary>
    /// 創建平坦利率曲線（固定利率）
    /// </summary>
    public IZeroCurve CreateFlatCurve(
        string currency,
        DateTime referenceDate,
        double flatRate)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (flatRate < 0)
            throw new ArgumentOutOfRangeException(nameof(flatRate), "利率不能為負值");

        return new FlatZeroCurve(currency, referenceDate, flatRate);
    }

    /// <summary>
    /// 從市場工具建立曲線（利率曲線自舉）
    /// </summary>
    public IZeroCurve BootstrapCurve(
        string currency,
        DateTime referenceDate,
        IEnumerable<MarketInstrument> instruments,
        Models.InterpolationMethod interpolationMethod = Models.InterpolationMethod.Linear)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(instruments);

        var instrumentsList = instruments.ToList();
        if (instrumentsList.Count == 0)
            throw new ArgumentException("市場工具不能為空", nameof(instruments));

        // CurveBootstrapper 直接返回完整的 IZeroCurve
        var bootstrapper = new CurveBootstrapper(currency, referenceDate);
        return bootstrapper.Bootstrap(instrumentsList, interpolationMethod);
    }
}
