using System.Text.Json;

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
}
