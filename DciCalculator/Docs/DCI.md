# DCI Pricing Engine – 開發技術總覽 (.NET 8 / C#)

本專案定位為 **DCI（Dual Currency Investment，雙幣投資）** 報價與風險分析引擎，提供定價、Greeks、情境分析與情境結果（Scenario）輸出。

> **語言**: C# 12.0  
> **Framework**: .NET 8  
> **設計哲學**: Domain 模型 + 高效計算；金額/匯率使用 `decimal`，統計/數值運算使用 `double`

---

## 1. 功能概述

### 1.1 核心模組

1. **期權定價**  
   - Black-Scholes（股權 / 一般期權）  
   - Garman-Kohlhagen（FX 期權）  
   - 隱含波動率反推（Newton‑Raphson）  
   - 極端情境處理（Deep ITM / OTM）
2. **Greeks 計算**：Delta / Gamma / Vega / Theta / Rho  
3. **DCI 特化邏輯**：存款利息 + 期權溢價組合、Coupon 計算、Knock-In payoff、與標準存款比較  
4. **市場資料處理**：Spot Bid/Ask/Mid、Forward 計算、Forward Points / Pips、基礎年化處理  
5. **延伸計畫（研擬中）**：Margin 調整、Strike Solver 強化、Day Count Convention 完整支持、外部市場快照、情境結果視覺化

### 1.2 設計準則

- 精度：金額與匯率四位小數（必要時可擴充）  
- 內聯：適度使用 `MethodImplOptions.AggressiveInlining`  
- 邊界：嚴格驗證輸入（負值、零值、NaN）  
- 高可測性：100% 針對公開邏輯可覆蓋  
- 可擴充：以純函式與 Immutable 模型為主

### 1.3 後續延伸（需求池）

- Trade Lifecycle（booking / amend / cancel）  
- Vol Surface Bootstrapping（由市場點建構）  
- Exotic 結構（Snowball / Range Accrual / Autocall）  
- CVA/DVA/FVA 初步框架

---

## 2. DCI 結構與名詞

### 2.1 DCI 定義

**DCI = 外幣存款（本金 + 存款利息） + 賣出一枚 FX Put（收取期權溢價）**

```
流程示意：
1. 輸入：Notional 10,000 USD；Spot = 30.50；Strike = 30.00；Tenor = 90 天
2. 結果：
   [情境 1] 到期 Spot ≥ 30.00 → 取回 10,000 USD + 存款利息（例：8% 年化折算）
   [情境 2] 到期 Spot < 30.00 → 以 Strike 交割成等值 TWD（Strike * Notional）+ 利息
```

**收益組成**：

- 存款利息（例：3%）  
- 期權溢價（Put Premium，例：5%）  
- 年化 Coupon ≈ 存款 + 期權溢價

### 2.2 主要欄位

| 欄位 | 說明 | 型別 |
|------|------|------|
| Notional | 外幣本金 | `decimal` |
| Spot | 現貨匯率（Bid / Ask / Mid） | `decimal` |
| Strike | 契約履約價 | `decimal` |
| Forward | 期限遠期匯率 | `decimal` |
| Coupon | 年化綜合收益率 | `double` |
| Volatility | 年化波動率 | `double` |
| Tenor | 年限（年） | `double` |
| Margin | 調整（pips 或 %） | `decimal` |
| Greeks | 風險敏感度 | `double` |

### 2.3 Bid / Ask / Mid

```
範例：
Bid: 30.48 → 可賣出 USD 換得 TWD
Ask: 30.52 → 可買入 USD 支付 TWD
Mid: 30.50 → 估價中間價

策略：模型估價以 Mid 為主；報價對客戶再加上 Spread + Margin。
```

---

## 3. 型別與精度策略

### 3.1 分類摘要

| 類別 | 說明 | 備註 |
|------|------|------|
| 金額 / 匯率 / 期權價格 | `decimal` | 四位或必要進位 |
| 數值統計 / 指數 / CDF | `double` | 使用 `Math.*` |
| 波動率 / Greeks | `double` | 數值結果 |
| 利率 / 日數換算 | `double` | 基於連續或單利 |

### 3.2 轉換範例

