using System.ComponentModel;
using ModelContextProtocol.Server;
using IpcMcp.Services;
using System;

namespace IpcMcp.Tools;

/// <summary>
/// Disabled tools - These tools are commented out by default.
/// To enable any of these tools, uncomment them and move them to IpcTools.cs
/// </summary>
public static partial class IpcTools
{
    // List named pipes tool - DISABLED BY DEFAULT
    // Uncomment to enable this tool
    /*
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
    */

    // Find named pipe tool - DISABLED BY DEFAULT
    // Uncomment to enable this tool
    /*
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
    */

    // Wait for named pipe tool - DISABLED (integrated into NamedPipe)
    // Uncomment to enable this tool
    /*
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
    */

    // Read named pipe tool - DISABLED (integrated into NamedPipe)
    // Uncomment to enable this tool
    /*
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
    */

    // Read mapped file tool - DISABLED (integrated into MappedFile)
    // Uncomment to enable this tool
    /*
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
    */

    // Write mapped file tool - DISABLED (integrated into MappedFile)
    // Uncomment to enable this tool
    /*
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
    */

    // List COM objects tool - DISABLED BY DEFAULT
    // Uncomment to enable this tool
    /*
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
    */

    // Process management tools - DISABLED BY DEFAULT
    // Uncomment to enable these tools
    /*
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
    */

    // Service management tools - DISABLED BY DEFAULT
    // Uncomment to enable these tools
    /*
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
    */

    // Window enumeration tool - DISABLED BY DEFAULT
    // Uncomment to enable this tool
    /*
    [McpServerTool, Description("List all windows with their titles, classes, handles, PIDs, and other properties.")]
    public static string ListWindows(IServiceProvider serviceProvider)
    {
        try
        {
            var service = GetService<WindowService>(serviceProvider);
            var windows = service.ListWindows();
            
            var result = new System.Text.StringBuilder();
            result.AppendLine("Handle\tPID\tThreadID\tTitle\tClassName\tVisible\tEnabled\tX\tY\tWidth\tHeight");
            result.AppendLine("------\t---\t--------\t-----\t---------\t-------\t-------\t-\t-\t-----\t------");
            
            foreach (var window in windows)
            {
                result.AppendLine($"{window.Handle}\t{window.ProcessId}\t{window.ThreadId}\t{window.Title}\t{window.ClassName}\t{window.IsVisible}\t{window.IsEnabled}\t{window.X}\t{window.Y}\t{window.Width}\t{window.Height}");
            }
            
            return result.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return FormatError("list_windows", ex);
        }
    }
    */
}
