# DCI Pricing Engine – 技術與產品文件 (v3.0 / .NET 8 / 2025-11)

本專案為 **DCI（Dual Currency Investment，雙幣投資）** 報價、風險與情境分析引擎。核心涵蓋：
期權定價 (FX / Equity)、Greeks、Strike 反求、Margin 調整、日數換算、利率曲線展期、波動率曲面插值、情境與 Monte Carlo 分佈。

> 語言: C# 12  
> Runtime: .NET 8  
> 精度策略: 金額/匯率 `decimal`；數值/統計 `double`  
> 設計: 依賴注入 (DI) + SOLID 原則 + Immutable record + 無副作用  
> 依賴: Microsoft.Extensions.DependencyInjection, MathNet.Numerics

---

## 1. 功能總覽

| 分類 | 功能 | 說明 |
|------|------|------|
| 定價模型 | Black-Scholes / Garman-Kohlhagen | 支援 Deep ITM/OTM 穩定性與折現因子/曲線 API |
| 高級定價 | 利率曲線 + 波動率曲面 | `PriceWithCurves` & 折現因子版本 |
| Greeks | Delta / Gamma / Vega / Theta / Rho | FX Put/Call；DCI 賣出 Put 方向自動翻轉 |
| DCI 報價 | 存款利息 + 賣出 Put 溢價 | `DciPricer.Quote/QuoteWithMargin` |
| Strike 求解 | Newton-Raphson | 目標 Coupon 對應行使價；梯形產生 |
| Margin | 行使價/價格調整 & 反推 | pips / 百分比；算最終 Coupon 或 target Margin |
| 日數換算 | Act/360, Act/365, Act/Act, 30/360, Bus/252 | 年期計算 / 到期日 / 工作天 |
| 利率曲線 | Flat / Linear / CubicSpline / Bootstrap | ZeroRate / DiscountFactor / ForwardRate |
| 波動率曲面 | Flat / Interpolated (雙線性) | Strike/Tenor 查詢、ATM、Moneyness |
| 情境分析 | Spot/Vol shifts + 快速報告 | 組合分析 / 排列輸出 / 敏感度近似 |
| Monte Carlo | GBM Spot 模擬 | PnL 分佈統計 (均值/分位數) |

---

## 2. DCI 結構與收益來源

DCI = 外幣存款 (本金 + 存款利息) + 賣出一枚 FX Put (收取期權溢價)

到期兩種結果（以 USD/TWD 為例）：

1. Spot ≥ Strike → 贖回原外幣本金 + 存款利息。
2. Spot < Strike → 以 Strike 兌換成本幣 (Strike * Notional)；外幣等值已鎖定，仍保留存款利息。

年化 Coupon ≈ 存款利息年化 + 期權費折算年化。

主要輸入 (record `DciInput`): `NotionalForeign`, `SpotQuote`, `Strike`, `RateDomestic`, `RateForeign`, `Volatility`, `TenorInYears`, `DepositRateAnnual`。

---

## 3. 精度與型別

| 類別 | 型別 | 備註 |
|------|------|------|
| 匯率 / 金額 / 期權溢價 | `decimal` | 四位常用；四捨五入 `MidpointRounding.AwayFromZero` |
| 常態分布 / 指數 / Greeks | `double` | 效能與數值穩定 |
| 利率 / 波動率 / 年期 | `double` | 連續複利假設 |

轉換範式：僅在計算邊界集中做 `decimal`→`double`，結果輸出前再回轉與四捨五入。

---

## 4. 專案分層架構

```text
Core/              核心介面定義 (IPricingModel, IDciPricingEngine, IGreeksCalculator...)
Services/          業務邏輯服務 (DciPricingEngine, GreeksCalculatorService, MarginService...)
  ├─ Pricing/      定價相關服務
  └─ Margin/       保證金計算服務
PricingModels/     定價模型實作 (GarmanKohlhagenModel)
Algorithms/        數值與定價核心 (BlackSholes.cs, GarmanKohlhagen.cs, FxMath.cs)
Curves/            利率曲線 (IZeroCurve, FlatZeroCurve, LinearInterpolatedCurve, CubicSplineCurve)
VolSurfaces/       波動率曲面 (IVolSurface, FlatVolSurface, InterpolatedVolSurface)
Factories/         工廠類別 (CurveFactory, VolSurfaceFactory)
Builders/          建構器模式 (DciInputBuilder, MarketDataSnapshotBuilder)
Validation/        驗證框架 (IValidator, ValidationPipeline)
DayCount/          日數計算策略 (Act365Calculator, Act360Calculator...)
DependencyInjection/  DI 註冊 (ServiceCollectionExtensions)
Models/            Immutable Domain Records (DciInput, DciQuoteResult...)
[舊類別]/          向後相容的靜態包裝 (已標記 Obsolete)
Tests/             單元與整合測試
Docs/              本文件
```

