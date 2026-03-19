using System.Linq;
using System.Text.Json;
using IpcMcp.Models;
using IpcMcp.Services;
using Microsoft.AspNetCore.Mvc;

namespace IpcMcp.Controllers;

[ApiController]
[Route("")]
public class McpController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private readonly NamedPipeService _namedPipeService;
    private readonly MemoryMappedFileService _mmfService;
    private readonly PInvokeService _pInvokeService;
    private readonly ComService _comService;
    private readonly RegistryService _registryService;

    public McpController(
        NamedPipeService namedPipeService,
        MemoryMappedFileService mmfService,
        PInvokeService pInvokeService,
        ComService comService,
        RegistryService registryService)
    {
        _namedPipeService = namedPipeService;
        _mmfService = mmfService;
        _pInvokeService = pInvokeService;
        _comService = comService;
        _registryService = registryService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleRequest([FromBody] JsonElement body)
    {
        var request = DeserializeRequest(body);
        if (request == null)
        {
            return BadRequest(new { error = "Invalid request format" });
        }

        var response = new McpResponse
        {
            JsonRpc = "2.0",
            Id = request.Id
        };

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    response.Result = CreateInitializeResult();
                    break;
                case "tools/list":
                    response.Result = CreateToolsListResult();
                    break;
                case "tools/call":
                    response = await HandleToolsCall(request, response);
                    break;
                default:
                    response.Error = new McpError { Code = -32601, Message = "Method not found" };
                    break;
            }
        }
        catch (Exception ex)
        {
            response.Error = new McpError
            {
                Code = -32603,
                Message = "Internal error",
                Data = ex.Message
            };
        }

        return Ok(response);
    }

    private static McpRequest? DeserializeRequest(JsonElement body)
    {
        try
        {
            return JsonSerializer.Deserialize<McpRequest>(body, JsonOptions);
        }
        catch
        {
            if (body.TryGetProperty("request", out var requestElement))
            {
                return JsonSerializer.Deserialize<McpRequest>(requestElement, JsonOptions);
            }
        }
        return null;
    }

    private static object CreateInitializeResult() => new
    {
        protocolVersion = "2024-11-05",
        capabilities = new { tools = new { } },
        serverInfo = new { name = "ipc-mcp", version = "1.1.0" }
    };

    private static object CreateToolsListResult() => new
    {
        tools = new[]
        {
            new { name = "list_named_pipes", description = "List all available named pipes" },
            new { name = "find_named_pipe", description = "Find named pipes matching a pattern" },
            new { name = "wait_for_named_pipe", description = "Wait for a named pipe to become available" },
            new { name = "list_mapped_files", description = "List memory-mapped files" },
            new { name = "list_pinvoke_pipes", description = "List pipes via P/Invoke" },
            new { name = "list_com_objects", description = "List available COM objects" },
            new { name = "read_named_pipe", description = "Read from a named pipe" },
            new { name = "send_named_pipe_message", description = "Send message to named pipe" },
            new { name = "wait_for_named_pipe_message", description = "Wait for message on named pipe" },
            new { name = "read_mapped_file", description = "Read from memory-mapped file" },
            new { name = "send_mapped_file_message", description = "Write to memory-mapped file" },
            new { name = "send_pinvoke_message", description = "Send message via P/Invoke" },
            new { name = "send_com_message", description = "Send message via COM" },
            new { name = "search_registry", description = "Search the Windows registry using glob patterns, similar to regedit search. Supports searching in key names, value names, and value data with multithreaded performance." }
        }
    };

    private async Task<McpResponse> HandleToolsCall(McpRequest request, McpResponse response)
    {
        if (!request.Params.HasValue)
        {
            response.Error = new McpError { Code = -32602, Message = "Invalid params" };
            return response;
        }

        var (toolName, arguments) = ExtractToolCallParams(request.Params.Value);
        if (string.IsNullOrEmpty(toolName))
        {
            response.Error = new McpError { Code = -32602, Message = "Invalid params" };
            return response;
        }

        try
        {
            response.Result = await HandleToolCall(toolName, arguments);
        }
        catch (Exception ex)
        {
            response.Error = new McpError { Code = -32603, Message = "Internal error", Data = ex.Message };
        }
        
        return response;
    }

    private static (string? toolName, Dictionary<string, object>? arguments) ExtractToolCallParams(JsonElement paramsElement)
    {
        try
        {
            var toolCall = JsonSerializer.Deserialize<ToolCallParams>(paramsElement, JsonOptions);
            if (toolCall?.Name != null)
            {
                var arguments = toolCall.Arguments?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : kvp.Value);
                return (toolCall.Name, arguments);
            }
        }
        catch
        {
            // Fall through to direct extraction
        }

        if (paramsElement.TryGetProperty("name", out var nameElement) &&
            paramsElement.TryGetProperty("arguments", out var argsElement))
        {
            var name = nameElement.GetString();
            var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argsElement, JsonOptions);
            return (name, ConvertArguments(arguments));
        }

        return (null, null);
    }

    private static Dictionary<string, object>? ConvertArguments(Dictionary<string, object>? arguments)
    {
        if (arguments == null) return null;

        var result = new Dictionary<string, object>();
        foreach (var kvp in arguments)
        {
            result[kvp.Key] = kvp.Value is JsonElement jsonElement 
                ? ConvertJsonElement(jsonElement) 
                : kvp.Value;
        }
        return result;
    }

    private static T GetArg<T>(Dictionary<string, object>? args, string key, T defaultValue) where T : IConvertible
    {
        if (args == null || !args.ContainsKey(key)) return defaultValue;
        try
        {
            return (T)Convert.ChangeType(args[key], typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string? GetArgString(Dictionary<string, object>? args, string key, string? defaultValue = null)
    {
        return args?.GetValueOrDefault(key)?.ToString() ?? defaultValue;
    }

    private async Task<object> HandleToolCall(string toolName, Dictionary<string, object>? arguments)
    {
        return toolName switch
        {
            "list_named_pipes" => CreateTextResponse(string.Join("\n", _namedPipeService.ListNamedPipes())),
            "find_named_pipe" => CreateTextResponse(string.Join("\n", _namedPipeService.FindNamedPipe(
                GetArgString(arguments, "pattern") ?? "",
                GetArg<bool>(arguments, "caseSensitive", false)))),
            "wait_for_named_pipe" => CreateTextResponse(await _namedPipeService.WaitForNamedPipe(
                GetArgString(arguments, "pipeName") ?? "",
                GetArg<int>(arguments, "timeout", 30000),
                GetArg<int>(arguments, "checkInterval", 500))),
            "list_mapped_files" => CreateTextResponse(string.Join("\n", _mmfService.ListMappedFiles())),
            "list_pinvoke_pipes" => CreateTextResponse(string.Join("\n", _pInvokeService.ListPInvokePipes())),
            "list_com_objects" => CreateTextResponse(string.Join("\n", _comService.ListComObjects())),
            "read_named_pipe" => CreateTextResponse(await _namedPipeService.ReadNamedPipe(
                GetArgString(arguments, "pipeName") ?? "",
                GetArg<int>(arguments, "timeout", 5000))),
            "send_named_pipe_message" => CreateTextResponse(await _namedPipeService.SendNamedPipeMessage(
                GetArgString(arguments, "pipeName") ?? "",
                GetArgString(arguments, "message") ?? "",
                GetArg<int>(arguments, "timeout", 5000))),
            "wait_for_named_pipe_message" => CreateTextResponse(await _namedPipeService.WaitForNamedPipeMessage(
                GetArgString(arguments, "pipeName") ?? "",
                GetArg<int>(arguments, "timeout", 30000))),
            "read_mapped_file" => CreateTextResponse(_mmfService.ReadMappedFile(
                GetArgString(arguments, "mapName") ?? "",
                GetArg<long>(arguments, "offset", 0),
                GetArg<int>(arguments, "length", 4096))),
            "send_mapped_file_message" => CreateTextResponse(_mmfService.SendMappedFileMessage(
                GetArgString(arguments, "mapName") ?? "",
                GetArgString(arguments, "message") ?? "",
                GetArg<long>(arguments, "offset", 0))),
            "send_pinvoke_message" => CreateTextResponse(_pInvokeService.SendPInvokeMessage(
                GetArgString(arguments, "target") ?? "",
                GetArgString(arguments, "message") ?? "")),
            "send_com_message" => CreateTextResponse(_comService.SendComMessage(
                GetArgString(arguments, "progId") ?? "",
                GetArgString(arguments, "method") ?? "",
                GetArgString(arguments, "parameters") != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(GetArgString(arguments, "parameters") ?? "{}", JsonOptions)
                    : null)),
            "search_registry" => CreateTextResponse(_registryService.SearchRegistry(
                GetArgString(arguments, "query") ?? "",
                GetArgString(arguments, "path"),
                GetArg<bool>(arguments, "search_keys", true),
                GetArg<bool>(arguments, "search_values", true),
                GetArg<bool>(arguments, "search_data", true),
                GetArgString(arguments, "hive", "HKEY_CURRENT_USER") ?? "HKEY_CURRENT_USER")),
            _ => throw new Exception($"Unknown tool: {toolName}")
        };
    }

    private static object CreateTextResponse(string text) => new { content = new[] { new { type = "text", text } } };

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : (object)element.GetInt64(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                prop => prop.Name,
                prop => ConvertJsonElement(prop.Value)),
            _ => element.GetRawText()
        };
    }
}
