using Microsoft.AspNetCore.Mvc;
using IpcMcp.Models;
using IpcMcp.Services;
using System.Text.Json;
using System.Linq;

namespace IpcMcp.Controllers;

[ApiController]
[Route("")]
public class McpController : ControllerBase
{
    private readonly NamedPipeService _namedPipeService;
    private readonly MemoryMappedFileService _mmfService;
    private readonly PInvokeService _pInvokeService;
    private readonly ComService _comService;

    public McpController(
        NamedPipeService namedPipeService,
        MemoryMappedFileService mmfService,
        PInvokeService pInvokeService,
        ComService comService)
    {
        _namedPipeService = namedPipeService;
        _mmfService = mmfService;
        _pInvokeService = pInvokeService;
        _comService = comService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleRequest([FromBody] JsonElement body)
    {
        // Try to deserialize the request - handle both direct JSON-RPC and wrapped formats
        McpRequest? request = null;
        try
        {
            // First try direct JSON-RPC format
            request = JsonSerializer.Deserialize<McpRequest>(body, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch
        {
            // If that fails, try wrapped format { "request": { ... } }
            if (body.TryGetProperty("request", out var requestElement))
            {
                request = JsonSerializer.Deserialize<McpRequest>(requestElement, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
        }

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
                    response.Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "ipc-mcp",
                            version = "1.0.0"
                        }
                    };
                    break;

                case "tools/list":
                    response.Result = new
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
                            new { name = "send_com_message", description = "Send message via COM" }
                        }
                    };
                    break;

                case "tools/call":
                    ToolCallParams? toolCall = null;
                    Dictionary<string, object>? argumentsDict = null;
                    
                    if (request.Params.HasValue)
                    {
                        // Try to deserialize as ToolCallParams first
                        try
                        {
                            toolCall = JsonSerializer.Deserialize<ToolCallParams>(
                                request.Params.Value,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            
                            // Convert JsonElement values in arguments to proper types
                            if (toolCall?.Arguments != null)
                            {
                                argumentsDict = new Dictionary<string, object>();
                                foreach (var kvp in toolCall.Arguments)
                                {
                                    if (kvp.Value is JsonElement jsonElement)
                                    {
                                        argumentsDict[kvp.Key] = ConvertJsonElement(jsonElement);
                                    }
                                    else
                                    {
                                        argumentsDict[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // If deserialization fails, try to extract name and arguments directly
                            if (request.Params.Value.TryGetProperty("name", out var nameElement))
                            {
                                var name = nameElement.GetString();
                                if (request.Params.Value.TryGetProperty("arguments", out var argsElement))
                                {
                                    argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                        argsElement,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                    
                                    toolCall = new ToolCallParams { Name = name, Arguments = argumentsDict };
                                }
                            }
                        }
                    }
                    
                    if (toolCall?.Name == null)
                    {
                        response.Error = new McpError { Code = -32602, Message = "Invalid params" };
                        break;
                    }

                    response.Result = await HandleToolCall(toolCall.Name, argumentsDict ?? toolCall.Arguments);
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

    private async Task<object> HandleToolCall(string toolName, Dictionary<string, object>? arguments)
    {
        return toolName switch
        {
            "list_named_pipes" => new { content = new[] { new { type = "text", text = string.Join("\n", _namedPipeService.ListNamedPipes()) } } },
            
            "find_named_pipe" => new
            {
                content = new[]
                {
                    new { type = "text", text = string.Join("\n", _namedPipeService.FindNamedPipe(
                        arguments?.GetValueOrDefault("pattern")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("caseSensitive") && Convert.ToBoolean(arguments["caseSensitive"])
                    ))}
                }
            },
            
            "wait_for_named_pipe" => new
            {
                content = new[]
                {
                    new { type = "text", text = await _namedPipeService.WaitForNamedPipe(
                        arguments?.GetValueOrDefault("pipeName")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("timeout") ? Convert.ToInt32(arguments["timeout"]) : 30000,
                        arguments != null && arguments.ContainsKey("checkInterval") ? Convert.ToInt32(arguments["checkInterval"]) : 500
                    )}
                }
            },
            
            "list_mapped_files" => new { content = new[] { new { type = "text", text = string.Join("\n", _mmfService.ListMappedFiles()) } } },
            "list_pinvoke_pipes" => new { content = new[] { new { type = "text", text = string.Join("\n", _pInvokeService.ListPInvokePipes()) } } },
            "list_com_objects" => new { content = new[] { new { type = "text", text = string.Join("\n", _comService.ListComObjects()) } } },
            
            "read_named_pipe" => new
            {
                content = new[]
                {
                    new { type = "text", text = await _namedPipeService.ReadNamedPipe(
                        arguments?.GetValueOrDefault("pipeName")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("timeout") ? Convert.ToInt32(arguments["timeout"]) : 5000
                    )}
                }
            },
            
            "send_named_pipe_message" => new
            {
                content = new[]
                {
                    new { type = "text", text = await _namedPipeService.SendNamedPipeMessage(
                        arguments?.GetValueOrDefault("pipeName")?.ToString() ?? "",
                        arguments?.GetValueOrDefault("message")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("timeout") ? Convert.ToInt32(arguments["timeout"]) : 5000
                    )}
                }
            },
            
            "wait_for_named_pipe_message" => new
            {
                content = new[]
                {
                    new { type = "text", text = await _namedPipeService.WaitForNamedPipeMessage(
                        arguments?.GetValueOrDefault("pipeName")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("timeout") ? Convert.ToInt32(arguments["timeout"]) : 30000
                    )}
                }
            },
            
            "read_mapped_file" => new
            {
                content = new[]
                {
                    new { type = "text", text = _mmfService.ReadMappedFile(
                        arguments?.GetValueOrDefault("mapName")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("offset") ? Convert.ToInt64(arguments["offset"]) : 0,
                        arguments != null && arguments.ContainsKey("length") ? Convert.ToInt32(arguments["length"]) : 4096
                    )}
                }
            },
            
            "send_mapped_file_message" => new
            {
                content = new[]
                {
                    new { type = "text", text = _mmfService.SendMappedFileMessage(
                        arguments?.GetValueOrDefault("mapName")?.ToString() ?? "",
                        arguments?.GetValueOrDefault("message")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("offset") ? Convert.ToInt64(arguments["offset"]) : 0
                    )}
                }
            },
            
            "send_pinvoke_message" => new
            {
                content = new[]
                {
                    new { type = "text", text = _pInvokeService.SendPInvokeMessage(
                        arguments?.GetValueOrDefault("target")?.ToString() ?? "",
                        arguments?.GetValueOrDefault("message")?.ToString() ?? ""
                    )}
                }
            },
            
            "send_com_message" => new
            {
                content = new[]
                {
                    new { type = "text", text = _comService.SendComMessage(
                        arguments?.GetValueOrDefault("progId")?.ToString() ?? "",
                        arguments?.GetValueOrDefault("method")?.ToString() ?? "",
                        arguments != null && arguments.ContainsKey("parameters") 
                            ? JsonSerializer.Deserialize<Dictionary<string, object>>(
                                arguments["parameters"]?.ToString() ?? "{}")
                            : null
                    )}
                }
            },
            
            _ => throw new Exception($"Unknown tool: {toolName}")
        };
    }

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
