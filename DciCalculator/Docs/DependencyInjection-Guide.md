# DCI Calculator 依賴注入使用指南

**版本**: 3.0  
**最後更新**: 2025-11-24

---

## 概述

DCI Calculator 完全支援依賴注入 (Dependency Injection, DI)，所有核心服務都已重構為可注入的實例服務，同時保留了向後相容的靜態 API。

## 快速開始

### 1. 安裝相依套件

專案已包含必要的 NuGet 套件：

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

### 2. 註冊服務

使用 `AddDciServices()` 擴展方法註冊所有 DCI 服務：

```csharp
using DciCalculator.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// 註冊所有 DCI 服務（使用預設的 Garman-Kohlhagen 定價模型）
services.AddDciServices();

var serviceProvider = services.BuildServiceProvider();
```

### 3. 使用服務

透過建構子注入或直接從 `ServiceProvider` 取得服務：

```csharp
using DciCalculator.Core.Interfaces;
using DciCalculator.Models;

// 方法 1: 建構子注入（推薦）
public class TradingService
{
    private readonly IDciPricingEngine _pricingEngine;
    private readonly IGreeksCalculator _greeksCalculator;
    
    public TradingService(
        IDciPricingEngine pricingEngine,
        IGreeksCalculator greeksCalculator)
    {
        _pricingEngine = pricingEngine;
        _greeksCalculator = greeksCalculator;
    }
    
    public void ProcessTrade()
    {
        var input = new DciInput(/* ... */);
        var quote = _pricingEngine.Quote(input);
        var greeks = _greeksCalculator.CalculateDciGreeks(input, quote);
    }
}

// 方法 2: 直接取得（測試或主控台應用程式）
var pricingEngine = serviceProvider.GetRequiredService<IDciPricingEngine>();
var quote = pricingEngine.Quote(input);
```

## 已註冊的服務

所有服務都註冊為 **Singleton** 生命週期（無狀態、執行緒安全、可重用）：

| 介面 | 實作 | 說明 |
|------|------|------|
| `IPricingModel` | `GarmanKohlhagenModel` | FX 選擇權定價模型 |
| `IDciPricingEngine` | `DciPricingEngine` | DCI 結構報價引擎 |
| `IGreeksCalculator` | `GreeksCalculatorService` | Greeks 計算 (Delta, Gamma, Vega, Theta, Rho) |
| `IMarginService` | `MarginService` | 保證金與價差計算 |
| `IStrikeSolver` | `StrikeSolverService` | Strike 反推求解器 |
| `IScenarioAnalyzer` | `ScenarioAnalyzerService` | 情境分析與蒙地卡羅模擬 |
| `IDciPayoffCalculator` | `DciPayoffCalculatorService` | DCI 到期損益計算 |
| `ICurveFactory` | `CurveFactory` | 利率曲線工廠 |
| `IVolSurfaceFactory` | `VolSurfaceFactory` | 波動率曲面工廠 |

## 進階配置

### 自訂定價模型

如果您有自己的定價模型實作，可以使用泛型重載：

```csharp
using DciCalculator.Core.Interfaces;

// 自訂定價模型
public class CustomPricingModel : IPricingModel
{
    // 實作 IPricingModel 介面...
}

// 註冊時指定自訂模型
services.AddDciServices<CustomPricingModel>();
```

### 使用配置器進行細部控制

如需更進階的配置（例如替換特定服務實作），可使用配置器：

```csharp
services.AddDciServices(config =>
{
    // 範例：使用自訂 Greeks 計算器（需自行實作）
    // config.WithGreeksCalculator<MyCustomGreeksCalculator>();
});
```

## 測試最佳實踐

### 單元測試使用 Mock

使用 Moq 或 NSubstitute 建立 Mock 服務：

```csharp
using Moq;
using Xunit;
using DciCalculator.Core.Interfaces;
using DciCalculator.Services.Pricing;

public class DciPricingEngineTests
{
    [Fact]
    public void Quote_ShouldUseInjectedPricingModel()
    {
        // Arrange
        var mockPricingModel = new Mock<IPricingModel>();
        mockPricingModel
            .Setup(m => m.PriceFxOption(It.IsAny</*...*/>())) 
            .Returns(100.0);
        
        var engine = new DciPricingEngine(mockPricingModel.Object);
        var input = new DciInput(/* ... */);
        
        // Act
        var quote = engine.Quote(input);
        
        // Assert
        mockPricingModel.Verify(m => m.PriceFxOption(/*...*/), Times.Once);
    }
}
```

