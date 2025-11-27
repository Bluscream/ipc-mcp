using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
            using var cts = new CancellationTokenSource(timeoutMs);
            var startTime = DateTime.UtcNow;
            
            // Try InOut first, then fall back to In if that fails
            NamedPipeClientStream? client = null;
            bool connected = false;
            
            try
            {
                client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                var connectTask = client.ConnectAsync(cts.Token);
                var connectTimeoutTask = Task.Delay(timeoutMs, CancellationToken.None);
                var connectCompleted = await Task.WhenAny(connectTask, connectTimeoutTask);
                
                if (connectCompleted == connectTask && !connectTask.IsFaulted)
                {
                    await connectTask;
                    connected = true;
                }
            }
            catch
            {
                // Try In direction instead
                client?.Dispose();
                client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                var connectTask = client.ConnectAsync(cts.Token);
                var connectTimeoutTask = Task.Delay(timeoutMs, CancellationToken.None);
                var connectCompleted = await Task.WhenAny(connectTask, connectTimeoutTask);
                
                if (connectCompleted == connectTask && !connectTask.IsFaulted)
                {
                    await connectTask;
                    connected = true;
                }
            }
            
            if (!connected || client == null)
            {
                throw new TimeoutException($"Timeout connecting to pipe '{pipeName}' after {timeoutMs}ms");
            }
            
            using (client)
            {
                // Calculate remaining time for read
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
                
                if (remainingTime <= 0)
                {
                    throw new TimeoutException($"Timeout reading from pipe '{pipeName}' - connection took too long");
                }
                
                using var reader = new StreamReader(client, Encoding.UTF8);
                using var readCts = new CancellationTokenSource(remainingTime);
                
                // Read with remaining timeout
                var readTask = reader.ReadToEndAsync(readCts.Token);
                var readTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
                var readCompleted = await Task.WhenAny(readTask, readTimeoutTask);
                
                if (readCompleted == readTimeoutTask)
                {
                    readCts.Cancel();
                    throw new TimeoutException($"Timeout reading from pipe '{pipeName}' after {timeoutMs}ms total");
                }
                
                return await readTask;
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timeout reading from pipe '{pipeName}' after {timeoutMs}ms");
        }
        catch (TimeoutException)
        {
            throw; // Re-throw timeout exceptions as-is
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
            var startTime = DateTime.UtcNow;
            using var cts = new CancellationTokenSource(timeoutMs);
            
            // Use InOut direction to allow both sending and receiving
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            
            // Connect with timeout
            var connectTask = client.ConnectAsync(cts.Token);
            var connectTimeoutTask = Task.Delay(timeoutMs, CancellationToken.None);
            var connectCompleted = await Task.WhenAny(connectTask, connectTimeoutTask);
            
            if (connectCompleted == connectTimeoutTask)
            {
                cts.Cancel();
                throw new TimeoutException($"Timeout connecting to pipe '{pipeName}' after {timeoutMs}ms");
            }
            
            await connectTask; // Ensure connection completed
            
            // Calculate remaining time for write
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                throw new TimeoutException($"Timeout sending message to pipe '{pipeName}' - connection took too long");
            }
            
            // Send the message
            var bytes = Encoding.UTF8.GetBytes(message);
            using var writeCts = new CancellationTokenSource(remainingTime);
            await client.WriteAsync(bytes, 0, bytes.Length, writeCts.Token);
            await client.FlushAsync(writeCts.Token);
            
            // Calculate remaining time for read response
            elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                // If no time left, return success message (message was sent)
                return "Message sent successfully (no time remaining for response)";
            }
            
            // Wait for and read the response
            using var reader = new StreamReader(client, Encoding.UTF8);
            using var readCts = new CancellationTokenSource(remainingTime);
            
            var readTask = reader.ReadToEndAsync(readCts.Token);
            var readTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
            var readCompleted = await Task.WhenAny(readTask, readTimeoutTask);
            
            if (readCompleted == readTimeoutTask)
            {
                readCts.Cancel();
                // If timeout waiting for response, return that message was sent but no response received
                return "Message sent successfully (no response received within timeout)";
            }
            
            var response = await readTask;
            return string.IsNullOrEmpty(response) ? "Message sent successfully (empty response)" : response;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timeout sending message to pipe '{pipeName}' after {timeoutMs}ms");
        }
        catch (TimeoutException)
        {
            throw; // Re-throw timeout exceptions as-is
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send message to pipe '{pipeName}': {ex.Message}");
        }
    }

    public async Task<string> WaitForNamedPipeMessage(string pipeName, int timeoutMs = 30000, int checkIntervalMs = 500, string? pattern = null)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            
            // Compile regex pattern if provided
            Regex? regex = null;
            if (!string.IsNullOrEmpty(pattern))
            {
                try
                {
                    regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Singleline);
                }
                catch (ArgumentException ex)
                {
                    throw new Exception($"Invalid regex pattern '{pattern}': {ex.Message}");
                }
            }
            
            // First, wait for the pipe to become available (like wait_for_named_pipe)
            NamedPipeClientStream? client = null;
            bool pipeAvailable = false;
            
            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    // Try InOut first, then fall back to In
                    try
                    {
                        client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                        var testConnectTask = client.ConnectAsync(100);
                        var testDelayTask = Task.Delay(100);
                        var testCompletedTask = await Task.WhenAny(testConnectTask, testDelayTask);
                        
                        if (testCompletedTask == testConnectTask && !testConnectTask.IsFaulted)
                        {
                            await testConnectTask;
                            pipeAvailable = true;
                            break;
                        }
                        client.Dispose();
                        client = null;
                    }
                    catch
                    {
                        client?.Dispose();
                        client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                        var testConnectTask = client.ConnectAsync(100);
                        var testDelayTask = Task.Delay(100);
                        var testCompletedTask = await Task.WhenAny(testConnectTask, testDelayTask);
                        
                        if (testCompletedTask == testConnectTask && !testConnectTask.IsFaulted)
                        {
                            await testConnectTask;
                            pipeAvailable = true;
                            break;
                        }
                        client.Dispose();
                        client = null;
                    }
                }
                catch
                {
                    // Pipe not available yet, continue waiting
                    client?.Dispose();
                    client = null;
                }
                
                // Wait before next check
                await Task.Delay(checkIntervalMs);
            }
            
            if (!pipeAvailable || client == null)
            {
                throw new TimeoutException($"Timeout waiting for named pipe '{pipeName}' to become available after {timeoutMs}ms");
            }
            
            using (client)
            {
                // Calculate remaining time for read
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
                
                if (remainingTime <= 0)
                {
                    throw new TimeoutException($"Timeout waiting for message on pipe '{pipeName}' - pipe availability check took too long");
                }
                
                using var reader = new StreamReader(client, Encoding.UTF8);
                
                // If no pattern, read until end or timeout
                if (regex == null)
                {
                    using var readCts = new CancellationTokenSource(remainingTime);
                    
                    var readTask = reader.ReadToEndAsync(readCts.Token);
                    var readTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
                    var readCompleted = await Task.WhenAny(readTask, readTimeoutTask);
                    
                    if (readCompleted == readTimeoutTask)
                    {
                        readCts.Cancel();
                        throw new TimeoutException($"Timeout waiting for message on pipe '{pipeName}' after {timeoutMs}ms total");
                    }
                    
                    return await readTask;
                }
                
                // With pattern, read line by line or in chunks and check each against pattern
                var buffer = new StringBuilder();
                var readStartTime = DateTime.UtcNow;
                
                while ((DateTime.UtcNow - readStartTime).TotalMilliseconds < remainingTime)
                {
                    var lineElapsed = (DateTime.UtcNow - readStartTime).TotalMilliseconds;
                    var lineTimeout = Math.Max(100, remainingTime - (int)lineElapsed);
                    
                    if (lineTimeout <= 0)
                    {
                        break;
                    }
                    
                    using var lineCts = new CancellationTokenSource((int)lineTimeout);
                    
                    try
                    {
                        // Try reading a line first (more efficient for line-based protocols)
                        var lineTask = reader.ReadLineAsync(lineCts.Token).AsTask();
                        var lineTimeoutTask = Task.Delay((int)lineTimeout, CancellationToken.None);
                        var lineCompleted = await Task.WhenAny(lineTask, lineTimeoutTask);
                        
                        if (lineCompleted == lineTask && !lineTask.IsFaulted)
                        {
                            var line = await lineTask;
                            if (line != null)
                            {
                                buffer.AppendLine(line);
                                var message = buffer.ToString();
                                
                                // Check if current message matches pattern
                                if (regex.IsMatch(message))
                                {
                                    return message.TrimEnd();
                                }
                                
                                // Also check just the line
                                if (regex.IsMatch(line))
                                {
                                    return line;
                                }
                            }
                            else
                            {
                                // End of stream, check what we have
                                if (buffer.Length > 0)
                                {
                                    var message = buffer.ToString();
                                    if (regex.IsMatch(message))
                                    {
                                        return message.TrimEnd();
                                    }
                                }
                                break;
                            }
                        }
                        else
                        {
                            // Timeout or faulted, check what we have so far
                            if (buffer.Length > 0)
                            {
                                var message = buffer.ToString();
                                if (regex.IsMatch(message))
                                {
                                    return message.TrimEnd();
                                }
                            }
                            
                            // If we have time, try reading more
                            if ((DateTime.UtcNow - readStartTime).TotalMilliseconds < remainingTime - 100)
                            {
                                await Task.Delay(50); // Small delay before next read attempt
                                continue;
                            }
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout reading line, check buffer
                        if (buffer.Length > 0)
                        {
                            var message = buffer.ToString();
                            if (regex.IsMatch(message))
                            {
                                return message.TrimEnd();
                            }
                        }
                        break;
                    }
                }
                
                // Final check of buffer
                if (buffer.Length > 0)
                {
                    var finalMessage = buffer.ToString();
                    if (regex.IsMatch(finalMessage))
                    {
                        return finalMessage.TrimEnd();
                    }
                }
                
                throw new TimeoutException($"Timeout waiting for message matching pattern '{pattern}' on pipe '{pipeName}' after {timeoutMs}ms total");
            }
        }
        catch (TimeoutException)
        {
            throw; // Re-throw timeout exceptions as-is
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
            using var cts = new CancellationTokenSource(timeoutMs);
            var startTime = DateTime.UtcNow;
            
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            
            // Connect with timeout
            var connectTask = client.ConnectAsync(cts.Token);
            var connectTimeoutTask = Task.Delay(timeoutMs, CancellationToken.None);
            var connectCompleted = await Task.WhenAny(connectTask, connectTimeoutTask);
            
            if (connectCompleted == connectTimeoutTask)
            {
                cts.Cancel();
                throw new TimeoutException($"Timeout connecting to pipe '{pipeName}' after {timeoutMs}ms");
            }
            
            await connectTask; // Ensure connection completed
            
            // Calculate remaining time for write
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var remainingTime = Math.Max(0, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                throw new TimeoutException($"Timeout on pipe '{pipeName}' - connection took too long");
            }
            
            // Send with remaining timeout
            var sendBytes = Encoding.UTF8.GetBytes(message);
            using var writeCts = new CancellationTokenSource(remainingTime);
            await client.WriteAsync(sendBytes, 0, sendBytes.Length, writeCts.Token);
            await client.FlushAsync(writeCts.Token);
            
            // Calculate remaining time for read
            elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            remainingTime = Math.Max(0, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                throw new TimeoutException($"Timeout on pipe '{pipeName}' - write took too long");
            }
            
            // Receive with remaining timeout
            using var reader = new StreamReader(client, Encoding.UTF8);
            using var readCts = new CancellationTokenSource(remainingTime);
            
            var readTask = reader.ReadToEndAsync(readCts.Token);
            var readTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
            var readCompleted = await Task.WhenAny(readTask, readTimeoutTask);
            
            if (readCompleted == readTimeoutTask)
            {
                readCts.Cancel();
                throw new TimeoutException($"Timeout reading response from pipe '{pipeName}' after {timeoutMs}ms total");
            }
            
            return await readTask;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timeout on pipe '{pipeName}' after {timeoutMs}ms");
        }
        catch (TimeoutException)
        {
            throw; // Re-throw timeout exceptions as-is
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
