using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class Gdi32
{
    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject([In] IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member