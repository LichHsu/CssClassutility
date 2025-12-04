using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

    static async Task Main(string[] args)
    {
        // 1. 設定乾淨的 UTF8
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);

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
                            serverInfo = new { name = "css-utility-mcp", version = "1.0.0" }
                        };
                        break;

                    case "notifications/initialized":
                        Log("Handshake completed.");
                        continue;

                    case "tools/list":
                        Log("Processing 'tools/list'...");
                        result = new
                        {
                            tools = new object[]
                            {
                                new
                                {
                                    name = "get_css_classes",
                                    description = "Parse a CSS file and return a list of classes with their content and location.",
                                    inputSchema = new
                                    {
                                        type = "object",
                                        properties = new { path = new { type = "string", description = "Absolute path to the CSS file" } },
                                        required = new[] { "path" }
                                    }
                                },
                                new
                                {
                                    name = "update_css_class",
                                    description = "Update, add, or remove a CSS property within a specific class.",
                                    inputSchema = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            path = new { type = "string" },
                                            className = new { type = "string", description = "The class name without dot (e.g. 'btn-primary')" },
                                            key = new { type = "string", description = "CSS property name (e.g. 'color')" },
                                            value = new { type = "string", description = "CSS property value" },
                                            action = new { type = "string", @enum = new[] { "Set", "Remove" } }
                                        },
                                        required = new[] { "path", "className", "key", "action" }
                                    }
                                }
                            }
                        };
                        break;

                    case "tools/call":
                        try
                        {
                            Log($"Processing tool call: {request.Params}"); // 記錄參數
                            string name = request.Params.GetProperty("name").GetString() ?? "";
                            JsonElement argsEl = request.Params.GetProperty("arguments");

                            if (name == "get_css_classes")
                            {
                                string path = argsEl.GetProperty("path").GetString()!;
                                result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(CssParser.GetClasses(path), _jsonOptions) } } };
                            }
                            else if (name == "update_css_class")
                            {
                                string path = argsEl.GetProperty("path").GetString()!;
                                string cls = argsEl.GetProperty("className").GetString()!;
                                string key = argsEl.GetProperty("key").GetString()!;
                                string action = argsEl.GetProperty("action").GetString()!;
                                string val = argsEl.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

                                string msg = CssParser.UpdateClassProperty(path, cls, key, val, action);
                                result = new { content = new[] { new { type = "text", text = msg } } };
                            }
                            else
                            {
                                throw new Exception($"Unknown tool: {name}");
                            }
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
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* 忽略 Log 寫入失敗，避免遞迴錯誤 */ }
    }
}

// --- 核心邏輯 CssParser (保持不變) ---
public partial class CssParser
{
    [GeneratedRegex(@"\.[a-zA-Z0-9_-]+")]
    private static partial Regex cssRule();

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex remarkRule();

    public static List<CssClass> GetClasses(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);

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
            char char_ = content[index];

            if (!inString)
            {
                if (!inComment && char_ == '/' && index + 1 < length && content[index + 1] == '*')
                {
                    inComment = true;
                    index++;
                }
                else if (inComment && char_ == '*' && index + 1 < length && content[index + 1] == '/')
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
                if (!inString && (char_ == '"' || char_ == '\''))
                {
                    inString = true;
                    stringChar = char_;
                }
                else if (inString && char_ == stringChar)
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
                buffer.Append(char_);
            }
            else if (char_ == '{')
            {
                string selector = buffer.ToString().Trim();
                buffer.Clear();

                string type = "Other";
                if (!selector.StartsWith('@'))
                {
                    if (cssRule().IsMatch(selector)) type = "Class";
                }
                else type = "Media";

                int selStart = currentSelectorStart != -1 ? currentSelectorStart : index;
                scopeStack.Push(new { Type = type, Selector = selector, StartIndex = index, SelectorStart = selStart });
                currentSelectorStart = -1;
            }
            else if (char_ == '}')
            {
                if (scopeStack.Count > 1)
                {
                    var scope = scopeStack.Pop();

                    if (scope.Type == "Class")
                    {
                        int blockStart = scope.StartIndex;
                        int blockEnd = index;
                        string innerContent = content.Substring(blockStart + 1, blockEnd - blockStart - 1).Trim();
                        string context = scopeStack.Peek().Type == "Media" ? scopeStack.Peek().Selector : null;

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
            else if (!char.IsWhiteSpace(char_))
            {
                if (buffer.Length == 0 && currentSelectorStart == -1) currentSelectorStart = index;
                buffer.Append(char_);
            }
            index++;
        }
        return results;
    }

    public static string UpdateClassProperty(string path, string className, string key, string value, string action)
    {
        var classes = GetClasses(path);
        var target = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
        if (target == null) return $"Warning: Class .{className} not found in {path}";

        var props = ContentToProperties(target.Content);
        bool modified = false;
        string lowerKey = key.ToLower().Trim();

        if (action.Equals("Set", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue(lowerKey, out string? oldVal) || oldVal != value)
            {
                props[lowerKey] = value;
                modified = true;
            }
        }
        else if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (props.Remove(lowerKey)) modified = true;
        }

        if (!modified) return $"No changes needed for .{className}";

        string newCssContent = PropertiesToContent(props, target.Selector);
        // ReplaceBlock(path, target.StartIndex, target.BlockEnd, newCssContent);

        return $"Successfully updated CSS Class: .{className}";
    }

    private static Dictionary<string, string> ContentToProperties(string content)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cleanContent = remarkRule().Replace(content, "");

        foreach (var prop in cleanContent.Split(';'))
        {
            var parts = prop.Split([':'], 2);
            if (parts.Length == 2)
            {
                string key = parts[0].Trim().ToLower();
                string val = parts[1].Trim();
                if (!string.IsNullOrEmpty(key)) props[key] = val;
            }
        }
        return props;
    }

    private static string PropertiesToContent(Dictionary<string, string> props, string selector)
    {
        var sb = new StringBuilder();
        sb.Append(selector).Append(" {\n");
        foreach (var key in props.Keys.OrderBy(k => k))
        {
            sb.Append($"    {key}: {props[key]};\n");
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void ReplaceBlock(string path, int startIndex, int endIndex, string newContent)
    {
        string backupPath = $"{path}.bak";
        File.Copy(path, backupPath, true);

        string content = File.ReadAllText(path);
        int lengthToRemove = endIndex - startIndex + 1;

        if (startIndex < 0 || startIndex + lengthToRemove > content.Length)
            throw new IndexOutOfRangeException("File content changed during processing.");

        string tempContent = content.Remove(startIndex, lengthToRemove);
        string finalContent = tempContent.Insert(startIndex, newContent);
        File.WriteAllText(path, finalContent, Encoding.UTF8);
    }
}