# DCI Pricing Engine – 開發說明文件 (.NET 8 / C#)

本文件是 **DCI（Dual Currency Investment, 雙幣投資）** 報價與計算引擎的完整開發說明。

> **語言**: C# 12.0  
> **Framework**: .NET 8  
> **設計原則**: Domain 物件 + 計算模組分離，金額/匯率使用 `decimal`，金融模型使用 `double`

---

## 1. 專案概述

### 1.1 核心功能

? **已實現功能**：

1. **期權定價引擎**
   - Black-Scholes 歐式期權定價（股票/指數）
   - Garman-Kohlhagen FX 期權定價
   - 隱含波動度計算（Newton-Raphson 方法）
   - 數值穩定性優化（Deep ITM/OTM 處理）

2. **Greeks 風險指標**
   - Delta（價格敏感度）
   - Gamma（Delta 變化率）
   - Vega（波動度敏感度）
   - Theta（時間衰減）
   - Rho（利率敏感度，本幣/外幣）

3. **DCI 核心計算**
   - DCI 報價引擎（定存利息 + 期權利息）
   - 年化收益率（Coupon）計算
   - 到期 Payoff 計算（Knock-In 判斷）
   - 盈虧分析（vs. 單純定存）

4. **市場數據工具**
   - Spot Bid/Ask/Mid 處理
   - Forward 匯率計算（利率平價理論）
   - Forward Points / Pips 轉換
   - 折現因子計算

5. **進階功能**（本次新增）
   - ? Margin 加成計算（銀行利潤）
   - ? Strike Solver（反推目標 Coupon）
   - ? Day Count Convention（日期計算）
   - ? 市場數據快照（整合定價輸入）
   - ? 情境分析（敏感度測試）

### 1.2 設計目標

- ? **高精度**：金額計算精確到小數點後第 4 位
- ? **高效能**：AggressiveInlining + 零 heap 分配
- ? **數值穩定**：處理極端參數和邊界條件
- ? **可測試性**：100% 單元測試覆蓋率
- ? **可擴展性**：模組化設計，易於加入新功能

### 1.3 非目標（暫不實現）

- ? Trade Lifecycle 管理（booking、amend、cancel）
- ? Vol Surface Bootstrapping（使用 Flat Vol）
- ? Exotic 結構（Snowball、Range Accrual、Autocall）
- ? CVA/DVA/FVA 調整（信用和資金成本）

---

## 2. 金融概念與術語

### 2.1 DCI 結構

**DCI（Dual Currency Investment）** = 外幣定存 + 賣出 FX Put 期權

```
客戶行為：
- 投入本金：10,000 USD
- Strike：30.00 TWD/USD（略低於 Spot 30.50）
- 期限：90 天

到期結果：
[情境 1] Spot ? 30.00 → 領回 10,000 USD + 高利息（年化 8%）
[情境 2] Spot < 30.00 → 以 30.00 被迫轉換成 300,000 TWD + 利息
```

**收益來源**：
- 定存利息：基礎利率（例如 3%）
- 期權利息：賣出 Put 期權的 Premium 折算（例如 5%）
- 總 Coupon：8%（定存 3% + 期權 5%）

### 2.2 關鍵術語

| 術語 | 說明 | 型別 |
|------|------|------|
| **Notional** | 投資本金（外幣） | `decimal` |
| **Spot** | 即期匯率（Bid/Ask/Mid） | `decimal` |
| **Strike** | 履約價（匯率） | `decimal` |
| **Forward** | 遠期匯率（基於利率平價） | `decimal` |
| **Coupon** | 年化收益率 | `double` |
| **Volatility** | 年化波動度 | `double` |
| **Tenor** | 期限（年） | `double` |
| **Margin** | 銀行利潤加成（pips 或 %） | `decimal` |
| **Greeks** | 風險敏感度指標 | `double` |

### 2.3 Bid/Ask/Mid 概念

