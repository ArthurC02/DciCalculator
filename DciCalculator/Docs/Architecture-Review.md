# DCI Pricing Engine - 架構文件

**版本**: 3.0  
**最後更新**: 2025-11-24  
**評估範圍**: 完整專案架構與 SOLID 原則合規性

---

##  專案概述

DCI Calculator 是一個專業的雙幣投資（Dual Currency Investment）定價引擎，採用 .NET 8 與 C# 12 開發。專案已完成現代化重構，全面支援依賴注入（DI）與 SOLID 設計原則，同時保留向後相容的靜態 API。

### 核心特性

- **完整 DI 支援**：所有服務可透過 `IServiceCollection` 註冊與注入
- **SOLID 設計**：遵循單一職責、開放封閉、依賴反轉等原則
- **高可測試性**：介面抽象設計，便於單元測試與 Mock
- **向後相容**：保留靜態 API（標記為 Obsolete）供舊程式碼使用
- **測試完整**：154 個單元測試，涵蓋所有核心功能

### 測試狀態

```text
總測試數: 154
通過: 154 (100%)
失敗: 0
```

---

##  架構設計

### 專案結構

```text
DciCalculator/
 Core/Interfaces/              核心介面定義
 Services/                     業務邏輯服務
    Pricing/                  定價相關服務
    Margin/                   保證金計算服務
 PricingModels/                定價模型實作
 Algorithms/                   數值計算核心
 Curves/                       利率曲線
 VolSurfaces/                  波動率曲面
 Factories/                    工廠模式
 Builders/                     建構器模式
 Validation/                   驗證框架
 DayCount/                     日數計算策略
 DependencyInjection/          DI 註冊
 Models/                       領域模型（Immutable Records）
 [舊靜態類別]                  向後相容（已標記 Obsolete）
```

---

##  SOLID 原則評估

### 1. 單一職責原則（SRP） 良好

每個服務類別都有明確且單一的職責：

- `DciPricingEngine` - 專注於 DCI 報價計算
- `GreeksCalculatorService` - 專注於希臘值計算
- `StrikeSolverService` - 專注於行使價反推
- `MarginService` - 專注於保證金計算
- `ScenarioAnalyzerService` - 專注於情境分析

### 2. 開放封閉原則（OCP） 優秀

透過介面與策略模式實現可擴展性：

- **定價模型**：`IPricingModel` 介面，可新增不同定價模型
- **利率曲線**：`IZeroCurve` 介面，支援多種插值方法
- **波動率曲面**：`IVolSurface` 介面，支援平坦與插值曲面
- **日數計算**：策略模式，支援多種日數慣例
- **驗證器**：`IValidator<T>` 介面，可組合多個驗證器

### 3. 里氏替換原則（LSP） 完全合規

所有介面實作都可正確替換，行為一致。

### 4. 介面隔離原則（ISP） 良好

介面設計精簡且職責明確，無冗餘方法。

### 5. 依賴反轉原則（DIP） 完全實現

高層模組依賴於抽象而非具體實作：

```csharp
public class DciPricingEngine : IDciPricingEngine
{
    private readonly IPricingModel _pricingModel;
    
    public DciPricingEngine(IPricingModel pricingModel)
    {
        _pricingModel = pricingModel;
    }
}
```

---

##  設計模式

### 已實作的設計模式

- **依賴注入（Dependency Injection）**
- **策略模式（Strategy Pattern）** - 利率曲線、波動率曲面、日數計算
- **工廠模式（Factory Pattern）** - CurveFactory、VolSurfaceFactory
- **建構器模式（Builder Pattern）** - DciInputBuilder、MarketDataSnapshotBuilder
- **責任鏈模式（Chain of Responsibility）** - ValidationPipeline

---

##  效能與品質

### 效能策略

- **內聯優化**：關鍵路徑使用 `[MethodImpl(AggressiveInlining)]`
- **預先計算**：避免重複計算 `sqrtT`、`volSqrtT`
- **深度 ITM/OTM 優化**：`|d1| > 20` 時直接使用內含價值近似
- **Singleton 服務**：所有服務為無狀態 Singleton，記憶體效率高

### 品質保證

- **154 個單元測試**：涵蓋所有核心功能
- **參數驗證**：嚴格檢查負值、零值、NaN/Infinity
- **不可變資料結構**：使用 `record` 類型確保資料不可變
- **執行緒安全**：所有服務為無狀態，天然執行緒安全

---

##  架構評分

| 評估項目 | 評分 | 說明 |
|---------|------|------|
| **SOLID 原則** | A | 完整實現所有 SOLID 原則 |
| **可測試性** | A | 所有依賴可模擬，便於單元測試 |
| **可維護性** | A | 清晰的分層架構，職責明確 |
| **可擴展性** | A | 介面抽象，支援插件式擴展 |
| **效能** | A | 優化的計算路徑，高效能 |
| **文件完整度** | A | 完整的技術文件與使用指南 |

**總體評分：A（優秀）**

---

##  使用建議

### 新專案

建議使用依賴注入方式：

```csharp
// 1. 註冊服務
services.AddDciServices();

// 2. 建構子注入
public class TradingService
{
    private readonly IDciPricingEngine _pricingEngine;
    
    public TradingService(IDciPricingEngine pricingEngine)
    {
        _pricingEngine = pricingEngine;
    }
}
```

### 現有專案遷移

可分階段遷移：

1. **第一階段**：在應用程式啟動時設置 DI 容器
2. **第二階段**：新功能使用 DI 服務
3. **第三階段**：逐步重構現有程式碼

舊的靜態 API 仍可正常使用（會產生編譯警告）：

```csharp
//  可用但已棄用
var quote = DciPricer.Quote(input);

//  建議使用
var quote = pricingEngine.Quote(input);
```

---

##  相關文件

- [DCI 產品說明](./DCI.md) - DCI 結構與功能說明
- [依賴注入使用指南](./DependencyInjection-Guide.md) - DI 設定與最佳實踐

---

**文件版本**：3.0  
**最後更新**：2025-11-24