### 整合測試使用真實服務

```csharp
[Fact]
public void IntegrationTest_EndToEndQuote()
{
    // 建立真實的 DI 容器
    var services = new ServiceCollection();
    services.AddDciServices();
    var provider = services.BuildServiceProvider();
    
    // 取得服務並執行完整流程
    var engine = provider.GetRequiredService<IDciPricingEngine>();
    var greeks = provider.GetRequiredService<IGreeksCalculator>();
    
    var input = CreateTestInput();
    var quote = engine.Quote(input);
    var dciGreeks = greeks.CalculateDciGreeks(input, quote);
    
    Assert.True(quote.CouponAnnual > 0);
}
```

## 向後相容性

所有舊的靜態 API 仍然可用，但已標記為 `[Obsolete]` 並會產生編譯警告：

```csharp
// ⚠️ 舊 API（仍可使用但不建議）
var quote = DciPricer.Quote(input);  // 產生警告

// ✅ 新 API（建議使用）
var engine = serviceProvider.GetRequiredService<IDciPricingEngine>();
var quote = engine.Quote(input);
```

靜態類別內部已改為委派到新的服務實例，因此不會影響現有功能。

## 遷移指南

### 從靜態 API 遷移

| 舊寫法 (靜態) | 新寫法 (DI) |
|--------------|------------|
| `DciPricer.Quote(input)` | `_pricingEngine.Quote(input)` |
| `GarmanKohlhagen.PriceFxOption(...)` | `_pricingModel.PriceFxOption(...)` |
| `GreeksCalculator.CalculateDciGreeks(...)` | `_greeksCalculator.CalculateDciGreeks(...)` |
| `StrikeSolver.SolveStrike(...)` | `_strikeSolver.SolveStrike(...)` |
| `ScenarioAnalyzer.Analyze(...)` | `_scenarioAnalyzer.Analyze(...)` |

### 分步遷移策略

1. **第一階段：引入 DI 容器**
   - 在應用程式啟動時設置 `ServiceCollection`
   - 註冊 DCI 服務
   - 現有程式碼繼續使用靜態 API（無需修改）

2. **第二階段：新功能使用 DI**
   - 所有新開發的功能使用注入的服務
   - 逐步重構高價值模組

3. **第三階段：全面遷移**
   - 批量重構現有程式碼
   - 移除對靜態 API 的依賴
   - 最終移除 `[Obsolete]` 的靜態類別

## 效益

### 1. 可測試性

- 可使用 Mock/Stub 進行單元測試
- 不再依賴靜態類別，易於隔離測試

### 2. 可擴展性

- 可輕鬆替換定價模型或計算邏輯
- 支援多種實作共存

### 3. 可維護性

- 明確的相依性關係
- 遵循 SOLID 原則（DIP, OCP）

### 4. 執行緒安全

- Singleton 服務無狀態，自然執行緒安全
- 無需手動管理靜態鎖

## 效能考量

- **服務註冊**：一次性成本，應用程式啟動時執行
- **服務解析**：Singleton 服務只建立一次，後續存取效能與靜態類別相同
- **記憶體使用**：每個服務只有一個實例，記憶體佔用最小

## 常見問題

### Q: 為什麼使用 Singleton 而非 Transient？

A: DCI 計算服務都是無狀態的純函數，使用 Singleton 可提供最佳效能且保證執行緒安全。

### Q: 舊的靜態 API 何時移除？

A: 計劃在下一個主要版本 (v4.0) 移除，給予充足的遷移時間。當前版本 (v3.0) 仍完全支援靜態 API。

### Q: 可以混用靜態 API 和 DI API 嗎？

A: 可以，但不建議。建議統一使用 DI 方式以獲得最佳的架構品質。

### Q: 如何在主控台應用程式使用？

A: 參考「快速開始」章節，使用 `ServiceCollection` 和 `ServiceProvider`。

## 範例專案

完整範例請參考：

- 單元測試：`DciCalculator.Tests` 專案
- 整合測試：`Phase4IntegrationTests.cs`

## 相關文件

- [架構文件](./Architecture-Review.md) - 專案架構設計與 SOLID 原則評估
- [DCI 產品說明](./DCI.md) - DCI 結構與技術文件

---

**文件版本**：3.0  
**最後更新**：2025-11-24
