# IPC MCP Server

A .NET 10 HTTP-based MCP server for Windows IPC operations.

## Features

- **Named Pipes**: List, read, send, wait for messages
- **Memory-Mapped Files**: List, read, write
- **P/Invoke IPC**: Access Windows APIs directly
- **COM Objects**: Interact with COM components

## Setup

1. Build the server:
```powershell
cd IpcMcp
dotnet build -c Release
```

2. Run as Administrator (recommended) with a token:
```powershell
.\run_server.ps1 -Token "your-secret-token-here"
```

3. Configure in `mcp.json`:
```json
{
  "ipc": {
    "url": "http://localhost:23481",
    "headers": {
      "Authorization": "Bearer your-secret-token-here"
    }
  }
}
```

**Security Notes:**
- The server only accepts connections from localhost (127.0.0.1)
- A token is required for authentication
- Use a strong, random token in production

## Available Tools

### Named Pipes
- `list_named_pipes` - List all available named pipes
- `read_named_pipe` - Read from a named pipe
- `send_named_pipe_message` - Send message to named pipe
- `wait_for_named_pipe_message` - Wait for message on named pipe

### Memory-Mapped Files
- `list_mapped_files` - List memory-mapped files
- `read_mapped_file` - Read from memory-mapped file
- `send_mapped_file_message` - Write to memory-mapped file

### P/Invoke
- `list_pinvoke_pipes` - List pipes via P/Invoke
- `send_pinvoke_message` - Send message via P/Invoke

### COM
- `list_com_objects` - List available COM objects
- `send_com_message` - Send message via COM

## Usage Examples

### List Named Pipes
```json
{
  "tool": "list_named_pipes",
  "arguments": {}
}
```

### Read from Named Pipe
```json
{
  "tool": "read_named_pipe",
  "arguments": {
    "pipeName": "my-pipe",
    "timeout": 5000
  }
}
```

### Send Message to Named Pipe
```json
{
  "tool": "send_named_pipe_message",
  "arguments": {
    "pipeName": "my-pipe",
    "message": "Hello from MCP!",
    "timeout": 5000
  }
}
```

## Configuration

Set the port via environment variable:
```powershell
$env:IPC_MCP_PORT = "9090"
.\run_server.ps1
```

## Security

⚠️ **This server provides access to Windows IPC mechanisms!**

- Run with Administrator privileges for full access
- Some operations may require elevated permissions
- Use with caution in production environments
