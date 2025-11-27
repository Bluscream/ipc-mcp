using System.Runtime.InteropServices;
using System.Text;

namespace IpcMcp.Services;

public class PInvokeService
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
    
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_COPYDATA = 0x004A;

    public List<string> ListPInvokePipes()
    {
        // Use the same method as named pipes since they're the same thing
        // This is just an alternative way to access them
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
            throw new Exception($"Failed to list P/Invoke pipes: {ex.Message}");
        }
        return pipes;
    }

    public string SendPInvokeMessage(string target, string message)
    {
        try
        {
            // Try to find window by class name or window name
            IntPtr hWnd = FindWindow(target, null);
            if (hWnd == IntPtr.Zero)
            {
                // Try by window name
                hWnd = FindWindow(null, target);
            }
            
            if (hWnd == IntPtr.Zero)
            {
                throw new Exception($"Window '{target}' not found");
            }
            
            // Send WM_SETTEXT message
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var sb = new StringBuilder(message);
            var result = SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, sb);
            
            if (result == IntPtr.Zero)
            {
                throw new Exception($"Failed to send message to window '{target}'");
            }
            
            return $"Message sent to window '{target}' successfully";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send P/Invoke message: {ex.Message}");
        }
    }
}
