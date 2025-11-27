using System.IO.MemoryMappedFiles;
using System.Text;

namespace IpcMcp.Services;

public class MemoryMappedFileService
{
    public List<string> ListMappedFiles()
    {
        // Note: Windows doesn't provide a direct API to list all memory-mapped files
        // This would require P/Invoke to QueryDosDevice or similar
        // For now, return empty list with note
        return new List<string>();
    }

    public string ReadMappedFile(string mapName, long offset = 0, int length = 4096)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(mapName);
            using var accessor = mmf.CreateViewAccessor(offset, length);
            var bytes = new byte[length];
            accessor.ReadArray(offset, bytes, 0, length);
            
            // Find null terminator
            var nullIndex = Array.IndexOf(bytes, (byte)0);
            if (nullIndex >= 0)
            {
                Array.Resize(ref bytes, nullIndex);
            }
            
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to read mapped file '{mapName}': {ex.Message}");
        }
    }

    public void WriteMappedFile(string mapName, string data, long offset = 0)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Write);
            using var accessor = mmf.CreateViewAccessor(offset, data.Length);
            var bytes = Encoding.UTF8.GetBytes(data);
            accessor.WriteArray(offset, bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to write to mapped file '{mapName}': {ex.Message}");
        }
    }

    public string SendMappedFileMessage(string mapName, string message, long offset = 0)
    {
        WriteMappedFile(mapName, message, offset);
        return "Message written successfully";
    }
}

