using System.Text.Json;
using CssClassutility.AI;
using CssClassutility.Models;
using CssClassutility.Core;
using CssClassutility.Operations;

namespace CssClassutility;

/// <summary>
/// 新增的 MCP 工具處理器
/// </summary>
public partial class Program
{
    /// <summary>
    /// 取得新增的工具定義
    /// </summary>
    public static object[] GetExtendedToolDefinitions()
    {
        return
        [
            // 13. diagnosis_css_struct
            new
            {
                name = "diagnosis_css_struct",
                description = "診斷 CSS 結構完整性：檢查大括號配對、偵測重複 Class。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" }
                    },
                    required = new[] { "path" }
                }
            },
            // 14. get_duplicate_classes
            new
            {
                name = "get_duplicate_classes",
                description = "回傳 CSS 檔案中重複的 Class 列表。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" }
                    },
                    required = new[] { "path" }
                }
            },
            // 15. restructure_css
            new
            {
                name = "restructure_css",
                description = "重構 CSS 檔案：去除多餘空行、按 Class 名稱排序。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" }
                    },
                    required = new[] { "path" }
                }
            },
            // 16. take_css_class
            new
            {
                name = "take_css_class",
                description = "回傳指定 Class 的原始 CSS 文字。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        className = new { type = "string", description = "Class 名稱 (不含點號)" },
                        index = new { type = "integer", description = "若有多個同名 Class，指定第幾個 (0-based，預設 0)" }
                    },
                    required = new[] { "path", "className" }
                }
            },
            // 17. merge_css_class_from_file
            new
            {
                name = "merge_css_class_from_file",
                description = "從另一個 CSS 檔案合併指定 Class 的屬性。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        targetPath = new { type = "string", description = "目標 CSS 檔案絕對路徑" },
                        targetClassName = new { type = "string", description = "目標 Class 名稱" },
                        sourcePath = new { type = "string", description = "來源 CSS 檔案絕對路徑" },
                        sourceClassName = new { type = "string", description = "來源 Class 名稱" },
                        strategy = new { type = "string", @enum = new[] { "Overwrite", "FillMissing", "PruneDuplicate" } },
                        targetIndex = new { type = "integer", description = "目標 Class 索引 (0-based)" },
                        sourceIndex = new { type = "integer", description = "來源 Class 索引 (0-based)" }
                    },
                    required = new[] { "targetPath", "targetClassName", "sourcePath", "sourceClassName" }
                }
            },
            // 18. identify_design_tokens
            new
            {
                name = "identify_design_tokens",
                description = "識別 CSS 檔案中可轉換為設計 token 的值（顏色、間距、字體等），回傳重複值的統計與建議的 token 名稱。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        minOccurrences = new { type = "integer", description = "最少出現次數才納入建議（預設 2）" }
                    },
                    required = new[] { "path" }
                }
            },
            // 19. trace_css_usage
            new
            {
                name = "trace_css_usage",
                description = "追蹤 CSS class 在專案中的使用位置（支援 HTML/Razor/JSX/Vue），回傳所有使用該 class 的檔案與行號。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "要追蹤的 class 名稱（不含點號）" },
                        projectRoot = new { type = "string", description = "專案根目錄路徑" },
                        fileExtensions = new { type = "array", items = new { type = "string" }, description = "要搜尋的副檔名（例如 ['.razor', '.html']，預設包含常見格式）" }
                    },
                    required = new[] { "className", "projectRoot" }
                }
            },
            // 20. suggest_css_refactoring
            new
            {
                name = "suggest_css_refactoring",
                description = "分析 CSS 檔案並提供智能重構建議（提取共用屬性、使用 token、合併相似 class 等）。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        minPriority = new { type = "integer", description = "最低優先級（1-10，預設 1，數值越高代表越重要）" }
                    },
                    required = new[] { "path" }
                }
            },
            // 21. batch_replace_property_values
            new
            {
                name = "batch_replace_property_values",
                description = "在多個 class 中批次替換特定屬性值（支援精確匹配或正則表達式）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        oldValue = new { type = "string", description = "要替換的舊值（或正則表達式模式）" },
                        newValue = new { type = "string", description = "新值" },
                        propertyFilter = new { type = "string", description = "僅替換特定屬性的值（例如 'padding'，可選）" },
                        useRegex = new { type = "boolean", description = "是否將 oldValue 視為正則表達式（預設 false）" }
                    },
                    required = new[] { "path", "oldValue", "newValue" }
                }
            },
            // 22. analyze_variable_impact
            new
            {
                name = "analyze_variable_impact",
                description = "分析修改某個 CSS 變數會影響哪些 class（包括直接與間接引用）",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        variableName = new { type = "string", description = "CSS 變數名稱（例如 '--primary-color'）" }
                    },
                    required = new[] { "path", "variableName" }
                }
            },
            // 23. start_css_session
            new
            {
                name = "start_css_session",
                description = "開啟一個新的 CSS 編輯工作階段 (可選載入檔案)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "要載入的 CSS 檔案路徑 (可選)" }
                    }
                }
            },
            // 24. get_css_session
            new
            {
                name = "get_css_session",
                description = "取得指定工作階段的詳細資訊。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "工作階段 ID" }
                    },
                    required = new[] { "sessionId" }
                }
            },
            // 25. update_css_session_content
            new
            {
                name = "update_css_session_content",
                description = "更新工作階段的 CSS 內容。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "工作階段 ID" },
                        newContent = new { type = "string", description = "新的 CSS 內容" }
                    },
                    required = new[] { "sessionId", "newContent" }
                }
            },
            // 26. save_css_session
            new
            {
                name = "save_css_session",
                description = "將工作階段的內容儲存至檔案。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "工作階段 ID" },
                        targetPath = new { type = "string", description = "儲存目標路徑 (若未指定則使用原始路徑)" }
                    },
                    required = new[] { "sessionId" }
                }
            },
            // 27. close_css_session
            new
            {
                name = "close_css_session",
                description = "關閉工作階段。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "工作階段 ID" }
                    },
                    required = new[] { "sessionId" }
                }
            },
            // 28. list_css_sessions
            new
            {
                name = "list_css_sessions",
                description = "列出所有活躍的工作階段。",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            // 29. consolidate_css_files
            new
            {
                name = "consolidate_css_files",
                description = "批次合併多個 CSS 檔案到目標檔案。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePaths = new { type = "array", items = new { type = "string" }, description = "來源 CSS 檔案路徑列表 (可選)" },
                        sourceDirectory = new { type = "string", description = "來源目錄路徑 (可選)" },
                        targetPath = new { type = "string", description = "目標 CSS 檔案路徑" },
                        strategy = new { type = "string", @enum = new[] { "Overwrite", "FillMissing" }, description = "合併策略" }
                    },
                    required = new[] { "targetPath" }
                }
            }
        ];
    }

    /// <summary>
    /// 處理新增的工具呼叫
    /// </summary>
    public static string? HandleExtendedToolCall(string name, JsonElement args)
    {
        return name switch
        {
            "diagnosis_css_struct" => HandleDiagnosisCssStruct(args),
            "get_duplicate_classes" => HandleGetDuplicateClasses(args),
            "restructure_css" => HandleRestructureCss(args),
            "take_css_class" => HandleTakeCssClass(args),
            "merge_css_class_from_file" => HandleMergeCssClassFromFile(args),
            "identify_design_tokens" => HandleIdentifyDesignTokens(args),
            "trace_css_usage" => HandleTraceCssUsage(args),
            "suggest_css_refactoring" => HandleSuggestRefactoring(args),
            "batch_replace_property_values" => HandleBatchReplacePropertyValues(args),
            "analyze_variable_impact" => HandleAnalyzeVariableImpact(args),
            "start_css_session" => HandleStartCssSession(args),
            "get_css_session" => HandleGetCssSession(args),
            "update_css_session_content" => HandleUpdateCssSessionContent(args),
            "save_css_session" => HandleSaveCssSession(args),
            "close_css_session" => HandleCloseCssSession(args),
            "list_css_sessions" => HandleListCssSessions(args),
            "consolidate_css_files" => HandleConsolidateCssFiles(args),
            _ => null // 不是擴充工具
        };
    }

    private static string HandleDiagnosisCssStruct(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        var result = CssParser.DiagnosisCssStruct(path);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleGetDuplicateClasses(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        var result = CssParser.GetDuplicateClasses(path);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleRestructureCss(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        return CssParser.RestructureCss(path);
    }

    private static string HandleTakeCssClass(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string className = args.GetProperty("className").GetString()!;
        int index = args.TryGetProperty("index", out var i) ? i.GetInt32() : 0;
        return CssParser.TakeCssClass(path, className, index);
    }

    private static string HandleMergeCssClassFromFile(JsonElement args)
    {
        string targetPath = args.GetProperty("targetPath").GetString()!;
        string targetClassName = args.GetProperty("targetClassName").GetString()!;
        string sourcePath = args.GetProperty("sourcePath").GetString()!;
        string sourceClassName = args.GetProperty("sourceClassName").GetString()!;
        string strategyStr = args.TryGetProperty("strategy", out var s) ? s.GetString() ?? "Overwrite" : "Overwrite";
        int targetIndex = args.TryGetProperty("targetIndex", out var ti) ? ti.GetInt32() : 0;
        int sourceIndex = args.TryGetProperty("sourceIndex", out var si) ? si.GetInt32() : 0;

        var strategy = Enum.Parse<MergeStrategy>(strategyStr, true);
        return CssParser.MergeCssClassFromFile(
            targetPath, targetClassName, 
            sourcePath, sourceClassName, 
            strategy, targetIndex, sourceIndex);
    }

    private static string HandleIdentifyDesignTokens(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        int minOccurrences = args.TryGetProperty("minOccurrences", out var m) ? m.GetInt32() : 2;
        
        var result = DesignTokenAnalyzer.IdentifyDesignTokens(path, minOccurrences);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleTraceCssUsage(JsonElement args)
    {
        string className = args.GetProperty("className").GetString()!;
        string projectRoot = args.GetProperty("projectRoot").GetString()!;
        string[]? extensions = null;
        
        if (args.TryGetProperty("fileExtensions", out var ext))
        {
            var list = new List<string>();
            foreach (var item in ext.EnumerateArray())
            {
                var val = item.GetString();
                if (val != null) list.Add(val);
            }
            extensions = list.ToArray();
        }
        
        var result = UsageTracer.TraceCssUsage(className, projectRoot, extensions);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleSuggestRefactoring(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        int minPriority = args.TryGetProperty("minPriority", out var p) ? p.GetInt32() : 1;
        
        var result = RefactoringAdvisor.SuggestRefactoring(path, minPriority);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleBatchReplacePropertyValues(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string oldValue = args.GetProperty("oldValue").GetString()!;
        string newValue = args.GetProperty("newValue").GetString()!;
        string? propertyFilter = args.TryGetProperty("propertyFilter", out var p) ? p.GetString() : null;
        bool useRegex = args.TryGetProperty("useRegex", out var r) && r.GetBoolean();
        
        var result = BatchReplacer.BatchReplacePropertyValues(path, oldValue, newValue, propertyFilter, useRegex);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleAnalyzeVariableImpact(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string variableName = args.GetProperty("variableName").GetString()!;
        
        var result = VariableAnalyzer.AnalyzeVariableImpact(path, variableName);
        return JsonSerializer.Serialize(result, _jsonPrettyOptions);
    }

    private static string HandleStartCssSession(JsonElement args)
    {
        string? filePath = args.TryGetProperty("filePath", out var f) ? f.GetString() : null;
        var session = CssSessionManager.CreateSession(filePath);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    private static string HandleGetCssSession(JsonElement args)
    {
        string sessionId = args.GetProperty("sessionId").GetString()!;
        var session = CssSessionManager.GetSession(sessionId);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    private static string HandleUpdateCssSessionContent(JsonElement args)
    {
        string sessionId = args.GetProperty("sessionId").GetString()!;
        string newContent = args.GetProperty("newContent").GetString()!;
        CssSessionManager.UpdateSessionContent(sessionId, newContent);
        var session = CssSessionManager.GetSession(sessionId);
        return JsonSerializer.Serialize(session, _jsonPrettyOptions);
    }

    private static string HandleSaveCssSession(JsonElement args)
    {
        string sessionId = args.GetProperty("sessionId").GetString()!;
        string? targetPath = args.TryGetProperty("targetPath", out var t) ? t.GetString() : null;
        CssSessionManager.SaveSession(sessionId, targetPath);
        return $"工作階段 {sessionId} 已儲存至 {(targetPath ?? "原始路徑")}";
    }

    private static string HandleCloseCssSession(JsonElement args)
    {
        string sessionId = args.GetProperty("sessionId").GetString()!;
        CssSessionManager.CloseSession(sessionId);
        return $"工作階段 {sessionId} 已關閉";
    }

    private static string HandleListCssSessions(JsonElement args)
    {
        var sessions = CssSessionManager.ListSessions();
        return JsonSerializer.Serialize(sessions, _jsonPrettyOptions);
    }

    private static string HandleConsolidateCssFiles(JsonElement args)
    {
        string targetPath = args.GetProperty("targetPath").GetString()!;
        string strategyStr = args.TryGetProperty("strategy", out var s) ? s.GetString() ?? "Overwrite" : "Overwrite";
        var strategy = Enum.Parse<MergeStrategy>(strategyStr, true);

        var sourcePaths = new List<string>();

        if (args.TryGetProperty("sourcePaths", out var paths))
        {
            foreach (var item in paths.EnumerateArray())
            {
                var val = item.GetString();
                if (val != null) sourcePaths.Add(val);
            }
        }

        if (args.TryGetProperty("sourceDirectory", out var dir))
        {
            string? dirPath = dir.GetString();
            if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
            {
                sourcePaths.AddRange(Directory.GetFiles(dirPath, "*.css"));
            }
        }

        if (sourcePaths.Count == 0)
        {
            throw new ArgumentException("必須提供 sourcePaths 或 sourceDirectory");
        }

        return CssMerger.BatchMerge(sourcePaths.Distinct(), targetPath, strategy);
    }
}