```
市場報價：
Bid: 30.48  ← 銀行願意買 USD 的價格（客戶賣 USD）
Ask: 30.52  ← 銀行願意賣 USD 的價格（客戶買 USD）
Mid: 30.50  ← 理論中間價（定價基準）

定價邏輯：
- 理論價使用 Mid
- 對客報價根據方向調整（+ Bid/Ask Spread + Margin）
```

---

## 3. 型別與精度策略

### 3.1 型別選擇原則

| 用途 | 型別 | 原因 |
|------|------|------|
| 匯率、金額、利息 | `decimal` | 避免浮點誤差，精確到 4 位小數 |
| 數學模型計算 | `double` | 高效率，支援 Math.* 函數 |
| 波動度、Greeks | `double` | 數學模型輸出 |
| 比率、百分比 | `double` | 通常用於計算，非最終金額 |

### 3.2 轉換規則

```csharp
// ? 允許：decimal → double（進入模型層）
double spotD = (double)spotDecimal;
double price = GarmanKohlhagen.PriceFxOption(spotD, ...);

// ? 允許：double → decimal（回到金額層，四捨五入）
decimal result = Math.Round((decimal)priceD, 4, MidpointRounding.AwayFromZero);

// ? 禁止：金額計算直接用 double
double amount = 10000.0 * 0.1;  // 錯誤！應該用 decimal
```

### 3.3 精度要求

- **匯率精度**：小數點後第 4 位（例如 30.5000）
- **金額精度**：小數點後第 4 位（例如 1,234.5678 USD）
- **利率精度**：小數點後第 4 位（例如 0.0515 = 5.15%）
- **Greeks 精度**：小數點後第 6 位

---

## 4. 專案架構

### 4.1 目錄結構

```
DciCalculator/
├── Algorithms/                    # 核心演算法
│   ├── MathFx.cs                 # 數學函數（Normal CDF/PDF, Discount Factor）
│   ├── BlackScholes.cs           # Black-Scholes 定價
│   ├── GarmanKohlhagen.cs        # Garman-Kohlhagen FX 定價
│   ├── GreeksCalculator.cs       # Greeks 計算
│   ├── MarginCalculator.cs       # Margin 加成計算
│   ├── StrikeSolver.cs           # Strike 反推求解器
│   └── DayCountCalculator.cs     # Day Count Convention
│
├── Models/                        # Domain 物件
│   ├── OptionType.cs             # Call/Put 列舉
│   ├── FxQuote.cs                # Spot Bid/Ask/Mid
│   ├── DciInput.cs               # DCI 輸入參數
│   ├── DciQuoteResult.cs         # DCI 報價結果
│   ├── DciPayoffResult.cs        # DCI 到期回報
│   ├── GreeksResult.cs           # Greeks 結果
│   ├── MarketDataSnapshot.cs     # 市場數據快照
│   └── ScenarioResult.cs         # 情境分析結果
│
├── Calculators/                   # 計算器
│   ├── DciPricer.cs              # DCI 主報價引擎
│   ├── DciPayoffCalculator.cs    # 到期 Payoff 計算
│   └── ScenarioAnalyzer.cs       # 情境分析
│
└── Docs/
    └── DCI.md                     # 本文件

DciCalculator.Tests/
├── BlackScholesTests.cs           # Black-Scholes 測試（21 個測試）
├── GarmanKohlhagenTests.cs        # Garman-Kohlhagen 測試（11 個測試）
├── MarginCalculatorTests.cs       # Margin 測試
├── StrikeSolverTests.cs           # Strike Solver 測試
└── DciPricerTests.cs              # DCI 整合測試
```

### 4.2 核心類別說明

#### 4.2.1 期權定價

**BlackScholes.cs**
```csharp
public static double Price(
    double spot, double strike, double rate,
    double volatility, double timeToMaturity,
    OptionType optionType);

public static double ImpliedVolatility(
    double marketPrice, double spot, double strike,
    double rate, double timeToMaturity,
    OptionType optionType, double initialGuess = 0.3);
```

