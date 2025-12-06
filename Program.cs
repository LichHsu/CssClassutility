using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CssClassutility.Testing;
using CssClassutility.Operations;
using CssClassutility.Models;
using CssClassutility.MCP;
using CssClassutility.Core;
using CssClassutility.AI;

namespace CssClassutility;

public partial class Program
{
    // 偵錯用：設定 Log 檔案路徑 (會產生在 exe 同層目錄)
    private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mcp_debug_log.txt");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false, // 必須為 false，MCP 是一行一個 JSON
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions _jsonPrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static async Task Main(string[] args)
    {
        // 1. 設定乾淨的 UTF8
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

        // 檢查是否為測試模式
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

        // 2. 啟動記錄
        Log("=== Server Started ===");

        try
        {
            while (true)
            {
                // 3. 讀取輸入並記錄
                string? line = await Console.In.ReadLineAsync();
                if (line == null) break;

                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null) continue;

                object? result = null;

                switch (request.Method)
                {
                    case "initialize":
                        Log("Processing 'initialize'...");
                        result = new
                        {
                            protocolVersion = "2024-11-05", // Assuming 2024-11-05 as per older code
                            capabilities = new
                            {
                                tools = new { listChanged = true },
                                resources = new { listChanged = true, subscribe = true }
                            },
                            serverInfo = new { name = "CssClassutility", version = "1.0.0" }
                        };
                        break;

                    case "notifications/initialized":
                        Log("Handshake completed.");
                        continue;

                    case "tools/list":
                        result = new { tools = GetToolDefinitions() };
                        break;

                    case "tools/call":
                        try
                        {
                            result = HandleToolCall(request.Params);
                        }
                        catch (Exception toolEx)
                        {
                            Log($"[TOOL ERROR]: {toolEx.Message}");
                            var response = new JsonRpcResponse
                            {
                                Id = request.Id,
                                Error = new { code = -32602, message = toolEx.Message }
                            };
                            SendResponse(response);
                            continue;
                        }
                        break;


                }

                if (result != null)
                {
                    var response = new JsonRpcResponse { Id = request.Id, Result = result };
                    SendResponse(response);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[FATAL ERROR]: {ex}");
        }
    }
    
    // ...





    /// <summary>
    /// 取得所有工具定義
    /// </summary>
    public static object[] GetToolDefinitions()
    {
        var baseTools = new object[]
        {
            // 1. get_css_classes
            new
            {
                name = "get_css_classes",
                description = "解析 CSS 檔案並回傳 Class 定義列表 (含 StartIndex, BlockEnd 用於精確定位)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new { path = new { type = "string", description = "CSS 檔案的絕對路徑" } },
                    required = new[] { "path" }
                }
            },
            // 2. update_css_class
            new
            {
                name = "update_css_class",
                description = "直接修改 CSS 檔案中指定 Class 的屬性 (新增、更新或刪除)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        className = new { type = "string", description = "Class 名稱 (不含點號，例如 'btn-primary')" },
                        key = new { type = "string", description = "CSS 屬性名稱 (例如 'color')" },
                        value = new { type = "string", description = "CSS 屬性值 (action 為 Remove 時可省略)" },
                        action = new { type = "string", @enum = new[] { "Set", "Remove" }, description = "操作類型：Set (新增/更新) 或 Remove (刪除)" }
                    },
                    required = new[] { "path", "className", "key", "action" }
                }
            },
            // 3. compare_css_style
            new
            {
                name = "compare_css_style",
                description = "語義化比較兩個 CSS 樣式區塊是否相同 (忽略空白、註解與屬性順序)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        styleA = new { type = "string", description = "第一個樣式內容 (不含選擇器和大括號)" },
                        styleB = new { type = "string", description = "第二個樣式內容" }
                    },
                    required = new[] { "styleA", "styleB" }
                }
            },
            // 4. remove_css_class
            new
            {
                name = "remove_css_class",
                description = "安全地從 CSS 檔案中移除指定的 Class 定義 (自動建立備份，並驗證語法完整性)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        className = new { type = "string", description = "要移除的 Class 名稱 (不含點號)" }
                    },
                    required = new[] { "path", "className" }
                }
            },
            // 5. convert_to_css_json
            new
            {
                name = "convert_to_css_json",
                description = "將 CSS 檔案中的指定 Class 轉換為 JSON 實體格式 (屬性自動排序)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "CSS 檔案的絕對路徑" },
                        className = new { type = "string", description = "Class 名稱 (不含點號)" }
                    },
                    required = new[] { "path", "className" }
                }
            },
            // 6. convert_from_css_json
            new
            {
                name = "convert_from_css_json",
                description = "將 JSON 實體轉換為 CSS 字串。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        entity = new { type = "string", description = "JSON 實體字串 (包含 name, selector, properties)" }
                    },
                    required = new[] { "entity" }
                }
            },
            // 7. merge_css_class
            new
            {
                name = "merge_css_class",
                description = "將來源 Class 的屬性合併到目標 Class (支援多種策略：Overwrite 覆蓋、FillMissing 補齊、PruneDuplicate 移除重複)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        targetPath = new { type = "string", description = "目標 CSS 檔案絕對路徑" },
                        targetClassName = new { type = "string", description = "目標 Class 名稱" },
                        sourceObject = new { type = "string", description = "來源物件：JSON 檔案路徑，或 'path/to/file.css:.className' 格式" },
                        strategy = new { type = "string", @enum = new[] { "Overwrite", "FillMissing", "PruneDuplicate" } }
                    },
                    required = new[] { "targetPath", "targetClassName", "sourceObject" }
                }
            },
            // 8. export_css_to_entities
            new
            {
                name = "export_css_to_entities",
                description = "將 CSS 檔案實體化為 JSON 檔案集合 (每個 Class 一個 JSON 檔案)。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        cssPath = new { type = "string", description = "來源 CSS 檔案路徑" },
                        outputRoot = new { type = "string", description = "實體輸出根目錄 (預設 .\\CssEntities)" },
                        cleanMode = new { type = "string", @enum = new[] { "Keep", "DeleteAll", "KeepSoftDeleted" }, description = "清理模式" }
                    },
                    required = new[] { "cssPath" }
                }
            },
            // 9. import_css_from_entities
            new
            {
                name = "import_css_from_entities",
                description = "從 JSON 實體集合建置 CSS 檔案。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceDir = new { type = "string", description = "實體來源目錄" },
                        outputFile = new { type = "string", description = "輸出 CSS 檔案路徑" },
                        includeSoftDeleted = new { type = "boolean", description = "是否包含軟刪除的實體 (檔名以 _ 開頭)" }
                    },
                    required = new[] { "sourceDir", "outputFile" }
                }
            },
            // 10. get_css_entity
            new
            {
                name = "get_css_entity",
                description = "讀取並解析 CSS 實體 JSON 檔案。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "JSON 實體檔案路徑" }
                    },
                    required = new[] { "path" }
                }
            },
            // 11. update_css_entity_property
            new
            {
                name = "update_css_entity_property",
                description = "修改 CSS 實體 JSON 檔案的屬性。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "JSON 實體檔案路徑" },
                        key = new { type = "string", description = "CSS 屬性名稱" },
                        value = new { type = "string", description = "CSS 屬性值" },
                        action = new { type = "string", @enum = new[] { "Set", "Remove" } }
                    },
                    required = new[] { "path", "key", "action" }
                }
            },
            // 12. merge_css_entity
            new
            {
                name = "merge_css_entity",
                description = "合併兩個 CSS 實體 JSON 檔案。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        targetPath = new { type = "string", description = "目標 JSON 實體檔案路徑 (將被修改)" },
                        sourcePath = new { type = "string", description = "來源 JSON 實體檔案路徑 (唯讀)" },
                        strategy = new { type = "string", @enum = new[] { "Overwrite", "FillMissing", "PruneDuplicate" } }
                    },
                    required = new[] { "targetPath", "sourcePath" }
                }
            }
        };

        return [..baseTools, ..GetExtendedToolDefinitions()];
    }

    public static object HandleToolCall(JsonElement paramsEl)
    {
        string name = paramsEl.GetProperty("name").GetString() ?? "";
        JsonElement args = paramsEl.GetProperty("arguments");

        // 先嘗試擴充工具
        string? extResult = HandleExtendedToolCall(name, args);
        if (extResult != null)
            return new { content = new[] { new { type = "text", text = extResult } } };

        string resultText = name switch
        {
            "get_css_classes" => HandleGetCssClasses(args),
            "update_css_class" => HandleUpdateCssClass(args),
            "compare_css_style" => HandleCompareCssStyle(args),
            "remove_css_class" => HandleRemoveCssClass(args),
            "convert_to_css_json" => HandleConvertToCssJson(args),
            "convert_from_css_json" => HandleConvertFromCssJson(args),
            "merge_css_class" => HandleMergeCssClass(args),
            "export_css_to_entities" => HandleExportCssToEntities(args),
            "import_css_from_entities" => HandleImportCssFromEntities(args),
            "get_css_entity" => HandleGetCssEntity(args),
            "update_css_entity_property" => HandleUpdateCssEntityProperty(args),
            "merge_css_entity" => HandleMergeCssEntity(args),
            _ => throw new Exception($"Unknown tool: {name}")
        };

        return new { content = new[] { new { type = "text", text = resultText } } };
    }

    #region Tool Handlers

    private static string HandleGetCssClasses(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        var classes = CssParser.GetClasses(path);
        return JsonSerializer.Serialize(classes, _jsonOptions);
    }

    private static string HandleUpdateCssClass(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string className = args.GetProperty("className").GetString()!;
        string key = args.GetProperty("key").GetString()!;
        string action = args.GetProperty("action").GetString()!;
        string value = args.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

        return CssParser.UpdateClassProperty(path, className, key, value, action);
    }

    private static string HandleCompareCssStyle(JsonElement args)
    {
        string styleA = args.GetProperty("styleA").GetString()!;
        string styleB = args.GetProperty("styleB").GetString()!;

        var result = CssParser.CompareCssStyle(styleA, styleB);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    private static string HandleRemoveCssClass(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string className = args.GetProperty("className").GetString()!;

        return CssParser.RemoveCssClass(path, className);
    }

    private static string HandleConvertToCssJson(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string className = args.GetProperty("className").GetString()!;

        var entity = CssParser.ConvertToCssJson(path, className);
        return JsonSerializer.Serialize(entity, _jsonPrettyOptions);
    }

    private static string HandleConvertFromCssJson(JsonElement args)
    {
        string entityJson = args.GetProperty("entity").GetString()!;
        var entity = JsonSerializer.Deserialize<CssEntity>(entityJson, _jsonOptions)
            ?? throw new Exception("無法解析 JSON 實體");
        return CssParser.ConvertFromCssJson(entity);
    }

    private static string HandleMergeCssClass(JsonElement args)
    {
        string targetPath = args.GetProperty("targetPath").GetString()!;
        string targetClassName = args.GetProperty("targetClassName").GetString()!;
        string sourceObject = args.GetProperty("sourceObject").GetString()!;
        string strategyStr = args.TryGetProperty("strategy", out var s) ? s.GetString() ?? "Overwrite" : "Overwrite";

        var strategy = Enum.Parse<MergeStrategy>(strategyStr, true);
        return CssParser.MergeCssClass(targetPath, targetClassName, sourceObject, strategy);
    }

    private static string HandleExportCssToEntities(JsonElement args)
    {
        string cssPath = args.GetProperty("cssPath").GetString()!;
        string outputRoot = args.TryGetProperty("outputRoot", out var o) ? o.GetString() ?? ".\\CssEntities" : ".\\CssEntities";
        string cleanModeStr = args.TryGetProperty("cleanMode", out var c) ? c.GetString() ?? "Keep" : "Keep";

        return CssParser.ExportCssToEntities(cssPath, outputRoot, cleanModeStr);
    }

    private static string HandleImportCssFromEntities(JsonElement args)
    {
        string sourceDir = args.GetProperty("sourceDir").GetString()!;
        string outputFile = args.GetProperty("outputFile").GetString()!;
        bool includeSoftDeleted = args.TryGetProperty("includeSoftDeleted", out var i) && i.GetBoolean();

        return CssParser.ImportCssFromEntities(sourceDir, outputFile, includeSoftDeleted);
    }

    private static string HandleGetCssEntity(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        var entity = CssParser.GetCssEntity(path);
        return JsonSerializer.Serialize(entity, _jsonPrettyOptions);
    }

    private static string HandleUpdateCssEntityProperty(JsonElement args)
    {
        string path = args.GetProperty("path").GetString()!;
        string key = args.GetProperty("key").GetString()!;
        string action = args.GetProperty("action").GetString()!;
        string value = args.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

        return CssParser.UpdateCssEntityProperty(path, key, value, action);
    }

    private static string HandleMergeCssEntity(JsonElement args)
    {
        string targetPath = args.GetProperty("targetPath").GetString()!;
        string sourcePath = args.GetProperty("sourcePath").GetString()!;
        string strategyStr = args.TryGetProperty("strategy", out var s) ? s.GetString() ?? "Overwrite" : "Overwrite";

        var strategy = Enum.Parse<MergeStrategy>(strategyStr, true);
        return CssParser.MergeCssEntity(targetPath, sourcePath, strategy);
    }

    #endregion

    // 封裝發送邏輯，確保記錄與發送一致
    private static void SendResponse(JsonRpcResponse response)
    {
        string json = JsonSerializer.Serialize(response, _jsonOptions);
        Log($"[SEND]: {json}");
        // 使用 Write + \n 取代 WriteLine，有時候可以避免 Windows \r\n 造成的解析問題
        Console.Write(json + "\n");
        Console.Out.Flush();
    }

    // 簡單的檔案寫入 Log
    private static void Log(string message)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); }
        catch { }
    }
}