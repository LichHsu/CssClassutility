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
        // 1. 設定乾淨的 UTF8
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        // 檢查是否為測試模式或其他 CLI 指令
        if (args.Length > 0)
        {
            if (args[0] == "--test")
            {
                TestRunner.RunAllTests();
                return;
            }

            if (args[0] == "identify-tokens")
            {
                // Usage: identify-tokens <path> [minOccurrences]
                string path = args[1];
                int minOccurrences = args.Length > 2 ? int.Parse(args[2]) : 2;
                var result = DesignTokenAnalyzer.IdentifyDesignTokens(path, minOccurrences);
                Console.WriteLine(JsonSerializer.Serialize(result, _jsonPrettyOptions));
                return;
            }

            if (args[0] == "replace-batch")
            {
                // Usage: replace-batch <path> <oldValue> <newValue>
                string path = args[1];
                string oldValue = args[2];
                string newValue = args[3];
                var result = BatchReplacer.BatchReplacePropertyValues(path, oldValue, newValue);
                Console.WriteLine(JsonSerializer.Serialize(result, _jsonPrettyOptions));
                return;
            }
            if (args[0] == "check-missing")
            {
                // Usage: check-missing <cssPath> <classesFile>
                string cssPath = args[1];
                string classesFile = args[2];
                var classes = File.ReadLines(classesFile);
                var result = CssConsistencyChecker.CheckMissingClasses(cssPath, classes);
                Console.WriteLine(JsonSerializer.Serialize(result, _jsonPrettyOptions));
                return;
            }
        }

        var server = new McpServer("CssClassutility", "1.0.0");
        
        // 自動掃描現有 Assembly 中的所有 [McpTool]
        server.RegisterToolsFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());

        await server.RunAsync(args);
    }

    #region Base Tool Handlers (now static methods with Attributes)

    [McpTool("get_css_classes", "解析 CSS 檔案並回傳 Class 定義列表 (含 StartIndex, BlockEnd 用於精確定位)。")]
    public static string GetCssClasses([McpParameter("CSS 檔案的絕對路徑")] string path)
    {
        var classes = CssParser.GetClasses(path);
        return JsonSerializer.Serialize(classes, _jsonOptions);
    }

    [McpTool("update_css_class", "直接修改 CSS 檔案中指定 Class 的屬性 (新增、更新或刪除)。")]
    public static string UpdateCssClass(
        [McpParameter("CSS 檔案的絕對路徑")] string path,
        [McpParameter("Class 名稱 (不含點號，例如 'btn-primary')")] string className,
        [McpParameter("CSS 屬性名稱 (例如 'color')")] string key,
        [McpParameter("操作類型：Set (新增/更新) 或 Remove (刪除)")] string action,
        [McpParameter("CSS 屬性值 (action 為 Remove 時可省略)", false)] string value = "")
    {
        return CssParser.UpdateClassProperty(path, className, key, value, action);
    }

    [McpTool("compare_css_style", "語義化比較兩個 CSS 樣式區塊是否相同 (忽略空白、註解與屬性順序)。")]
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

    [McpTool("get_css_entity", "讀取並解析 CSS 實體 JSON 檔案。")]
    public static string GetCssEntity([McpParameter("JSON 實體檔案路徑")] string path)
    {
        var entity = CssParser.GetCssEntity(path);
        return JsonSerializer.Serialize(entity, _jsonPrettyOptions);
    }

    [McpTool("update_css_entity_property", "修改 CSS 實體 JSON 檔案的屬性。")]
    public static string UpdateCssEntityProperty(
        [McpParameter("JSON 實體檔案路徑")] string path,
        [McpParameter("CSS 屬性名稱")] string key,
        [McpParameter("操作類型：Set (新增/更新) 或 Remove (刪除)")] string action,
        [McpParameter("CSS 屬性值 (action 為 Remove 時可省略)", false)] string value = "")
    {
        return CssParser.UpdateCssEntityProperty(path, key, value, action);
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

    #endregion
}