using DciCalculator.Models;
using DciCalculator.VolSurfaces;

namespace DciCalculator.Factories;

/// <summary>
/// 波動率曲面工廠實作
/// </summary>
public class VolSurfaceFactory : IVolSurfaceFactory
{
    /// <summary>
    /// 創建插值波動率曲面
    /// </summary>
    public IVolSurface CreateInterpolatedVolSurface(
        string currencyPair,
        DateTime referenceDate,
        IEnumerable<VolSurfacePoint> points)
    {
        ArgumentNullException.ThrowIfNull(currencyPair);
        ArgumentNullException.ThrowIfNull(points);

        var pointsList = points.ToList();
        
        if (pointsList.Count == 0)
            throw new ArgumentException("波動率曲面點位不能為空", nameof(points));

        return new InterpolatedVolSurface(currencyPair, referenceDate, pointsList);
    }

    /// <summary>
    /// 創建平坦波動率曲面（固定波動率）
    /// </summary>
    public IVolSurface CreateFlatVolSurface(
        string currencyPair,
        DateTime referenceDate,
        double flatVol)
    {
        ArgumentNullException.ThrowIfNull(currencyPair);
        
        if (flatVol < 0)
            throw new ArgumentException("波動率不能為負值", nameof(flatVol));
        
        if (flatVol > 10)
            throw new ArgumentException("波動率過大，請檢查是否使用百分比單位（應使用小數，如 0.12 表示 12%）", nameof(flatVol));

        return new FlatVolSurface(currencyPair, referenceDate, flatVol);
    }

    /// <summary>
    /// 從市場報價創建波動率曲面（使用 Smile 參數）
    /// </summary>
    public IVolSurface CreateVolSurfaceFromSmile(
        string currencyPair,
        DateTime referenceDate,
        double atmVol,
        VolSmileParameters smileParameters)
    {
        ArgumentNullException.ThrowIfNull(currencyPair);
        ArgumentNullException.ThrowIfNull(smileParameters);

        if (atmVol < 0)
            throw new ArgumentException("ATM 波動率不能為負值", nameof(atmVol));

        // 生成波動率曲面點位 - 使用 ATM 和 Smile 參數建立完整曲面
        var points = new List<VolSurfacePoint>();
        
        // 典型的 Delta 點位：10D Put, 25D Put, ATM, 25D Call, 10D Call
        var deltas = new[] { -0.10, -0.25, 0.0, 0.25, 0.10 };
        
        // 假設建立多個到期日的曲面（1M, 3M, 6M, 1Y）
        var tenors = new[] { 30, 90, 180, 365 };

        foreach (var tenorDays in tenors)
        {
            var tenorYears = tenorDays / 365.0;
            
            foreach (var delta in deltas)
            {
                // 使用 ATM Vol 加上 Smile 調整
                var adjustedVol = atmVol + CalculateSmileAdjustment(delta, smileParameters);
                
                // 假設 ATM Strike = 100，Delta 偏離對應 Strike 偏離
                var strike = 100.0 + (delta * 10.0); // 簡化的 Strike 映射
                
                points.Add(new VolSurfacePoint
                {
                    Strike = strike,
                    Tenor = tenorYears,
                    Volatility = adjustedVol
                });
            }
        }

        return new InterpolatedVolSurface(currencyPair, referenceDate, points);
    }

    /// <summary>
    /// 計算基於 Delta 的波動率微笑調整
    /// </summary>
    private static double CalculateSmileAdjustment(double delta, VolSmileParameters smileParams)
    {
        // 簡化的 Smile 模型：使用二次函數近似
        // Vol(delta) = ATM + RiskReversal * delta + Butterfly * delta^2
        
        var absDelta = Math.Abs(delta);
        var riskReversalEffect = smileParams.RiskReversal25D * delta;
        var butterflyEffect = smileParams.Butterfly25D * absDelta * absDelta;
        
        return riskReversalEffect + butterflyEffect;
    }
}
