using System.IO.Pipes;
using System.Text;
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
            
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            
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
            
            // Calculate remaining time for read
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var remainingTime = Math.Max(0, timeoutMs - (int)elapsed);
            
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
            
            // First, wait for the pipe to become available
            var waitStartTime = DateTime.UtcNow;
            var waitTimeout = TimeSpan.FromMilliseconds(timeoutMs);
            var checkInterval = 100; // Check every 100ms
            
            bool pipeAvailable = false;
            while (DateTime.UtcNow - waitStartTime < waitTimeout)
            {
                try
                {
                    using var testClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                    var testConnectTask = testClient.ConnectAsync(100);
                    var testDelayTask = Task.Delay(100);
                    var testCompleted = await Task.WhenAny(testConnectTask, testDelayTask);
                    
                    if (testCompleted == testConnectTask && !testConnectTask.IsFaulted)
                    {
                        await testConnectTask;
                        pipeAvailable = true;
                        break;
                    }
                }
                catch
                {
                    // Pipe not available yet, continue waiting
                }
                
                await Task.Delay(checkInterval);
            }
            
            if (!pipeAvailable)
            {
                throw new TimeoutException($"Timeout waiting for pipe '{pipeName}' to become available after {timeoutMs}ms");
            }
            
            // Calculate remaining time for actual send operation
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var remainingTime = Math.Max(100, timeoutMs - (int)elapsed); // At least 100ms for the send
            
            using var cts = new CancellationTokenSource(remainingTime);
            
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            
            // Connect with remaining timeout
            var connectTask = client.ConnectAsync(cts.Token);
            var connectTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
            var connectCompleted = await Task.WhenAny(connectTask, connectTimeoutTask);
            
            if (connectCompleted == connectTimeoutTask)
            {
                cts.Cancel();
                throw new TimeoutException($"Timeout connecting to pipe '{pipeName}' after {timeoutMs}ms total");
            }
            
            await connectTask; // Ensure connection completed
            
            // Calculate remaining time for write
            elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var writeTime = Math.Max(100, timeoutMs - (int)elapsed);
            
            if (writeTime <= 0)
            {
                throw new TimeoutException($"Timeout sending message to pipe '{pipeName}' - connection took too long");
            }
            
            var bytes = Encoding.UTF8.GetBytes(message);
            using var writeCts = new CancellationTokenSource(writeTime);
            
            // Write with remaining timeout
            await client.WriteAsync(bytes, 0, bytes.Length, writeCts.Token);
            await client.FlushAsync(writeCts.Token);
            
            return "Message sent successfully";
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

    public async Task<string> WaitForNamedPipeMessage(string pipeName, int timeoutMs = 30000, int checkIntervalMs = 500)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            
            // First, wait for the pipe to become available (like wait_for_named_pipe)
            bool pipeAvailable = false;
            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    // Try to connect to check if pipe exists and is available
                    using var testClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                    var testConnectTask = testClient.ConnectAsync(100); // Quick connection test
                    var testDelayTask = Task.Delay(100);
                    
                    var testCompletedTask = await Task.WhenAny(testConnectTask, testDelayTask);
                    
                    if (testCompletedTask == testConnectTask && !testConnectTask.IsFaulted)
                    {
                        // Pipe is available
                        await testConnectTask; // Ensure connection is complete
                        pipeAvailable = true;
                        break;
                    }
                }
                catch
                {
                    // Pipe not available yet, continue waiting
                }
                
                // Wait before next check
                await Task.Delay(checkIntervalMs);
            }
            
            if (!pipeAvailable)
            {
                throw new TimeoutException($"Timeout waiting for named pipe '{pipeName}' to become available after {timeoutMs}ms");
            }
            
            // Calculate remaining time for read
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                throw new TimeoutException($"Timeout waiting for message on pipe '{pipeName}' - pipe availability check took too long");
            }
            
            // Now connect and read the message
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            using var readCts = new CancellationTokenSource(remainingTime);
            
            var clientConnectTask = client.ConnectAsync(readCts.Token);
            var clientConnectTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
            var clientConnectCompleted = await Task.WhenAny(clientConnectTask, clientConnectTimeoutTask);
            
            if (clientConnectCompleted == clientConnectTimeoutTask)
            {
                readCts.Cancel();
                throw new TimeoutException($"Timeout connecting to pipe '{pipeName}' after {timeoutMs}ms total");
            }
            
            await clientConnectTask; // Ensure connection completed
            
            // Calculate remaining time for read
            elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            remainingTime = Math.Max(100, timeoutMs - (int)elapsed);
            
            if (remainingTime <= 0)
            {
                throw new TimeoutException($"Timeout waiting for message on pipe '{pipeName}' - connection took too long");
            }
            
            using var reader = new StreamReader(client, Encoding.UTF8);
            using var readCts2 = new CancellationTokenSource(remainingTime);
            
            // Read with remaining timeout
            var readTask = reader.ReadToEndAsync(readCts2.Token);
            var readTimeoutTask = Task.Delay(remainingTime, CancellationToken.None);
            var readCompleted = await Task.WhenAny(readTask, readTimeoutTask);
            
            if (readCompleted == readTimeoutTask)
            {
                readCts2.Cancel();
                throw new TimeoutException($"Timeout waiting for message on pipe '{pipeName}' after {timeoutMs}ms total");
            }
            
            return await readTask;
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
