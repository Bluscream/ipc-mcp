using System.ComponentModel;
using ModelContextProtocol.Server;
using IpcMcp.Services;
using System;

namespace IpcMcp.Tools;

[McpServerToolType]
public static class IpcTools
{
    private static T GetService<T>(IServiceProvider serviceProvider) where T : class
    {
        return serviceProvider.GetRequiredService<T>();
    }

    private static string FormatError(string toolName, Exception ex)
    {
        var errorMessage = $"Failed to execute {toolName}!\n\n{ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException != null)
        {
            errorMessage += $"\n\nInner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }
        return errorMessage;
    }

    private static bool IsToolEnabled(string toolCategory)
    {
        // Check environment variable: IPC_MCP_ENABLED_TOOLS
        // Format: comma-separated list like "pipes,processes,services" or "all"
        var enabledTools = Environment.GetEnvironmentVariable("IPC_MCP_ENABLED_TOOLS") ?? "all";
        
        if (enabledTools.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        var categories = enabledTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return categories.Any(c => c.Equals(toolCategory, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDisabledMessage(string toolName, string category)
    {
        return $"Tool '{toolName}' is disabled. Enable it by setting environment variable IPC_MCP_ENABLED_TOOLS to include '{category}' or 'all'.";
    }

    [McpServerTool, Description("List all available named pipes")]
    public static string ListNamedPipes(IServiceProvider serviceProvider)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return string.Join("\n", service.ListNamedPipes());
        }
        catch (Exception ex)
        {
            return FormatError("list_named_pipes", ex);
        }
    }

    [McpServerTool, Description("Find named pipes matching a pattern")]
    public static string FindNamedPipe(
        IServiceProvider serviceProvider,
        [Description("The search pattern")] string pattern,
        [Description("Whether the search is case-sensitive")] bool caseSensitive = false)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            var pipes = service.FindNamedPipe(pattern, caseSensitive);
            return string.Join("\n", pipes);
        }
        catch (Exception ex)
        {
            return FormatError("find_named_pipe", ex);
        }
    }

    [McpServerTool, Description("Wait for a named pipe to become available")]
    public static async Task<string> WaitForNamedPipe(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to wait for")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        [Description("Check interval in milliseconds")] int checkInterval = 500)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return await service.WaitForNamedPipe(pipeName, timeout, checkInterval);
        }
        catch (Exception ex)
        {
            return FormatError("wait_for_named_pipe", ex);
        }
    }

