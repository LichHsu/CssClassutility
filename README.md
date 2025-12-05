# CssClassutility

> 一個強大的 MCP (Model Context Protocol) 伺服器，專為 AI 代理設計，提供完整的 CSS 類別管理與重構功能。

## 🌟 專案簡介

**CssClassutility** 是一個以 C# 開發的 MCP 伺服器，提供 **28 個專業工具**來協助 AI 代理進行 CSS 檔案的解析、操作、診斷與重構。特別適合用於：

- 🔧 CSS 程式碼重構與優化
- 🎨 建立與管理設計系統
- 📊 CSS 品質分析與技術債務管理
- 🔍 追蹤與清理未使用的樣式
- 🤖 AI 輔助的智能 CSS 管理

---

## ✨ 核心功能

### 📋 基礎操作工具（工具 1-12）

| 工具名稱                     | 功能說明                                                 |
| ---------------------------- | -------------------------------------------------------- |
| `get_css_classes`            | 解析 CSS 檔案並回傳所有 Class 定義列表（含精確位置資訊） |
| `update_css_class`           | 直接修改 CSS 檔案中指定 Class 的屬性（新增、更新或刪除） |
| `compare_css_style`          | 語義化比較兩個 CSS 樣式區塊是否相同（忽略空白與順序）    |
| `remove_css_class`           | 安全地移除 CSS Class（自動建立備份並驗證語法）           |
| `convert_to_css_json`        | 將 CSS Class 轉換為 JSON 實體格式（屬性自動排序）        |
| `convert_from_css_json`      | 將 JSON 實體轉換回 CSS 字串                              |
| `merge_css_class`            | 合併 CSS Class 屬性（支援多種策略：覆寫/補齊/移除重複）  |
| `export_css_to_entities`     | 將 CSS 檔案實體化為 JSON 檔案集合                        |
| `import_css_from_entities`   | 從 JSON 實體集合重建 CSS 檔案                            |
| `consolidate_css_files`      | 批次合併目錄中的 CSS 檔案至單一檔案                      |
| `get_css_entity`             | 讀取並解析 CSS 實體 JSON 檔案                            |
| `update_css_entity_property` | 修改 CSS 實體 JSON 檔案的屬性                            |
| `merge_css_entity`           | 合併兩個 CSS 實體 JSON 檔案                              |

### 🔍 進階診斷工具（工具 13-17）

| 工具名稱                    | 功能說明                                              |
| --------------------------- | ----------------------------------------------------- |
| `diagnosis_css_struct`      | 診斷 CSS 結構完整性（檢查大括號配對、偵測重複 Class） |
| `get_duplicate_classes`     | 回傳 CSS 檔案中重複的 Class 列表                      |
| `restructure_css`           | 重構 CSS 檔案（去除多餘空行、按 Class 名稱排序）      |
| `take_css_class`            | 回傳指定 Class 的原始 CSS 文字                        |
| `merge_css_class_from_file` | 從另一個 CSS 檔案合併指定 Class 的屬性                |

### 🤖 AI 輔助與分析工具（工具 18-22）⭐ 新增

| 工具名稱                        | 功能說明                                                       |
| ------------------------------- | -------------------------------------------------------------- |
| `identify_design_tokens`        | 識別可轉換為設計 token 的硬編碼值（顏色、間距、字體等）        |
| `trace_css_usage`               | 追蹤 CSS class 在專案中的使用位置（支援 HTML/Razor/JSX/Vue）   |
| `suggest_css_refactoring`       | 分析 CSS 並提供智能重構建議（提取共用屬性、合併相似 class 等） |
| `batch_replace_property_values` | 在多個 class 中批次替換特定屬性值（支援正則表達式）            |
| `analyze_variable_impact`       | 分析修改某個 CSS 變數會影響哪些 class（包括直接與間接引用）    |

### 📝 CSS 工作階段管理工具（工具 23-28）⭐ 新增

| 工具名稱                     | 功能說明                                     |
| ---------------------------- | -------------------------------------------- |
| `start_css_session`          | 開啟一個新的 CSS 編輯工作階段 (可選載入檔案) |
| `get_css_session`            | 取得指定工作階段的詳細資訊                   |
| `update_css_session_content` | 更新工作階段的 CSS 內容                      |
| `save_css_session`           | 將工作階段的內容儲存至檔案                   |
| `close_css_session`          | 關閉工作階段                                 |
| `list_css_sessions`          | 列出所有活躍的工作階段                       |

### 💻 CLI 命令列模式（CLI Mode）⭐ 新增

除了 MCP 伺服器模式，本工具也支援直接透過命令列執行常用任務：

