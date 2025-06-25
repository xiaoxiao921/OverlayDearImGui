using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

[StructLayout(LayoutKind.Sequential)]
public struct MARGINS
{
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member