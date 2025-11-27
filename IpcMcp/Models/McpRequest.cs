using System.Text.Json;

namespace IpcMcp.Models;

public class McpRequest
{
    public string? JsonRpc { get; set; } = "2.0";
    public JsonElement? Id { get; set; }
    public string? Method { get; set; }
    public JsonElement? Params { get; set; }
}

public class ToolCallParams
{
    public string? Name { get; set; }
    public Dictionary<string, object>? Arguments { get; set; }
}
