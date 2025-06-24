using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

public static partial class User32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public WindowMessage message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
}
