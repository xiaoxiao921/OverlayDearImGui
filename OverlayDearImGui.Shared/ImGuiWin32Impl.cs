using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using OverlayDearImGui.Windows;

namespace OverlayDearImGui;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static unsafe class ImGuiWin32Impl
{
    private delegate uint XInputGetCapabilitiesDelegate(uint a, uint b, IntPtr c);
    private delegate uint XInputGetStateDelegate(uint a, IntPtr b);

    private static IntPtr _windowHandle;
    private static IntPtr _mouseHandle;
    private static int _mouseTrackedArea;   // 0: not tracked, 1: client are, 2: non-client area
    private static int _mouseButtonsDown;
    private static long _time;
    private static long _ticksPerSecond;
    private static ImGuiMouseCursor _lastMouseCursor;

    //private static bool _hasGamepad;
    //private static bool _wantUpdateHasGamepad;
    private static IntPtr _xInputDLL;
    private static XInputGetCapabilitiesDelegate _xInputGetCapabilities;
    private static XInputGetStateDelegate _xInputGetState;
    //private static IntPtr _classNamePtr;

    //private static bool[] _imguiMouseIsDown = new bool[5];

    private static bool ImGui_ImplWin32_InitEx(void* windowHandle, bool platform_has_own_dc)
    {
        var io = ImGui.GetIO();
        if (_windowHandle != IntPtr.Zero)
        {
            Log.Error("Already initialized a platform backend!");
            return false;
        }

        if (!Kernel32.QueryPerformanceFrequency(out var perf_frequency))
            return false;
        if (!Kernel32.QueryPerformanceCounter(out var perf_counter))
            return false;

        // Setup backend capabilities flags
        io.BackendPlatformName = (byte*)Marshal.StringToHGlobalAnsi("imgui_impl_win32_c#");
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors | // We can honor GetMouseCursor() values (optional)
                                                               ImGuiBackendFlags.HasSetMousePos; // We can honor io.WantSetMousePos requests (optional, rarely used)
                                                                                                 //ImGuiBackendFlags.HasSetMousePos | // We can honor io.WantSetMousePos requests (optional, rarely used)
                                                                                                 //ImGuiBackendFlags.PlatformHasViewports;

        _windowHandle = (IntPtr)windowHandle;
        _ticksPerSecond = perf_frequency;
        _time = perf_counter;
        _lastMouseCursor = ImGuiMouseCursor.Count;

        // Set platform dependent data in viewport
        var viewport = ImGui.GetMainViewport();
        viewport.PlatformHandle = viewport.PlatformHandleRaw = windowHandle;
        //if (io.BackendFlags.HasFlag(ImGuiBackendFlags.PlatformHasViewports))
        //ImGui_ImplWin32_InitPlatformInterface();

        // Dynamically load XInput library
        //_wantUpdateHasGamepad = true;
        var xinput_dll_names = new List<string>()
        {
            "xinput1_4.dll",   // Windows 8+
            "xinput1_3.dll",   // DirectX SDK
            "xinput9_1_0.dll", // Windows Vista, Windows 7
            "xinput1_2.dll",   // DirectX SDK
            "xinput1_1.dll"    // DirectX SDK
        };

        for (int n = 0; n < xinput_dll_names.Count; n++)
        {
            var dll = Kernel32.LoadLibrary(xinput_dll_names[n]);
            if (dll != IntPtr.Zero)
            {
                _xInputDLL = dll;
                _xInputGetCapabilities = Marshal.GetDelegateForFunctionPointer<XInputGetCapabilitiesDelegate>(Kernel32.GetProcAddress(dll, "XInputGetCapabilities"));
                _xInputGetState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(Kernel32.GetProcAddress(dll, "XInputGetState"));
                break;
            }
        }

        return true;
    }

    //private static int GetButton(WindowMessage msg, ulong wParam)
    //{
    //    switch (msg)
    //    {
    //        case WindowMessage.WM_LBUTTONUP:
    //        case WindowMessage.WM_LBUTTONDOWN:
    //        case WindowMessage.WM_LBUTTONDBLCLK:
    //            return 0;
    //        case WindowMessage.WM_RBUTTONUP:
    //        case WindowMessage.WM_RBUTTONDOWN:
    //        case WindowMessage.WM_RBUTTONDBLCLK:
    //            return 1;
    //        case WindowMessage.WM_MBUTTONUP:
    //        case WindowMessage.WM_MBUTTONDOWN:
    //        case WindowMessage.WM_MBUTTONDBLCLK:
    //            return 2;
    //        case WindowMessage.WM_XBUTTONUP:
    //        case WindowMessage.WM_XBUTTONDOWN:
    //        case WindowMessage.WM_XBUTTONDBLCLK:
    //            return GET_XBUTTON_WPARAM((IntPtr)wParam) == XBUTTON1 ? 3 : 4;
    //        default:
    //            return 0;
    //    }
    //}

    /// <summary>
    /// Processes window messages. Supports both WndProcA and WndProcW.
    /// </summary>
    // <param name="hWnd">Handle of the window.</param>
    // <param name="msg">Type of window message.</param>
    // <param name="wParam">wParam.</param>
    // <param name="lParam">lParam.</param>
    // <returns>Return value, if not doing further processing.</returns>
    //public static unsafe IntPtr? ProcessWndProcW(IntPtr hWnd, WindowMessage msg, void* wParam, void* lParam)
    //{
    //    if (!ImGui.GetCurrentContext().IsNull)
    //    {
    //        var io = ImGui.GetIO();

    //        switch (msg)
    //        {
    //            case WindowMessage.WM_LBUTTONDOWN:
    //            case WindowMessage.WM_LBUTTONDBLCLK:
    //            case WindowMessage.WM_RBUTTONDOWN:
    //            case WindowMessage.WM_RBUTTONDBLCLK:
    //            case WindowMessage.WM_MBUTTONDOWN:
    //            case WindowMessage.WM_MBUTTONDBLCLK:
    //            case WindowMessage.WM_XBUTTONDOWN:
    //            case WindowMessage.WM_XBUTTONDBLCLK:
    //                {
    //                    var button = GetButton(msg, (ulong)wParam);
    //                    if (io.WantCaptureMouse)
    //                    {
    //                        if (!ImGui.IsAnyMouseDown() && User32.GetCapture() == IntPtr.Zero)
    //                            User32.SetCapture(hWnd);

    //                        io.MouseDown[button] = true;
    //                        _imguiMouseIsDown[button] = true;
    //                        return IntPtr.Zero;
    //                    }
    //                    break;
    //                }
    //            case WindowMessage.WM_LBUTTONUP:
    //            case WindowMessage.WM_RBUTTONUP:
    //            case WindowMessage.WM_MBUTTONUP:
    //            case WindowMessage.WM_XBUTTONUP:
    //                {
    //                    var button = GetButton(msg, (ulong)wParam);
    //                    if (io.WantCaptureMouse && _imguiMouseIsDown[button])
    //                    {
    //                        if (!ImGui.IsAnyMouseDown() && User32.GetCapture() == hWnd)
    //                            User32.ReleaseCapture();

    //                        io.MouseDown[button] = false;
    //                        _imguiMouseIsDown[button] = false;
    //                        return IntPtr.Zero;
    //                    }
    //                    break;
    //                }
    //            case WindowMessage.WM_MOUSEWHEEL:
    //                if (io.WantCaptureMouse)
    //                {
    //                    io.MouseWheel += (float)GET_WHEEL_DELTA_WPARAM((IntPtr)wParam) /
    //                                     WHEEL_DELTA;
    //                    return IntPtr.Zero;
    //                }

    //                break;
    //            case WindowMessage.WM_MOUSEHWHEEL:
    //                if (io.WantCaptureMouse)
    //                {
    //                    io.MouseWheelH += (float)GET_WHEEL_DELTA_WPARAM((IntPtr)wParam) /
    //                                      WHEEL_DELTA;
    //                    return IntPtr.Zero;
    //                }

    //                break;
    //            case WindowMessage.WM_KEYDOWN:
    //            case WindowMessage.WM_SYSKEYDOWN:
    //            case WindowMessage.WM_KEYUP:
    //            case WindowMessage.WM_SYSKEYUP:
    //                bool isKeyDown = (msg == WindowMessage.WM_KEYDOWN || msg == WindowMessage.WM_SYSKEYDOWN);
    //                if ((int)wParam < 256)
    //                {
    //                    // Submit modifiers
    //                    ImGui_ImplWin32_UpdateKeyModifiers();

    //                    // Obtain virtual key code
    //                    // (keypad enter doesn't have its own... VK_RETURN with KF_EXTENDED flag means keypad enter, see IM_VK_KEYPAD_ENTER definition for details, it is mapped to ImGuiKey.KeyPadEnter.)
    //                    var vk = (VirtualKey)(int)wParam;
    //                    if (((int)wParam == (int)VirtualKey.Return) && ((int)lParam & (256 << 16)) > 0)
    //                        vk = (VirtualKey.Return + 256);

