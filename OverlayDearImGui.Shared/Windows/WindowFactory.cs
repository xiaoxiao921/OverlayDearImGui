using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class WindowFactory
{
    const uint WS_POPUP = 0x80000000;
    const uint WS_VISIBLE = 0x10000000;
    const uint WS_EX_TOPMOST = 0x00000008;
    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;

    public static (IntPtr hwnd, WNDCLASSEXW wc) CreateClassicWindow(string name)
    {
        WNDCLASSEXW wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0x0001, // CS_CLASSDC
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Overlay.WndProc),
            hInstance = Kernel32.GetModuleHandle(null),
            lpszClassName = name
        };

        var atom = User32.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"RegisterClassExW failed: 0x{error:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        var hwnd = User32.CreateWindowW(
            name,
            "Overlay",
            WS_POPUP,
            Overlay.GameRect.X, Overlay.GameRect.Y,
            Overlay.GameRect.Width, Overlay.GameRect.Height,
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero
        );


        if (hwnd == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"CreateWindowExW failed: 0x{error:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        return (hwnd, wc);
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member