using System.Text;
using System.Text.Json;
using CssClassutility.MCP;

namespace CssClassutility.MCP;

/// <summary>
/// MCP 伺服器
/// </summary>
public static class McpServer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task RunAsync()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // MCP 初始化訊息
        await SendMessage(new
        {
            jsonrpc = "2.0",
            method = "initialized",
            @params = new { }
        });

        // 主循環：讀取並處理 JSON-RPC 請求
        string? line;
        while ((line = await Console.In.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null) continue;

                await HandleRequest(request);
            }
            catch (Exception ex)
            {
                await SendError(-32700, $"解析錯誤: {ex.Message}", null);
            }
        }
    }

    private static async Task HandleRequest(JsonRpcRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(request.Params),
                "tools/list" => HandleToolsList(),
                "tools/call" => HandleToolCall(request.Params),
                _ => throw new Exception($"未知方法: {request.Method}")
            };

            await SendResponse(result, request.Id);
        }
        catch (Exception ex)
        {
            await SendError(-32603, ex.Message, request.Id);
        }
    }

    private static object HandleInitialize(JsonElement @params)
    {
        return new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new
            {
                name = "CssClassutility",
                version = "2.1.0"
            },
            capabilities = new
            {
                tools = new { }
            }
        };
    }

    private static object HandleToolsList()
    {
        return new
        {
            tools = Program.GetToolDefinitions()
        };
    }

    private static object HandleToolCall(JsonElement @params)
    {
        string name = @params.GetProperty("name").GetString()!;
        var args = @params.GetProperty("arguments");

        var result = Program.HandleToolCall(args);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = result
                }
            }
        };
    }

    private static async Task SendMessage(object message)
    {
        string json = JsonSerializer.Serialize(message, _jsonOptions);
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();
    }

    private static async Task SendResponse(object? result, object? id)
    {
        await SendMessage(new JsonRpcResponse
        {
            Result = result,
            Id = id
        });
    }

    private static async Task SendError(int code, string message, object? id)
    {
        await SendMessage(new JsonRpcResponse
        {
            Error = new { code, message },
            Id = id
        });
    }
}