    //                    // Submit key event
    //                    var key = ImGui_ImplWin32_VirtualKeyToImGuiKey(vk);
    //                    var scancode = ((int)lParam & 0xff0000) >> 16;
    //                    if (key != ImGuiKey.None && io.WantTextInput)
    //                    {
    //                        ImGui_ImplWin32_AddKeyEvent(key, isKeyDown, vk, scancode);
    //                        return IntPtr.Zero;
    //                    }

    //                    // Submit individual left/right modifier events
    //                    if (vk == VirtualKey.Shift)
    //                    {
    //                        // Important: Shift keys tend to get stuck when pressed together, missing key-up events are corrected in ImGui_ImplWin32_ProcessKeyEventsWorkarounds()
    //                        if (IsVkDown(VirtualKey.LeftShift) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftShift, isKeyDown, VirtualKey.LeftShift, scancode); }
    //                        if (IsVkDown(VirtualKey.RightShift) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightShift, isKeyDown, VirtualKey.RightShift, scancode); }
    //                    }
    //                    else if (vk == VirtualKey.Control)
    //                    {
    //                        if (IsVkDown(VirtualKey.LeftControl) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftCtrl, isKeyDown, VirtualKey.LeftControl, scancode); }
    //                        if (IsVkDown(VirtualKey.RightControl) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightCtrl, isKeyDown, VirtualKey.RightControl, scancode); }
    //                    }
    //                    else if (vk == VirtualKey.Menu)
    //                    {
    //                        if (IsVkDown(VirtualKey.LeftMenu) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftAlt, isKeyDown, VirtualKey.LeftMenu, scancode); }
    //                        if (IsVkDown(VirtualKey.RightMenu) == isKeyDown) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightAlt, isKeyDown, VirtualKey.RightMenu, scancode); }
    //                    }
    //                }
    //                break;
    //            case WindowMessage.WM_CHAR:
    //                if (io.WantTextInput)
    //                {
    //                    io.AddInputCharacter((uint)wParam);
    //                    return IntPtr.Zero;
    //                }
    //                break;
    //            // this never seemed to work reasonably, but I'll leave it for now
    //            case WindowMessage.WM_SETCURSOR:
    //                if (io.WantCaptureMouse)
    //                {
    //                    const int HTCLIENT = 1;
    //                    if (LOWORD((IntPtr)lParam) == HTCLIENT && ImGui_ImplWin32_UpdateMouseCursor())
    //                    {
    //                        // this message returns 1 to block further processing
    //                        // because consistency is no fun
    //                        return (IntPtr)1;
    //                    }
    //                }
    //                break;
    //            // TODO: Decode why IME is miserable
    //            // case WindowMessage.WM_IME_NOTIFY:
    //            // return HandleImeMessage(hWnd, (long) wParam, (long) lParam);
    //            default:
    //                break;
    //        }
    //    }

    //    // We did not produce a result - return -1
    //    return null;
    //}

    //private static WndProcSignature _wndProcDelegate;
    //private delegate IntPtr WndProcSignature(IntPtr hWnd, WindowMessage msg, void* wParam, void* lParam);
    //private static unsafe IntPtr WndProcDetour(IntPtr hWnd, WindowMessage msg, void* wParam, void* lParam)
    //{
    //    // Attempt to process the result of this window message
    //    // We will return the result here if we consider the message handled
    //    var processResult = ProcessWndProcW(hWnd, msg, wParam, lParam);

    //    if (processResult != null) return processResult.Value;

    //    // The message wasn't handled, but it's a platform window
    //    // So we have to handle some messages ourselves
    //    // BUT we might have disposed the context, so check that
    //    if (ImGui.GetCurrentContext().IsNull)
    //        return User32.DefWindowProc(hWnd, msg, (IntPtr)wParam, (IntPtr)lParam);
    //    ImGuiViewportPtr viewport = ImGui.FindViewportByPlatformHandle((void*)hWnd);

    //    if (!viewport.IsNull)
    //    {
    //        switch (msg)
    //        {
    //            case WindowMessage.WM_CLOSE:
    //                viewport.PlatformRequestClose = true;
    //                return IntPtr.Zero;
    //            case WindowMessage.WM_MOVE:
    //                viewport.PlatformRequestMove = true;
    //                break;
    //            case WindowMessage.WM_SIZE:
    //                viewport.PlatformRequestResize = true;
    //                break;
    //            case WindowMessage.WM_MOUSEACTIVATE:
    //                // We never want our platform windows to be active, or else Windows will think we
    //                // want messages dispatched with its hWnd. We don't. The only way to activate a platform
    //                // window is via clicking, it does not appear on the taskbar or alt-tab, so we just
    //                // brute force behavior here.

    //                // Make the game the foreground window. This prevents ImGui windows from becoming
    //                // choppy when users have the "limit FPS" option enabled in-game
    //                User32.SetForegroundWindow(_windowHandle);

    //                // Also set the window capture to the main window, as focus will not cause
    //                // future messages to be dispatched to the main window unless it is receiving capture
    //                User32.SetCapture(_windowHandle);

    //                // We still want to return MA_NOACTIVATE
    //                // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate
    //                return (IntPtr)0x3;
    //            case WindowMessage.WM_NCHITTEST:
    //                // Let mouse pass-through the window. This will allow the backend to set io.MouseHoveredViewport properly (which is OPTIONAL).
    //                // The ImGuiViewportFlags_NoInputs flag is set while dragging a viewport, as want to detect the window behind the one we are dragging.
    //                // If you cannot easily access those viewport flags from your windowing/event code: you may manually synchronize its state e.g. in
    //                // your main loop after calling UpdatePlatformWindows(). Iterate all viewports/platform windows and pass the flag to your windowing system.
    //                if (viewport.Flags.HasFlag(ImGuiViewportFlags.NoInputs))
    //                {
    //                    // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest
    //                    return (IntPtr)uint.MaxValue;
    //                }
    //                break;
    //        }
    //    }

    //    return User32.DefWindowProc(hWnd, msg, (IntPtr)wParam, (IntPtr)lParam);
    //}

    //private static unsafe void ImGui_ImplWin32_UpdateMonitors()
    //{
    //    ImGuiPlatformIOPtr platformIO = ImGui.GetPlatformIO();
    //    int numMonitors = User32.GetSystemMetrics(User32.SystemMetric.SM_CMONITORS);

    //    // Allocate the memory for ImGuiPlatformMonitor array
    //    int sizeOfMonitor = sizeof(ImGuiPlatformMonitor);
    //    IntPtr data = Marshal.AllocHGlobal(sizeOfMonitor * numMonitors);

    //    // Initialize each element in the buffer to default
    //    ImGuiPlatformMonitor* monitorsPtr = (ImGuiPlatformMonitor*)data;
    //    for (int i = 0; i < numMonitors; i++)
    //    {
    //        monitorsPtr[i] = new ImGuiPlatformMonitor();
    //    }

    //    // Assign to ImGui's monitor list
    //    platformIO.Monitors = new ImVector<ImGuiPlatformMonitor>(numMonitors, numMonitors, monitorsPtr);

    //    // Prepare iterator
    //    int* iterator = (int*)Marshal.AllocHGlobal(sizeof(int));
    //    *iterator = 0;

    //    bool success = User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, ImGui_ImplWin32_UpdateMonitors_EnumFunc,
    //                                               new IntPtr(iterator));
    //}


    //private static bool ImGui_ImplWin32_UpdateMonitors_EnumFunc(IntPtr nativeMonitor, IntPtr hdc, ref RectStruct LPRECT,
    //                                                               IntPtr LPARAM)
    //{
    //    // Get and increment iterator
    //    int monitorIndex = *(int*)LPARAM;
    //    *(int*)LPARAM = *(int*)LPARAM + 1;

    //    User32.MonitorInfoEx info = new User32.MonitorInfoEx();
    //    info.Init();
    //    if (!User32.GetMonitorInfo(nativeMonitor, ref info))
    //    {
    //        int error = Marshal.GetLastWin32Error();
    //        Log.Error($"GetMonitorInfo failed for monitor #{monitorIndex}. Win32 error code: {error}");
    //        return true;
    //    }

    //    if (monitorIndex >= ImGui.GetPlatformIO().Monitors.Size)
    //    {
    //        Log.Error($"Monitor index {monitorIndex} out of range for ImGui monitors");
    //        return true;
    //    }

    //    // Give ImGui the info for this display
    //    var imMonitor = ImGui.GetPlatformIO().Monitors[monitorIndex];
    //    imMonitor.MainPos = new Vector2(info.Monitor.Left, info.Monitor.Top);
    //    imMonitor.MainSize = new Vector2(info.Monitor.Right - info.Monitor.Left,
    //                                     info.Monitor.Bottom - info.Monitor.Top);
    //    imMonitor.WorkPos = new Vector2(info.WorkArea.Left, info.WorkArea.Top);
    //    imMonitor.WorkSize =
    //        new Vector2(info.WorkArea.Right - info.WorkArea.Left, info.WorkArea.Bottom - info.WorkArea.Top);
    //    imMonitor.DpiScale = 1f;