**GarmanKohlhagen.cs**
```csharp
public static double PriceFxOption(
    double spot, double strike,
    double rDomestic, double rForeign,
    double volatility, double timeToMaturity,
    OptionType optionType);

public static double ImpliedVolatility(...);
```

#### 4.2.2 DCI 計算

**DciPricer.cs**
```csharp
public static DciQuoteResult Quote(DciInput input);
public static DciQuoteResult QuoteWithMargin(
    DciInput input, 
    decimal marginPips);
```

**DciPayoffCalculator.cs**
```csharp
public static DciPayoffResult CalculatePayoff(
    DciInput input,
    DciQuoteResult quoteResult,
    decimal spotAtMaturity);

public static decimal CalculatePnLVsDeposit(
    DciInput input,
    DciPayoffResult payoffResult);
```

#### 4.2.3 新增工具

**MarginCalculator.cs**
```csharp
// 加上 Margin（以 pips 或百分比）
public static decimal ApplyMarginPips(
    decimal theoreticalPrice,
    decimal marginPips,
    decimal spot);

public static decimal ApplyMarginPercent(
    decimal theoreticalPrice,
    decimal marginPercent);
```

**StrikeSolver.cs**
```csharp
// 反推達到目標 Coupon 所需的 Strike
public static decimal SolveStrike(
    DciInput input,
    double targetCoupon,
    decimal strikeGuess);
```

**DayCountCalculator.cs**
```csharp
// 計算兩日期間的年化期間
public static double YearFraction(
    DateTime startDate,
    DateTime endDate,
    DayCountConvention convention);
```

---

## 5. 使用範例

### 5.1 基本 DCI 報價

```csharp
using DciCalculator;
using DciCalculator.Models;

// 1. 建立市場數據
var spotQuote = new FxQuote(Bid: 30.48m, Ask: 30.52m); // Mid = 30.50

// 2. 建立 DCI 輸入
var input = new DciInput(
    NotionalForeign: 10_000m,        // 10,000 USD
    SpotQuote: spotQuote,
    Strike: 30.00m,                  // 30.00 TWD/USD
    RateDomestic: 0.015,             // TWD 1.5%
    RateForeign: 0.05,               // USD 5%
    Volatility: 0.10,                // 10% vol
    TenorInYears: 90.0 / 365.0,      // 90 天
    DepositRateAnnual: 0.03          // 定存 3%
);

// 3. 計算報價
var quote = DciPricer.Quote(input);

// 4. 輸出結果
Console.WriteLine($"本金: {quote.NotionalForeign:N2} USD");
Console.WriteLine($"定存利息: {quote.InterestFromDeposit:N4} USD");
Console.WriteLine($"期權利息: {quote.InterestFromOption:N4} USD");
Console.WriteLine($"總利息: {quote.TotalInterestForeign:N4} USD");
Console.WriteLine($"年化收益率: {quote.CouponAnnual:P2}");
```

### 5.2 加上 Margin

```csharp
// 加上 10 pips 的銀行利潤
decimal marginPips = 10m;
var quoteWithMargin = DciPricer.QuoteWithMargin(input, marginPips);

Console.WriteLine($"原始 Coupon: {quote.CouponAnnual:P2}");
Console.WriteLine($"加 Margin 後: {quoteWithMargin.CouponAnnual:P2}");
```

### 5.3 反推 Strike

```csharp
// 目標：年化收益率 8%
double targetCoupon = 0.08;

decimal optimalStrike = StrikeSolver.SolveStrike(
    input, 
    targetCoupon, 
    strikeGuess: 30.00m
);

Console.WriteLine($"達到 8% Coupon 所需 Strike: {optimalStrike:F4}");
```

### 5.4 計算 Greeks

```csharp
var greeks = GreeksCalculator.CalculateDciGreeks(input);

Console.WriteLine($"Delta: {greeks.Delta:F6}");
Console.WriteLine($"Gamma: {greeks.Gamma:F6}");
Console.WriteLine($"Vega: {greeks.Vega:F6}");
Console.WriteLine($"Theta (daily): {greeks.Theta:F6}");
```

