using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static partial class User32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public Int32 X;
        public Int32 Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member