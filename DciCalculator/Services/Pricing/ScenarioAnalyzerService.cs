using DciCalculator.Core.Interfaces;
using DciCalculator.Models;

namespace DciCalculator.Services.Pricing;

/// <summary>
/// 情境分析器服務。
/// 用途：測試 Spot 與 Vol 變動對 DCI 報價與利息的影響。
/// 
/// 應用：
/// - 交易管理：評估不同變動幅度的影響
/// - 風險監控：近似估算 Delta、Vega 敏感度
/// - 客戶溝通：展示多種市場情境下 Coupon 與利息的變化
/// </summary>
public class ScenarioAnalyzerService : IScenarioAnalyzer
{
    private readonly IDciPricingEngine _pricingEngine;
    private readonly IDciPayoffCalculator _payoffCalculator;

    /// <summary>
    /// 建立情境分析器服務。
    /// </summary>
    /// <param name="pricingEngine">DCI 定價引擎</param>
    /// <param name="payoffCalculator">DCI 報酬計算器</param>
    public ScenarioAnalyzerService(
        IDciPricingEngine pricingEngine,
        IDciPayoffCalculator payoffCalculator)
    {
        _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
        _payoffCalculator = payoffCalculator ?? throw new ArgumentNullException(nameof(payoffCalculator));
    }

    /// <summary>
    /// 執行情境分析。
    /// </summary>
    /// <param name="baseInput">基準 DCI 輸入</param>
    /// <param name="spotShifts">Spot 移動列表（pips）</param>
    /// <param name="volShifts">Vol 變動列表（絕對量，例如 ±0.02）</param>
    /// <returns>全部情境結果列表</returns>
    public IReadOnlyList<ScenarioResult> Analyze(
        DciInput baseInput,
        IEnumerable<decimal> spotShifts,
        IEnumerable<double> volShifts)
    {
        ArgumentNullException.ThrowIfNull(baseInput);
        ArgumentNullException.ThrowIfNull(spotShifts);
        ArgumentNullException.ThrowIfNull(volShifts);

        var results = new List<ScenarioResult>();

        // 計算基準報價
        var baseQuote = _pricingEngine.Quote(baseInput);
        decimal baseSpotMid = baseInput.SpotQuote.Mid;
        double baseVol = baseInput.Volatility;

        foreach (decimal spotShift in spotShifts)
        {
            foreach (double volShift in volShifts)
            {
                // 調整 Spot（pips 轉換成匯率）
                decimal newSpotMid = baseSpotMid + spotShift * 0.01m; // pips to rate
                var newSpotQuote = new FxQuote(
                    Bid: newSpotMid - 0.01m,
                    Ask: newSpotMid + 0.01m
                );

                // 調整 Vol（避免低於最小值）
                double newVol = Math.Max(0.01, baseVol + volShift);

                // 建立新的情境輸入
                var scenarioInput = baseInput with
                {
                    SpotQuote = newSpotQuote,
                    Volatility = newVol
                };

                // 計算情境報價
                var scenarioQuote = _pricingEngine.Quote(scenarioInput);

                // 計算 Coupon / 利息變化
                double couponChange = scenarioQuote.CouponAnnual - baseQuote.CouponAnnual;
                decimal interestChange = scenarioQuote.TotalInterestForeign - baseQuote.TotalInterestForeign;

                // 累積結果
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
    /// 快速預設情境分析（內建組合）。
    /// Spot: -10, -5, 0, +5, +10 pips
    /// Vol: -2%, 0%, +2%
    /// </summary>
    public IReadOnlyList<ScenarioResult> QuickAnalyze(DciInput baseInput)
    {
        var spotShifts = new decimal[] { -10m, -5m, 0m, 5m, 10m };
        var volShifts = new double[] { -0.02, 0.0, 0.02 };

        return Analyze(baseInput, spotShifts, volShifts);
    }

    /// <summary>
    /// 計算敏感度（單一步長近似）。
    /// </summary>
    public SensitivityResult CalculateSensitivities(
        DciInput baseInput,
        decimal spotBump = 0.01m,
        double volBump = 0.01)
    {
        ArgumentNullException.ThrowIfNull(baseInput);

        // 計算基準報價
        var baseQuote = _pricingEngine.Quote(baseInput);
        double baseCoupon = baseQuote.CouponAnnual;

        // Spot 敏感度（Delta）
        decimal spotMid = baseInput.SpotQuote.Mid;
        var spotUpInput = baseInput with
        {
            SpotQuote = new FxQuote(
                Bid: spotMid + spotBump * 0.01m - 0.01m,
                Ask: spotMid + spotBump * 0.01m + 0.01m
            )
        };
        var spotUpQuote = _pricingEngine.Quote(spotUpInput);
        double spotDelta = (spotUpQuote.CouponAnnual - baseCoupon) / (double)spotBump;

        // Vol 敏感度（Vega）
        var volUpInput = baseInput with { Volatility = baseInput.Volatility + volBump };
        var volUpQuote = _pricingEngine.Quote(volUpInput);
        double volVega = (volUpQuote.CouponAnnual - baseCoupon) / volBump;

        return new SensitivityResult(spotDelta, volVega);
    }

    /// <summary>
    /// 計算到期 PnL 分佈（Monte Carlo 模擬）。
    /// 假設 Spot 期間服從幾何布朗運動 (GBM)。
    /// </summary>
    public PnLDistribution CalculatePnLDistribution(
        DciInput baseInput,
        int scenarios = 1000,
        double spotVolatility = 0.10,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(baseInput);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scenarios);

        var baseQuote = _pricingEngine.Quote(baseInput);
        decimal spotMid = baseInput.SpotQuote.Mid;
        double drift = baseInput.RateDomestic - baseInput.RateForeign;
        double timeToMaturity = baseInput.TenorInYears;

        var pnls = new List<decimal>(scenarios);
        var random = seed.HasValue ? new Random(seed.Value) : new Random(42);

        for (int i = 0; i < scenarios; i++)
        {
            // 模擬 Spot 終值（GBM 尾端）
            double z = GenerateNormalRandom(random);
            double spotAtMaturity = (double)spotMid * Math.Exp(
                (drift - 0.5 * spotVolatility * spotVolatility) * timeToMaturity +
                spotVolatility * Math.Sqrt(timeToMaturity) * z
            );

            // 計算 Payoff
            var payoff = _payoffCalculator.CalculatePayoff(
                baseInput,
                baseQuote,
                (decimal)spotAtMaturity
            );

            // 計算相對存款的 PnL
            decimal pnl = _payoffCalculator.CalculatePnLVsDeposit(baseInput, payoff);
            pnls.Add(pnl);
        }

        // 統計分佈結果
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
    /// 產生情境分析報告（文字格式）。
    /// </summary>
    public string GenerateReport(IReadOnlyList<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
            return "無情境結果";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== DCI 情境分析報告 ===");
        report.AppendLine();
        report.AppendLine($"{"Spot Shift",12} | {"Vol Shift",10} | {"Coupon",8} | {"CouponΔ",10} | {"InterestΔ",12}");
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

    /// <summary>
    /// 產生標準常態亂數（Box-Muller）。
    /// </summary>
    private static double GenerateNormalRandom(Random random)
    {
        double u1 = random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