#### 1. 識別設計 Token
```bash
dotnet run -- identify-tokens --path "path/to/style.css" [minOccurrences]
```

#### 2. 批次取代
```bash
dotnet run -- replace-batch --path "path/to/style.css" --old "#ff0000" --new "var(--red-500)"
```


---

## 🚀 快速開始

### 安裝

1. 確保已安裝 .NET 10.0 SDK
2. 編譯專案：
```bash
dotnet build
```

### 設定 MCP 客戶端

在您的 MCP 客戶端配置中加入：

```json
{
  "mcpServers": {
    "css-utility": {
      "command": "d:\\path\\to\\CssClassutility\\bin\\Debug\\net10.0\\CssClassutility.exe",
      "args": []
    }
  }
}
```

### 測試運行

執行內建測試：
```bash
dotnet run --project CssClassutility.csproj -- --test
```

---

## 📘 使用範例

### 範例 1：識別設計 Token

```json
{
  "name": "identify_design_tokens",
  "arguments": {
    "path": "d:\\project\\styles.css",
    "minOccurrences": 2
  }
}
```

**回傳結果**：
```json
{
  "colors": {
    "#3b82f6": {
      "value": "#3b82f6",
      "occurrences": 5,
      "suggestedTokenName": "--color-primary-500",
      "usedInClasses": ["btn-primary", "link", "badge"]
    }
  },
  "spacings": {
    "16px": {
      "value": "16px",
      "occurrences": 8,
      "suggestedTokenName": "--spacing-4",
      "usedInClasses": ["card", "button", "input"]
    }
  }
}
```

### 範例 2：追蹤 CSS 使用情況

```json
{
  "name": "trace_css_usage",
  "arguments": {
    "className": "btn-primary",
    "projectRoot": "d:\\project"
  }
}
```

**回傳結果**：
```json
{
  "className": "btn-primary",
  "totalOccurrences": 12,
  "locations": [
    {
      "filePath": "d:\\project\\Home.razor",
      "lineNumber": 42,
      "context": "<button class=\"btn-primary\">Submit</button>"
    }
  ]
}
```

### 範例 3：獲取重構建議

```json
{
  "name": "suggest_css_refactoring",
  "arguments": {
    "path": "d:\\project\\theme.css",
    "minPriority": 5
  }
}
```

**回傳結果**：
```json
{
  "filePath": "d:\\project\\theme.css",
  "suggestions": [
    {
      "type": "extract-common-properties",
      "description": "屬性 'padding:20px' 在 5 個 class 中重複出現",
      "affectedClasses": ["card-1", "card-2", "card-3", "card-4", "card-5"],
      "priority": 5
    },
    {
      "type": "use-design-token",
      "description": "發現 8 個可轉換為設計 token 的硬編碼值",
      "priority": 7,
      "details": {
        "colorTokens": 5,
        "spacingTokens": 3
      }
    }
  ]
}
```

### 範例 4：批次替換屬性值

```json
{
  "name": "batch_replace_property_values",
  "arguments": {
    "path": "d:\\project\\styles.css",
    "oldValue": "#333",
    "newValue": "var(--text-primary)",
    "propertyFilter": "color"
  }
}
```

### 範例 5：分析變數影響