核心設計要點：

- **依賴注入優先**：所有服務可透過 DI 容器註冊與注入
- **SOLID 原則**：介面分離、依賴反轉、開放封閉原則
- **無狀態服務**：所有服務為 Singleton，執行緒安全
- **資料不可變**：使用 record/readonly struct
- **嚴格參數檢核**：負值、零值、極端值、NaN/Infinity 立即擲例外
- **向後相容**：保留靜態 API（標記為 Obsolete）
- **Deep ITM/OTM**：`|d1| > 20` 直接內含價值近似，提高速度

---

## 5. 主要 API 節點

### 5.1 依賴注入方式（建議）

```csharp
// 註冊服務
services.AddDciServices();

// 透過建構子注入使用
public class TradingService
{
    private readonly IDciPricingEngine _pricingEngine;
    private readonly IGreeksCalculator _greeksCalculator;
    private readonly IStrikeSolver _strikeSolver;
    
    public TradingService(
        IDciPricingEngine pricingEngine,
        IGreeksCalculator greeksCalculator,
        IStrikeSolver strikeSolver)
    {
        _pricingEngine = pricingEngine;
        _greeksCalculator = greeksCalculator;
        _strikeSolver = strikeSolver;
    }
    
    public void ProcessTrade()
    {
        // DCI 報價
        var quote = _pricingEngine.Quote(input);
        var quoteWithMargin = _pricingEngine.QuoteWithMargin(input, 0.10);
        
        // Greeks
        var greeks = _greeksCalculator.CalculateDciGreeks(input, quote);
        
        // Strike 求解
        var strike = _strikeSolver.SolveStrike(input, 0.08);
    }
}
```

### 5.2 靜態 API（舊版，已棄用）

```csharp
// ⚠️ 以下為向後相容的靜態 API，標記為 Obsolete
// 新程式碼請使用依賴注入方式

// FX 期權定價
double GarmanKohlhagen.PriceFxOption(...);

// DCI 報價
DciQuoteResult DciPricer.Quote(DciInput input);

// Greeks
GreeksResult GreeksCalculator.CalculateDciGreeks(DciInput input);

// Strike 求解
decimal StrikeSolver.SolveStrike(...);

// Margin
decimal MarginCalculator.ApplyMarginToStrike(...);

// 情境分析
IReadOnlyList<ScenarioResult> ScenarioAnalyzer.Analyze(...);
```

---

## 6. 利率曲線與波動率曲面

### 6.1 利率曲線 (IZeroCurve)

- `FlatZeroCurve`: 常數利率快速測試。
- `LinearInterpolatedCurve`: 節點線性插值，支援外推邊界截取。
- `CubicSplineCurve`: 平滑二階連續；長期擴充。
- `CurveBootstrapper`: 由 `DepositInstrument` + `SwapInstrument` 展期抽取零利率，支援失敗回退迭代。

### 6.2 波動率曲面 (IVolSurface)

- `FlatVolSurface`: 單一波動率。
- `InterpolatedVolSurface`: Strike × Tenor 雙線性插值；支援 ATM / Moneyness。

使用曲線/曲面定價：`GarmanKohlhagen.PriceWithCurves` 或折現因子版本 `PriceWithDiscountFactors`。

---

## 7. 日數與年期 (DayCountCalculator)

支援 `Act365`, `Act360`, `ActAct`, `Thirty360`, `Bus252`；提供：

- `YearFraction(start, end, convention)`
- `CalculateMaturityDate(start, tenorDays, adjustForWeekends)`

工作天計算採簡化(不含假日)，可後續擴充行事曆介面。

---

## 8. 情境與風險

`ScenarioAnalyzer`：

- Spot/Vol shifts 列表生成 `ScenarioResult`（包括 Coupon 與利息變化）。
- 快速模板 `QuickAnalyze`。
- 近似敏感度 `CalculateSensitivities` (單步 bump)。
- Monte Carlo 分佈 `CalculatePnLDistribution`：輸出 `PnLDistribution`（均值/中位/分位數/VaR）。

