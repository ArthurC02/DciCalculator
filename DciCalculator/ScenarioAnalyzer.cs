using DciCalculator.Models;

namespace DciCalculator;

/// <summary>
/// 情境分析器
/// 壓力測試：評估 Spot、Vol 變化對 DCI 報價的影響
/// 
/// 用途：
/// - 風險管理：了解市場變動對報價的影響
/// - 對沖決策：計算需要的 Delta 對沖量
/// - 客戶展示：展示不同市場情境下的收益
/// </summary>
public static class ScenarioAnalyzer
{
    /// <summary>
    /// 執行情境分析
    /// </summary>
    /// <param name="baseInput">基準 DCI 輸入</param>
    /// <param name="spotShifts">Spot 變動（pips）</param>
    /// <param name="volShifts">Vol 變動（絕對值，例如 ±0.02）</param>
    /// <returns>所有情境的結果</returns>
    public static IReadOnlyList<ScenarioResult> Analyze(
        DciInput baseInput,
        IEnumerable<decimal> spotShifts,
        IEnumerable<double> volShifts)
    {
        ArgumentNullException.ThrowIfNull(baseInput);
        ArgumentNullException.ThrowIfNull(spotShifts);
        ArgumentNullException.ThrowIfNull(volShifts);

        var results = new List<ScenarioResult>();

        // 計算基準值
        var baseQuote = DciPricer.Quote(baseInput);
        decimal baseSpotMid = baseInput.SpotQuote.Mid;
        double baseVol = baseInput.Volatility;

        foreach (decimal spotShift in spotShifts)
        {
            foreach (double volShift in volShifts)
            {
                // 調整 Spot
                decimal newSpotMid = baseSpotMid + spotShift * 0.01m; // pips to rate
                var newSpotQuote = new FxQuote(
                    Bid: newSpotMid - 0.01m,
                    Ask: newSpotMid + 0.01m
                );

                // 調整 Vol
                double newVol = Math.Max(0.01, baseVol + volShift);

                // 建立新情境輸入
                var scenarioInput = baseInput with
                {
                    SpotQuote = newSpotQuote,
                    Volatility = newVol
                };

                // 計算報價
                var scenarioQuote = DciPricer.Quote(scenarioInput);

                // 計算變化
                double couponChange = scenarioQuote.CouponAnnual - baseQuote.CouponAnnual;
                decimal interestChange = scenarioQuote.TotalInterestForeign - baseQuote.TotalInterestForeign;

                // 記錄結果
                results.Add(new ScenarioResult(
                    SpotShift: spotShift,
                    VolShift: volShift,
                    Spot: newSpotMid,
                    Volatility: newVol,
                    Coupon: scenarioQuote.CouponAnnual,
                    CouponChange: couponChange,
                    TotalInterest: scenarioQuote.TotalInterestForeign,
                    InterestChange: interestChange
                ));
            }
        }

        return results;
    }

    /// <summary>
    /// 快速情境分析（預設設定）
    /// Spot: ±10, ±5, 0 pips
    /// Vol: ±2%, 0
    /// </summary>
    public static IReadOnlyList<ScenarioResult> QuickAnalyze(DciInput baseInput)
    {
        var spotShifts = new decimal[] { -10m, -5m, 0m, 5m, 10m };
        var volShifts = new double[] { -0.02, 0.0, 0.02 };

        return Analyze(baseInput, spotShifts, volShifts);
    }

    /// <summary>
    /// 計算敏感度（單一變數）
    /// </summary>
    public static (double SpotDelta, double VolVega) CalculateSensitivities(DciInput baseInput)
    {
        ArgumentNullException.ThrowIfNull(baseInput);

        // 計算基準
        var baseQuote = DciPricer.Quote(baseInput);
        double baseCoupon = baseQuote.CouponAnnual;

        // Spot 敏感度（Delta）
        decimal spotBump = 1m; // 1 pip
        decimal spotMid = baseInput.SpotQuote.Mid;
        var spotUpInput = baseInput with
        {
            SpotQuote = new FxQuote(
                Bid: spotMid + spotBump * 0.01m - 0.01m,
                Ask: spotMid + spotBump * 0.01m + 0.01m
            )
        };
        var spotUpQuote = DciPricer.Quote(spotUpInput);
        double spotDelta = (spotUpQuote.CouponAnnual - baseCoupon) / (double)spotBump;

        // Vol 敏感度（Vega）
        double volBump = 0.01; // 1%
        var volUpInput = baseInput with { Volatility = baseInput.Volatility + volBump };
        var volUpQuote = DciPricer.Quote(volUpInput);
        double volVega = (volUpQuote.CouponAnnual - baseCoupon) / volBump;

        return (spotDelta, volVega);
    }

