using System.ComponentModel;
using ModelContextProtocol.Server;
using IpcMcp.Services;
using System;

namespace IpcMcp.Tools;

[McpServerToolType]
public static partial class IpcTools
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

    [McpServerTool, Description("Interact with a named pipe. If pattern is set to '.*', waits for a response. Otherwise, just waits for the pipe to exist. Optionally sends a message first.")]
    public static async Task<string> NamedPipe(
        IServiceProvider serviceProvider,
        [Description("Name of the pipe")] string pipeName,
        [Description("Optional message to send first")] string? message = null,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        [Description("Check interval in milliseconds when waiting for pipe")] int checkInterval = 500,
        [Description("Regex pattern to filter messages. Set to '.*' to wait for any response. If null, just waits for pipe to exist.")] string? pattern = null)
    {
        try
        {
            var service = GetService<NamedPipeService>(serviceProvider);
            return await service.NamedPipe(pipeName, message, timeout, checkInterval, pattern);
        }
        catch (Exception ex)
        {
            return FormatError("named_pipe", ex);
        }
    }


    [McpServerTool, Description("Read from or write to a memory-mapped file. If message is provided, writes to the file. Otherwise, reads from it.")]
    public static string MappedFile(
        IServiceProvider serviceProvider,
        [Description("Name of the memory-mapped file")] string mapName,
        [Description("Optional message to write. If not provided, reads from the file.")] string? message = null,
        [Description("Offset to start reading/writing from")] long offset = 0,
        [Description("Number of bytes to read (only used when reading)")] int length = 4096)
    {
        try
        {
            var service = GetService<MemoryMappedFileService>(serviceProvider);
            return service.MappedFile(mapName, message, offset, length);
        }
        catch (Exception ex)
        {
            return FormatError("mapped_file", ex);
        }
    }

    [McpServerTool, Description("Read from or write to the Windows registry. If valueName and value are provided, writes to the registry. Otherwise, reads from it.")]
    public static string Registry(
        IServiceProvider serviceProvider,
        [Description("Registry key path (e.g., 'Software\\MyApp\\Settings')")] string keyPath,
        [Description("Registry value name (optional, if not provided, lists all values in the key)")] string? valueName = null,
        [Description("Value to write (optional, if not provided, reads from registry)")] string? value = null,
        [Description("Value type for writing (String, DWord, QWord, Binary, MultiString, ExpandString)")] string valueType = "String",
        [Description("Registry hive (HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, HKEY_CLASSES_ROOT, HKEY_USERS, HKEY_CURRENT_CONFIG)")] string hive = "HKEY_CURRENT_USER")
    {
        try
        {
            var service = GetService<RegistryService>(serviceProvider);
            
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(valueName))
            {
                // Write mode
                return service.WriteRegistry(keyPath, valueName, value, valueType, hive);
            }
            else
            {
                // Read mode
                return service.ReadRegistry(keyPath, valueName, hive);
            }
        }
        catch (Exception ex)
        {
            return FormatError("registry", ex);
        }
    }


    [McpServerTool, Description("Query COM database or call COM object methods. If method is provided, calls the method. Otherwise, queries the COM object or lists all COM objects.")]
    public static string Com(
        IServiceProvider serviceProvider,
        [Description("COM ProgID (optional if querying)")] string? progId = null,
        [Description("Method to call (optional, if not provided, queries the COM object)")] string? method = null,
        [Description("Parameters as JSON string (only used when calling a method)")] string? parameters = null,
        [Description("CLSID to query (alternative to ProgID)")] string? clsid = null)
    {
        try
        {
            var service = GetService<ComService>(serviceProvider);
            
            // If method is provided, call it
            if (!string.IsNullOrEmpty(method) && !string.IsNullOrEmpty(progId))
            {
                Dictionary<string, object>? paramsDict = null;
                
                if (!string.IsNullOrEmpty(parameters))
                {
                    paramsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parameters);
                }
                
                return service.SendComMessage(progId, method, paramsDict);
            }
            else
            {
                // Query COM database
                return service.QueryComObject(progId, clsid);
            }
        }
        catch (Exception ex)
        {
            return FormatError("com", ex);
        }
    }

    [McpServerTool, Description("Execute a shell command. Returns the command output. Will run as admin if the server is running as admin.")]
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


    [McpServerTool, Description("List all available IPC resources (processes, windows, COM objects, named pipes, services, memory-mapped files) as a compact JSON object.")]
    public static string List(
        IServiceProvider serviceProvider,
        [Description("Timeout in milliseconds for listing processes")] int processTimeout = 30000)
    {
        try
        {
            var result = new Dictionary<string, object>();

            // List Processes
            try
            {
                var processService = GetService<ProcessService>(serviceProvider);
                var processText = processService.ListProcesses(processTimeout);
                var processes = new List<Dictionary<string, string>>();
                
                var lines = processText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 2) // Skip header lines
                {
                    for (int i = 2; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split('\t');
                        if (parts.Length >= 3)
                        {
                            processes.Add(new Dictionary<string, string>
                            {
                                { "PID", parts[0] },
                                { "Name", parts[1] },
                                { "CommandLine", parts.Length > 2 ? parts[2] : "" }
                            });
                        }
                    }
                }
                result["Processes"] = processes;
            }
            catch (Exception ex)
            {
                result["Processes"] = new[] { new { Error = ex.Message } };
            }

            // List Windows
            try
            {
                var windowService = GetService<WindowService>(serviceProvider);
                var windows = windowService.ListWindows();
                result["Windows"] = windows.Select(w => new Dictionary<string, object>
                {
                    { "Handle", w.Handle.ToString() },
                    { "Title", w.Title },
                    { "ClassName", w.ClassName },
                    { "ProcessId", w.ProcessId },
                    { "ThreadId", w.ThreadId },
                    { "IsVisible", w.IsVisible },
                    { "IsEnabled", w.IsEnabled },
                    { "X", w.X },
                    { "Y", w.Y },
                    { "Width", w.Width },
                    { "Height", w.Height }
                }).ToList();
            }
            catch (Exception ex)
            {
                result["Windows"] = new[] { new { Error = ex.Message } };
            }

            // List COM Objects
            try
            {
                var comService = GetService<ComService>(serviceProvider);
                result["COM Objects"] = comService.ListComObjects();
            }
            catch (Exception ex)
            {
                result["COM Objects"] = new[] { new { Error = ex.Message } };
            }

            // List Named Pipes
            try
            {
                var namedPipeService = GetService<NamedPipeService>(serviceProvider);
                result["Named Pipes"] = namedPipeService.ListNamedPipes();
            }
            catch (Exception ex)
            {
                result["Named Pipes"] = new[] { new { Error = ex.Message } };
            }

            // List Services
            try
            {
                var serviceService = GetService<ServiceService>(serviceProvider);
                var serviceText = serviceService.ListServices();
                var services = new List<Dictionary<string, string>>();
                
                var lines = serviceText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 2) // Skip header lines
                {
                    for (int i = 2; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split('\t');
                        if (parts.Length >= 4)
                        {
                            services.Add(new Dictionary<string, string>
                            {
                                { "Name", parts[0] },
                                { "DisplayName", parts[1] },
                                { "Status", parts[2] },
                                { "StartType", parts[3] }
                            });
                        }
                    }
                }
                result["Services"] = services;
            }
            catch (Exception ex)
            {
                result["Services"] = new[] { new { Error = ex.Message } };
            }

            // List Memory-Mapped Files
            try
            {
                var mmfService = GetService<MemoryMappedFileService>(serviceProvider);
                result["Memory-Mapped Files"] = mmfService.ListMappedFiles();
            }
            catch (Exception ex)
            {
                result["Memory-Mapped Files"] = new[] { new { Error = ex.Message } };
            }

            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return FormatError("list", ex);
        }
    }
}
