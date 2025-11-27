using Microsoft.Win32;
using System.Text;

namespace IpcMcp.Services;

public class RegistryService
{
    public string ReadRegistry(string keyPath, string? valueName = null, string hive = "HKEY_CURRENT_USER")
    {
        try
        {
            RegistryKey? baseKey = GetRegistryHive(hive);
            if (baseKey == null)
            {
                throw new Exception($"Invalid registry hive: {hive}");
            }

            using (baseKey)
            {
                // Remove the hive prefix from keyPath if present
                var cleanPath = keyPath;
                if (cleanPath.StartsWith(hive + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    cleanPath = cleanPath.Substring(hive.Length + 1);
                }
                else if (cleanPath.StartsWith(hive, StringComparison.OrdinalIgnoreCase))
                {
                    cleanPath = cleanPath.Substring(hive.Length);
                }

                using var key = baseKey.OpenSubKey(cleanPath);
                if (key == null)
                {
                    throw new Exception($"Registry key not found: {hive}\\{cleanPath}");
                }

                if (string.IsNullOrEmpty(valueName))
                {
                    // Return all values in the key
                    var result = new StringBuilder();
                    result.AppendLine($"Registry Key: {hive}\\{cleanPath}");
                    result.AppendLine("Values:");
                    
                    foreach (var name in key.GetValueNames())
                    {
                        var value = key.GetValue(name);
                        var valueType = key.GetValueKind(name);
                        result.AppendLine($"  {name} ({valueType}) = {value}");
                    }
                    
                    // Also list subkeys
                    var subKeys = key.GetSubKeyNames();
                    if (subKeys.Length > 0)
                    {
                        result.AppendLine("\nSubkeys:");
                        foreach (var subKey in subKeys)
                        {
                            result.AppendLine($"  {subKey}");
                        }
                    }
                    
                    return result.ToString().TrimEnd();
                }
                else
                {
                    // Return specific value
                    var value = key.GetValue(valueName);
                    if (value == null)
                    {
                        throw new Exception($"Registry value not found: {hive}\\{cleanPath}\\{valueName}");
                    }
                    
                    var valueType = key.GetValueKind(valueName);
                    return $"{valueName} ({valueType}) = {value}";
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to read registry: {ex.Message}");
        }
    }

    public string WriteRegistry(string keyPath, string valueName, string value, string valueType = "String", string hive = "HKEY_CURRENT_USER")
    {
        try
        {
            RegistryKey? baseKey = GetRegistryHive(hive);
            if (baseKey == null)
            {
                throw new Exception($"Invalid registry hive: {hive}");
            }

            using (baseKey)
            {
                // Remove the hive prefix from keyPath if present
                var cleanPath = keyPath;
                if (cleanPath.StartsWith(hive + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    cleanPath = cleanPath.Substring(hive.Length + 1);
                }
                else if (cleanPath.StartsWith(hive, StringComparison.OrdinalIgnoreCase))
                {
                    cleanPath = cleanPath.Substring(hive.Length);
                }

                // Create or open the key
                using var key = baseKey.CreateSubKey(cleanPath, true);
                if (key == null)
                {
                    throw new Exception($"Failed to create/open registry key: {hive}\\{cleanPath}");
                }

                // Convert value based on type
                RegistryValueKind kind = valueType.ToLower() switch
                {
                    "string" or "reg_sz" => RegistryValueKind.String,
                    "dword" or "reg_dword" => RegistryValueKind.DWord,
                    "qword" or "reg_qword" => RegistryValueKind.QWord,
                    "binary" or "reg_binary" => RegistryValueKind.Binary,
                    "multistring" or "reg_multi_sz" => RegistryValueKind.MultiString,
                    "expandstring" or "reg_expand_sz" => RegistryValueKind.ExpandString,
                    _ => RegistryValueKind.String
                };

                object? convertedValue = kind switch
                {
                    RegistryValueKind.DWord => int.Parse(value),
                    RegistryValueKind.QWord => long.Parse(value),
                    RegistryValueKind.Binary => Convert.FromHexString(value.Replace(" ", "").Replace("-", "")),
                    RegistryValueKind.MultiString => value.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries),
                    _ => value
                };

                key.SetValue(valueName, convertedValue, kind);
                return $"Successfully wrote {valueName} = {value} ({kind}) to {hive}\\{cleanPath}";
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to write registry: {ex.Message}");
        }
    }

    private RegistryKey? GetRegistryHive(string hive)
    {
        return hive.ToUpper() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => null
        };
    }
}