```csharp
// decimal → double（高斯 CDF）
double spotD = (double)spotDecimal;
double price = GarmanKohlhagen.PriceFxOption(spotD, ...);

// double → decimal（展示四位精度）
decimal shown = Math.Round((decimal)price, 4, MidpointRounding.AwayFromZero);

// 金額計算保持 decimal
decimal interest = notional * (decimal)couponRate;
```

### 3.3 精度需求

- Spot：四位（例：30.5000）  
- 金額：四位（例：1,234.5678 USD）  
- 利率：四位（例：0.0515 = 5.15%）  
- Greeks：六位或更多視需求

---

## 4. 專案結構

### 4.1 目錄

```
DciCalculator/
  Algorithms/          # 核心算法
    MathFx.cs          # Normal CDF/PDF, Discount Factor
    BlackScholes.cs    # Black-Scholes 定價
    GarmanKohlhagen.cs # Garman-Kohlhagen FX 定價
    GreeksCalculator.cs# Greeks 計算
    MarginCalculator.cs# Margin 調整
    StrikeSolver.cs    # Strike 反求
    DayCountCalculator.cs # 日數換算

  Models/              # Domain 模型
    OptionType.cs
    FxQuote.cs
    DciInput.cs
    DciQuoteResult.cs
    DciPayoffResult.cs
    GreeksResult.cs
    MarketDataSnapshot.cs
    ScenarioResult.cs

  Calculators/         # 聚合邏輯
    DciPricer.cs
    DciPayoffCalculator.cs
    ScenarioAnalyzer.cs

  Docs/
    DCI.md

DciCalculator.Tests/
  BlackScholesTests.cs
  GarmanKohlhagenTests.cs
  MarginCalculatorTests.cs
  StrikeSolverTests.cs
  （其餘待擴充）
```

### 4.2 重要介面摘要

**BlackScholes.cs**

```csharp
double Price(double spot, double strike, double rate,
             double volatility, double timeToMaturity,
             OptionType optionType);

double ImpliedVolatility(double marketPrice, double spot, double strike,
                         double rate, double timeToMaturity,
                         OptionType optionType, double initialGuess = 0.3);
```

**GarmanKohlhagen.cs**

```csharp
double PriceFxOption(double spot, double strike,
                     double rDomestic, double rForeign,
                     double volatility, double timeToMaturity,
                     OptionType optionType);
double ImpliedVolatility(...);
```

**DciPricer.cs**

```csharp
DciQuoteResult Quote(DciInput input);
DciQuoteResult QuoteWithMargin(DciInput input, decimal marginPips);
```

**StrikeSolver.cs**

```csharp
decimal SolveStrike(DciInput input, double targetCoupon, decimal strikeGuess);
```

**DayCountCalculator.cs**

```csharp
double YearFraction(DateTime startDate, DateTime endDate, DayCountConvention convention);
```

---

## 5. 使用範例

### 5.1 建立輸入並估價

```csharp
var spotQuote = new FxQuote(Bid: 30.48m, Ask: 30.52m); // Mid ≈ 30.50
var input = new DciInput(
    NotionalForeign: 10_000m,
    SpotQuote: spotQuote,
    Strike: 30.00m,
    RateDomestic: 0.015,
    RateForeign: 0.05,
    Volatility: 0.10,
    TenorInYears: 90.0 / 365.0,
    DepositRateAnnual: 0.03);

var quote = DciPricer.Quote(input);
Console.WriteLine($"外幣本金: {quote.NotionalForeign:N2} USD");
Console.WriteLine($"存款利息: {quote.InterestFromDeposit:N4} USD");
Console.WriteLine($"期權溢價: {quote.InterestFromOption:N4} USD");
Console.WriteLine($"總收益: {quote.TotalInterestForeign:N4} USD");
Console.WriteLine($"年化 Coupon: {quote.CouponAnnual:P2}");
```

### 5.2 加上 Margin

```csharp
decimal marginPips = 10m;
var withMargin = DciPricer.QuoteWithMargin(input, marginPips);
Console.WriteLine($"原始 Coupon: {quote.CouponAnnual:P2}");
Console.WriteLine($"調整後 Coupon: {withMargin.CouponAnnual:P2}");
```

### 5.3 反求 Strike（目標 Coupon）

