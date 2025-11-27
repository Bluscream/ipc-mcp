using System.Text;
using System.Management;

namespace IpcMcp.Services;

public class ServiceService
{
    public string ListServices()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("Name\tDisplayName\tStatus\tStartType");
            result.AppendLine("----\t-----------\t------\t---------");
            
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
            var services = searcher.Get()
                .Cast<ManagementObject>()
                .OrderBy(s => s["Name"]?.ToString() ?? "")
                .ToList();
            
            foreach (var service in services)
            {
                try
                {
                    var name = service["Name"]?.ToString() ?? "Unknown";
                    var displayName = service["DisplayName"]?.ToString() ?? "";
                    var state = service["State"]?.ToString() ?? "Unknown";
                    var startMode = service["StartMode"]?.ToString() ?? "Unknown";
                    
                    result.AppendLine($"{name}\t{displayName}\t{state}\t{startMode}");
                }
                catch
                {
                    // Skip services we can't access
                    continue;
                }
            }
            
            return result.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to list services: {ex.Message}");
        }
    }

    public string StartService(string serviceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Service WHERE Name = '{serviceName.Replace("'", "''")}'");
            var services = searcher.Get().Cast<ManagementObject>().ToList();
            
            if (services.Count == 0)
            {
                throw new Exception($"Service '{serviceName}' not found");
            }
            
            var service = services[0];
            var state = service["State"]?.ToString() ?? "";
            
            if (state == "Running")
            {
                return $"Service '{serviceName}' is already running";
            }
            
            if (state == "Start Pending")
            {
                return $"Service '{serviceName}' is already starting";
            }
            
            // Invoke the StartService method
            var result = service.InvokeMethod("StartService", null);
            
            if (result != null && (uint)result != 0)
            {
                var errorCode = (uint)result;
                throw new Exception($"Failed to start service. Error code: {errorCode}");
            }
            
            // Wait for service to start (up to 30 seconds)
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < timeout)
            {
                service.Get();
                state = service["State"]?.ToString() ?? "";
                if (state == "Running")
                {
                    return $"Service '{serviceName}' started successfully";
                }
                if (state != "Start Pending")
                {
                    throw new Exception($"Service '{serviceName}' failed to start. Current state: {state}");
                }
                Thread.Sleep(500);
            }
            
            throw new TimeoutException($"Service '{serviceName}' did not start within 30 seconds");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start service '{serviceName}': {ex.Message}");
        }
    }

    public string StopService(string serviceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Service WHERE Name = '{serviceName.Replace("'", "''")}'");
            var services = searcher.Get().Cast<ManagementObject>().ToList();
            
            if (services.Count == 0)
            {
                throw new Exception($"Service '{serviceName}' not found");
            }
            
            var service = services[0];
            var state = service["State"]?.ToString() ?? "";
            
            if (state == "Stopped")
            {
                return $"Service '{serviceName}' is already stopped";
            }
            
            if (state == "Stop Pending")
            {
                return $"Service '{serviceName}' is already stopping";
            }
            
            // Check if service can be stopped
            var acceptStop = service["AcceptStop"]?.ToString() ?? "False";
            if (acceptStop != "True")
            {
                throw new Exception($"Service '{serviceName}' cannot be stopped");
            }
            
            // Invoke the StopService method
            var result = service.InvokeMethod("StopService", null);
            
            if (result != null && (uint)result != 0)
            {
                var errorCode = (uint)result;
                throw new Exception($"Failed to stop service. Error code: {errorCode}");
            }
            
            // Wait for service to stop (up to 30 seconds)
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < timeout)
            {
                service.Get();
                state = service["State"]?.ToString() ?? "";
                if (state == "Stopped")
                {
                    return $"Service '{serviceName}' stopped successfully";
                }
                if (state != "Stop Pending")
                {
                    throw new Exception($"Service '{serviceName}' failed to stop. Current state: {state}");
                }
                Thread.Sleep(500);
            }
            
            throw new TimeoutException($"Service '{serviceName}' did not stop within 30 seconds");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to stop service '{serviceName}': {ex.Message}");
        }
    }
}
