using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public enum PROCESS_DPI_AWARENESS
{
    PROCESS_DPI_UNAWARE = 0, PROCESS_SYSTEM_DPI_AWARE = 1, PROCESS_PER_MONITOR_DPI_AWARE = 2
}

public enum MONITOR_DPI_TYPE { MDT_EFFECTIVE_DPI = 0, MDT_ANGULAR_DPI = 1, MDT_RAW_DPI = 2, MDT_DEFAULT = MDT_EFFECTIVE_DPI }

public static class Shellscalingapi
{
    [DllImport("shcore.dll")]
    public static extern IntPtr SetProcessDpiAwareness(PROCESS_DPI_AWARENESS value);

    [DllImport("shcore.dll")]
    internal static extern uint GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member