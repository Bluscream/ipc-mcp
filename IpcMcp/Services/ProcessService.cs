using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Management;

namespace IpcMcp.Services;

public class ProcessService
{
    public async Task<string> ShellExecute(string command, string? arguments = null, int timeoutMs = 30000)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments ?? "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await Task.Run(() => process.WaitForExit(), cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw new TimeoutException($"Command '{command}' timed out after {timeoutMs}ms");
            }

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            if (process.ExitCode != 0)
            {
                var errorMsg = string.IsNullOrEmpty(error) ? "Command failed" : error;
                return $"Exit code: {process.ExitCode}\n{errorMsg}\n{output}";
            }

            return string.IsNullOrEmpty(output) ? "Command executed successfully (no output)" : output;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Command '{command}' timed out after {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to execute command '{command}': {ex.Message}");
        }
    }

    public string StartProcess(string executable, string? arguments = null, bool waitForExit = false, int timeoutMs = 30000)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception($"Failed to start process '{executable}'");
            }

            if (waitForExit)
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                var completed = process.WaitForExit(timeoutMs);
                
                if (!completed)
                {
                    throw new TimeoutException($"Process '{executable}' did not exit within {timeoutMs}ms");
                }

                return $"Process '{executable}' started and exited with code {process.ExitCode}";
            }

            return $"Process '{executable}' started successfully (PID: {process.Id})";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start application '{executable}': {ex.Message}");
        }
    }

    public string ListProcesses(int timeoutMs = 30000)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var processes = Process.GetProcesses();
            var result = new StringBuilder();
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            
            foreach (var process in processes.OrderBy(p => p.ProcessName))
            {
                // Check timeout periodically
                if ((DateTime.UtcNow - startTime) > timeout)
                {
                    throw new TimeoutException($"Listing processes timed out after {timeoutMs}ms");
                }
                
                try
                {
                    var processName = process.ProcessName;
                    var pid = process.Id;
                    string commandLine = "";
                    
                    try
                    {
                        // Try to get command line using WMI (more reliable)
                        using var searcher = new ManagementObjectSearcher(
                            $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            commandLine = obj["CommandLine"]?.ToString() ?? "";
                            break;
                        }
                    }
                    catch
                    {
                        // Fallback: try to get main module path
                        try
                        {
                            commandLine = process.MainModule?.FileName ?? "";
                        }
                        catch
                        {
                            commandLine = "N/A (access denied)";
                        }
                    }
                    
                    result.AppendLine($"{pid}\t{processName}\t{commandLine}");
                }
                catch
                {
                    // Skip processes we can't access
                    continue;
                }
            }
            
            return result.ToString().TrimEnd();
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to list processes: {ex.Message}");
        }
    }

    public string KillProcess(List<string>? names = null, List<int>? ids = null)
    {
        try
        {
            var processes = new List<Process>();
            
            // Collect processes by names
            if (names != null && names.Count > 0)
            {
                foreach (var name in names)
                {
                    var processName = name.Replace(".exe", "");
                    var foundProcesses = Process.GetProcessesByName(processName);
                    processes.AddRange(foundProcesses);
                }
            }
            
            // Collect processes by IDs
            if (ids != null && ids.Count > 0)
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var process = Process.GetProcessById(id);
                        if (!processes.Any(p => p.Id == id))
                        {
                            processes.Add(process);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process doesn't exist, skip it
                    }
                }
            }
            
            if (processes.Count == 0)
            {
                return "No processes found to kill";
            }
            
            var killed = new List<string>();
            var failed = new List<string>();
            
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    killed.Add($"{process.ProcessName} (PID: {process.Id})");
                }
                catch (Exception ex)
                {
                    failed.Add($"{process.ProcessName} (PID: {process.Id}): {ex.Message}");
                }
            }
            
            var result = new StringBuilder();
            if (killed.Count > 0)
            {
                result.AppendLine($"Successfully killed {killed.Count} process(es):");
                foreach (var item in killed)
                {
                    result.AppendLine($"  - {item}");
                }
            }
            
            if (failed.Count > 0)
            {
                result.AppendLine($"Failed to kill {failed.Count} process(es):");
                foreach (var item in failed)
                {
                    result.AppendLine($"  - {item}");
                }
            }
            
            return result.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to kill processes: {ex.Message}");
        }
    }
}
