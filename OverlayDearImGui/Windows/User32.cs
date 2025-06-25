using System;
using System.Runtime.InteropServices;

namespace OverlayDearImGui.Windows;

public static partial class User32
{
    [Flags]
    public enum KeyFlag : int
    {
        /// <summary>
        /// Manipulates the extended key flag.
        /// </summary>
        KF_EXTENDED = 0x0100,
        /// <summary>
        /// Manipulates the dialog mode flag, which indicates whether a dialog box is active.
        /// </summary>
        KF_DLGMODE = 0x0800,
        /// <summary>
        /// Manipulates the menu mode flag, which indicates whether a menu is active.
        /// </summary>
        KF_MENUMODE = 0x1000,
        /// <summary>
        /// Manipulates the ALT key flag, which indicated if the ALT key is pressed.
        /// </summary>
        KF_ALTDOWN = 0x2000,
        /// <summary>
        /// Manipulates the repeat count.
        /// </summary>
        KF_REPEAT = 0x4000,
        /// <summary>
        /// Manipulates the transition state flag.
        /// </summary>
        KF_UP = 0x8000
    }

    [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>
    /// Unregisters a window class, freeing the memory required for the class.
    /// </summary>
    /// <param name="lpClassName">
    /// Type: LPCTSTR
    /// A null-terminated string or a class atom. If lpClassName is a string, it specifies the window class name.
    /// This class name must have been registered by a previous call to the RegisterClass or RegisterClassEx function.
    /// System classes, such as dialog box controls, cannot be unregistered. If this parameter is an atom,
    ///   it must be a class atom created by a previous call to the RegisterClass or RegisterClassEx function.
    /// The atom must be in the low-order word of lpClassName; the high-order word must be zero.
    ///
    /// </param>
    /// <param name="hInstance">
    /// A handle to the instance of the module that created the class.
    ///
    /// </param>
    /// <returns>
    /// Type: BOOL
    /// If the function succeeds, the return value is nonzero.
    /// If the class could not be found or if a window still exists that was created with the class, the return value is zero.
    /// To get extended error information, call GetLastError.
    ///
    /// </returns>
    [DllImport("user32.dll")]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public enum ShowWindowCommand : int
    {
        Hide = 0,
        ShowNormal = 1,
        ShowMinimized = 2,
        ShowMaximized = 3,
        ShowNoActivate = 4,
        Show = 5,
        Minimize = 6,
        ShowMinNoActive = 7,
        ShowNA = 8,
        Restore = 9,
        ShowDefault = 10,
        ForceMinimize = 11
    }

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, WindowMessage uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("User32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    // size of a device name string
    private const int CCHDEVICENAME = 32;

    /// <summary>
    /// The MONITORINFOEX structure contains information about a display monitor.
    /// The GetMonitorInfo function stores information into a MONITORINFOEX structure or a MONITORINFO structure.
    /// The MONITORINFOEX structure is a superset of the MONITORINFO structure. The MONITORINFOEX structure adds a string member to contain a name
    /// for the display monitor.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MonitorInfoEx
    {
        /// <summary>
        /// The size, in bytes, of the structure. Set this member to sizeof(MONITORINFOEX) (72) before calling the GetMonitorInfo function.
        /// Doing so lets the function determine the type of structure you are passing to it.
        /// </summary>
        public int Size;

        /// <summary>
        /// A RECT structure that specifies the display monitor rectangle, expressed in virtual-screen coordinates.
        /// Note that if the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.
        /// </summary>
        public RectStruct Monitor;

        /// <summary>
        /// A RECT structure that specifies the work area rectangle of the display monitor that can be used by applications,
        /// expressed in virtual-screen coordinates. Windows uses this rectangle to maximize an application on the monitor.
        /// The rest of the area in rcMonitor contains system windows such as the task bar and side bars.
        /// Note that if the monitor is not the primary display monitor, some of the rectangle's coordinates may be negative values.
        /// </summary>
        public RectStruct WorkArea;

        /// <summary>
        /// The attributes of the display monitor.
        ///
        /// This member can be the following value:
        ///   1 : MONITORINFOF_PRIMARY
        /// </summary>
        public uint Flags;

        /// <summary>
        /// A string that specifies the device name of the monitor being used. Most applications have no use for a display monitor name,
        /// and so can save some bytes by using a MONITORINFO structure.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string DeviceName;

        public void Init()
        {
            this.Size = 40 + 2 * CCHDEVICENAME;
            this.DeviceName = string.Empty;
        }
    }

    /// <summary>
    /// The RECT structure defines the coordinates of the upper-left and lower-right corners of a rectangle.
    /// </summary>
    /// <see cref="http://msdn.microsoft.com/en-us/library/dd162897%28VS.85%29.aspx"/>
    /// <remarks>
    /// By convention, the right and bottom edges of the rectangle are normally considered exclusive.
    /// In other words, the pixel whose coordinates are ( right, bottom ) lies immediately outside of the the rectangle.
    /// For example, when RECT is passed to the FillRect function, the rectangle is filled up to, but not including,
    /// the right column and bottom row of pixels. This structure is identical to the RECTL structure.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct RectStruct
    {
        /// <summary>
        /// The x-coordinate of the upper-left corner of the rectangle.
        /// </summary>
        public int Left;

        /// <summary>
        /// The y-coordinate of the upper-left corner of the rectangle.
        /// </summary>
        public int Top;

        /// <summary>
        /// The x-coordinate of the lower-right corner of the rectangle.
        /// </summary>
        public int Right;

        /// <summary>
        /// The y-coordinate of the lower-right corner of the rectangle.
        /// </summary>
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(SystemMetric smIndex);

    [DllImport("user32.dll")]
    public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowUnicode(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

    [DllImport("user32.dll")]
    public static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle,
    bool bMenu, uint dwExStyle);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(User32.VirtualKey vKey);

    public static IntPtr CreateWindowW(
        [In, Optional] string lpClassName,
        [In, Optional] string lpWindowName,
        [In] uint dwStyle,
        [In] int X,
        [In] int Y,
        [In] int nWidth,
        [In] int nHeight,
        [In, Optional] IntPtr hWndParent,
        [In, Optional] IntPtr hMenu,
        [In, Optional] IntPtr hInstance,
        [In, Optional] IntPtr lpParam
    )
    {
        return CreateWindowExW(0, lpClassName, lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
    }

    /// <summary>
    /// The services requested. This member can be a combination of the following values.
    /// </summary>
    /// <seealso cref="http://msdn.microsoft.com/en-us/library/ms645604%28v=vs.85%29.aspx"/>
    [Flags]
    public enum TMEFlags : uint
    {
        /// <summary>
        /// The caller wants to cancel a prior tracking request. The caller should also specify the type of tracking that it wants to cancel. For example, to cancel hover tracking, the caller must pass the TME_CANCEL and TME_HOVER flags.
        /// </summary>
        TME_CANCEL = 0x80000000,
        /// <summary>
        /// The caller wants hover notification. Notification is delivered as a WM_MOUSEHOVER message.
        /// If the caller requests hover tracking while hover tracking is already active, the hover timer will be reset.
        /// This flag is ignored if the mouse pointer is not over the specified window or area.
        /// </summary>
        TME_HOVER = 0x00000001,
        /// <summary>
        /// The caller wants leave notification. Notification is delivered as a WM_MOUSELEAVE message. If the mouse is not over the specified window or area, a leave notification is generated immediately and no further tracking is performed.
        /// </summary>
        TME_LEAVE = 0x00000002,
        /// <summary>
        /// The caller wants hover and leave notification for the nonclient areas. Notification is delivered as WM_NCMOUSEHOVER and WM_NCMOUSELEAVE messages.
        /// </summary>
        TME_NONCLIENT = 0x00000010,
        /// <summary>
        /// The function fills in the structure instead of treating it as a tracking request. The structure is filled such that had that structure been passed to TrackMouseEvent, it would generate the current tracking. The only anomaly is that the hover time-out returned is always the actual time-out and not HOVER_DEFAULT, if HOVER_DEFAULT was specified during the original TrackMouseEvent request.
        /// </summary>
        TME_QUERY = 0x40000000,
    }

    [DllImport("user32.dll")]
    public static extern int TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);
    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT
    {
        public Int32 cbSize;    // using Int32 instead of UInt32 is safe here, and this avoids casting the result  of Marshal.SizeOf()
        [MarshalAs(UnmanagedType.U4)]
        public TMEFlags dwFlags;
        public IntPtr hWnd;
        public UInt32 dwHoverTime;

        public TRACKMOUSEEVENT(TMEFlags dwFlags, IntPtr hWnd, UInt32 dwHoverTime)
        {
            this.cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT));
            this.dwFlags = dwFlags;
            this.hWnd = hWnd;
            this.dwHoverTime = dwHoverTime;
        }
    }

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetCapture();

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT p);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>
    ///     Retrieves a handle to the foreground window (the window with which the user is currently working). The system
    ///     assigns a slightly higher priority to the thread that creates the foreground window than it does to other threads.
    ///     <para>See https://msdn.microsoft.com/en-us/library/windows/desktop/ms633505%28v=vs.85%29.aspx for more information.</para>
    /// </summary>
    /// <returns>
    ///     C++ ( Type: Type: HWND )<br /> The return value is a handle to the foreground window. The foreground window
    ///     can be NULL in certain circumstances, such as when a window is losing activation.
    /// </returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(
    uint dwExStyle,
     [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
     [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
    uint dwStyle,
    int X, int Y,
    int nWidth, int nHeight,
    IntPtr hWndParent, IntPtr hMenu,
    IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, GetWindowType uCmd);

    public const uint SWP_NOREDRAW = 0x0008;

    public const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public enum DisplayAffinity : uint
    {
        None = 0,
        Monitor = 1
    }

    [DllImport("user32.dll")]
    public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, DisplayAffinity affinity);

