using System.Text.Json;
using System.Text.Json.Serialization;

namespace IpcMcp.Models;

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
