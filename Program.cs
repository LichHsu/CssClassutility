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

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        if (args.Length > 0)
        {
            if (args[0] == "--test")
            {
                TestRunner.RunAllTests();
                return;
            }
            if (args[0] == "suggest-vars")
            {
                string path = args[1];
                int threshold = args.Length > 2 ? int.Parse(args[2]) : 3;
                Console.WriteLine(SuggestCssVariables(path, threshold));
                return;
            }
            if (args[0] == "get-components")
            {
                string path = args[1];
                Console.WriteLine(GetCssComponents(path));
                return;
            }
        }

        var server = new McpServer("CssClassutility", "1.0.0");
        server.RegisterToolsFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
        await server.RunAsync(args);
    }

    public static string CompareCssStyle(
        [McpParameter("第一個樣式內容 (不含選擇器和大括號)")] string styleA,
        [McpParameter("第二個樣式內容")] string styleB)
    {
        var result = CssParser.CompareCssStyle(styleA, styleB);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpTool("remove_css_class", "安全地從 CSS 檔案中移除指定的 Class 定義 (自動建立備份，並驗證語法完整性)。")]
    public static string RemoveCssClass(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("要移除的 Class 名稱 (不含點號)")] string className)
    {
        return CssParser.RemoveCssClass(path, className);
    }

    [McpTool("convert_to_css_json", "將 CSS 檔案中的指定 Class 轉換為 JSON 實體格式 (屬性自動排序)。")]
    public static string ConvertToCssJson(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("Class 名稱 (不含點號)")] string className)
    {
        var entity = CssParser.ConvertToCssJson(path, className);
        return JsonSerializer.Serialize(entity, _jsonPrettyOptions);
    }

    [McpTool("convert_from_css_json", "將 JSON 實體轉換為 CSS 字串。")]
    public static string ConvertFromCssJson([McpParameter("JSON 實體字串 (包含 name, selector, properties)")] string entity)
    {
        var entityObj = JsonSerializer.Deserialize<CssEntity>(entity, _jsonOptions)
            ?? throw new Exception("無法解析 JSON 實體");
        return CssParser.ConvertFromCssJson(entityObj);
    }

    [McpTool("merge_css_class", "將來源 Class 的屬性合併到目標 Class (支援多種策略：Overwrite 覆蓋、FillMissing 補齊、PruneDuplicate 移除重複)。")]
    public static string MergeCssClass(
        [McpParameter("目標 CSS 檔案絕對路徑")] string targetPath,
        [McpParameter("目標 Class 名稱")] string targetClassName,
        [McpParameter("來源物件：JSON 檔案路徑，或 'path/to/file.css:.className' 格式")] string sourceObject,
        [McpParameter("合併策略 (Overwrite, FillMissing, PruneDuplicate)", false)] string strategy = "Overwrite")
    {
        var stratEnum = Enum.Parse<MergeStrategy>(strategy, true);
        return CssParser.MergeCssClass(targetPath, targetClassName, sourceObject, stratEnum);
    }

    [McpTool("export_css_to_entities", "將 CSS 檔案實體化為 JSON 檔案集合 (每個 Class 一個 JSON 檔案)。")]
    public static string ExportCssToEntities(
        [McpParameter("來源 CSS 檔案路徑")] string cssPath,
        [McpParameter("實體輸出根目錄 (預設 .\\CssEntities)", false)] string outputRoot = ".\\CssEntities",
        [McpParameter("清理模式 (Keep, DeleteAll, KeepSoftDeleted)", false)] string cleanMode = "Keep")
    {
        return CssParser.ExportCssToEntities(cssPath, outputRoot, cleanMode);
    }

    [McpTool("import_css_from_entities", "從 JSON 實體集合建置 CSS 檔案。")]
    public static string ImportCssFromEntities(
        [McpParameter("實體來源目錄")] string sourceDir,
        [McpParameter("輸出 CSS 檔案路徑")] string outputFile,
        [McpParameter("是否包含軟刪除的實體 (檔名以 _ 開頭)", false)] bool includeSoftDeleted = false)
    {
        return CssParser.ImportCssFromEntities(sourceDir, outputFile, includeSoftDeleted);
    }

    [McpTool("merge_css_entity", "合併兩個 CSS 實體 JSON 檔案。")]
    public static string MergeCssEntity(
        [McpParameter("目標 JSON 實體檔案路徑 (將被修改)")] string targetPath,
        [McpParameter("來源 JSON 實體檔案路徑 (唯讀)")] string sourcePath,
        [McpParameter("合併策略 (Overwrite, FillMissing, PruneDuplicate)", false)] string strategy = "Overwrite")
    {
        var stratEnum = Enum.Parse<MergeStrategy>(strategy, true);
        return CssParser.MergeCssEntity(targetPath, sourcePath, stratEnum);
    }

    [McpTool("process_css_in_memory", "在記憶體中執行一系列 CSS 操作 (Set/Remove/Merge) 是一次性寫回，大幅提升效能。")]
    public static string ProcessCssInMemory(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("操作列表 (JSON array of {op: 'Set'/'Remove'/'Merge', ...})")] string operationsJson)
    {
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

    [McpTool("get_css_components", "識別並分組 CSS 元件 (例如將 .btn, .btn:hover 歸類為同一組)。")]
    public static string GetCssComponents([McpParameter("CSS 檔案路徑")] string path)
    {
        var groups = CssComponentGrouper.GroupComponents(path);
        return JsonSerializer.Serialize(groups, _jsonPrettyOptions);
    }

    [McpTool("suggest_css_variables", "建議可提取為變數的 CSS 值 (例如重複使用的色碼)。")]
    public static string SuggestCssVariables(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("最少重複次數", false)] int threshold = 3)
    {
        var suggestions = CssVariableSuggester.SuggestVariables(path, threshold);
        return JsonSerializer.Serialize(suggestions, _jsonPrettyOptions);
    }

    [McpTool("generate_minimal_css", "生成最小化的 CSS (移除未被使用的 Class)。危險操作：請務必提供 allowList 保護動態 Class。")]
    public static string GenerateMinimalCss(
        [McpParameter("CSS 檔案路徑")] string path,
        [McpParameter("已使用的 Class 列表 (JSON array)")] string usedClassesJson,
        [McpParameter("允許保留的 Class 列表 (JSON array)", false)] string allowListJson = "[]")
    {
        var used = JsonSerializer.Deserialize<List<string>>(usedClassesJson, _jsonOptions) ?? new();
        var allow = JsonSerializer.Deserialize<List<string>>(allowListJson, _jsonOptions) ?? new();
        return CssDeadCodeEliminator.GenerateMinimalCss(path, used, allow);
    }
}