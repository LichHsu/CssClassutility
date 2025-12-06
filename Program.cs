using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CssClassutility.Testing;
using CssClassutility.Operations;
using CssClassutility.Models;
using CssClassutility.Core;
using CssClassutility.AI;
using Lichs.MCP.Core;
using Lichs.MCP.Core.Attributes;

namespace CssClassutility;

public partial class Program
{
    private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mcp_debug_log.txt");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions _jsonPrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        if (args.Length > 0 && args[0] == "--test")
        {
            TestRunner.RunAllTests();
            return;
        }

        var server = new McpServer("CssClassutility", "2.0.0");
        server.RegisterToolsFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
        await server.RunAsync(args);
    }

    // =========================================================================================
    // Core Tools (Consolidated)
    // =========================================================================================

    [McpTool("analyze_css", "對 CSS 進行分析，支援變數建議 (Variables)、元件分組 (Components) 與 缺失檢查 (Missing)。")]
    public static string AnalyzeCss(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("分析類型 (Variables, Components, Missing)")] string analysisType,
        [McpParameter("選用參數 (JSON): { threshold: int, classesToCheck: [] }", false)] string optionsJson = "{}")
    {
        var options = JsonSerializer.Deserialize<JsonElement>(optionsJson);
        
        if (analysisType.Equals("Variables", StringComparison.OrdinalIgnoreCase))
        {
            int threshold = 3;
            if (options.TryGetProperty("threshold", out var t)) threshold = t.GetInt32();
            var results = CssVariableSuggester.SuggestVariables(path, threshold);
            return JsonSerializer.Serialize(results, _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Components", StringComparison.OrdinalIgnoreCase))
        {
            var results = CssComponentGrouper.GroupComponents(path);
            return JsonSerializer.Serialize(results, _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Missing", StringComparison.OrdinalIgnoreCase))
        {
            var classesToCheck = new List<string>();
            if (options.TryGetProperty("classesToCheck", out var cArray) && cArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cArray.EnumerateArray()) classesToCheck.Add(item.GetString() ?? "");
            }
            var result = CssConsistencyChecker.CheckMissingClasses(path, classesToCheck);
            return JsonSerializer.Serialize(result, _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Usage", StringComparison.OrdinalIgnoreCase))
        {
            string className = "";
            string projectRoot = "";
            if (options.TryGetProperty("className", out var cn)) className = cn.GetString() ?? "";
            if (options.TryGetProperty("projectRoot", out var pr)) projectRoot = pr.GetString() ?? "";
            
            // 需確保 UsageTracer 命名空間正確引用
            // UsageTracer 在 CssClassutility.AI 命名空間 (已引用)
            var result = UsageTracer.TraceCssUsage(className, projectRoot);
            return JsonSerializer.Serialize(result, _jsonPrettyOptions);
        }

        throw new ArgumentException($"未知的分析類型: {analysisType}");
    }

    [McpTool("edit_css", "批次編輯 CSS (在記憶體中執行 Set/Remove/Merge 操作後一次寫回)。")]
    public static string EditCss(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("操作列表 (JSON array): [{ 'op': 'Set'/'Remove'/'Merge', 'className': '...', ... }]")] string operationsJson)
    {
        // 這是原本 process_css_in_memory 的邏輯，但改名為更直覺的 edit_css
        var processor = new InMemoryCssProcessor(path);
        var operations = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(operationsJson, _jsonOptions);
        var results = new List<string>();

        if (operations != null)
        {
            foreach (var op in operations)
            {
                if (!op.TryGetValue("op", out string? type)) continue;

                if (type == "Set" || type == "Remove")
                {
                    string className = op.GetValueOrDefault("className", "");
                    string key = op.GetValueOrDefault("key", "");
                    string value = op.GetValueOrDefault("value", "");
                    results.Add(processor.ProcessClass(className, type, key, value));
                }
                else if (type == "Merge")
                {
                    string source = op.GetValueOrDefault("source", "");
                    string strategy = op.GetValueOrDefault("strategy", "Overwrite");
                    results.Add(processor.Merge(source, strategy));
                }
            }
        }

        processor.Save();
        return string.Join("\n", results);
    }

    [McpTool("consolidate_css", "合併多個 CSS 檔案到目標檔案。")]
    public static string ConsolidateCss(
        [McpParameter("目標 CSS 檔案路徑")] string targetPath,
        [McpParameter("來源 CSS 檔案路徑陣列")] string[] sourcePaths,
        [McpParameter("合併策略 (Overwrite, FillMissing)", false)] string strategy = "Overwrite")
    {
        var stratEnum = Enum.Parse<MergeStrategy>(strategy, true);
        return CssMerger.BatchMerge(sourcePaths, targetPath, stratEnum);
    }

    [McpTool("purge_css", "移除未使用的 CSS Class (Dead Code Elimination)。")]
    public static string PurgeCss(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("已使用的 Class 列表 (JSON array)")] string usedClassesJson,
        [McpParameter("允許保留的 Class 列表 (JSON array, e.g. 動態生成的 class)", false)] string allowListJson = "[]")
    {
        var used = JsonSerializer.Deserialize<List<string>>(usedClassesJson, _jsonOptions) ?? new();
        var allow = JsonSerializer.Deserialize<List<string>>(allowListJson, _jsonOptions) ?? new();
        // 原本的 GenerateMinimalCss
        return CssDeadCodeEliminator.GenerateMinimalCss(path, used, allow);
    }

    [McpTool("get_css_info", "取得指定 Class 的詳細結構資訊 (JSON)。")]
    public static string GetCssInfo(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("Class 名稱 (不含點號)")] string className)
    {
        // 原本的 ConvertToCssJson
        var entity = CssParser.ConvertToCssJson(path, className);
        return JsonSerializer.Serialize(entity, _jsonPrettyOptions);
    }

    // Helper for debugging logs if needed
    private static void Log(string message)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n"); } catch { }
    }
}