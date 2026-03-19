using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetActiveConsoleSessionId();

    static void Main() {
        try {
            Console.WriteLine($"WTSGetActiveConsoleSessionId: {WTSGetActiveConsoleSessionId()}");
        } catch (Exception ex) {
            Console.WriteLine($"WTSGetActiveConsoleSessionId failed: {ex.Message}");
        }

        try {
            Console.WriteLine($"GetActiveConsoleSessionId: {GetActiveConsoleSessionId()}");
        } catch (Exception ex) {
            Console.WriteLine($"GetActiveConsoleSessionId failed: {ex.Message}");
        }
    }
}
