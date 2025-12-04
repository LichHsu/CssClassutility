using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CssClassutility.Testing;
using CssClassutility.Operations;

namespace CssClassutility;

// --- MCP 協議模型 ---
public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;
    [JsonPropertyName("params")]
    public JsonElement Params { get; set; }
    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Error { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

// --- 資料結構定義 ---
public class CssClass
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; }

    [JsonPropertyName("blockEnd")]
    public int BlockEnd { get; set; }
}

/// <summary>
/// CSS 實體結構 (JSON 格式)
/// </summary>
public class CssEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public SortedDictionary<string, string> Properties { get; set; } = [];

    [JsonPropertyName("metadata")]
    public CssEntityMetadata Metadata { get; set; } = new();
}

public class CssEntityMetadata
{
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

/// <summary>
/// CSS 樣式比較結果
/// </summary>
public class CssCompareResult
{
    [JsonPropertyName("isIdentical")]
    public bool IsIdentical { get; set; }

    [JsonPropertyName("normalizedA")]
    public string NormalizedA { get; set; } = string.Empty;

    [JsonPropertyName("normalizedB")]
    public string NormalizedB { get; set; } = string.Empty;
}

/// <summary>
/// 合併策略列舉
/// </summary>
public enum MergeStrategy
{
    Overwrite,
    FillMissing,
    PruneDuplicate
}

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
        if (args.Length > 0 && args[0] == "--test")
        {
            TestRunner.RunAllTests();
            return;
        }

        // 2. 啟動記錄
        Log("=== Server Started ===");

        try
        {
            while (true)
            {
                // 3. 讀取輸入並記錄
                string? line = await Console.In.ReadLineAsync();

                // 如果是 null 代表串流結束 (Pipe 斷開)
                if (line == null)
                {
                    Log("Input stream closed (null received). Exiting.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    // 有時候會有空的 newline，忽略但不記錄以免洗版，或者也可以記錄下來觀察
                    continue;
                }

                Log($"[RECV]: {line}");

                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null)
                {
                    Log("[WARN]: Deserialization returned null.");
                    continue;
                }

                object? result = null;

                switch (request.Method)
                {
                    case "initialize":
                        Log("Processing 'initialize'...");
                        result = new
                        {
                            protocolVersion = "2024-11-05",
                            capabilities = new { tools = new { } },
                            serverInfo = new { name = "css-utility-mcp", version = "2.0.0" }
                        };
                        break;

                    case "notifications/initialized":
                        Log("Handshake completed.");
                        continue;

                    case "tools/list":
                        Log("Processing 'tools/list'...");
                        result = new { tools = GetToolDefinitions() };
                        break;

                    case "tools/call":
                        try
                        {
                            Log($"Processing tool call: {request.Params}");
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
            // 捕捉最外層的錯誤 (例如 JSON 解析嚴重失敗)
            Log($"[FATAL ERROR]: {ex}");
        }
    }

    /// <summary>
    /// 取得所有工具定義
    /// </summary>
    public static object[] GetToolDefinitions()
    {
        return
        [
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
            },
            // === 擴充工具 ===
            ..GetExtendedToolDefinitions()
        ];
    }

    /// <summary>
    /// 處理工具呼叫
    /// </summary>
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

// --- 核心邏輯 CssParser ---
public partial class CssParser
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex timeRule();
    [GeneratedRegex(@"(.+\.css):\.?(.+)")]
    private static partial Regex cssRule();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex(@"\.[a-zA-Z0-9_-]+")]
    private static partial Regex CssRulePattern();

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex CommentPattern();

    #region 核心解析

