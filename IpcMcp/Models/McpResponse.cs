using System.Text.Json;

namespace IpcMcp.Models;

public class McpResponse
{
    public string? JsonRpc { get; set; } = "2.0";
    public JsonElement? Id { get; set; }
    public object? Result { get; set; }
    public McpError? Error { get; set; }
}

public class McpError
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}
