# CssClassUtility - CSS 管理與重構 MCP 伺服器

> **Part of Lichs.MCP Workspace**

`CssClassUtility` 是一個強大的 AI 代理輔助工具，提供超過 30 個專業功能來進行 CSS 的解析、操作、診斷與重構。

本專案基於 **Lichs.MCP.Core** 構建，支援標準 JSON-RPC 協定與自動工具掃描。

## 🌟 核心功能

*   **基礎操作**: 解析 (`get_css_classes`)、修改 (`update_css_class`)、移除 (`remove_css_class`)、比較 (`compare_css_style`)。
*   **JSON 實體管理**: 將 CSS 轉換為 JSON 實體 (`convert_to_css_json`) 以便進行精細操作與版本控制。
*   **進階重構**: 批次合併 (`merge_css_class`, `consolidate_css_files`)、結構診斷 (`diagnosis_css_struct`)、去重 (`get_duplicate_classes`)。
*   **AI 輔助分析**: 
    *   `identify_design_tokens`: 識別可提取的設計 Token (顏色、間距等)。
    *   `trace_css_usage`: 全域追蹤 Class 使用狀況 (支援 HTML/Razor/React/Vue)。
    *   `suggest_css_refactoring`: 提供智能重構建議。
    *   `analyze_css_usage`: 偵測 Unused 與 Undefined Class。
*   **工作階段管理**: 支援記憶體內編輯 (`start_css_session`)，提升批次操作效能。

## 🚀 快速開始

### 環境需求
- .NET 10.0 SDK

### 建置
由於本專案是 Solution 的一部分，建議從根目錄建置：

```bash
cd "d:\Lichs Projects\MCP"
dotnet build Lichs.MCP.slnx
```

### MCP 客戶端配置
```json
{
  "mcpServers": {
    "css-utility": {
      "command": "dotnet",
      "args": ["d:\\Lichs Projects\\MCP\\CssClassUtility\\bin\\Debug\\net10.0\\CssClassutility.dll"]
    }
  }
}
```

## 💻 CLI 模式

保留了常用的 CLI 指令以方便人類使用者直接操作：

- **識別 Token**: `dotnet run -- identify-tokens <path> [minOccurrences]`
- **批次取代**: `dotnet run -- replace-batch <path> <oldValue> <newValue>`
- **檢查遺失**: `dotnet run -- check-missing <cssPath> <classesFile>`

## 📚 工具列表 (部分精選)

詳細工具列表請透過 MCP `tools/list` 指令獲取。

| 工具名稱                  | 描述                            |
| :------------------------ | :------------------------------ |
| `get_css_classes`         | 解析 CSS 檔案並回傳 Class 列表  |
| `update_css_class`        | 新增、修改或移除 CSS 屬性       |
| `trace_css_usage`         | 追蹤 Class 在專案中的使用位置   |
| `analyze_css_usage`       | 全域分析 Unused/Undefined Class |
| `consolidate_css_files`   | 批次合併 CSS 檔案               |
| `suggest_css_refactoring` | 獲取重構建議                    |

---
*Powered by Lichs.MCP.Core*