    //    ImGui.GetPlatformIO().Monitors[monitorIndex] = imMonitor;

    //    return true;
    //}

    //private static void ImGui_ImplWin32_GetWin32StyleFromViewportFlags(ImGuiViewportFlags flags,
    //                                                               ref User32.WindowStyles outStyle,
    //                                                               ref User32.WindowStylesEx outExStyle)
    //{
    //    if (flags.HasFlag(ImGuiViewportFlags.NoDecoration))
    //        outStyle = User32.WindowStyles.WS_POPUP;
    //    else
    //        outStyle = User32.WindowStyles.WS_OVERLAPPEDWINDOW;

    //    if (flags.HasFlag(ImGuiViewportFlags.NoTaskBarIcon))
    //        outExStyle = User32.WindowStylesEx.WS_EX_TOOLWINDOW;
    //    else
    //        outExStyle = User32.WindowStylesEx.WS_EX_APPWINDOW;

    //    if (flags.HasFlag(ImGuiViewportFlags.TopMost))
    //        outExStyle |= User32.WindowStylesEx.WS_EX_TOPMOST;
    //}

    //private static int viewportI = 0;
    //private static void ImGui_ImplWin32_CreateWindow(ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)Marshal.AllocHGlobal(Marshal.SizeOf<ImGuiViewportDataWin32>());
    //    viewport.PlatformUserData = (void*)(IntPtr)data;
    //    viewport.Flags =
    //        (
    //            ImGuiViewportFlags.NoTaskBarIcon |
    //            ImGuiViewportFlags.NoFocusOnClick |
    //            ImGuiViewportFlags.NoRendererClear |
    //            ImGuiViewportFlags.NoFocusOnAppearing |
    //            viewport.Flags
    //        );
    //    ImGui_ImplWin32_GetWin32StyleFromViewportFlags(viewport.Flags, ref data->DwStyle, ref data->DwExStyle);

    //    IntPtr parentWindow = IntPtr.Zero;
    //    if (viewport.ParentViewportId != 0)
    //    {
    //        ImGuiViewportPtr parentViewport = ImGui.FindViewportByID(viewport.ParentViewportId);
    //        parentWindow = (IntPtr)parentViewport.PlatformHandle;
    //    }

    //    // Create window
    //    RECT rect;
    //    rect.Left = (int)viewport.Pos.X;
    //    rect.Top = (int)viewport.Pos.Y;
    //    rect.Right = (int)(viewport.Pos.X + viewport.Size.X);
    //    rect.Bottom = (int)(viewport.Pos.Y + viewport.Size.Y);
    //    User32.AdjustWindowRectEx(ref rect, data->DwStyle, false, data->DwExStyle);

    //    var (hwnd, wc) = WindowFactory.CreateClassicWindow("ImGui Platform " + viewportI++);
    //    //data->Hwnd = User32.CreateWindowExW(
    //    //data->DwExStyle, "ImGui Platform", "Untitled", data->DwStyle,
    //    //rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top,
    //    //parentWindow, IntPtr.Zero, Kernel32.GetModuleHandle(null),
    //    //IntPtr.Zero);

    //    data->Hwnd = hwnd;

    //    // User32.GetWindowThreadProcessId(data->Hwnd, out var windowProcessId);
    //    // var currentThreadId = Kernel32.GetCurrentThreadId();
    //    // var currentProcessId = Kernel32.GetCurrentProcessId();

    //    // Allow transparent windows
    //    // TODO: Eventually...
    //    ImGui_ImplWin32_EnableAlphaCompositing(data->Hwnd);

    //    data->HwndOwned = true;
    //    viewport.PlatformRequestResize = false;
    //    viewport.PlatformHandle = viewport.PlatformHandleRaw = (void*)data->Hwnd;
    //}

    //private static void ImGui_ImplWin32_DestroyWindow(ImGuiViewportPtr viewport)
    //{
    //    // This is also called on the main viewport for some reason, and we never set that viewport's PlatformUserData
    //    if (viewport.PlatformUserData == null) return;

    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    if (User32.GetCapture() == data->Hwnd)
    //    {
    //        // Transfer capture so if we started dragging from a window that later disappears, we'll still receive the MOUSEUP event.
    //        User32.ReleaseCapture();
    //        User32.SetCapture(_windowHandle);
    //    }

    //    if (data->Hwnd != IntPtr.Zero && data->HwndOwned)
    //    {
    //        var result = User32.DestroyWindow(data->Hwnd);

    //        const int ERROR_ACCESS_DENIED = 0x5;
    //        if (result == false && Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED)
    //        {
    //            // We are disposing, and we're doing it from a different thread because of course we are
    //            // Just send the window the close message
    //            User32.PostMessage(data->Hwnd, WindowMessage.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    //        }
    //    }

    //    data->Hwnd = IntPtr.Zero;
    //    Marshal.FreeHGlobal((IntPtr)viewport.PlatformUserData);
    //    viewport.PlatformUserData = viewport.PlatformHandle = null;
    //}

    //private static void ImGui_ImplWin32_ShowWindow(ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    if (viewport.Flags.HasFlag(ImGuiViewportFlags.NoFocusOnAppearing))
    //        User32.ShowWindow(data->Hwnd, ShowWindowCommand.ShowNoActivate);
    //    else
    //        User32.ShowWindow(data->Hwnd, ShowWindowCommand.Show);
    //}

    //static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    //static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    //static readonly IntPtr HWND_TOP = new IntPtr(0);
    //static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

    //static readonly int GWL_STYLE = -16;

    //public const int SWP_NOZORDER = 0x0004;
    //public const int SWP_NOACTIVATE = 0x0010;
    //public const int SWP_FRAMECHANGED = 0x0020;
    //public const int SWP_NOMOVE = 0x0002;
    //public const int SWP_NOSIZE = 0x0001;

    //private static void ImGui_ImplWin32_UpdateWindow(ImGuiViewportPtr viewport)
    //{
    //    // (Optional) Update Win32 style if it changed _after_ creation.
    //    // Generally they won't change unless configuration flags are changed, but advanced uses (such as manually rewriting viewport flags) make this useful.
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    viewport.Flags =
    //        (
    //            ImGuiViewportFlags.NoTaskBarIcon |
    //            ImGuiViewportFlags.NoFocusOnClick |
    //            ImGuiViewportFlags.NoRendererClear |
    //            ImGuiViewportFlags.NoFocusOnAppearing |
    //            viewport.Flags
    //        );
    //    User32.WindowStyles newStyle = 0;
    //    User32.WindowStylesEx newExStyle = 0;
    //    ImGui_ImplWin32_GetWin32StyleFromViewportFlags(viewport.Flags, ref newStyle, ref newExStyle);

    //    // Only reapply the flags that have been changed from our point of view (as other flags are being modified by Windows)
    //    if (data->DwStyle != newStyle || data->DwExStyle != newExStyle)
    //    {
    //        // (Optional) Update TopMost state if it changed _after_ creation
    //        bool topMostChanged = (data->DwExStyle & User32.WindowStylesEx.WS_EX_TOPMOST) !=
    //                              (newExStyle & User32.WindowStylesEx.WS_EX_TOPMOST);

    //        IntPtr insertAfter = IntPtr.Zero;
    //        if (topMostChanged)
    //        {
    //            if (viewport.Flags.HasFlag(ImGuiViewportFlags.TopMost))
    //                insertAfter = HWND_TOPMOST;
    //            else
    //                insertAfter = HWND_NOTOPMOST;
    //        }

    //        var swpFlag = topMostChanged ? 0 : SWP_NOZORDER;

    //        // Apply flags and position (since it is affected by flags)
    //        data->DwStyle = newStyle;
    //        data->DwExStyle = newExStyle;

    //        User32.SetWindowLongPtr(data->Hwnd, GWL_STYLE,
    //                             (IntPtr)data->DwStyle);
    //        User32.SetWindowLongPtr(data->Hwnd, GWL_EXSTYLE,
    //                             (IntPtr)data->DwExStyle);

    //        // Create window
    //        RECT rect;
    //        rect.Left = (int)viewport.Pos.X;
    //        rect.Top = (int)viewport.Pos.Y;
    //        rect.Right = (int)(viewport.Pos.X + viewport.Size.X);
    //        rect.Bottom = (int)(viewport.Pos.Y + viewport.Size.Y);
    //        User32.AdjustWindowRectEx(ref rect, data->DwStyle, false, data->DwExStyle);
    //        User32.SetWindowPos(data->Hwnd, insertAfter,
    //                            rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top,
    //                            (uint)swpFlag |
    //                            SWP_NOACTIVATE |
    //                            SWP_FRAMECHANGED);

    //        // This is necessary when we alter the style
    //        User32.ShowWindow(data->Hwnd, ShowWindowCommand.ShowNoActivate);
    //        viewport.PlatformRequestMove = viewport.PlatformRequestResize = true;
    //    }
    //}