Greeks：採 Garman-Kohlhagen，賣出 Put 方向自動翻轉（部位視角）。

---

## 9. 使用範例

### 9.1 依賴注入方式（建議）

```csharp
using Microsoft.Extensions.DependencyInjection;
using DciCalculator.DependencyInjection;

// 1. 註冊服務
var services = new ServiceCollection();
services.AddDciServices();
var serviceProvider = services.BuildServiceProvider();

// 2. 取得服務
var pricingEngine = serviceProvider.GetRequiredService<IDciPricingEngine>();
var greeksCalculator = serviceProvider.GetRequiredService<IGreeksCalculator>();
var strikeSolver = serviceProvider.GetRequiredService<IStrikeSolver>();

// 3. 建立輸入
var input = new DciInput(
    NotionalForeign: 10_000m,
    SpotQuote: new FxQuote(30.48m, 30.52m),
    Strike: 30.00m,
    RateDomestic: 0.015,
    RateForeign: 0.050,
    Volatility: 0.10,
    TenorInYears: 90.0 / 365.0,
    DepositRateAnnual: 0.03);

// 4. 使用服務
var quote = pricingEngine.Quote(input);
var quoteWithMargin = pricingEngine.QuoteWithMargin(input, 0.10);
var greeks = greeksCalculator.CalculateDciGreeks(input, quote);
var strikeFor8Pct = strikeSolver.SolveStrike(input, 0.08);
```

### 9.2 使用 Builder 模式

```csharp
using DciCalculator.Builders;

// 流暢 API 建立輸入
var input = DciInputBuilder.CreateTypicalUsdTwd()
    .WithNotional(10_000m)
    .WithStrike(30.00m)
    .WithTenorDays(90)
    .WithDepositRate(0.03)
    .Build();

var quote = pricingEngine.Quote(input);
```

### 9.3 靜態 API（舊版）

```csharp
// ⚠️ 此方式已棄用，僅供向後相容
var quote = DciPricer.Quote(input);
var greeks = GreeksCalculator.CalculateDciGreeks(input);
var strikeFor8Pct = StrikeSolver.SolveStrike(input, 0.08);
```

---

## 10. 效能與品質策略

- 內聯：Critical path (`[MethodImpl(AggressiveInlining)]`).
- 避免重複計算：預先計算 `sqrtT`, `volSqrtT`, `ln(S/K)`。
- Deep ITM/OTM 近似：`HandleDeepOptions` 降低常態 CDF 次數。
- 演算法回退：Bootstrap Swap 首先使用閉式；失敗回退迭代。
- 嚴格參數驗證：發現錯誤立即擲出明確例外，縮短問題定位時間。

測試覆蓋：定價邊界 (低/高 vol, Near expiry)、Greeks 合理性、求解器收斂、曲線展期、情境分析、日數換算。

---

## 11. FAQ

**Q: 為什麼使用 decimal + double 雙型別?**  金額需四位精度與避免二進位浮點累積；統計/指數運算用 double 速度快且數值庫依賴 double。

**Q: Deep ITM/OTM 如何處理?**  當 |d1| 超過閾值直接回傳折現內含價值 (避免常態 CDF underflow)。

**Q: Strike 求解失敗怎麼辦?**  可能導數過低或目標 Coupon 不合理；建議調整初始猜測或使用梯形比較。

**Q: 曲線展期遇異常?**  先檢查輸入市場工具排序與 Tenor 重複；Swap 可能需迭代回退。

**Q: Bus/252 是否含市場假日?**  目前僅排除週末；可擴充交易所行事曆介面。

---

## 12. 未來擴充方向

- Vol Surface Bootstrapping（Smile 參數化）
- Barrier / KI / KO 結構化產品
- 交易生命週期管理
- 行事曆與假日支援
- 分散式計算（批量報價）
- CVA/DVA 對手風險評估
- 高階 Greeks（Vanna / Volga）
- 曲線/曲面序列化與外部資料匯入

---

## 13. 版本資訊

**版本**: 3.0  
**最後更新**: 2025-11-24  
**Runtime**: .NET 8  
**授權**: 參見根目錄 `LICENSE.txt`

---

## 14. 相關文件

- [依賴注入使用指南](./DependencyInjection-Guide.md) - DI 設定與最佳實踐
- [架構文件](./Architecture-Review.md) - 完整架構說明與 SOLID 設計