```csharp
double targetCoupon = 0.08; // 8%
decimal optimalStrike = StrikeSolver.SolveStrike(input, targetCoupon, 30.00m);
Console.WriteLine($"達成 8% Coupon 的 Strike: {optimalStrike:F4}");
```

### 5.4 Greeks

```csharp
var greeks = GreeksCalculator.CalculateDciGreeks(input);
Console.WriteLine($"Delta: {greeks.Delta:F6}");
Console.WriteLine($"Gamma: {greeks.Gamma:F6}");
Console.WriteLine($"Vega : {greeks.Vega:F6}");
Console.WriteLine($"Theta: {greeks.Theta:F6}");
```

### 5.5 情境分析

```csharp
var scenarios = ScenarioAnalyzer.Analyze(
    input,
    spotShifts: new[] { -10m, -5m, 0m, 5m, 10m },
    volShifts: new[] { -0.02, 0.00, 0.02 });

foreach (var s in scenarios)
    Console.WriteLine($"Spot Shift {s.SpotShift:+0;-0}: Coupon {s.Coupon:P2}, PnL {s.PnL:N2}");
```

---

## 6. 效能與品質

### 6.1 效能策略

- 適度內聯（Critical path）
- 減少臨時配置（stack vs heap）
- 極端值近似（|d| > 20 → CDF≈0 / 1）
- Deep ITM/OTM 近似：以折現內含價值
- 邊界參數快回傳 / 避免迴圈

### 6.2 品質檢核

- Put-Call Parity 全覆蓋場景  
- Near maturity / Zero vol / Deep ITM/OTM 行為  
- Implied Volatility 收斂（誤差 < 0.01%）  
- Spot / Strike / Vol / Tenor 輸入驗證  
- FX 場景（USD/TWD 90 天）

---

## 7. 演進路線

### 7.1 基礎（短期）

- Margin 邏輯拆分  
- Strike Solver 改良  
- Day Count Convention 增補  
- 市場快照擴充  
- 情境分析輸出格式

### 7.2 進階（中期）

- Zero Curve + 插值  
- Flat / Interpolated Vol Surface  
- Business Day Calendar  
- Trade Valuation  

### 7.3 高階（長期）

- Barrier / KI / KO DCI  
- Vol Surface Bootstrapping  
- CVA / DVA  
- Snowball / Autocall 支援

---

## 8. 測試概況

### 8.1 覆蓋目標

- BlackScholesTests（期權定價）  
- GarmanKohlhagenTests（FX 定價）  
- GreeksCalculatorTests  
- MarginCalculatorTests  
- StrikeSolverTests  
- DciPricerTests

### 8.2 測試類型

1. 基本定價（ATM / ITM / OTM）  
2. Put-Call Parity  
3. 邊界行為（Near maturity / Zero vol / Deep ITM/OTM）  
4. 異常參數（負值 / NaN / Infinity）  
5. 隱含波動率反推  
6. FX 場景（USD/TWD）

---

## 9. 常見問答

**Q1: 為何 Spot 用 decimal，運算最後又轉 double？**  
A: 金額與匯率保持 decimal 精度；統計函式（log / exp / CDF）使用 double 避免性能損失，轉換集中且四捨五入後再輸出。  

**Q2: Deep ITM/OTM 如何處理？**  
A: |d1| > 20 時 CDF 近似 0/1，直接回傳折現內含價值減少運算。  

**Q3: Margin 對收益的影響？**  
A: Coupon = 存款利息 + 期權溢價（已含 Spread + Margin）；Margin 調整主要影響報價層的最終收益。  

**Q4: Strike Solver 收斂機制？**  
A: 使用 Newton‑Raphson；誤差 < 0.01% 時停止，必要時可退回區間二分。  

---

## 10. 參考資料

### 10.1 學術

- Hull, J. C. (2018). *Options, Futures, and Other Derivatives* (10th ed.)
- Garman, M. B., & Kohlhagen, S. W. (1983). *Foreign Currency Option Values*

### 10.2 實務

- Bloomberg OVML (Option Valuation Models)  
- CME FX Options Specs  
- ISDA Definitions（利率與外匯）

---

**版本**: 2.0  
**最後更新**: 2024  
**維護團隊**: DCI Pricing Engine Team