    //private static unsafe Vector2* ImGui_ImplWin32_GetWindowPos(IntPtr unk, ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;
    //    var vec2 = (Vector2*)Marshal.AllocHGlobal(Marshal.SizeOf<Vector2>());

    //    POINT pt = new POINT { X = 0, Y = 0 };
    //    User32.ClientToScreen(data->Hwnd, ref pt);
    //    vec2->X = pt.X;
    //    vec2->Y = pt.Y;

    //    return vec2;
    //}

    //private static void ImGui_ImplWin32_SetWindowPos(ImGuiViewportPtr viewport, Vector2 pos)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    RECT rect;
    //    rect.Left = (int)pos.X;
    //    rect.Top = (int)pos.Y;
    //    rect.Right = (int)pos.X;
    //    rect.Bottom = (int)pos.Y;

    //    User32.AdjustWindowRectEx(ref rect, data->DwStyle, false, data->DwExStyle);
    //    User32.SetWindowPos(data->Hwnd, IntPtr.Zero,
    //                        rect.Left, rect.Top, 0, 0,
    //                        SWP_NOZORDER |
    //                        SWP_NOSIZE |
    //                        SWP_NOACTIVATE);
    //}

    //private static Vector2* ImGui_ImplWin32_GetWindowSize(IntPtr size, ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;
    //    var vec2 = (Vector2*)Marshal.AllocHGlobal(Marshal.SizeOf<Vector2>());

    //    User32.GetClientRect(data->Hwnd, out var rect);
    //    vec2->X = rect.Right - rect.Left;
    //    vec2->Y = rect.Bottom - rect.Top;

    //    return vec2;
    //}

    //private static void ImGui_ImplWin32_SetWindowSize(ImGuiViewportPtr viewport, Vector2 size)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    RECT rect;
    //    rect.Left = 0;
    //    rect.Top = 0;
    //    rect.Right = (int)size.X;
    //    rect.Bottom = (int)size.Y;

    //    User32.AdjustWindowRectEx(ref rect, data->DwStyle, false, data->DwExStyle);
    //    User32.SetWindowPos(data->Hwnd, IntPtr.Zero,
    //                        0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top,
    //                        SWP_NOZORDER |
    //                        SWP_NOMOVE |
    //                        SWP_NOACTIVATE);
    //}

    //private static void ImGui_ImplWin32_SetWindowFocus(ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    User32.BringWindowToTop(data->Hwnd);
    //    User32.SetForegroundWindow(data->Hwnd);
    //    User32.SetFocus(data->Hwnd);
    //}

    //private static bool ImGui_ImplWin32_GetWindowFocus(ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;
    //    return User32.GetForegroundWindow() == data->Hwnd;
    //}

    //private static byte ImGui_ImplWin32_GetWindowMinimized(ImGuiViewportPtr viewport)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;
    //    return (byte)(User32.IsIconic(data->Hwnd) ? 1 : 0);
    //}

    //private static void ImGui_ImplWin32_SetWindowTitle(ImGuiViewportPtr viewport, string title)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;
    //    User32.SetWindowText(data->Hwnd, title);
    //}

    //private static void ImGui_ImplWin32_SetWindowAlpha(ImGuiViewportPtr viewport, float alpha)
    //{
    //    var data = (ImGuiViewportDataWin32*)viewport.PlatformUserData;

    //    if (alpha < 1.0f)
    //    {
    //        User32.WindowStylesEx gwl =
    //            (User32.WindowStylesEx)User32.GetWindowLongPtr(data->Hwnd, GWL_EXSTYLE);
    //        User32.WindowStylesEx style = gwl | User32.WindowStylesEx.WS_EX_LAYERED;
    //        User32.SetWindowLongPtr(data->Hwnd, GWL_EXSTYLE,
    //                             (IntPtr)style);
    //        User32.SetLayeredWindowAttributes(data->Hwnd, 0, (byte)(255 * alpha), 0x2); //0x2 = LWA_ALPHA
    //    }
    //    else
    //    {
    //        User32.WindowStylesEx gwl =
    //            (User32.WindowStylesEx)User32.GetWindowLongPtr(data->Hwnd, GWL_EXSTYLE);
    //        User32.WindowStylesEx style = gwl & ~User32.WindowStylesEx.WS_EX_LAYERED;
    //        User32.SetWindowLongPtr(data->Hwnd, GWL_EXSTYLE,
    //                             (IntPtr)style);
    //    }
    //}

    // TODO: Decode why IME is miserable
    // private void ImGui_ImplWin32_SetImeInputPos(ImGuiViewportPtr viewport, Vector2 pos) {
    //     Win32.COMPOSITIONFORM cs = new Win32.COMPOSITIONFORM(
    //         0x20,
    //         new Win32.POINT(
    //             (int) (pos.X - viewport.Pos.X),
    //             (int) (pos.Y - viewport.Pos.Y)),
    //         new Win32.RECT(0, 0, 0, 0)
    //     );
    //     var hwnd = viewport.PlatformHandle;
    //     if (hwnd != IntPtr.Zero) {
    //         var himc = Win32.ImmGetContext(hwnd);
    //         if (himc != IntPtr.Zero) {
    //             Win32.ImmSetCompositionWindow(himc, ref cs);
    //             Win32.ImmReleaseContext(hwnd, himc);
    //         }
    //     }
    // }

    // TODO Alpha when it's no longer forced
    //private static void ImGui_ImplWin32_EnableAlphaCompositing(IntPtr hwnd)
    //{
    //}

    //private static unsafe void ImGui_ImplWin32_InitPlatformInterface()
    //{
    //    _classNamePtr = Marshal.StringToHGlobalUni("OverlayDearImGui Platform");

    //    _wndProcDelegate = WndProcDetour;

    //    var wcex = new WNDCLASSEXW();
    //    wcex.cbSize = (uint)Marshal.SizeOf(wcex);
    //    wcex.style = User32.CS_HREDRAW | User32.CS_VREDRAW;
    //    wcex.cbClsExtra = 0;
    //    wcex.cbWndExtra = 0;
    //    wcex.hInstance = Kernel32.GetModuleHandle(null);
    //    wcex.hIcon = IntPtr.Zero;
    //    wcex.hCursor = IntPtr.Zero;
    //    wcex.hbrBackground = new IntPtr(2); // COLOR_BACKGROUND is 1, so...
    //    wcex.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
    //    wcex.lpszMenuName = null;
    //    wcex.lpszClassName = _classNamePtr;

    //    wcex.hIconSm = IntPtr.Zero;
    //    User32.RegisterClassEx(ref wcex);

    //    ImGui_ImplWin32_UpdateMonitors();

    //    // Register platform interface (will be coupled with a renderer interface)
    //    ImGuiPlatformIOPtr io = ImGui.GetPlatformIO();
    //    io.PlatformCreateWindow = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_CreateWindow);
    //    io.PlatformDestroyWindow = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_DestroyWindow);
    //    io.PlatformShowWindow = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_ShowWindow);
    //    io.PlatformSetWindowPos = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_SetWindowPos);
    //    io.PlatformGetWindowPos = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_GetWindowPos);
    //    io.PlatformSetWindowSize = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_SetWindowSize);
    //    io.PlatformGetWindowSize = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_GetWindowSize);
    //    io.PlatformSetWindowFocus = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_SetWindowFocus);
    //    io.PlatformGetWindowFocus = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_GetWindowFocus);
    //    io.PlatformGetWindowMinimized = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_GetWindowMinimized);
    //    io.PlatformSetWindowTitle = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_SetWindowTitle);
    //    io.PlatformSetWindowAlpha = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_SetWindowAlpha);
    //    io.PlatformUpdateWindow = (void*)Marshal.GetFunctionPointerForDelegate(ImGui_ImplWin32_UpdateWindow);
    //    // io.Platform_SetImeInputPos = Marshal.GetFunctionPointerForDelegate(MonitorsetImeInputPos);

    //    // Register main window handle (which is owned by the main application, not by us)
    //    // This is mostly for simplicity and consistency, so that our code (e.g. mouse handling etc.) can use same logic for main and secondary viewports.
    //    ImGuiViewportPtr mainViewport = ImGui.GetMainViewport();

    //    var data = (ImGuiViewportDataWin32*)Marshal.AllocHGlobal(Marshal.SizeOf<ImGuiViewportDataWin32>());
    //    mainViewport.PlatformUserData = (void*)(IntPtr)data;
    //    data->Hwnd = _windowHandle;
    //    data->HwndOwned = false;
    //    mainViewport.PlatformHandle = (void*)_windowHandle;
    //}

    //// Helper structure we store in the void* RenderUserData field of each ImGuiViewport to easily retrieve our backend data->
    //private struct ImGuiViewportDataWin32
    //{
    //    public IntPtr Hwnd;
    //    public bool HwndOwned;
    //    public User32.WindowStyles DwStyle;
    //    public User32.WindowStylesEx DwExStyle;
    //}

