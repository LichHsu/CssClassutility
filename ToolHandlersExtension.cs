using CssClassutility.AI;
using CssClassutility.Core;
using CssClassutility.Diagnostics;
using CssClassutility.Models;
using CssClassutility.Operations;
using System.Text.Json;
using Lichs.MCP.Core.Attributes;

namespace CssClassutility;

/// <summary>
/// 新增的 MCP 工具處理器
/// </summary>
public partial class Program
{
    private static readonly JsonSerializerOptions _jsonPrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpTool("diagnosis_css_struct", "診斷 CSS 結構完整性：檢查大括號配對、偵測重複 Class。")]
    public static string DiagnosisCssStruct([McpParameter("CSS 檔案的絕對路徑")] string path)
    {
        var result = CssParser.DiagnosisCssStruct(path);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("get_duplicate_classes", "回傳 CSS 檔案中重複的 Class 列表。")]
    public static string GetDuplicateClasses([McpParameter("CSS 檔案的絕對路徑")] string path)
    {
        var result = CssParser.GetDuplicateClasses(path);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("restructure_css", "重構 CSS 檔案：去除多餘空行、按 Class 名稱排序。")]
    public static string RestructureCss([McpParameter("CSS 檔案的絕對路徑")] string path)
    {
        return CssParser.RestructureCss(path);
    }

    [McpTool("take_css_class", "回傳指定 Class 的原始 CSS 文字。")]
    public static string TakeCssClass(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("Class 名稱 (不含點號)")] string className,
        [McpParameter("若有多個同名 Class，指定第幾個 (0-based，預設 0)", false)] int index = 0)
    {
        return CssParser.TakeCssClass(path, className, index);
    }

    [McpTool("merge_css_class_from_file", "從另一個 CSS 檔案合併指定 Class 的屬性。")]
    public static string MergeCssClassFromFile(
        [McpParameter("目標 CSS 檔案絕對路徑")] string targetPath,
        [McpParameter("目標 Class 名稱")] string targetClassName,
        [McpParameter("來源 CSS 檔案絕對路徑")] string sourcePath,
        [McpParameter("來源 Class 名稱")] string sourceClassName,
        [McpParameter("合併策略 (Overwrite, FillMissing, PruneDuplicate)", false)] string strategy = "Overwrite",
        [McpParameter("目標 Class 索引 (0-based)", false)] int targetIndex = 0,
        [McpParameter("來源 Class 索引 (0-based)", false)] int sourceIndex = 0)
    {
        var stratEnum = Enum.Parse<MergeStrategy>(strategy, true);
        return CssParser.MergeCssClassFromFile(
            targetPath, targetClassName,
            sourcePath, sourceClassName,
            stratEnum, targetIndex, sourceIndex);
    }

    [McpTool("identify_design_tokens", "識別 CSS 檔案中可轉換為設計 token 的值（顏色、間距、字體等），回傳重複值的統計與建議的 token 名稱。")]
    public static string IdentifyDesignTokens(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("最少出現次數才納入建議（預設 2）", false)] int minOccurrences = 2)
    {
        var result = DesignTokenAnalyzer.IdentifyDesignTokens(path, minOccurrences);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("trace_css_usage", "追蹤 CSS class 在專案中的使用位置（支援 HTML/Razor/JSX/Vue），回傳所有使用該 class 的檔案與行號。")]
    public static string TraceCssUsage(
        [McpParameter("要追蹤的 class 名稱（不含點號）")] string className,
        [McpParameter("專案根目錄路徑")] string projectRoot,
        [McpParameter("要搜尋的副檔名（例如 ['.razor', '.html']，預設包含常見格式）", false)] string[]? fileExtensions = null)
    {
        var result = UsageTracer.TraceCssUsage(className, projectRoot, fileExtensions);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("suggest_css_refactoring", "分析 CSS 檔案並提供智能重構建議（提取共用屬性、使用 token、合併相似 class 等）。")]
    public static string SuggestRefactoring(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("最低優先級（1-10，預設 1，數值越高代表越重要）", false)] int minPriority = 1)
    {
        var result = RefactoringAdvisor.SuggestRefactoring(path, minPriority);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("batch_replace_property_values", "在多個 class 中批次替換特定屬性值（支援精確匹配或正則表達式）")]
    public static string BatchReplacePropertyValues(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("要替換的舊值（或正則表達式模式）")] string oldValue,
        [McpParameter("新值")] string newValue,
        [McpParameter("僅替換特定屬性的值（例如 'padding'，可選）", false)] string? propertyFilter = null,
        [McpParameter("是否將 oldValue 視為正則表達式（預設 false）", false)] bool useRegex = false)
    {
        var result = BatchReplacer.BatchReplacePropertyValues(path, oldValue, newValue, propertyFilter, useRegex);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("analyze_variable_impact", "分析修改某個 CSS 變數會影響哪些 class（包括直接與間接引用）")]
    public static string AnalyzeVariableImpact(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("CSS 變數名稱（例如 '--primary-color'）")] string variableName)
    {
        var result = VariableAnalyzer.AnalyzeVariableImpact(path, variableName);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    [McpTool("start_css_session", "開啟一個新的 CSS 編輯工作階段 (可選載入檔案)。")]
    public static string StartCssSession([McpParameter("要載入的 CSS 檔案路徑 (可選)", false)] string? filePath = null)
    {
        var session = CssSessionManager.CreateSession(filePath);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    [McpTool("get_css_session", "取得指定工作階段的詳細資訊。")]
    public static string GetCssSession([McpParameter("工作階段 ID")] string sessionId)
    {
        var session = CssSessionManager.GetSession(sessionId);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    [McpTool("update_css_session_content", "更新工作階段的 CSS 內容。")]
    public static string UpdateCssSessionContent(
        [McpParameter("工作階段 ID")] string sessionId,
        [McpParameter("新的 CSS 內容")] string newContent)
    {
        CssSessionManager.UpdateSessionContent(sessionId, newContent);
        var session = CssSessionManager.GetSession(sessionId);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    [McpTool("save_css_session", "將工作階段的內容儲存至檔案。")]
    public static string SaveCssSession(
        [McpParameter("工作階段 ID")] string sessionId,
        [McpParameter("儲存目標路徑 (若未指定則使用原始路徑)", false)] string? targetPath = null)
    {
        CssSessionManager.SaveSession(sessionId, targetPath);
        return $"工作階段 {sessionId} 已儲存至 {(targetPath ?? "原始路徑")}";
    }

    [McpTool("close_css_session", "關閉工作階段。")]
    public static string CloseCssSession([McpParameter("工作階段 ID")] string sessionId)
    {
        CssSessionManager.CloseSession(sessionId);
        return $"工作階段 {sessionId} 已關閉";
    }

    [McpTool("list_css_sessions", "列出所有活躍的工作階段。")]
    public static string ListCssSessions()
    {
        var sessions = CssSessionManager.ListSessions();
        return JsonSerializer.Serialize(sessions, _jsonPrettyOptions);
    }

    [McpTool("consolidate_css_files", "批次合併多個 CSS 檔案到目標檔案。")]
    public static string ConsolidateCssFiles(
        [McpParameter("目標 CSS 檔案路徑")] string targetPath,
        [McpParameter("來源 CSS 檔案路徑列表 (可選)", false)] string[]? sourcePaths = null,
        [McpParameter("包含來源路徑的檔案 (JSON 陣列或純文字行) (優先於 sourcePaths)", false)] string? sourcePathsFile = null,
        [McpParameter("來源目錄路徑 (可選)", false)] string? sourceDirectory = null,
        [McpParameter("合併策略 (Overwrite, FillMissing)", false)] string strategy = "Overwrite")
    {
        var stratEnum = Enum.Parse<MergeStrategy>(strategy, true);

        // Check for file-based input first (Standardization)
        if (!string.IsNullOrEmpty(sourcePathsFile))
        {
             return CssMerger.BatchMerge(sourcePathsFile, targetPath, stratEnum);
        }

        var pathsList = new List<string>();

        if (sourcePaths != null)
        {
            pathsList.AddRange(sourcePaths);
        }

        if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            pathsList.AddRange(Directory.GetFiles(sourceDirectory, "*.css"));
        }

        if (pathsList.Count == 0)
        {
            throw new ArgumentException("必須提供 sourcePaths, sourcePathsFile 或 sourceDirectory");
        }

        return CssMerger.BatchMerge(pathsList.Distinct(), targetPath, stratEnum);
    }

    [McpTool("analyze_css_usage", "全域分析 CSS 使用狀況：比對 CSS 檔案定義與專案中的實際使用，找出 Unused 與 Undefined Class。")]
    public static string AnalyzeCssUsage(
        [McpParameter("來源 CSS 檔案路徑")] string cssPath,
        [McpParameter("要掃描的專案根目錄")] string projectRoot,
        [McpParameter("要掃描的副檔名 (預設 .razor, .html)", false)] string[]? fileExtensions = null,
        [McpParameter("要忽略的目錄名稱 (預設 bin, obj, node_modules)", false)] string[]? ignorePaths = null)
    {
        return JsonSerializer.Serialize(CssUsageAnalyzer.AnalyzeUsage(cssPath, projectRoot, fileExtensions, ignorePaths), _jsonPrettyOptions);
    }

    [McpTool("check_missing_classes", "檢查提供的 Class 列表是否存在於指定的 Global CSS 檔案中。")]
    public static string CheckMissingClasses(
        [McpParameter("Global/Theme CSS 檔案路徑")] string cssPath,
        [McpParameter("要檢查的 Class 列表 (可選)", false)] string[]? classes = null,
        [McpParameter("包含 Class 列表的檔案路徑 (JSON 陣列或純文字行) (優先於 classes)", false)] string? classesFilePath = null)
    {
        var classesList = new List<string>();

        if (!string.IsNullOrEmpty(classesFilePath) && File.Exists(classesFilePath))
        {
            try { classesList.AddRange(JsonSerializer.Deserialize<string[]>(File.ReadAllText(classesFilePath)) ?? Array.Empty<string>()); }
            catch { classesList.AddRange(File.ReadLines(classesFilePath)); }
        }
        else if (classes != null)
        {
            classesList.AddRange(classes);
        }
        
        var result = CssConsistencyChecker.CheckMissingClasses(cssPath, classesList);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }
}
