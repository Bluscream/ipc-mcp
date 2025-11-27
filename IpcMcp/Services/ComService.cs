using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IpcMcp.Services;

public class ComService
{
    public List<string> ListComObjects()
    {
        var comObjects = new List<string>();
        
        try
        {
            // Query registry for COM objects
            using var classesRoot = Registry.ClassesRoot;
            using var clsidKey = classesRoot.OpenSubKey("CLSID");
            
            if (clsidKey != null)
            {
                foreach (var clsid in clsidKey.GetSubKeyNames())
                {
                    try
                    {
                        using var clsidSubKey = clsidKey.OpenSubKey(clsid);
                        if (clsidSubKey != null)
                        {
                            // Try to get ProgID
                            using var progIdKey = clsidSubKey.OpenSubKey("ProgID");
                            if (progIdKey != null)
                            {
                                var progId = progIdKey.GetValue(null)?.ToString();
                                if (!string.IsNullOrEmpty(progId))
                                {
                                    comObjects.Add(progId);
                                }
                            }
                            
                            // Also try VersionIndependentProgID
                            using var versionIndependentKey = clsidSubKey.OpenSubKey("VersionIndependentProgID");
                            if (versionIndependentKey != null)
                            {
                                var progId = versionIndependentKey.GetValue(null)?.ToString();
                                if (!string.IsNullOrEmpty(progId) && !comObjects.Contains(progId))
                                {
                                    comObjects.Add(progId);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid entries
                        continue;
                    }
                }
            }
            
            // If enumeration failed or returned empty, return common ones
            if (comObjects.Count == 0)
            {
                comObjects.AddRange(new[]
                {
                    "Shell.Application",
                    "Scripting.FileSystemObject",
                    "WScript.Shell"
                });
            }
        }
        catch
        {
            // Fallback to common COM objects on error
            comObjects.AddRange(new[]
            {
                "Shell.Application",
                "Scripting.FileSystemObject",
                "WScript.Shell"
            });
        }
        
        return comObjects.Distinct().OrderBy(x => x).ToList();
    }

    public string SendComMessage(string progId, string method, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var type = Type.GetTypeFromProgID(progId);
            if (type == null)
            {
                throw new Exception($"COM object '{progId}' not found");
            }
            
            var comObject = Activator.CreateInstance(type);
            if (comObject == null)
            {
                throw new Exception($"Failed to create instance of '{progId}'");
            }
            
            // Find the method
            var methodInfo = type.GetMethod(method);
            if (methodInfo == null)
            {
                throw new Exception($"Method '{method}' not found on COM object '{progId}'");
            }
            
            // Get method parameters
            var paramInfos = methodInfo.GetParameters();
            object? result;
            
            if (paramInfos.Length == 0)
            {
                // No parameters
                result = methodInfo.Invoke(comObject, null);
            }
            else if (parameters != null && parameters.Count > 0)
            {
                // Convert parameters to method signature
                var args = new object[paramInfos.Length];
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var paramInfo = paramInfos[i];
                    if (parameters.TryGetValue(paramInfo.Name ?? "", out var paramValue))
                    {
                        // Try to convert to the expected type
                        args[i] = Convert.ChangeType(paramValue, paramInfo.ParameterType)!;
                    }
                    else if (paramInfo.HasDefaultValue)
                    {
                        args[i] = paramInfo.DefaultValue ?? (paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType)! : null)!;
                    }
                    else
                    {
                        throw new Exception($"Missing required parameter '{paramInfo.Name}'");
                    }
                }
                result = methodInfo.Invoke(comObject, args);
            }
            else
            {
                // Use default values for all parameters
                var args = new object[paramInfos.Length];
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var paramInfo = paramInfos[i];
                    args[i] = paramInfo.HasDefaultValue 
                        ? (paramInfo.DefaultValue ?? (paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType)! : null)!)
                        : (paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType)! : null)!;
                }
                result = methodInfo.Invoke(comObject, args);
            }
            
            // Return result as string
            if (result == null)
            {
                return $"Method '{method}' executed successfully (returned null)";
            }
            
            return result.ToString() ?? $"Method '{method}' executed successfully";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send COM message: {ex.Message}");
        }
    }
}