    public static bool ImGui_ImplWin32_Init(void* hwnd)
    {
        return ImGui_ImplWin32_InitEx(hwnd, false);
    }

    public static bool ImGui_ImplWin32_InitForOpenGL(void* hwnd)
    {
        // OpenGL needs CS_OWNDC
        return ImGui_ImplWin32_InitEx(hwnd, true);
    }

    public static void Shutdown()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            Log.Error("No platform backend to shutdown, or already shutdown?");
            return;
        }

        var io = ImGui.GetIO();

        // Unload XInput library
        if (_xInputDLL != IntPtr.Zero)
            Kernel32.FreeLibrary(_xInputDLL);

        io.BackendPlatformName = null;
        io.BackendPlatformUserData = null;
        io.BackendFlags &= ~(ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos | ImGuiBackendFlags.HasGamepad);
    }

    static bool ImGui_ImplWin32_UpdateMouseCursor()
    {
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) != 0)
            return false;

        ImGuiMouseCursor imgui_cursor = ImGui.GetMouseCursor();
        if (imgui_cursor == ImGuiMouseCursor.None || io.MouseDrawCursor)
        {
            // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
            User32.SetCursor(IntPtr.Zero);
        }
        else
        {
            const int
                IDC_ARROW = 32512,
                IDC_IBEAM = 32513,
                IDC_SIZENWSE = 32642,
                IDC_SIZENESW = 32643,
                IDC_SIZEWE = 32644,
                IDC_SIZENS = 32645,
                IDC_SIZEALL = 32646,
                IDC_NO = 32648,
                IDC_HAND = 32649;

            var win32_cursor = IDC_ARROW;
            switch (imgui_cursor)
            {
                case ImGuiMouseCursor.Arrow: win32_cursor = IDC_ARROW; break;
                case ImGuiMouseCursor.TextInput: win32_cursor = IDC_IBEAM; break;
                case ImGuiMouseCursor.ResizeAll: win32_cursor = IDC_SIZEALL; break;
                case ImGuiMouseCursor.ResizeEw: win32_cursor = IDC_SIZEWE; break;
                case ImGuiMouseCursor.ResizeNs: win32_cursor = IDC_SIZENS; break;
                case ImGuiMouseCursor.ResizeNesw: win32_cursor = IDC_SIZENESW; break;
                case ImGuiMouseCursor.ResizeNwse: win32_cursor = IDC_SIZENWSE; break;
                case ImGuiMouseCursor.Hand: win32_cursor = IDC_HAND; break;
                case ImGuiMouseCursor.NotAllowed: win32_cursor = IDC_NO; break;
            }
            User32.SetCursor(User32.LoadCursor(IntPtr.Zero, win32_cursor));
        }
        return true;
    }

    static bool IsVkDown(VirtualKey vk)
    {
        return (User32.GetKeyState(vk) & 0x8000) != 0;
    }

    static void ImGui_ImplWin32_AddKeyEvent(ImGuiKey key, bool down, VirtualKey native_keycode, int native_scancode = -1)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(key, down);
        io.SetKeyEventNativeData(key, (int)native_keycode, native_scancode); // To support legacy indexing (<1.87 user code)
    }

    static void ImGui_ImplWin32_ProcessKeyEventsWorkarounds()
    {
        // Left & right Shift keys: when both are pressed together, Windows tend to not generate the WM_KEYUP event for the first released one.
        if (ImGui.IsKeyDown(ImGuiKey.LeftShift) && !IsVkDown(VirtualKey.LeftShift))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftShift, false, VirtualKey.LeftShift);
        if (ImGui.IsKeyDown(ImGuiKey.RightShift) && !IsVkDown(VirtualKey.RightShift))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightShift, false, VirtualKey.RightShift);

        // Sometimes WM_KEYUP for Win key is not passed down to the app (e.g. for Win+V on some setups, according to GLFW).
        if (ImGui.IsKeyDown(ImGuiKey.LeftSuper) && !IsVkDown(VirtualKey.LeftWindows))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftSuper, false, VirtualKey.LeftWindows);
        if (ImGui.IsKeyDown(ImGuiKey.RightSuper) && !IsVkDown(VirtualKey.RightWindows))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightSuper, false, VirtualKey.RightWindows);
    }

    public static void ImGui_ImplWin32_UpdateKeyModifiers()
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, IsVkDown(VirtualKey.Control));
        io.AddKeyEvent(ImGuiKey.ModShift, IsVkDown(VirtualKey.Shift));
        io.AddKeyEvent(ImGuiKey.ModAlt, IsVkDown(VirtualKey.Menu));
        io.AddKeyEvent(ImGuiKey.ModSuper, IsVkDown(VirtualKey.Application));
    }

    public static void ImGui_ImplWin32_UpdateMouseData()
    {
        var io = ImGui.GetIO();

        IntPtr focused_window = User32.GetForegroundWindow();
        bool is_app_focused = focused_window == _windowHandle;
        if (is_app_focused)
        {
            // (Optional) Set OS mouse position from Dear ImGui if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
            if (io.WantSetMousePos)
            {
                User32.POINT pos = new User32.POINT((int)io.MousePos.X, (int)io.MousePos.Y);
                if (User32.ClientToScreen(_windowHandle, ref pos))
                    User32.SetCursorPos(pos.X, pos.Y);
            }

            // (Optional) Fallback to provide mouse position when focused (WM_MOUSEMOVE already provides this when hovered or captured)
            // This also fills a short gap when clicking non-client area: WM_NCMOUSELEAVE -> modal OS move -> gap -> WM_NCMOUSEMOVE
            if (!io.WantSetMousePos && _mouseTrackedArea == 0)
            {
                User32.POINT pos;
                if (User32.GetCursorPos(out pos) && User32.ScreenToClient(_windowHandle, ref pos))
                    io.AddMousePosEvent(pos.X, pos.Y);
            }
        }
    }

    // Gamepad navigation mapping
    static void ImGui_ImplWin32_UpdateGamepads()
    {
        /*var io = ImGui.GetIO();
        ImGui_ImplWin32_Data* bd = ImGui_ImplWin32_GetBackendData();
        //if ((io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) == 0) // FIXME: Technically feeding gamepad shouldn't depend on this now that they are regular inputs.
        //    return;

        // Calling XInputGetState() every frame on disconnected gamepads is unfortunately too slow.
        // Instead we refresh gamepad availability by calling XInputGetCapabilities() _only_ after receiving WM_DEVICECHANGE.
        if (bd->WantUpdateHasGamepad)
        {
            XINPUT_CAPABILITIES caps = { };
            bd->HasGamepad = bd->XInputGetCapabilities ? (bd->XInputGetCapabilities(0, XINPUT_FLAG_GAMEPAD, &caps) == ERROR_SUCCESS) : false;
            bd->WantUpdateHasGamepad = false;
        }

        io.BackendFlags &= ~ImGuiBackendFlags_HasGamepad;
        XINPUT_STATE xinput_state;
        XINPUT_GAMEPAD & gamepad = xinput_state.Gamepad;
        if (!bd->HasGamepad || bd->XInputGetState == nullptr || bd->XInputGetState(0, &xinput_state) != ERROR_SUCCESS)
            return;
        io.BackendFlags |= ImGuiBackendFlags_HasGamepad;

        MAP_BUTTON(ImGuiKey_GamepadStart, XINPUT_GAMEPAD_START);
        MAP_BUTTON(ImGuiKey_GamepadBack, XINPUT_GAMEPAD_BACK);
        MAP_BUTTON(ImGuiKey_GamepadFaceLeft, XINPUT_GAMEPAD_X);
        MAP_BUTTON(ImGuiKey_GamepadFaceRight, XINPUT_GAMEPAD_B);
        MAP_BUTTON(ImGuiKey_GamepadFaceUp, XINPUT_GAMEPAD_Y);
        MAP_BUTTON(ImGuiKey_GamepadFaceDown, XINPUT_GAMEPAD_A);
        MAP_BUTTON(ImGuiKey_GamepadDpadLeft, XINPUT_GAMEPAD_DPAD_LEFT);
        MAP_BUTTON(ImGuiKey_GamepadDpadRight, XINPUT_GAMEPAD_DPAD_RIGHT);
        MAP_BUTTON(ImGuiKey_GamepadDpadUp, XINPUT_GAMEPAD_DPAD_UP);
        MAP_BUTTON(ImGuiKey_GamepadDpadDown, XINPUT_GAMEPAD_DPAD_DOWN);
        MAP_BUTTON(ImGuiKey_GamepadL1, XINPUT_GAMEPAD_LEFT_SHOULDER);
        MAP_BUTTON(ImGuiKey_GamepadR1, XINPUT_GAMEPAD_RIGHT_SHOULDER);
        MAP_ANALOG(ImGuiKey_GamepadL2, gamepad.bLeftTrigger, XINPUT_GAMEPAD_TRIGGER_THRESHOLD, 255);
        MAP_ANALOG(ImGuiKey_GamepadR2, gamepad.bRightTrigger, XINPUT_GAMEPAD_TRIGGER_THRESHOLD, 255);
        MAP_BUTTON(ImGuiKey_GamepadL3, XINPUT_GAMEPAD_LEFT_THUMB);
        MAP_BUTTON(ImGuiKey_GamepadR3, XINPUT_GAMEPAD_RIGHT_THUMB);
        MAP_ANALOG(ImGuiKey_GamepadLStickLeft, gamepad.sThumbLX, -XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, -32768);
        MAP_ANALOG(ImGuiKey_GamepadLStickRight, gamepad.sThumbLX, +XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, +32767);
        MAP_ANALOG(ImGuiKey_GamepadLStickUp, gamepad.sThumbLY, +XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, +32767);
        MAP_ANALOG(ImGuiKey_GamepadLStickDown, gamepad.sThumbLY, -XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, -32768);
        MAP_ANALOG(ImGuiKey_GamepadRStickLeft, gamepad.sThumbRX, -XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, -32768);
        MAP_ANALOG(ImGuiKey_GamepadRStickRight, gamepad.sThumbRX, +XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, +32767);
        MAP_ANALOG(ImGuiKey_GamepadRStickUp, gamepad.sThumbRY, +XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, +32767);
        MAP_ANALOG(ImGuiKey_GamepadRStickDown, gamepad.sThumbRY, -XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, -32768);*/
    }

    public static void NewFrame()
    {
        var io = ImGui.GetIO();

        // Setup display size (every frame to accommodate for window resizing)
        User32.GetClientRect(_windowHandle, out var rect);
        io.DisplaySize = new Vector2(rect.Right - rect.Left, rect.Bottom - rect.Top);

        // Setup time step
        Kernel32.QueryPerformanceCounter(out var current_time);
        io.DeltaTime = (float)(current_time - _time) / _ticksPerSecond;
        _time = current_time;

        // Update OS mouse position
        ImGui_ImplWin32_UpdateMouseData();

        // Process workarounds for known Windows key handling issues
        ImGui_ImplWin32_ProcessKeyEventsWorkarounds();

        // Update OS mouse cursor with the cursor requested by imgui
        ImGuiMouseCursor mouse_cursor = io.MouseDrawCursor ? ImGuiMouseCursor.None : ImGui.GetMouseCursor();
        if (_lastMouseCursor != mouse_cursor)
        {
            _lastMouseCursor = mouse_cursor;
            ImGui_ImplWin32_UpdateMouseCursor();
        }

        // Update game controllers (if enabled and available)
        ImGui_ImplWin32_UpdateGamepads();
    }

    public const VirtualKey IM_VK_KEYPAD_ENTER = (VirtualKey)((int)VirtualKey.Return + 256);

    // Map VK_xxx to ImGuiKey_xxx.
    public static ImGuiKey ImGui_ImplWin32_VirtualKeyToImGuiKey(VirtualKey wParam)
    {
        switch (wParam)
        {
            case VirtualKey.Tab: return ImGuiKey.Tab;
            case VirtualKey.Left: return ImGuiKey.LeftArrow;
            case VirtualKey.Right: return ImGuiKey.RightArrow;
            case VirtualKey.Up: return ImGuiKey.UpArrow;
            case VirtualKey.Down: return ImGuiKey.DownArrow;
            case VirtualKey.Prior: return ImGuiKey.PageUp;
            case VirtualKey.Next: return ImGuiKey.PageDown;
            case VirtualKey.Home: return ImGuiKey.Home;
            case VirtualKey.End: return ImGuiKey.End;
            case VirtualKey.Insert: return ImGuiKey.Insert;
            case VirtualKey.Delete: return ImGuiKey.Delete;
            case VirtualKey.Back: return ImGuiKey.Backspace;
            case VirtualKey.Space: return ImGuiKey.Space;
            case VirtualKey.Return: return ImGuiKey.Enter;
            case VirtualKey.Escape: return ImGuiKey.Escape;
            case VirtualKey.OEM7: return ImGuiKey.Apostrophe;
            case VirtualKey.OEMComma: return ImGuiKey.Comma;
            case VirtualKey.OEMMinus: return ImGuiKey.Minus;
            case VirtualKey.OEMPeriod: return ImGuiKey.Period;
            case VirtualKey.OEM2: return ImGuiKey.Slash;
            case VirtualKey.OEM1: return ImGuiKey.Semicolon;
            case VirtualKey.OEMPlus: return ImGuiKey.Equal;
            case VirtualKey.OEM4: return ImGuiKey.LeftBracket;
            case VirtualKey.OEM5: return ImGuiKey.Backslash;
            case VirtualKey.OEM6: return ImGuiKey.RightBracket;
            case VirtualKey.OEM3: return ImGuiKey.GraveAccent;
            case VirtualKey.CapsLock: return ImGuiKey.CapsLock;
            case VirtualKey.ScrollLock: return ImGuiKey.ScrollLock;
            case VirtualKey.NumLock: return ImGuiKey.NumLock;
            case VirtualKey.Snapshot: return ImGuiKey.PrintScreen;
            case VirtualKey.Pause: return ImGuiKey.Pause;
            case VirtualKey.Numpad0: return ImGuiKey.Keypad0;
            case VirtualKey.Numpad1: return ImGuiKey.Keypad1;
            case VirtualKey.Numpad2: return ImGuiKey.Keypad2;
            case VirtualKey.Numpad3: return ImGuiKey.Keypad3;
            case VirtualKey.Numpad4: return ImGuiKey.Keypad4;
            case VirtualKey.Numpad5: return ImGuiKey.Keypad5;
            case VirtualKey.Numpad6: return ImGuiKey.Keypad6;
            case VirtualKey.Numpad7: return ImGuiKey.Keypad7;
            case VirtualKey.Numpad8: return ImGuiKey.Keypad8;
            case VirtualKey.Numpad9: return ImGuiKey.Keypad9;
            case VirtualKey.Decimal: return ImGuiKey.KeypadDecimal;
            case VirtualKey.Divide: return ImGuiKey.KeypadDivide;
            case VirtualKey.Multiply: return ImGuiKey.KeypadMultiply;
            case VirtualKey.Subtract: return ImGuiKey.KeypadSubtract;
            case VirtualKey.Add: return ImGuiKey.KeypadAdd;
            case IM_VK_KEYPAD_ENTER: return ImGuiKey.KeypadEnter;
            case VirtualKey.LeftShift: return ImGuiKey.LeftShift;
            case VirtualKey.LeftControl: return ImGuiKey.LeftCtrl;
            case VirtualKey.LeftMenu: return ImGuiKey.LeftAlt;
            case VirtualKey.LeftWindows: return ImGuiKey.LeftSuper;
            case VirtualKey.RightShift: return ImGuiKey.RightShift;
            case VirtualKey.RightControl: return ImGuiKey.RightCtrl;
            case VirtualKey.RightMenu: return ImGuiKey.RightAlt;
            case VirtualKey.RightWindows: return ImGuiKey.RightSuper;
            case VirtualKey.Application: return ImGuiKey.Menu;
            case (VirtualKey)'0': return ImGuiKey.Key0;
            case (VirtualKey)'1': return ImGuiKey.Key1;
            case (VirtualKey)'2': return ImGuiKey.Key2;
            case (VirtualKey)'3': return ImGuiKey.Key3;
            case (VirtualKey)'4': return ImGuiKey.Key4;
            case (VirtualKey)'5': return ImGuiKey.Key5;
            case (VirtualKey)'6': return ImGuiKey.Key6;
            case (VirtualKey)'7': return ImGuiKey.Key7;
            case (VirtualKey)'8': return ImGuiKey.Key8;
            case (VirtualKey)'9': return ImGuiKey.Key9;
            case (VirtualKey)'A': return ImGuiKey.A;
            case (VirtualKey)'B': return ImGuiKey.B;
            case (VirtualKey)'C': return ImGuiKey.C;
            case (VirtualKey)'D': return ImGuiKey.D;
            case (VirtualKey)'E': return ImGuiKey.E;
            case (VirtualKey)'F': return ImGuiKey.F;
            case (VirtualKey)'G': return ImGuiKey.G;
            case (VirtualKey)'H': return ImGuiKey.H;
            case (VirtualKey)'I': return ImGuiKey.I;
            case (VirtualKey)'J': return ImGuiKey.J;
            case (VirtualKey)'K': return ImGuiKey.K;
            case (VirtualKey)'L': return ImGuiKey.L;
            case (VirtualKey)'M': return ImGuiKey.M;
            case (VirtualKey)'N': return ImGuiKey.N;
            case (VirtualKey)'O': return ImGuiKey.O;
            case (VirtualKey)'P': return ImGuiKey.P;
            case (VirtualKey)'Q': return ImGuiKey.Q;
            case (VirtualKey)'R': return ImGuiKey.R;
            case (VirtualKey)'S': return ImGuiKey.S;
            case (VirtualKey)'T': return ImGuiKey.T;
            case (VirtualKey)'U': return ImGuiKey.U;
            case (VirtualKey)'V': return ImGuiKey.V;
            case (VirtualKey)'W': return ImGuiKey.W;
            case (VirtualKey)'X': return ImGuiKey.X;
            case (VirtualKey)'Y': return ImGuiKey.Y;
            case (VirtualKey)'Z': return ImGuiKey.Z;
            case VirtualKey.F1: return ImGuiKey.F1;
            case VirtualKey.F2: return ImGuiKey.F2;
            case VirtualKey.F3: return ImGuiKey.F3;
            case VirtualKey.F4: return ImGuiKey.F4;
            case VirtualKey.F5: return ImGuiKey.F5;
            case VirtualKey.F6: return ImGuiKey.F6;
            case VirtualKey.F7: return ImGuiKey.F7;
            case VirtualKey.F8: return ImGuiKey.F8;
            case VirtualKey.F9: return ImGuiKey.F9;
            case VirtualKey.F10: return ImGuiKey.F10;
            case VirtualKey.F11: return ImGuiKey.F11;
            case VirtualKey.F12: return ImGuiKey.F12;
            default: return ImGuiKey.None;
        }
    }

    // See https://learn.microsoft.com/en-us/windows/win32/tablet/system-events-and-mouse-messages
    // Prefer to call this at the top of the message handler to avoid the possibility of other Win32 calls interfering with Monitor
    static ImGuiMouseSource GetMouseSourceFromMessageExtraInfo()
    {
        var extra_info = (uint)User32.GetMessageExtraInfo();
        if ((extra_info & 0xFFFFFF80) == 0xFF515700)
            return ImGuiMouseSource.Pen;
        if ((extra_info & 0xFFFFFF80) == 0xFF515780)
            return ImGuiMouseSource.TouchScreen;
        return ImGuiMouseSource.Mouse;
    }

    public static int GET_X_LPARAM(IntPtr lp) => unchecked((short)(long)lp);
    public static int GET_Y_LPARAM(IntPtr lp) => unchecked((short)((long)lp >> 16));

    public static ushort HIWORD(IntPtr dwValue) => unchecked((ushort)((long)dwValue >> 16));
    public static ushort HIWORD(UIntPtr dwValue) => unchecked((ushort)((ulong)dwValue >> 16));

    public static ushort LOWORD(IntPtr dwValue) => unchecked((ushort)(long)dwValue);
    public static ushort LOWORD(UIntPtr dwValue) => unchecked((ushort)(ulong)dwValue);

    public static ushort GET_XBUTTON_WPARAM(UIntPtr val)
    {
        // #define GET_XBUTTON_WPARAM(wParam)  (HIWORD(wParam))
        return HIWORD(val);
    }
    const int XBUTTON1 = 1;
    const int WHEEL_DELTA = 120;
    public static ushort GET_XBUTTON_WPARAM(IntPtr val)
    {
        // #define GET_XBUTTON_WPARAM(wParam)  (HIWORD(wParam))
        return HIWORD(val);
    }

    internal static int GET_WHEEL_DELTA_WPARAM(IntPtr wParam)
    {
        return (short)HIWORD(wParam);
    }

    internal static int GET_WHEEL_DELTA_WPARAM(UIntPtr wParam)
    {
        return (short)HIWORD(wParam);
    }

    public static byte LOBYTE(ushort wValue) => (byte)(wValue & 0xff);

    public static IntPtr WndProcHandler(IntPtr hwnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        if (ImGui.GetCurrentContext().IsNull)
            return IntPtr.Zero;

        var io = ImGui.GetIO();

        switch (msg)
        {
            case WindowMessage.WM_MOUSEMOVE:
            case WindowMessage.WM_NCMOUSEMOVE:
                {
                    // We need to call TrackMouseEvent in order to receive WM_MOUSELEAVE events
                    ImGuiMouseSource mouse_source = GetMouseSourceFromMessageExtraInfo();
                    int area = msg == WindowMessage.WM_MOUSEMOVE ? 1 : 2;
                    _mouseHandle = hwnd;
                    if (_mouseTrackedArea != area)
                    {
                        User32.TRACKMOUSEEVENT tme_cancel = new(User32.TMEFlags.TME_CANCEL, hwnd, 0);
                        User32.TRACKMOUSEEVENT tme_track = new(area == 2 ? User32.TMEFlags.TME_LEAVE | User32.TMEFlags.TME_NONCLIENT : User32.TMEFlags.TME_LEAVE, hwnd, 0);
                        if (_mouseTrackedArea != 0)
                            User32.TrackMouseEvent(ref tme_cancel);
                        User32.TrackMouseEvent(ref tme_track);
                        _mouseTrackedArea = area;
                    }
                    User32.POINT mouse_pos = new(GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
                    if (msg == WindowMessage.WM_NCMOUSEMOVE && User32.ScreenToClient(hwnd, ref mouse_pos) == false) // WM_NCMOUSEMOVE are provided in absolute coordinates.
                        break;
                    io.AddMouseSourceEvent(mouse_source);
                    io.AddMousePosEvent(mouse_pos.X, mouse_pos.Y);
                    break;
                }
            case WindowMessage.WM_MOUSELEAVE:
            case WindowMessage.WM_NCMOUSELEAVE:
                {
                    int area = msg == WindowMessage.WM_MOUSELEAVE ? 1 : 2;
                    if (_mouseTrackedArea == area)
                    {
                        if (_mouseHandle == hwnd)
                            _mouseHandle = IntPtr.Zero;
                        _mouseTrackedArea = 0;
                        io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
                    }
                    break;
                }
            case WindowMessage.WM_LBUTTONDOWN:
            case WindowMessage.WM_LBUTTONDBLCLK:
            case WindowMessage.WM_RBUTTONDOWN:
            case WindowMessage.WM_RBUTTONDBLCLK:
            case WindowMessage.WM_MBUTTONDOWN:
            case WindowMessage.WM_MBUTTONDBLCLK:
            case WindowMessage.WM_XBUTTONDOWN:
            case WindowMessage.WM_XBUTTONDBLCLK:
                {
                    ImGuiMouseSource mouse_source = GetMouseSourceFromMessageExtraInfo();
                    int button = 0;
                    if (msg == WindowMessage.WM_LBUTTONDOWN || msg == WindowMessage.WM_LBUTTONDBLCLK) { button = 0; }
                    if (msg == WindowMessage.WM_RBUTTONDOWN || msg == WindowMessage.WM_RBUTTONDBLCLK) { button = 1; }
                    if (msg == WindowMessage.WM_MBUTTONDOWN || msg == WindowMessage.WM_MBUTTONDBLCLK) { button = 2; }
                    if (msg == WindowMessage.WM_XBUTTONDOWN || msg == WindowMessage.WM_XBUTTONDBLCLK) { button = GET_XBUTTON_WPARAM(wParam) == XBUTTON1 ? 3 : 4; }
                    if (_mouseButtonsDown == 0 && User32.GetCapture() == IntPtr.Zero)
                        User32.SetCapture(hwnd);
                    _mouseButtonsDown |= 1 << button;
                    io.AddMouseSourceEvent(mouse_source);
                    io.AddMouseButtonEvent(button, true);
                    return IntPtr.Zero;
                }
            case WindowMessage.WM_LBUTTONUP:
            case WindowMessage.WM_RBUTTONUP:
            case WindowMessage.WM_MBUTTONUP:
            case WindowMessage.WM_XBUTTONUP:
                {
                    ImGuiMouseSource mouse_source = GetMouseSourceFromMessageExtraInfo();
                    int button = 0;
                    if (msg == WindowMessage.WM_LBUTTONUP) { button = 0; }
                    if (msg == WindowMessage.WM_RBUTTONUP) { button = 1; }
                    if (msg == WindowMessage.WM_MBUTTONUP) { button = 2; }
                    if (msg == WindowMessage.WM_XBUTTONUP) { button = GET_XBUTTON_WPARAM(wParam) == XBUTTON1 ? 3 : 4; }
                    _mouseButtonsDown &= ~(1 << button);
                    if (_mouseButtonsDown == 0 && User32.GetCapture() == hwnd)
                        User32.ReleaseCapture();
                    io.AddMouseSourceEvent(mouse_source);
                    io.AddMouseButtonEvent(button, false);
                    return IntPtr.Zero;
                }
            case WindowMessage.WM_MOUSEWHEEL:
                io.AddMouseWheelEvent(0.0f, GET_WHEEL_DELTA_WPARAM(wParam) / (float)WHEEL_DELTA);
                return IntPtr.Zero;
            case WindowMessage.WM_MOUSEHWHEEL:
                io.AddMouseWheelEvent(-(float)GET_WHEEL_DELTA_WPARAM(wParam) / WHEEL_DELTA, 0.0f);
                return IntPtr.Zero;
            case WindowMessage.WM_KEYDOWN:
            case WindowMessage.WM_KEYUP:
            case WindowMessage.WM_SYSKEYDOWN:
            case WindowMessage.WM_SYSKEYUP:
                {
                    bool is_key_down = msg == WindowMessage.WM_KEYDOWN || msg == WindowMessage.WM_SYSKEYDOWN;
                    if ((int)wParam < 256)
                    {
                        // Submit modifiers
                        ImGui_ImplWin32_UpdateKeyModifiers();

                        // Obtain virtual key code
                        // (keypad enter doesn't have its own... VK_RETURN with KF_EXTENDED flag means keypad enter, see IM_VK_KEYPAD_ENTER definition for details, it is mapped to ImGuiKey_KeyPadEnter.)
                        VirtualKey vk = (VirtualKey)wParam;


                        bool isEnter = (VirtualKey)wParam == VirtualKey.Return;
                        bool hasExtendedKeyFlag = (HIWORD(lParam) & (ushort)User32.KeyFlag.KF_EXTENDED) != 0;
                        if (isEnter && hasExtendedKeyFlag)
                            vk = IM_VK_KEYPAD_ENTER;

                        // Submit key event
                        ImGuiKey key = ImGui_ImplWin32_VirtualKeyToImGuiKey(vk);
                        int scancode = LOBYTE(HIWORD(lParam));
                        if (key != ImGuiKey.None)
                            ImGui_ImplWin32_AddKeyEvent(key, is_key_down, vk, scancode);

                        // Submit individual left/right modifier events
                        if (vk == VirtualKey.Shift)
                        {
                            // Important: Shift keys tend to get stuck when pressed together, missing key-up events are corrected in ImGui_ImplWin32_ProcessKeyEventsWorkarounds()
                            if (IsVkDown(VirtualKey.LeftShift) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftShift, is_key_down, VirtualKey.LeftShift, scancode); }
                            if (IsVkDown(VirtualKey.RightShift) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightShift, is_key_down, VirtualKey.RightShift, scancode); }
                        }
                        else if (vk == VirtualKey.Control)
                        {
                            if (IsVkDown(VirtualKey.LeftControl) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftCtrl, is_key_down, VirtualKey.LeftControl, scancode); }
                            if (IsVkDown(VirtualKey.RightControl) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightCtrl, is_key_down, VirtualKey.RightControl, scancode); }
                        }
                        else if (vk == VirtualKey.Menu)
                        {
                            if (IsVkDown(VirtualKey.LeftMenu) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftAlt, is_key_down, VirtualKey.LeftMenu, scancode); }
                            if (IsVkDown(VirtualKey.RightMenu) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightAlt, is_key_down, VirtualKey.RightMenu, scancode); }
                        }
                    }
                    return IntPtr.Zero;
                }
            case WindowMessage.WM_SETFOCUS:
            case WindowMessage.WM_KILLFOCUS:
                io.AddFocusEvent(msg == WindowMessage.WM_SETFOCUS);
                return IntPtr.Zero;
            case WindowMessage.WM_CHAR:
                if (User32.IsWindowUnicode(hwnd))
                {
                    // You can also use ToAscii()+GetKeyboardState() to retrieve characters.
                    if ((int)wParam > 0 && (int)wParam < 0x10000)
                        io.AddInputCharacterUTF16((ushort)wParam);
                }
                else
                {
                    byte[] lolxd = new byte[1] { 0 };
                    lolxd[0] = *(byte*)&wParam;

                    const int wideBufferSize = 1;
                    var wideBuffer = Marshal.AllocHGlobal(wideBufferSize);
                    Kernel32.MultiByteToWideChar(Kernel32.CP_ACP, Kernel32.MB_PRECOMPOSED, lolxd, 1, wideBuffer, wideBufferSize);
                    var wideBufferChar = *(char*)wideBuffer;
                    io.AddInputCharacter(wideBufferChar);
                    Marshal.FreeHGlobal(wideBuffer);
                }
                return IntPtr.Zero;
            case WindowMessage.WM_SETCURSOR:
                const int HTCLIENT = 1;
                // This is required to restore cursor when transitioning from e.g resize borders to client area.
                if (LOWORD(lParam) == HTCLIENT && ImGui_ImplWin32_UpdateMouseCursor())
                    return new(1);
                return IntPtr.Zero;
            case WindowMessage.WM_DEVICECHANGE:
                //const int DBT_DEVNODES_CHANGED = 0x0007;
                //if ((uint)wParam == DBT_DEVNODES_CHANGED)
                //_wantUpdateHasGamepad = true;
                return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    static bool _IsWindowsVersionOrGreater(short major, short minor, short unused)
    {
        const uint VER_MAJORVERSION = 0x0000002;
        const uint VER_MINORVERSION = 0x0000001;
        const byte VER_GREATER_EQUAL = 3;

        var versionInfo = OSVERSIONINFOEX.Create();
        ulong conditionMask = 0;
        versionInfo.dwMajorVersion = major;
        versionInfo.dwMinorVersion = minor;
        Kernel32.VER_SET_CONDITION(ref conditionMask, VER_MAJORVERSION, VER_GREATER_EQUAL);
        Kernel32.VER_SET_CONDITION(ref conditionMask, VER_MINORVERSION, VER_GREATER_EQUAL);
        return Ntdll.RtlVerifyVersionInfo(&versionInfo, VER_MASK.VER_MAJORVERSION | VER_MASK.VER_MINORVERSION, (long)conditionMask) == 0 ? true : false;
    }

    static bool _IsWindowsVistaOrGreater() => _IsWindowsVersionOrGreater((short)Kernel32.HiByte(0x0600), LOBYTE(0x0600), 0); // _WIN32_WINNT_VISTA
    static bool _IsWindows8OrGreater() => _IsWindowsVersionOrGreater((short)Kernel32.HiByte(0x0602), LOBYTE(0x0602), 0); // _WIN32_WINNT_WIN8
    static bool _IsWindows8Point1OrGreater() => _IsWindowsVersionOrGreater((short)Kernel32.HiByte(0x0603), LOBYTE(0x0603), 0); // _WIN32_WINNT_WINBLUE
    static bool _IsWindows10OrGreater() => _IsWindowsVersionOrGreater((short)Kernel32.HiByte(0x0A00), LOBYTE(0x0A00), 0); // _WIN32_WINNT_WINTHRESHOLD / _WIN32_WINNT_WIN10

    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    // Helper function to enable DPI awareness without setting up a manifest
    static void ImGui_ImplWin32_EnableDpiAwareness()
    {
        if (_IsWindows10OrGreater())
        {
            User32.SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            return;
        }
        if (_IsWindows8Point1OrGreater())
        {
            Shellscalingapi.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
            return;
        }

        User32.SetProcessDPIAware();
    }

    public static float ImGui_ImplWin32_GetDpiScaleForMonitor(void* monitor)
    {
        uint xdpi;

        if (_IsWindows8Point1OrGreater())
        {
            Shellscalingapi.GetDpiForMonitor((IntPtr)monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out xdpi, out _);

            return xdpi / 96.0f;
        }

        const int LOGPIXELSX = 88;

        var dc = User32.GetDC(IntPtr.Zero);
        xdpi = (uint)Gdi32.GetDeviceCaps(dc, LOGPIXELSX);

        User32.ReleaseDC(IntPtr.Zero, dc);
        return xdpi / 96.0f;
    }

    public static unsafe float ImGui_ImplWin32_GetDpiScaleForHwnd(void* hwnd)
    {
        const int MONITOR_DEFAULTTONEAREST = 2;

        var monitor = User32.MonitorFromWindow((IntPtr)hwnd, MONITOR_DEFAULTTONEAREST);
        return ImGui_ImplWin32_GetDpiScaleForMonitor((void*)monitor);
    }

    public static unsafe void ImGui_ImplWin32_EnableAlphaCompositing(void* hwnd)
    {
        if (!_IsWindowsVistaOrGreater())
            return;

        var hres = Dwmapi.DwmIsCompositionEnabled(out bool composition);

        if (hres != 0 || !composition)
            return;

        hres = Dwmapi.DwmGetColorizationColor(out var color, out var opaque);

        if (_IsWindows8OrGreater() || hres == 0 && !opaque)
        {
            var region = Gdi32.CreateRectRgn(0, 0, -1, -1);
            Dwmapi.DWM_BLURBEHIND bb = new(true);
            bb.dwFlags |= Dwmapi.DWM_BB.BlurRegion;
            bb.hRgnBlur = region;
            Dwmapi.DwmEnableBlurBehindWindow((IntPtr)hwnd, ref bb);
            Gdi32.DeleteObject(region);
        }
        else
        {
            Dwmapi.DWM_BLURBEHIND bb = new(true);
            Dwmapi.DwmEnableBlurBehindWindow((IntPtr)hwnd, ref bb);
        }
    }

    internal static void Init(IntPtr windowHandle) => ImGui_ImplWin32_Init((void*)windowHandle);
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member