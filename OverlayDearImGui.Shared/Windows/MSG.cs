using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member