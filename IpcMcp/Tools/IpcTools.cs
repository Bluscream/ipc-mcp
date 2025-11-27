using System.ComponentModel;
using ModelContextProtocol.Server;
using IpcMcp.Services;

namespace IpcMcp.Tools;

[McpServerToolType]
public static class IpcTools
{
    private static T GetService<T>(IServiceProvider serviceProvider) where T : class
    {
        return serviceProvider.GetRequiredService<T>();
    }

    [McpServerTool, Description("List all available named pipes")]
    public static string ListNamedPipes(IServiceProvider serviceProvider)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        return string.Join("\n", service.ListNamedPipes());
    }

    [McpServerTool, Description("Find named pipes matching a pattern")]
    public static string FindNamedPipe(
        IServiceProvider serviceProvider,
        [Description("The search pattern")] string pattern,
        [Description("Whether the search is case-sensitive")] bool caseSensitive = false)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        var pipes = service.FindNamedPipe(pattern, caseSensitive);
        return string.Join("\n", pipes);
    }

    [McpServerTool, Description("Wait for a named pipe to become available")]
    public static async Task<string> WaitForNamedPipe(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to wait for")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        [Description("Check interval in milliseconds")] int checkInterval = 500)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        return await service.WaitForNamedPipe(pipeName, timeout, checkInterval);
    }

    [McpServerTool, Description("Read from a named pipe")]
    public static async Task<string> ReadNamedPipe(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to read from")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 5000)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        return await service.ReadNamedPipe(pipeName, timeout);
    }

    [McpServerTool, Description("Send message to named pipe")]
    public static async Task<string> SendNamedPipeMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to send to")] string pipeName,
        [Description("Message to send")] string message,
        [Description("Timeout in milliseconds")] int timeout = 5000)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        return await service.SendNamedPipeMessage(pipeName, message, timeout);
    }

    [McpServerTool, Description("Wait for message on named pipe. First waits for the pipe to become available, then waits for a message.")]
    public static async Task<string> WaitForNamedPipeMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to wait on")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        [Description("Check interval in milliseconds when waiting for pipe")] int checkInterval = 500)
    {
        var service = GetService<NamedPipeService>(serviceProvider);
        return await service.WaitForNamedPipeMessage(pipeName, timeout, checkInterval);
    }

    [McpServerTool, Description("Read from memory-mapped file")]
    public static string ReadMappedFile(
        IServiceProvider serviceProvider,
        [Description("Name of the memory-mapped file")] string mapName,
        [Description("Offset to start reading from")] long offset = 0,
        [Description("Number of bytes to read")] int length = 4096)
    {
        var service = GetService<MemoryMappedFileService>(serviceProvider);
        return service.ReadMappedFile(mapName, offset, length);
    }

    [McpServerTool, Description("Write to memory-mapped file")]
    public static string SendMappedFileMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the memory-mapped file")] string mapName,
        [Description("Message to write")] string message,
        [Description("Offset to start writing at")] long offset = 0)
    {
        var service = GetService<MemoryMappedFileService>(serviceProvider);
        return service.SendMappedFileMessage(mapName, message, offset);
    }

    [McpServerTool, Description("List available COM objects")]
    public static string ListComObjects(IServiceProvider serviceProvider)
    {
        var service = GetService<ComService>(serviceProvider);
        return string.Join("\n", service.ListComObjects());
    }

    [McpServerTool, Description("Send message via COM")]
    public static string SendComMessage(
        IServiceProvider serviceProvider,
        [Description("COM ProgID")] string progId,
        [Description("Method to call")] string method,
        [Description("Parameters as JSON string")] string? parameters = null)
    {
        var service = GetService<ComService>(serviceProvider);
        Dictionary<string, object>? paramsDict = null;
        
        if (!string.IsNullOrEmpty(parameters))
        {
            paramsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parameters);
        }
        
        return service.SendComMessage(progId, method, paramsDict);
    }

    [McpServerTool, Description("Execute a shell command with admin privileges. Returns the command output.")]
    public static async Task<string> ShellExecute(
        IServiceProvider serviceProvider,
        [Description("Command or executable to run")] string command,
        [Description("Command arguments")] string? arguments = null,
        [Description("Timeout in milliseconds")] int timeout = 30000)
    {
        var service = GetService<ProcessService>(serviceProvider);
        return await service.ShellExecute(command, arguments, timeout);
    }

    [McpServerTool, Description("Start an application with admin privileges. Can optionally wait for the process to exit.")]
    public static string StartProcess(
        IServiceProvider serviceProvider,
        [Description("Path to executable or application name")] string executable,
        [Description("Command line arguments")] string? arguments = null,
        [Description("Whether to wait for the process to exit")] bool waitForExit = false,
        [Description("Timeout in milliseconds if waiting for exit")] int timeout = 30000)
    {
        var service = GetService<ProcessService>(serviceProvider);
        return service.StartProcess(executable, arguments, waitForExit, timeout);
    }

    [McpServerTool, Description("List all running processes with their PIDs, names, and command lines.")]
    public static string ListProcesses(IServiceProvider serviceProvider)
    {
        var service = GetService<ProcessService>(serviceProvider);
        return service.ListProcesses();
    }

    [McpServerTool, Description("Kill one or more processes by PID or name. Returns success/failure status for each process.")]
    public static string KillProcess(
        IServiceProvider serviceProvider,
        [Description("List of process names to kill (e.g., [\"notepad.exe\", \"chrome.exe\"])")] List<string>? names = null,
        [Description("List of process IDs to kill (e.g., [1234, 5678])")] List<int>? ids = null)
    {
        var service = GetService<ProcessService>(serviceProvider);
        return service.KillProcess(names, ids);
    }
}
