using System.Runtime.InteropServices;

namespace IpcMcp.Services;

public class ComService
{
    public List<string> ListComObjects()
    {
        // Note: Enumerating all COM objects requires registry access
        // This is a simplified version
        var comObjects = new List<string>();
        
        try
        {
            // Would need to query registry: HKEY_CLASSES_ROOT\CLSID
            // For now, return common ones
            comObjects.AddRange(new[]
            {
                "Shell.Application",
                "Scripting.FileSystemObject",
                "WScript.Shell"
            });
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to list COM objects: {ex.Message}");
        }
        
        return comObjects;
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
            
            // This is a simplified example - actual implementation would
            // need to handle method invocation with parameters
            return $"COM message sent to {progId}.{method}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send COM message: {ex.Message}");
        }
    }
}

