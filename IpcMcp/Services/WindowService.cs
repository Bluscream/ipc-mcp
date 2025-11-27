using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace IpcMcp.Services;

public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class WindowService
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private List<WindowInfo> _windows = new();

    public List<WindowInfo> ListWindows()
    {
        _windows.Clear();
        EnumWindows(EnumWindowCallback, IntPtr.Zero);
        return _windows;
    }

    private bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
    {
        try
        {
            var window = new WindowInfo
            {
                Handle = hWnd
            };

            // Get window title
            var titleBuilder = new StringBuilder(256);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            window.Title = titleBuilder.ToString();

            // Get class name
            var classBuilder = new StringBuilder(256);
            GetClassName(hWnd, classBuilder, classBuilder.Capacity);
            window.ClassName = classBuilder.ToString();

            // Get process and thread IDs
            uint processId;
            window.ThreadId = (int)GetWindowThreadProcessId(hWnd, out processId);
            window.ProcessId = (int)processId;

            // Get visibility and enabled state
            window.IsVisible = IsWindowVisible(hWnd);
            window.IsEnabled = IsWindowEnabled(hWnd);

            // Get window rectangle
            if (GetWindowRect(hWnd, out RECT rect))
            {
                window.X = rect.Left;
                window.Y = rect.Top;
                window.Width = rect.Right - rect.Left;
                window.Height = rect.Bottom - rect.Top;
            }

            _windows.Add(window);
        }
        catch
        {
            // Skip windows we can't access
        }

        return true; // Continue enumeration
    }
}
