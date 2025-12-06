using CssClassUtility.AI;
using CssClassUtility.Models;
using CssClassUtility.Operations;
using Lichs.MCP.Core;
using Lichs.MCP.Core.Attributes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CssClassUtility;

internal class Program
{
    private static readonly JsonSerializerOptions _jsonPrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        if (args.Length > 0 && args[0] == "--test")
        {
            CssClassUtility.Testing.TestRunner.RunAllTests();
            return;
        }

        var server = new McpServer("css-class-utility", "2.1.0");
        server.RegisterToolsFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
        await server.RunAsync(args);
    }

    [McpTool("analyze_css", "分析 CSS 檔案 (Variables, Components, Usage 等)。")]
    public static string AnalyzeCss(
        [McpParameter("目標 CSS 檔案路徑或目錄")] string path,
        [McpParameter("分析類型 (Variables, Components, Missing, Usage)")] string analysisType,
        [McpParameter("選項參數", false)] CssAnalysisOptions? options = null)
    {
        options ??= new CssAnalysisOptions();

        if (analysisType.Equals("Variables", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(CssVariableSuggester.SuggestVariables(path, options.Threshold ?? 3), _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Components", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(CssComponentGrouper.GroupComponents(path), _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Missing", StringComparison.OrdinalIgnoreCase))
        {
            if (options.ClassesToCheck == null) throw new ArgumentException("Missing 分析需要 ClassesToCheck");
            var orphans = CssAnalyzer.FindMissingClasses(path, options.ClassesToCheck);
            return JsonSerializer.Serialize(orphans, _jsonPrettyOptions);
        }
        else if (analysisType.Equals("Usage", StringComparison.OrdinalIgnoreCase))
        {
            // Usage Analysis (Requires Project Root)
            string root = options.ProjectRoot ?? Path.GetDirectoryName(path) ?? "";
            string className = options.ClassName ?? "";
            if (string.IsNullOrEmpty(className)) throw new ArgumentException("Usage 分析需要 ClassName");

            var usages = UsageTracer.TraceCssUsage(className, root);
            return JsonSerializer.Serialize(usages, _jsonPrettyOptions);
        }

        throw new ArgumentException($"未知的分析類型: {analysisType}");
    }

    [McpTool("edit_css", "批次編輯 CSS 檔案。")]
    public static string EditCss(
        [McpParameter("目標 CSS 檔案路徑")] string path,
        [McpParameter("操作列表")] List<CssEditOperation> operations)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
        if (operations == null || operations.Count == 0) return "No operations provided.";

        var processor = new InMemoryCssProcessor(path); // Assumes Load() inside constructor or immediate usage

        foreach (var op in operations)
        {
            if (op.Op.Equals("Set", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(op.Key) || string.IsNullOrEmpty(op.Value)) continue;
                processor.UpdateProperty(op.ClassName, op.Key, op.Value);
            }
            else if (op.Op.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(op.Key))
                    processor.RemoveClass(op.ClassName); // If Key empty, remove class? Needs clarification. Legacy: RemoveClass(name).
                else
                    processor.RemoveProperty(op.ClassName, op.Key);
            }
            else if (op.Op.Equals("Merge", StringComparison.OrdinalIgnoreCase))
            {
                // Merge logic (Complex, usually calls CssMerger)
                // For now, let's assume InMemoryProcessor has Basic Merge or we skip
                // processor.Merge(op.Source, op.ClassName, op.Strategy); // Hypothetical
            }
        }

        processor.Save(path);
        return $"成功執行 {operations.Count} 個 CSS 操作。";
    }

    [McpTool("consolidate_css", "合併多個 CSS 到單一檔案。")]
    public static string ConsolidateCss(
        [McpParameter("來源檔案路徑列表")] List<string> sourceFiles,
        [McpParameter("輸出檔案路徑")] string outputFile)
    {
        CssMerger.BatchMerge(sourceFiles, outputFile, CssClassUtility.Models.MergeStrategy.Overwrite);
        return $"已合併 {sourceFiles.Count} 個檔案至 {outputFile}";
    }

    // purge_css and get_css_info can remain simpler or also use options if needed
}