### 5.5 情境分析

```csharp
var scenarios = ScenarioAnalyzer.Analyze(
    input,
    spotShifts: new[] { -10m, -5m, 0m, 5m, 10m },  // ±10 pips
    volShifts: new[] { -0.02, 0.0, 0.02 }          // ±2% vol
);

foreach (var scenario in scenarios)
{
    Console.WriteLine(
        $"Spot {scenario.SpotShift:+0;-0}: " +
        $"Coupon {scenario.Coupon:P2}, " +
        $"PnL {scenario.PnL:N2}"
    );
}
```

---

## 6. 效能與精度保證

### 6.1 效能優化

- ? **方法內聯**：`[MethodImpl(AggressiveInlining)]`
- ? **零 Heap 分配**：純 stack 計算
- ? **消除重複計算**：折現因子、sqrt(T) 等快取
- ? **快速路徑**：Deep ITM/OTM 提前返回
- ? **數值穩定性**：處理極端參數（|d| > 20）

### 6.2 精度驗證

- ? **Put-Call Parity**：32 個測試 100% 通過
- ? **邊界條件**：Near maturity、Zero vol、Deep ITM/OTM
- ? **隱含波動度**：Newton-Raphson 收斂至 0.01%
- ? **匯率精度**：所有金額四捨五入至第 4 位

---

## 7. 擴展計劃

### 7.1 短期計劃（已實現）

- ? Margin 計算
- ? Strike Solver
- ? Day Count Convention
- ? 市場數據快照
- ? 情境分析

### 7.2 中期計劃

- ? 利率曲線（Zero Curve + Interpolation）
- ? Flat Vol Surface（多期限波動度）
- ? Business Day Calendar（假日處理）
- ? Trade 生命週期（Booking、Valuation）

### 7.3 長期計劃

- ? KI/KO 結構（Barrier DCI）
- ? Vol Surface Bootstrapping
- ? CVA/DVA 調整
- ? Exotic 結構（Snowball、Autocall）

---

## 8. 測試策略

### 8.1 單元測試覆蓋

- ? BlackScholesTests（21 個測試）
- ? GarmanKohlhagenTests（11 個測試）
- ? GreeksCalculatorTests
- ? MarginCalculatorTests
- ? StrikeSolverTests
- ? DciPricerTests

### 8.2 測試類型

1. **基本定價測試**：ATM/ITM/OTM Call/Put
2. **Put-Call Parity**：驗證無套利
3. **邊界條件**：Near maturity、Zero vol、Deep ITM/OTM
4. **參數驗證**：負值、極端值、NaN/Infinity
5. **隱含波動度**：反推驗證
6. **實務場景**：USD/TWD DCI 90 天期

---

## 9. 常見問題

### Q1: 為何 Spot 用 decimal 但模型用 double？

**A**: 金額計算需要精確（避免 0.1+0.2≠0.3），但數學模型（log/exp）只能用 double。我們在邊界做型別轉換並四捨五入。

### Q2: Deep ITM/OTM 如何處理？

**A**: 當 |d1| > 20 時，N(d) ? 0 或 1，直接使用近似公式避免精度損失。

### Q3: Margin 如何影響報價？

**A**: Margin 降低期權價值 → 降低期權利息 → 降低總 Coupon。客戶看到的是扣除 Margin 後的收益率。

### Q4: Strike Solver 如何運作？

**A**: 使用 Newton-Raphson 方法，迭代調整 Strike 直到 Coupon 達到目標值（精度 0.01%）。

---

## 10. 參考資料

### 10.1 學術資源

- Hull, J. C. (2018). *Options, Futures, and Other Derivatives* (10th ed.)
- Garman, M. B., & Kohlhagen, S. W. (1983). *Foreign Currency Option Values*

### 10.2 實務資源

- Bloomberg OVML（Option Valuation Models）
- CME FX Options Specifications
- ISDA Definitions（利率和外匯）

---

**文件版本**: 2.0  
**最後更新**: 2024  
**維護者**: DCI Pricing Engine Team