    [McpServerTool, Description("Read from a named pipe")]
    public static async Task<string> ReadNamedPipe(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to read from")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 5000)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return await service.ReadNamedPipe(pipeName, timeout);
        }
        catch (Exception ex)
        {
            return FormatError("read_named_pipe", ex);
        }
    }

    [McpServerTool, Description("Send message to named pipe")]
    public static async Task<string> SendNamedPipeMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to send to")] string pipeName,
        [Description("Message to send")] string message,
        [Description("Timeout in milliseconds")] int timeout = 5000)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return await service.SendNamedPipeMessage(pipeName, message, timeout);
        }
        catch (Exception ex)
        {
            return FormatError("send_named_pipe_message", ex);
        }
    }

    [McpServerTool, Description("Wait for message on named pipe. First waits for the pipe to become available, then waits for a message. Optionally filters messages by regex pattern.")]
    public static async Task<string> WaitForNamedPipeMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe to wait on")] string pipeName,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        [Description("Check interval in milliseconds when waiting for pipe")] int checkInterval = 500,
        [Description("Optional regex pattern to filter messages. Only returns when a message matches the pattern.")] string? pattern = null)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return await service.WaitForNamedPipeMessage(pipeName, timeout, checkInterval, pattern);
        }
        catch (Exception ex)
        {
            return FormatError("wait_for_named_pipe_message", ex);
        }
    }

    [McpServerTool, Description("Read from memory-mapped file")]
    public static string ReadMappedFile(
        IServiceProvider serviceProvider,
        [Description("Name of the memory-mapped file")] string mapName,
        [Description("Offset to start reading from")] long offset = 0,
        [Description("Number of bytes to read")] int length = 4096)
    {
        try
        {
            var service = GetService<MemoryMappedFileService>(serviceProvider);
            return service.ReadMappedFile(mapName, offset, length);
        }
        catch (Exception ex)
        {
            return FormatError("read_mapped_file", ex);
        }
    }

    [McpServerTool, Description("Write to memory-mapped file")]
    public static string SendMappedFileMessage(
        IServiceProvider serviceProvider,
        [Description("Name of the memory-mapped file")] string mapName,
        [Description("Message to write")] string message,
        [Description("Offset to start writing at")] long offset = 0)
    {
        try
        {
            var service = GetService<MemoryMappedFileService>(serviceProvider);
            return service.SendMappedFileMessage(mapName, message, offset);
        }
        catch (Exception ex)
        {
            return FormatError("send_mapped_file_message", ex);
        }
    }

    [McpServerTool, Description("List available COM objects")]
    public static string ListComObjects(IServiceProvider serviceProvider)
    {
        try
        {
            var service = GetService<ComService>(serviceProvider);
            return string.Join("\n", service.ListComObjects());
        }
        catch (Exception ex)
        {
            return FormatError("list_com_objects", ex);
        }
    }

    [McpServerTool, Description("Send message via COM")]
    public static string SendComMessage(
        IServiceProvider serviceProvider,
        [Description("COM ProgID")] string progId,
        [Description("Method to call")] string method,
        [Description("Parameters as JSON string")] string? parameters = null)
    {
        try
        {
            var service = GetService<ComService>(serviceProvider);
            Dictionary<string, object>? paramsDict = null;
            
            if (!string.IsNullOrEmpty(parameters))
            {
                paramsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parameters);
            }
            
            return service.SendComMessage(progId, method, paramsDict);
        }
        catch (Exception ex)
        {
            return FormatError("send_com_message", ex);
        }
    }

    [McpServerTool, Description("Execute a shell command with admin privileges. Returns the command output.")]
    public static async Task<string> ShellExecute(
        IServiceProvider serviceProvider,
        [Description("Command or executable to run")] string command,
        [Description("Command arguments")] string? arguments = null,
        [Description("Timeout in milliseconds")] int timeout = 30000)
    {
        try
        {
            var service = GetService<ProcessService>(serviceProvider);
            return await service.ShellExecute(command, arguments, timeout);
        }
        catch (Exception ex)
        {
            return FormatError("shell_execute", ex);
        }
    }

    [McpServerTool, Description("Start an application with admin privileges. Can optionally wait for the process to exit.")]
    public static string StartProcess(
        IServiceProvider serviceProvider,
        [Description("Path to executable or application name")] string executable,
        [Description("Command line arguments")] string? arguments = null,
        [Description("Whether to wait for the process to exit")] bool waitForExit = false,
        [Description("Timeout in milliseconds if waiting for exit")] int timeout = 30000)
    {
        try
        {
            var service = GetService<ProcessService>(serviceProvider);
            return service.StartProcess(executable, arguments, waitForExit, timeout);
        }
        catch (Exception ex)
        {
            return FormatError("start_process", ex);
        }
    }

    [McpServerTool, Description("List all running processes with their PIDs, names, and command lines.")]
    public static string ListProcesses(
        IServiceProvider serviceProvider,
        [Description("Timeout in milliseconds")] int timeout = 30000)
    {
        try
        {
            var service = GetService<ProcessService>(serviceProvider);
            return service.ListProcesses(timeout);
        }
        catch (Exception ex)
        {
            return FormatError("list_processes", ex);
        }
    }

    [McpServerTool, Description("Kill one or more processes by PID or name. Returns success/failure status for each process.")]
    public static string KillProcess(
        IServiceProvider serviceProvider,
        [Description("List of process names to kill (e.g., [\"notepad.exe\", \"chrome.exe\"])")] List<string>? names = null,
        [Description("List of process IDs to kill (e.g., [1234, 5678])")] List<int>? ids = null)
    {
        try
        {
            var service = GetService<ProcessService>(serviceProvider);
            return service.KillProcess(names, ids);
        }
        catch (Exception ex)
        {
            return FormatError("kill_process", ex);
        }
    }

    [McpServerTool, Description("List all Windows services with their status and start type.")]
    public static string ListServices(IServiceProvider serviceProvider)
    {
        try
        {
            var service = GetService<ServiceService>(serviceProvider);
            return service.ListServices();
        }
        catch (Exception ex)
        {
            return FormatError("list_services", ex);
        }
    }

    [McpServerTool, Description("Start a Windows service.")]
    public static string StartService(
        IServiceProvider serviceProvider,
        [Description("Name of the service to start")] string serviceName)
    {
        try
        {
            var service = GetService<ServiceService>(serviceProvider);
            return service.StartService(serviceName);
        }
        catch (Exception ex)
        {
            return FormatError("start_service", ex);
        }
    }

    [McpServerTool, Description("Stop a Windows service.")]
    public static string StopService(
        IServiceProvider serviceProvider,
        [Description("Name of the service to stop")] string serviceName)
    {
        try
        {
            var service = GetService<ServiceService>(serviceProvider);
            return service.StopService(serviceName);
        }
        catch (Exception ex)
        {
            return FormatError("stop_service", ex);
        }
    }
}