    /// <summary>
    /// 解析 CSS 檔案並回傳 Class 定義列表
    /// </summary>
    public static List<CssClass> GetClasses(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("找不到檔案", path);

        string content = File.ReadAllText(path);
        var results = new List<CssClass>();
        var length = content.Length;

        var scopeStack = new Stack<dynamic>();
        scopeStack.Push(new { Type = "Root", Selector = "", SelectorStart = 0 });

        int index = 0;
        bool inComment = false;
        bool inString = false;
        char stringChar = ' ';
        var buffer = new StringBuilder();
        int currentSelectorStart = -1;

        while (index < length)
        {
            char c = content[index];

            if (!inString)
            {
                if (!inComment && c == '/' && index + 1 < length && content[index + 1] == '*')
                {
                    inComment = true;
                    index++;
                }
                else if (inComment && c == '*' && index + 1 < length && content[index + 1] == '/')
                {
                    inComment = false;
                    index += 2;
                    continue;
                }
            }

            if (inComment)
            {
                index++;
                continue;
            }

            if (!inComment)
            {
                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                }
                else if (inString && c == stringChar)
                {
                    bool isEscaped = false;
                    int backIndex = index - 1;
                    while (backIndex >= 0 && content[backIndex] == '\\')
                    {
                        isEscaped = !isEscaped;
                        backIndex--;
                    }

                    if (!isEscaped)
                    {
                        inString = false;
                        stringChar = ' ';
                    }
                }
            }

            if (inString)
            {
                buffer.Append(c);
            }
            else if (c == '{')
            {
                string selector = buffer.ToString().Trim();
                buffer.Clear();

                string type = "Other";
                if (!selector.StartsWith('@'))
                {
                    if (CssRulePattern().IsMatch(selector)) type = "Class";
                }
                else type = "Media";

                int selStart = currentSelectorStart != -1 ? currentSelectorStart : index;
                scopeStack.Push(new { Type = type, Selector = selector, StartIndex = index, SelectorStart = selStart });
                currentSelectorStart = -1;
            }
            else if (c == '}')
            {
                if (scopeStack.Count > 1)
                {
                    var scope = scopeStack.Pop();

                    if (scope.Type == "Class")
                    {
                        int blockStart = scope.StartIndex;
                        int blockEnd = index;
                        string innerContent = content.Substring(blockStart + 1, blockEnd - blockStart - 1).Trim();
                        string? context = scopeStack.Peek().Type == "Media" ? scopeStack.Peek().Selector : null;

                        var classMatches = Regex.Matches(scope.Selector, @"\.([a-zA-Z0-9_-]+)");
                        foreach (Match match in classMatches)
                        {
                            results.Add(new CssClass
                            {
                                ClassName = match.Groups[1].Value,
                                Selector = scope.Selector,
                                Content = innerContent,
                                Context = context,
                                File = path,
                                StartIndex = scope.SelectorStart,
                                BlockEnd = blockEnd
                            });
                        }
                    }
                }
                buffer.Clear();
                currentSelectorStart = -1;
            }
            else if (!char.IsWhiteSpace(c))
            {
                if (buffer.Length == 0 && currentSelectorStart == -1) currentSelectorStart = index;
                buffer.Append(c);
            }
            else
            {
                // 保留空白字符在 buffer 中
                if (buffer.Length > 0) buffer.Append(c);
            }
            index++;
        }
        return results;
    }

    #endregion

    #region 樣式比較

    /// <summary>
    /// 語義化比較兩個 CSS 樣式區塊是否相同
    /// </summary>
    public static CssCompareResult CompareCssStyle(string styleA, string styleB)
    {
        string normA = NormalizeCss(styleA);
        string normB = NormalizeCss(styleB);

        return new CssCompareResult
        {
            IsIdentical = normA == normB,
            NormalizedA = normA,
            NormalizedB = normB
        };
    }

    private static string NormalizeCss(string css)
    {
        // 移除註解
        css = CommentPattern().Replace(css, "");

        // 正規化空白
        css = timeRule().Replace(css, " ");

        // 解析屬性並排序
        var props = css.Split(';')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p =>
            {
                var parts = p.Split(':', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim().ToLower();
                    string val = parts[1].Trim();
                    return $"{key}: {val}";
                }
                return p;
            })
            .OrderBy(p => p);

        return string.Join(";", props);
    }

    #endregion

    #region Class 移除

    /// <summary>
    /// 安全地從檔案中移除 CSS Class 定義
    /// </summary>
    public static string RemoveCssClass(string path, string className)
    {
        if (!File.Exists(path)) return $"錯誤：找不到檔案 {path}";

        // 建立備份
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = $"{path}.safe_backup_{timestamp}";
        File.Copy(path, backupPath, true);

        try
        {
            string content = File.ReadAllText(path);
            var classes = GetClasses(path);

            var target = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                return $"警告：在 {path} 中找不到 Class .{className}";
            }

            // 安全檢查：群組選擇器
            if (target.Selector.Contains(','))
            {
                return $"安全防護：Class .{className} 屬於群組選擇器 '{target.Selector}'。目前不支援部分移除群組選擇器，請手動處理。";
            }

            // 移除內容
            int removeStartIndex = target.StartIndex;
            int removeEndIndex = target.BlockEnd;

            // 檢查前導換行/空白以移除空行
            while (removeStartIndex > 0 && char.IsWhiteSpace(content[removeStartIndex - 1]))
            {
                removeStartIndex--;
            }

            string newContent = content.Remove(removeStartIndex, removeEndIndex - removeStartIndex + 1);

            // 插入註解標記
            string comment = $"\n/* .{className} removed by CssClassManager */\n";
            newContent = newContent.Insert(removeStartIndex, comment);

            // 驗證完整性 (檢查大括號平衡)
            int openCount = newContent.Count(c => c == '{');
            int closeCount = newContent.Count(c => c == '}');

            if (openCount != closeCount)
            {
                throw new Exception($"移除後偵測到大括號不匹配！ (Open: {openCount}, Close: {closeCount})");
            }

            // 儲存檔案
            File.WriteAllText(path, newContent, Encoding.UTF8);
            return $"成功從 {path} 移除 .{className}（備份已建立於 {backupPath}）";
        }
        catch (Exception ex)
        {
            // 還原備份
            File.Copy(backupPath, path, true);
            return $"移除失敗：{ex.Message}（已從備份還原）";
        }
    }

    #endregion

    #region JSON 轉換

    /// <summary>
    /// 將 CSS Class 轉換為 JSON 實體格式
    /// </summary>
    public static CssEntity ConvertToCssJson(string path, string className)
    {
        var classes = GetClasses(path);
        var target = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"找不到 Class .{className}");

        return ConvertToCssJson(target, Path.GetFileNameWithoutExtension(path));
    }

    public static CssEntity ConvertToCssJson(CssClass cssClass, string? sourceFileName = null)
    {
        var props = ContentToProperties(cssClass.Content);
        var sortedProps = new SortedDictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);

        return new CssEntity
        {
            Name = cssClass.ClassName,
            Selector = cssClass.Selector,
            Properties = sortedProps,
            Metadata = new CssEntityMetadata
            {
                SourceFile = sourceFileName ?? Path.GetFileNameWithoutExtension(cssClass.File),
                Context = cssClass.Context
            }
        };
    }

    /// <summary>
    /// 將 JSON 實體轉換為 CSS 字串
    /// </summary>
    public static string ConvertFromCssJson(CssEntity entity)
    {
        var sb = new StringBuilder();
        sb.Append(entity.Selector).Append(" {\n");

        foreach (var kvp in entity.Properties)
        {
            sb.Append($"    {kvp.Key}: {kvp.Value};\n");
        }

        sb.Append('}');
        return sb.ToString();
    }

    #endregion

    #region Class 合併

    /// <summary>
    /// 將來源 Class 的屬性合併到目標 Class
    /// </summary>
    public static string MergeCssClass(string targetPath, string targetClassName, string sourceObject, MergeStrategy strategy)
    {
        var classes = GetClasses(targetPath);
        var targetClass = classes.FirstOrDefault(c => c.ClassName.Equals(targetClassName, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"目標 Class .{targetClassName} 不存在");

        var targetEntity = ConvertToCssJson(targetClass);
        var targetProps = targetEntity.Properties;

        // 取得來源屬性
        SortedDictionary<string, string>? sourceProps = null;

        if (sourceObject.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var sourceEntity = GetCssEntity(sourceObject);
            sourceProps = sourceEntity.Properties;
        }
        else if (cssRule().IsMatch(sourceObject))
        {
            var match = cssRule().Match(sourceObject);
            string srcFile = match.Groups[1].Value;
            string srcClass = match.Groups[2].Value;
            var srcClasses = GetClasses(srcFile);
            var srcTarget = srcClasses.FirstOrDefault(c => c.ClassName.Equals(srcClass, StringComparison.OrdinalIgnoreCase));
            if (srcTarget != null)
            {
                sourceProps = ConvertToCssJson(srcTarget).Properties;
            }
        }

        if (sourceProps == null)
            throw new Exception($"無法讀取來源物件: {sourceObject}");

        // 合併邏輯
        bool modified = MergeProperties(targetProps, sourceProps, strategy);

        if (modified)
        {
            string newCss = ConvertFromCssJson(targetEntity);
            ReplaceBlock(targetPath, targetClass.StartIndex, targetClass.BlockEnd, newCss);
            return $"已合併 CSS Class: .{targetClassName} ({strategy})";
        }

        return $"未變更: .{targetClassName}";
    }

    #endregion

    #region 實體管理 (JSON 檔案)

    /// <summary>
    /// 將 CSS 檔案實體化為 JSON 檔案集合
    /// </summary>
    public static string ExportCssToEntities(string cssPath, string outputRoot, string cleanMode)
    {
        if (!File.Exists(cssPath)) throw new FileNotFoundException("找不到檔案", cssPath);

        string fileName = Path.GetFileNameWithoutExtension(cssPath);
        string targetDir = Path.Combine(outputRoot, fileName);

        // 準備輸出目錄
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        else
        {
            // 處理清理模式
            if (cleanMode == "DeleteAll")
            {
                foreach (var f in Directory.GetFiles(targetDir, "*.json"))
                    File.Delete(f);
            }
            else if (cleanMode == "KeepSoftDeleted")
            {
                foreach (var f in Directory.GetFiles(targetDir, "*.json"))
                {
                    if (!Path.GetFileName(f).StartsWith('_'))
                        File.Delete(f);
                }
            }
        }

        // 解析 CSS
        var classes = GetClasses(cssPath);
        var createdFiles = new HashSet<string>();

        foreach (var cls in classes)
        {
            var entity = ConvertToCssJson(cls, fileName);
            string jsonFileName = $"{cls.ClassName}.json";
            string jsonPath = Path.Combine(targetDir, jsonFileName);

            // 處理重名衝突
            int counter = 1;
            while (createdFiles.Contains(jsonPath) || File.Exists(jsonPath))
            {
                jsonFileName = $"{cls.ClassName}_{counter}.json";
                jsonPath = Path.Combine(targetDir, jsonFileName);
                counter++;
            }

            createdFiles.Add(jsonPath);
            string json = JsonSerializer.Serialize(entity, _jsonOptions);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
        }

        return $"完成！共匯出 {classes.Count} 個實體至 {targetDir}";
    }

    /// <summary>
    /// 從 JSON 實體集合建置 CSS 檔案
    /// </summary>
    public static string ImportCssFromEntities(string sourceDir, string outputFile, bool includeSoftDeleted)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"找不到來源目錄: {sourceDir}");

        var files = Directory.GetFiles(sourceDir, "*.json");
        var entities = new List<CssEntity>();

        foreach (var file in files)
        {
            // 軟刪除檢查
            if (!includeSoftDeleted && Path.GetFileName(file).StartsWith('_'))
                continue;

            try
            {
                string json = File.ReadAllText(file);
                var entity = JsonSerializer.Deserialize<CssEntity>(json, _jsonOptions);
                if (entity != null) entities.Add(entity);
            }
            catch { /* 忽略解析錯誤的檔案 */ }
        }

        // 分組處理
        var grouped = entities.GroupBy(e => e.Metadata?.Context ?? "");
        var cssOutput = new List<string>();

        // 處理 Root Context (無 Context)
        var rootGroup = grouped.FirstOrDefault(g => string.IsNullOrEmpty(g.Key));
        if (rootGroup != null)
        {
            foreach (var entity in rootGroup)
            {
                cssOutput.Add(ConvertFromCssJson(entity));
            }
        }

        // 處理其他 Context (Media Queries 等)
        foreach (var group in grouped.Where(g => !string.IsNullOrEmpty(g.Key)))
        {
            string context = group.Key;
            cssOutput.Add($"\n{context} {{");

            foreach (var entity in group)
            {
                // 縮排內部規則
                string rule = ConvertFromCssJson(entity);
                var indentedRule = string.Join("\n", rule.Split('\n').Select(l => "    " + l));
                cssOutput.Add(indentedRule);
            }

            cssOutput.Add("}");
        }

        // 寫入檔案
        string finalCss = string.Join("\n\n", cssOutput);
        File.WriteAllText(outputFile, finalCss, Encoding.UTF8);

        return $"建置完成！輸出至: {outputFile}（共處理 {entities.Count} 個實體）";
    }

    /// <summary>
    /// 讀取並解析 CSS 實體 JSON 檔案
    /// </summary>
    public static CssEntity GetCssEntity(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"找不到檔案: {path}");

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CssEntity>(json, _jsonOptions)
            ?? throw new Exception("無法解析 JSON 實體");
    }

    /// <summary>
    /// 修改 CSS 實體的屬性
    /// </summary>
    public static string UpdateCssEntityProperty(string path, string key, string value, string action)
    {
        var entity = GetCssEntity(path);
        var props = entity.Properties;
        bool modified = false;
        key = key.ToLower().Trim();

        if (action.Equals("Set", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue(key, out string? oldVal) || oldVal != value)
            {
                props[key] = value;
                modified = true;
            }
        }
        else if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (props.Remove(key))
                modified = true;
        }

        if (modified)
        {
            string json = JsonSerializer.Serialize(entity, _jsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);
            return $"已更新實體: {path}";
        }

        return $"實體未變更: {path}";
    }

    /// <summary>
    /// 合併兩個 CSS 實體
    /// </summary>
    public static string MergeCssEntity(string targetPath, string sourcePath, MergeStrategy strategy)
    {
        var target = GetCssEntity(targetPath);
        var source = GetCssEntity(sourcePath);

        bool modified = MergeProperties(target.Properties, source.Properties, strategy);

        if (modified)
        {
            string json = JsonSerializer.Serialize(target, _jsonOptions);
            File.WriteAllText(targetPath, json, Encoding.UTF8);
            return $"已合併實體: {targetPath} ({strategy})";
        }

        return $"實體未變更: {targetPath}";
    }

    #endregion

    #region 輔助方法

    private static Dictionary<string, string> ContentToProperties(string content)
    {
        var dict = new Dictionary<string, string>();
        var props = content.Split(';');
        foreach (var p in props)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var parts = p.Split(':', 2);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return dict;
    }

    private static bool MergeProperties(SortedDictionary<string, string> target, SortedDictionary<string, string> source, MergeStrategy strategy)
    {
        bool modified = false;

        foreach (var kvp in source)
        {
            string key = kvp.Key;
            string val = kvp.Value;

            if (strategy == MergeStrategy.Overwrite)
            {
                if (!target.ContainsKey(key) || target[key] != val)
                {
                    target[key] = val;
                    modified = true;
                }
            }
            else if (strategy == MergeStrategy.FillMissing)
            {
                if (!target.ContainsKey(key))
                {
                    target[key] = val;
                    modified = true;
                }
            }
            else if (strategy == MergeStrategy.PruneDuplicate)
            {
                if (target.ContainsKey(key) && target[key] == val)
                {
                    target.Remove(key);
                    modified = true;
                }
            }
        }

        return modified;
    }

    private static void ReplaceBlock(string path, int start, int end, string newContent)
    {
        string content = File.ReadAllText(path);
        string before = content.Substring(0, start);
        string after = content.Substring(end + 1);
        File.WriteAllText(path, before + newContent + after, Encoding.UTF8);
    }

    public static string UpdateClassProperty(string path, string className, string key, string value, string action)
    {
        return CssUpdater.UpdateClassProperty(path, className, key, value, action);
    }

    #endregion
}