    /// <summary>
    /// 計算盈虧分佈（Monte Carlo 簡化版）
    /// 假設 Spot 在到期時的分佈
    /// </summary>
    public static PnLDistribution CalculatePnLDistribution(
        DciInput baseInput,
        int scenarios = 100,
        double spotVolatility = 0.10)
    {
        ArgumentNullException.ThrowIfNull(baseInput);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scenarios);

        var baseQuote = DciPricer.Quote(baseInput);
        decimal spotMid = baseInput.SpotQuote.Mid;
        double drift = baseInput.RateDomestic - baseInput.RateForeign;
        double timeToMaturity = baseInput.TenorInYears;

        var pnls = new List<decimal>(scenarios);
        var random = new Random(42); // 固定種子，可重現

        for (int i = 0; i < scenarios; i++)
        {
            // 產生隨機 Spot（對數常態分佈）
            double z = GenerateNormalRandom(random);
            double spotAtMaturity = (double)spotMid * Math.Exp(
                (drift - 0.5 * spotVolatility * spotVolatility) * timeToMaturity +
                spotVolatility * Math.Sqrt(timeToMaturity) * z
            );

            // 計算 Payoff
            var payoff = DciPayoffCalculator.CalculatePayoff(
                baseInput,
                baseQuote,
                (decimal)spotAtMaturity
            );

            // 計算 PnL（與單純定存相比）
            decimal pnl = DciPayoffCalculator.CalculatePnLVsDeposit(baseInput, payoff);
            pnls.Add(pnl);
        }

        // 統計
        pnls.Sort();
        decimal mean = pnls.Average();
        decimal median = pnls[scenarios / 2];
        decimal percentile5 = pnls[(int)(scenarios * 0.05)];
        decimal percentile95 = pnls[(int)(scenarios * 0.95)];

        return new PnLDistribution(
            Scenarios: scenarios,
            Mean: mean,
            Median: median,
            Percentile5: percentile5,
            Percentile95: percentile95,
            Min: pnls[0],
            Max: pnls[^1]
        );
    }

    /// <summary>
    /// 產生標準常態隨機數（Box-Muller 方法）
    /// </summary>
    private static double GenerateNormalRandom(Random random)
    {
        double u1 = random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// 產生情境分析報告（格式化輸出）
    /// </summary>
    public static string GenerateReport(IReadOnlyList<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
            return "無情境結果";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== DCI 情境分析報告 ===");
        report.AppendLine();
        report.AppendLine($"{"Spot Shift",12} | {"Vol Shift",10} | {"Coupon",8} | {"Coupon Δ",10} | {"Interest Δ",12}");
        report.AppendLine(new string('-', 70));

        foreach (var result in results)
        {
            report.AppendLine(
                $"{result.SpotShift,12:+0;-0;0} | " +
                $"{result.VolShift,10:+0.00;-0.00;0.00} | " +
                $"{result.Coupon,7:P2} | " +
                $"{result.CouponChange,9:+0.00%;-0.00%;0.00%} | " +
                $"{result.InterestChange,11:+0.00;-0.00;0.00}"
            );
        }

        return report.ToString();
    }
}

/// <summary>
/// 情境分析結果
/// </summary>
public sealed record ScenarioResult(
    decimal SpotShift,          // Spot 變動（pips）
    double VolShift,            // Vol 變動（絕對值）
    decimal Spot,               // 新 Spot
    double Volatility,          // 新 Vol
    double Coupon,              // 新 Coupon
    double CouponChange,        // Coupon 變化（vs. 基準）
    decimal TotalInterest,      // 總利息
    decimal InterestChange      // 利息變化（vs. 基準）
);

/// <summary>
/// 盈虧分佈統計
/// </summary>
public sealed record PnLDistribution(
    int Scenarios,              // 情境數量
    decimal Mean,               // 平均 PnL
    decimal Median,             // 中位數 PnL
    decimal Percentile5,        // 5% 分位數（VaR 95%）
    decimal Percentile95,       // 95% 分位數
    decimal Min,                // 最小 PnL
    decimal Max                 // 最大 PnL
);
