using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WtTactical {
    internal static class Native {
        public const int GWL_EXSTYLE=-20, WS_EX_TRANSPARENT=0x20, WS_EX_TOOLWINDOW=0x80, WS_EX_NOACTIVATE=0x08000000;
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int n);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h,int n,IntPtr value);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
        public static bool GameForeground() {
            try {uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);using(var p=Process.GetProcessById((int)pid))return p.ProcessName.Equals("aces",StringComparison.OrdinalIgnoreCase)||p.ProcessName.Equals("aces_BE",StringComparison.OrdinalIgnoreCase);}
            catch(ArgumentException){return false;}catch(System.ComponentModel.Win32Exception){return false;}
        }
    }
}
