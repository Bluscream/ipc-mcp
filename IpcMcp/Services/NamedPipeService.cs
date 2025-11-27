using System.IO.Pipes;
using System.Text;

namespace IpcMcp.Services;

public class NamedPipeService
{
    public List<string> ListNamedPipes()
    {
        var pipes = new List<string>();
        try
        {
            var pipeNames = Directory.GetFiles(@"\\.\pipe\");
            foreach (var pipe in pipeNames)
            {
                pipes.Add(pipe.Replace(@"\\.\pipe\", ""));
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to list named pipes: {ex.Message}");
        }
        return pipes;
    }

    public async Task<string> ReadNamedPipe(string pipeName, int timeoutMs = 5000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            await client.ConnectAsync(timeoutMs);
            
            using var reader = new StreamReader(client, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to read from pipe '{pipeName}': {ex.Message}");
        }
    }

    public async Task<string> SendNamedPipeMessage(string pipeName, string message, int timeoutMs = 5000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            await client.ConnectAsync(timeoutMs);
            
            var bytes = Encoding.UTF8.GetBytes(message);
            await client.WriteAsync(bytes, 0, bytes.Length);
            await client.FlushAsync();
            return "Message sent successfully";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send message to pipe '{pipeName}': {ex.Message}");
        }
    }

    public async Task<string> WaitForNamedPipeMessage(string pipeName, int timeoutMs = 30000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            await client.ConnectAsync(timeoutMs);
            
            using var reader = new StreamReader(client, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to wait for message on pipe '{pipeName}': {ex.Message}");
        }
    }

    public async Task<string> SendAndReceiveNamedPipe(string pipeName, string message, int timeoutMs = 5000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            await client.ConnectAsync(timeoutMs);
            
            // Send
            var sendBytes = Encoding.UTF8.GetBytes(message);
            await client.WriteAsync(sendBytes, 0, sendBytes.Length);
            await client.FlushAsync();
            
            // Receive
            using var reader = new StreamReader(client, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send/receive on pipe '{pipeName}': {ex.Message}");
        }
    }

    public List<string> FindNamedPipe(string pattern, bool caseSensitive = false)
    {
        try
        {
            var allPipes = ListNamedPipes();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            return allPipes
                .Where(pipe => pipe.Contains(pattern, comparison))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to find named pipes with pattern '{pattern}': {ex.Message}");
        }
    }

    public async Task<string> WaitForNamedPipe(string pipeName, int timeoutMs = 30000, int checkIntervalMs = 500)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            
            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    // Try to connect to check if pipe exists and is available
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                    var connectTask = client.ConnectAsync(100); // Quick connection test
                    var delayTask = Task.Delay(100);
                    
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    
                    if (completedTask == connectTask && !connectTask.IsFaulted)
                    {
                        // Pipe is available - connection succeeded
                        await connectTask; // Ensure connection is complete
                        return $"Named pipe '{pipeName}' is now available";
                    }
                }
                catch
                {
                    // Pipe not available yet, continue waiting
                }
                
                // Wait before next check
                await Task.Delay(checkIntervalMs);
            }
            
            throw new TimeoutException($"Timeout waiting for named pipe '{pipeName}' to become available after {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to wait for named pipe '{pipeName}': {ex.Message}");
        }
    }
}