```json
{
  "name": "analyze_variable_impact",
  "arguments": {
    "path": "d:\\project\\styles.css",
3. 使用 `batch_replace_property_values` 批次替換硬編碼值為 CSS 變數

### 場景 2：安全重構 CSS

1. 使用 `trace_css_usage` 檢查 class 是否被使用
2. 使用 `suggest_css_refactoring` 獲取重構建議
3. 使用 `merge_css_class` 或 `remove_css_class` 執行重構
4. 使用 `diagnosis_css_struct` 驗證結構完整性

### 場景 3：技術債務管理

1. 定期執行 `suggest_css_refactoring` 掃描專案
2. 根據優先級排序並處理建議
3. 使用 `get_duplicate_classes` 找出重複定義
3. 使用 `get_duplicate_classes` 找出重複定義
4. 使用 `restructure_css` 清理和整理程式碼

### 場景 4：高效能批次編輯

1. 使用 `start_css_session` 將 CSS 檔案載入記憶體
2. 進行多次 `update_css_session_content` 操作 (無需頻繁寫入硬碟)
3. 完成所有修改後，一次性呼叫 `save_css_session`
4. 使用 `close_css_session` 釋放資源

---

## 🛠️ 合併策略說明

在使用 `merge_css_class` 或 `merge_css_entity` 時，可使用以下策略：

- **Overwrite（覆寫）**：來源屬性會覆蓋目標中的同名屬性
- **FillMissing（補齊）**：僅新增目標中缺少的屬性，不覆寫現有屬性
- **PruneDuplicate（移除重複）**：移除目標中與來源相同的屬性

---

## 📊 支援的檔案格式

### CSS 追蹤支援
- `.html` - HTML 檔案
- `.razor` - Blazor Razor 元件
- `.jsx` / `.tsx` - React 元件
- `.vue` - Vue 元件
- `.cshtml` - ASP.NET MVC Razor 視圖
- `.aspx` - ASP.NET WebForms

### 自動排除目錄
- `node_modules`
- `bin` / `obj`
- `.git` / `.vs`
- `wwwroot\lib`

---

## 🔧 開發資訊

### 專案結構

```
CssClassutility/
├── AI/                          # AI 分析與建議邏輯 (DesignTokenAnalyzer, RefactoringAdvisor, UsageTracer)
├── Core/                        # 核心資料模型與比較邏輯 (CssDataModels, CssComparator)
├── Diagnostics/                 # 診斷與結構檢查 (StructureDiagnostic, DuplicateDetector)
├── MCP/                         # MCP 協議模型 (JsonRpcModels)
├── Models/                      # 資料模型定義 (AIModels, DiagnosticModels)
├── Operations/                  # CSS 修改與操作邏輯 (CssUpdater, CssMerger, CssRemover)
├── Testing/                     # 測試執行器 (TestRunner)
├── Program.cs                   # 主程式與 MCP 入口點
├── ToolHandlersExtension.cs     # 工具處理器擴充
└── README.md                    # 專案文件
```

### 技術堆疊

- **.NET 10.0** - 開發平台
- **MCP Protocol 2024-11-05** - Model Context Protocol
- **System.Text.Json** - JSON 序列化
- **System.Text.RegularExpressions** - CSS 解析

### 測試

執行全功能測試（共 23 個測試案例）：
```bash
cd "d:\Lichs Projects\MCP\CssClassutility"
dotnet run -- --test
```

測試會驗證：
- ✅ 所有 28 個 MCP 工具的正確性
- ✅ CSS 解析與修改的準確性
- ✅ JSON 轉換的完整性
- ✅ 診斷與重構功能
- ✅ AI 輔助工具的運作

---

## 📝 版本歷史

### v2.0.0（2025-12-04）
- ✨ 新增 5 個 AI 輔助與分析工具
  - `identify_design_tokens` - 設計 token 識別
  - `trace_css_usage` - CSS 使用追蹤
  - `suggest_css_refactoring` - 智能重構建議
  - `batch_replace_property_values` - 批次屬性替換
  - `analyze_variable_impact` - 變數影響分析
- 🏗️ 專案架構重構：模組化拆分為 Core, AI, Diagnostics, Operations 等目錄
- 📊 總工具數量達到 22 個
- 🔧 改進錯誤處理與日誌記錄

### v2.2.0（2025-12-05）
- ✨ 新增 CLI 命令列支援
  - `identify-tokens` - 設計 Token 識別
  - `replace-batch` - 批次取代
- ✨ 新增 `consolidate_css_files` 工具，支援批次合併與壓縮 CSS

### v2.1.0（2025-12-05）
- ✨ 新增 6 個 CSS 工作階段管理工具
  - `start_css_session`, `get_css_session`, `update_css_session_content`
  - `save_css_session`, `close_css_session`, `list_css_sessions`
- 🚀 支援記憶體內編輯，大幅提升批次操作效能
- 🔄 核心解析邏輯重構，支援純字串內容處理
- 📊 總工具數量達到 29 個

### v1.0.0
- 🎉 初始版本發布
- 📦 17 個核心 CSS 操作工具
- 🔍 診斷與重構功能

---

## 💡 最佳實踐

### 1. 安全操作
- 所有修改操作都會自動建立備份檔案
- 使用 `diagnosis_css_struct` 在大型重構前後驗證結構
- 在刪除 class 前使用 `trace_css_usage` 確認使用情況

### 2. 效能優化
- 對大型專案使用 `minOccurrences` 參數過濾結果
- 使用 `minPriority` 只獲取高優先級建議
- 定期使用 `restructure_css` 維護程式碼品質

### 3. 設計系統管理
- 先使用 `identify_design_tokens` 建立 token 清單
- 使用 `export_css_to_entities` 進行版本控制
- 定期執行 `suggest_css_refactoring` 發現改進機會

---

## 🤝 貢獻

歡迎提交 Issue 和 Pull Request！

---

## 📄 授權

本專案採用 MIT 授權條款。

---

## 🔗 相關資源

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [.NET 文件](https://docs.microsoft.com/dotnet/)

---

**享受使用 CssClassutility 進行 AI 輔助的 CSS 管理！** 🎨✨