using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcMcp.Models;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
    
    [JsonPropertyName("method")]
    public string? Method { get; set; }
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class ToolCallParams
{
    public string? Name { get; set; }
    public Dictionary<string, object>? Arguments { get; set; }
}
