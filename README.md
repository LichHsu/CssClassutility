# CssClassUtility (CSS 樣式工具)

這是一個強大的 MCP 伺服器，專門用於分析、處理與合併 CSS 檔案。
支援強型別參數，提升 AI 互動的準確性。

## 可用工具 (Tools)

### 1. `analyze_css`
分析 CSS 內容與使用狀況。
*   **參數**:
    *   `path` (string): 目標 CSS 檔案路徑或目錄。
    *   `analysisType` (string): 分析類型。
        *   `Variables`: 建議從重複值中提取變數。
        *   `Components`: 根據前綴分組組件。
        *   `Missing`: 尋找在 `classesToCheck` 中使用但 CSS 缺失的類別。
        *   `Unused`: 尋找在 CSS 中定義但未在 `KnownUsedClasses` 中出現的類別 (被動式分析)。
        *   `Usage`: 追蹤特定類別在專案中的使用狀況。
    *   `options`: (Object, 選填)
        *   `threshold`: 變數建議的最小頻率閾值。
        *   `classesToCheck`: 檢查列表 (用於 Missing 分析)。
        *   `KnownUsedClasses`: 已知使用的類別列表 (用於 Unused 分析)。
        *   `className`: 目標類別名稱 (用於 Usage 分析)。
        *   `projectRoot`: 搜尋範圍 (用於 Usage 分析)。

### 2. `edit_css`
批次編輯 CSS 檔案 (記憶體內處理)。
*   **參數**:
    *   `path` (string): 目標 CSS 檔案路徑。
    *   `operations`: (List of Objects, 操作列表)
        *   `op`: 操作 (`Set` 設定屬性, `Remove` 移除屬性/類別)。
        *   `className`: 目標 CSS 類別 (例如 `.btn-primary`)。
        *   `key`: CSS 屬性名稱 (例如 `color`)。
        *   `value`: CSS 屬性值。
        *   `source`: 來源檔案路徑 (用於合併操作)。
        *   `strategy`: 合併策略 (`Overwrite`, `FillMissing`)。

### 3. `consolidate_css`
合併多個 CSS 檔案至單一檔案。
*   **參數**:
    *   `sourceFiles` (List<string>): 來源檔案路徑列表。
    *   `outputFile` (string): 輸出路徑。

### 4. `deduplicate_css`
自動合併並清理 CSS 檔案中的重複定義。
*   **參數**:
    *   `path` (string): 目標 CSS 檔案路徑。
    *   **說明**: 若 CSS 中存在選擇器與上下文 (Context) 完全相同的重複區塊，此工具會將其合併為單一區塊，後定義的屬性會覆蓋先定義的屬性。

## 命令列介面 (CLI)

本工具亦支援直接透過 CLI 執行審查：

```bash
# 執行 CSS 審查 (檢查重複定義、空規則等)
CssClassUtility.exe audit css --path "你的 CSS 檔案或目錄"

# 執行 CSS 去重 (清理重複的 Class 定義)
CssClassUtility.exe deduplicate_css --path "你的 CSS 檔案"
```

## 開發與測試
*   執行 `dotnet build` 進行編譯。
*   執行 `CssClassUtility.exe --test` 運行內部單元測試。