    /// <summary>
    /// <para>The DestroyWindow function destroys the specified window. The function sends WM_DESTROY and WM_NCDESTROY messages to the window to deactivate it and remove the keyboard focus from it. The function also destroys the window's menu, flushes the thread message queue, destroys timers, removes clipboard ownership, and breaks the clipboard viewer chain (if the window is at the top of the viewer chain).</para>
    /// <para>If the specified window is a parent or owner window, DestroyWindow automatically destroys the associated child or owned windows when it destroys the parent or owner window. The function first destroys child or owned windows, and then it destroys the parent or owner window.</para>
    /// <para>DestroyWindow also destroys modeless dialog boxes created by the CreateDialog function.</para>
    /// </summary>
    /// <param name="hwnd">Handle to the window to be destroyed.</param>
    /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call GetLastError.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hwnd);

    // This helper static method is required because the 32-bit version of user32.dll does not contain this API
    // (on any versions of Windows), so linking the method will fail at run-time. The bridge dispatches the request
    // to the correct function (GetWindowLong in 32-bit mode and GetWindowLongPtr in 64-bit mode)
    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("USER32.dll")]
    public static extern short GetKeyState(VirtualKey nVirtKey);

    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax, uint wRemoveMsg
    );
    public const uint PM_REMOVE = 0x0001;

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    public static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex);
        else
            return GetWindowLongPtr32(hWnd, nIndex);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    public static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr CallWindowProc(IntPtr previousWindowProc, IntPtr windowHandle, WindowMessage message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);
}
