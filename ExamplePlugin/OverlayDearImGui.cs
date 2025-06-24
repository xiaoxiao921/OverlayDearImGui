using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx;
using ImGuiNET;
using OverlayDearImGui.Windows;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using UnityEngine.XR;
using static OverlayDearImGui.Windows.User32;
using BlendState = SharpDX.Direct3D11.BlendState;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using SamplerStateDescription = SharpDX.Direct3D11.SamplerStateDescription;
using ShaderResourceViewDescription = SharpDX.Direct3D11.ShaderResourceViewDescription;
using ShaderResourceViewDimension = SharpDX.Direct3D.ShaderResourceViewDimension;

namespace OverlayDearImGui;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class OverlayDearImGui : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "iDeathHD";
    public const string PluginName = "OverlayDearImGui";
    public const string PluginVersion = "2.0.0";

    private static Thread _renderThread;

    public void Awake()
    {
        Log.Init(Logger);

        //new Overlay().Render("Risk of Rain 2", "UnityWndClass");

        //User32.MessageBox(IntPtr.Zero, "hey", "overlay", 0);

        _renderThread = new Thread(() =>
        {
            try
            {
                new Overlay().Render("Risk of Rain 2", "UnityWndClass");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
        _renderThread.Start();
    }
}

public class Overlay
{
    private SharpDX.Direct3D11.Device _device;
    private DeviceContext _deviceContext;
    private SwapChain _swapChain;
    private RenderTargetView _mainRenderTargetView;

    void CreateRenderTarget()
    {
        // Get back buffer texture from swap chain
        using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
        {
            // Create RenderTargetView from back buffer
            _mainRenderTargetView = new RenderTargetView(_device, backBuffer);
        }
    }

    public void CleanupRenderTarget()
    {
        if (_mainRenderTargetView != null)
        {
            _mainRenderTargetView.Dispose();
            _mainRenderTargetView = null;
        }
    }

    public void CleanupDeviceD3D()
    {
        CleanupRenderTarget();

        if (_swapChain != null)
        {
            _swapChain.Dispose();
            _swapChain = null;
        }

        if (_deviceContext != null)
        {
            _deviceContext.Dispose();
            _deviceContext = null;
        }

        if (_device != null)
        {
            _device.Dispose();
            _device = null;
        }
    }

    public bool CreateDeviceD3D(IntPtr hWnd)
    {
        var swapChainDesc = new SwapChainDescription()
        {
            BufferCount = 2,
            ModeDescription = new ModeDescription()
            {
                Width = 0,
                Height = 0,
                Format = Format.R8G8B8A8_UNorm,
                RefreshRate = new Rational(60, 1)
            },
            Usage = Usage.RenderTargetOutput,
            OutputHandle = hWnd,
            SampleDescription = new SampleDescription(1, 0),
            IsWindowed = true,
            SwapEffect = SwapEffect.Discard,
            Flags = SwapChainFlags.AllowModeSwitch
        };

        FeatureLevel[] featureLevels =
        [
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_0
        ];

        //var debug = _device.QueryInterface<DeviceDebug>();
        //var infoQueue = debug.QueryInterface<SharpDX.Direct3D11.InfoQueue>();
        //infoQueue.SetBreakOnSeverity(MessageSeverity.Corruption, true);
        //infoQueue.SetBreakOnSeverity(MessageSeverity.Error, true);

        try
        {
            SharpDX.Direct3D11.Device.CreateWithSwapChain(
                DriverType.Hardware,
                //DeviceCreationFlags.Debug,
                DeviceCreationFlags.None,
                featureLevels,
                swapChainDesc,
                out _device, out _swapChain
            );
        }
        catch (Exception e)
        {
            Log.Error(e);

            try
            {
                SharpDX.Direct3D11.Device.CreateWithSwapChain(
                    DriverType.Warp,
                    //DeviceCreationFlags.Debug,
                    DeviceCreationFlags.None,
                    featureLevels,
                    swapChainDesc,
                    out _device, out _swapChain
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }

        _deviceContext = _device.ImmediateContext;

        CreateRenderTarget();
        return true;
    }

    const int LWA_ALPHA = 0x2;
    const int SW_SHOWDEFAULT = 10;

    public static string AssetsFolderPath = "";

    public static bool IsOpen = true;

    public static RECT GameRect;

    public static IntPtr GameHwnd;

    public unsafe void Render(string windowName, string windowClass)
    {
        GameHwnd = User32.FindWindowW(windowClass, windowName);

        AssetsFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets");

        User32.GetWindowRect(GameHwnd, out GameRect);

        //uint dwError = Banding.BandingCheck();
        //if (dwError != 0)
        //Log.Error($"UIAccess error: 0x{dwError:X8}");
        //var (hwnd, wc) = WindowFactory.CreateWindowWithBand("Banding");

        //var (hwnd, wc) = WindowFactory.CreateClassicOverlayWindow("OverlayDearImGui");
        var (hwnd, wc) = WindowFactory.CreateClassicWindow("OverlayDearImGui");

        //User32.SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        Dwmapi.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (!CreateDeviceD3D(hwnd))
        {
            CleanupDeviceD3D();
            User32.UnregisterClass(wc.lpszClassName, wc.hInstance);
            Log.Error("Failed CreateDeviceD3D");
            return;
        }

        User32.ShowWindow(hwnd, SW_SHOWDEFAULT);
        User32.UpdateWindow(hwnd);

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad;

        ImGui.StyleColorsDark();
        //ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] = new(0f, 0f, 0f, 1f);

        ImGuiWin32Impl.Init(hwnd);

        ImGuiDX11Impl.Init((void*)_device.NativePointer, (void*)_deviceContext.NativePointer);

        const int VK_INSERT = 0x2D;
        bool insertKeyPreviouslyDown = false;

        var clearColor = new RawVector4(0f, 0, 0, 0f);
        bool done = false;
        while (!done)
        {
            User32.MSG msg;
            while (User32.PeekMessage(out msg, hwnd, 0U, 0U, User32.PM_REMOVE))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);

                if (msg.message == WindowMessage.WM_QUIT)
                {
                    done = true;
                    break;
                }
            }

            bool insertKeyDown = (User32.GetAsyncKeyState(VK_INSERT) & 0x8000) != 0;

            if (insertKeyDown && !insertKeyPreviouslyDown)
            {
                Overlay.IsOpen = !Overlay.IsOpen;

                if (Overlay.IsOpen)
                {
                    User32.ShowWindow(hwnd, (int)User32.ShowWindowCommand.Restore);
                    User32.ShowWindow(hwnd, (int)User32.ShowWindowCommand.Show);
                    User32.SetForegroundWindow(hwnd);
                }
                else
                {
                    User32.ShowWindow(hwnd, (int)User32.ShowWindowCommand.Hide);
                    User32.ShowWindow(hwnd, (int)User32.ShowWindowCommand.Minimize);

                    User32.BringWindowToTop(GameHwnd);
                    User32.SetForegroundWindow(GameHwnd);
                }
            }

            insertKeyPreviouslyDown = insertKeyDown;

            if (g_ResizeWidth != 0 && g_ResizeHeight != 0)
            {
                CleanupRenderTarget();
                _swapChain.ResizeBuffers(0, (int)g_ResizeWidth, (int)g_ResizeHeight, Format.Unknown, 0);
                g_ResizeWidth = g_ResizeHeight = 0;
                CreateRenderTarget();
            }

            var FHwnd = User32.GetForegroundWindow();
            if (!(FHwnd == hwnd || FHwnd == GameHwnd))
            {
                //just to stop crazy gpu usage
                Kernel32.Sleep(16);
            }

            //bool visible = (User32.GetForegroundWindow() == hwnd || User32.GetForegroundWindow() == gameHwnd)
            //&& User32.IsWindowVisible(gameHwnd);

            bool visible = User32.IsWindowVisible(GameHwnd);

            User32.ShowWindow(hwnd, (int)(visible ? User32.ShowWindowCommand.Show : User32.ShowWindowCommand.Hide));

            if (!visible)
                Kernel32.Sleep(16);

            User32.GetWindowRect(GameHwnd, out var TmpRect);

            if (TmpRect.Left != GameRect.Left || TmpRect.Bottom != GameRect.Bottom ||
                TmpRect.Top != GameRect.Top || TmpRect.Right != GameRect.Right
                )
            {
                GameRect = TmpRect;

                User32.SetWindowPos(hwnd, (IntPtr)(-2) /* HWND_NOTOPMOST */, GameRect.X, GameRect.Y, GameRect.Width, GameRect.Height, User32.SWP_NOREDRAW);

                // set z-order aka place the overlay right above the game window -- phillip
                //var hwndAbove = User32.GetWindow(gameHwnd, User32.GetWindowType.GW_HWNDPREV);
                //User32.SetWindowPos(hwnd, hwndAbove, TmpPoint.X, TmpPoint.Y, GameRect.Right - GameRect.Left, GameRect.Bottom - GameRect.Top, User32.SWP_NOREDRAW);
                //var extendedStyle = ShowOverlay ?
                //User32.WindowStylesEx.WS_EX_LAYERED | User32.WindowStylesEx.WS_EX_TOOLWINDOW | User32.WindowStylesEx.WS_EX_NOACTIVATE |
                //((FHwnd == gameHwnd) ? User32.WindowStylesEx.WS_EX_TOPMOST : 0) :
                //User32.WindowStylesEx.WS_EX_TRANSPARENT | User32.WindowStylesEx.WS_EX_LAYERED | User32.WindowStylesEx.WS_EX_TOOLWINDOW;
                //User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, new IntPtr((uint)extendedStyle));
            }

            ImGuiDX11Impl.NewFrame();
            ImGuiWin32Impl.NewFrame();
            ImGui.NewFrame();

            User32.SetWindowDisplayAffinity(hwnd, User32.DisplayAffinity.None);

            if (IsOpen)
            {
                ImGui.ShowDemoWindow();
            }

            ImGui.Render();

            //var clearColorWithAlpha = new RawColor4(clearColor.X * clearColor.W, clearColor.Y * clearColor.W, clearColor.Z * clearColor.W, clearColor.W);
            _deviceContext.OutputMerger.SetRenderTargets(_mainRenderTargetView);
            //_deviceContext.ClearRenderTargetView(_mainRenderTargetView, clearColorWithAlpha);
            _deviceContext.ClearRenderTargetView(_mainRenderTargetView, new(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));
            ImGuiDX11Impl.RenderDrawData(ImGui.GetDrawData());

            _swapChain.Present(1, PresentFlags.None);
        }

        ImGuiDX11Impl.Shutdown();
        ImGuiWin32Impl.Shutdown();
        ImGui.DestroyContext();
        CleanupDeviceD3D();
        User32.DestroyWindow(hwnd);
        User32.UnregisterClass(wc.lpszClassName, wc.hInstance);
    }

    public static uint g_ResizeWidth;
    public static uint g_ResizeHeight;

    internal const UInt32 SC_KEYMENU = 0xF100;
    internal const UInt32 SIZE_MINIMIZED = 1;

    public static IntPtr WndProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        if (ImGuiWin32Impl.WndProcHandler(hWnd, msg, wParam, lParam) != IntPtr.Zero)
        {
            return new IntPtr(1);
        }

        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct LUID
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
public struct LUID_AND_ATTRIBUTES
{
    public LUID Luid;
    public uint Attributes;
}

[StructLayout(LayoutKind.Sequential)]
public struct PRIVILEGE_SET
{
    public uint PrivilegeCount;
    public uint Control;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
    public LUID_AND_ATTRIBUTES[] Privilege;
}

[StructLayout(LayoutKind.Sequential)]
public struct PROCESSENTRY32
{
    public uint dwSize;
    public uint cntUsage;
    public uint th32ProcessID;
    public IntPtr th32DefaultHeapID;
    public uint th32ModuleID;
    public uint cntThreads;
    public uint th32ParentProcessID;
    public int pcPriClassBase;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szExeFile;
}

[StructLayout(LayoutKind.Sequential)]
public struct STARTUPINFO
{
    public uint cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_INFORMATION
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
}

public enum TOKEN_TYPE
{
    TokenPrimary = 1,
    TokenImpersonation
}

public enum SECURITY_IMPERSONATION_LEVEL
{
    SecurityAnonymous,
    SecurityIdentification,
    SecurityImpersonation,
    SecurityDelegation
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WNDCLASSEXW
{
    public uint cbSize;
    public uint style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string lpszMenuName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string lpszClassName;
    public IntPtr hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
public struct MARGINS
{
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
}

public class Banding
{
    const int PRIVILEGE_SET_ALL_NECESSARY = 1;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool LookupPrivilegeValue(string lpSystemName, string lpName,
    out LUID lpLuid);

    public static uint CreateWinLogon(uint session, uint access, out IntPtr token)
    {
        token = IntPtr.Zero;

        var ps = new PRIVILEGE_SET
        {
            PrivilegeCount = 1,
            Control = PRIVILEGE_SET_ALL_NECESSARY,
            Privilege = new LUID_AND_ATTRIBUTES[1]
        };

        if (!LookupPrivilegeValue(null, "SeTcbPrivilege", out ps.Privilege[0].Luid))
            return (uint)Marshal.GetLastWin32Error();

        IntPtr snapshot = Kernel32.CreateToolhelp32Snapshot(Kernel32.SnapshotFlags.Process, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return (uint)Marshal.GetLastWin32Error();

        PROCESSENTRY32 pe32 = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
        uint status = 0x00000490; // ERROR_NOT_FOUND

        for (bool cont = Kernel32.Process32First(snapshot, ref pe32); cont; cont = Kernel32.Process32Next(snapshot, ref pe32))
        {
            if (!pe32.szExeFile.Equals("winlogon.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            IntPtr hProc = Kernel32.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pe32.th32ProcessID);
            if (hProc == IntPtr.Zero)
                continue;

            if (Kernel32.OpenProcessToken(hProc, Kernel32.TOKEN_QUERY | Kernel32.TOKEN_DUPLICATE, out IntPtr hToken))
            {
                bool hasTcb = false;
                if (Kernel32.PrivilegeCheck(hToken, ref ps, out hasTcb) && hasTcb)
                {
                    if (Kernel32.GetTokenInformation(hToken, 12 /* TokenSessionId */, out uint sid, sizeof(uint), out uint _)
                        && sid == session
                        && Kernel32.DuplicateTokenEx(hToken, access, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenImpersonation, out token))
                    {
                        status = 0; // ERROR_SUCCESS
                        Kernel32.CloseHandle(hToken);
                        Kernel32.CloseHandle(hProc);
                        break;
                    }
                }
                Kernel32.CloseHandle(hToken);
            }
            Kernel32.CloseHandle(hProc);
        }

        Kernel32.CloseHandle(snapshot);
        return status;
    }


    public static uint CreateToken(out IntPtr token)
    {
        token = IntPtr.Zero;

        if (!Kernel32.OpenProcessToken(Kernel32.GetCurrentProcess(), Kernel32.TOKEN_QUERY | Kernel32.TOKEN_DUPLICATE, out IntPtr tokenSelf))
            return (uint)Marshal.GetLastWin32Error();

        if (!Kernel32.GetTokenInformation(tokenSelf, 12 /* TokenSessionId */, out uint session, sizeof(uint), out uint _))
        {
            Kernel32.CloseHandle(tokenSelf);
            return (uint)Marshal.GetLastWin32Error();
        }

        uint status = CreateWinLogon(session, Kernel32.TOKEN_IMPERSONATE, out IntPtr winlogonToken);
        if (status != 0)
        {
            Kernel32.CloseHandle(tokenSelf);
            return status;
        }

        if (Kernel32.SetThreadToken(IntPtr.Zero, winlogonToken) &&
            Kernel32.DuplicateTokenEx(tokenSelf, Kernel32.TOKEN_QUERY | Kernel32.TOKEN_DUPLICATE | Kernel32.TOKEN_ASSIGN_PRIMARY | Kernel32.TOKEN_ADJUST_DEFAULT, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityAnonymous, TOKEN_TYPE.TokenPrimary, out token))
        {
            int uiAccess = 1;
            if (!Kernel32.SetTokenInformation(token, 26 /* TokenUIAccess */, ref uiAccess, sizeof(int)))
            {
                status = (uint)Marshal.GetLastWin32Error();
                Kernel32.CloseHandle(token);
                token = IntPtr.Zero;
            }
        }
        else
        {
            status = (uint)Marshal.GetLastWin32Error();
        }

        Kernel32.RevertToSelf();
        Kernel32.CloseHandle(tokenSelf);
        Kernel32.CloseHandle(winlogonToken);
        return status;
    }


    public static uint BandingCheck()
    {
        if (Kernel32.OpenProcessToken(Kernel32.GetCurrentProcess(), Kernel32.TOKEN_QUERY, out IntPtr hToken))
        {
            if (Kernel32.GetTokenInformation(hToken, 26 /* TokenUIAccess */, out int uiAccess, sizeof(int), out uint _) && uiAccess != 0)
            {
                Kernel32.CloseHandle(hToken);
                return 0; // ERROR_SUCCESS
            }
            Kernel32.CloseHandle(hToken);
        }

        uint status = CreateToken(out IntPtr token);
        if (status != 0)
        {
            Log.Error("CreateToken failed");
            return status;
        }

        STARTUPINFO si = new STARTUPINFO { cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO)) };
        PROCESS_INFORMATION pi;

        IntPtr cmdLine = Marshal.StringToHGlobalUni(Environment.CommandLine);
        bool result = Kernel32.CreateProcessAsUser(token, null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi);
        Marshal.FreeHGlobal(cmdLine);

        if (result)
        {
            Kernel32.CloseHandle(pi.hProcess);
            Kernel32.CloseHandle(pi.hThread);
            Kernel32.CloseHandle(token);
            Kernel32.ExitProcess(0);
        }

        Kernel32.CloseHandle(token);
        Log.Error("Reached here");
        return (uint)Marshal.GetLastWin32Error();
    }
}

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

        ushort atom = User32.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int error = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"RegisterClassExW failed: 0x{error:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        IntPtr hwnd = User32.CreateWindowW(
            name,
            "Overlay",
            WS_POPUP,
            Overlay.GameRect.X, Overlay.GameRect.Y,
            Overlay.GameRect.Width, Overlay.GameRect.Height,
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero
        );


        if (hwnd == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"CreateWindowExW failed: 0x{error:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        return (hwnd, wc);
    }

    public static (IntPtr hwnd, WNDCLASSEXW wc) CreateClassicOverlayWindow(string name)
    {
        WNDCLASSEXW wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0x0001, // CS_CLASSDC
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Overlay.WndProc),
            hInstance = Kernel32.GetModuleHandle(null),
            lpszClassName = name
        };

        ushort atom = User32.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"RegisterClassExW error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        IntPtr hwnd = User32.CreateWindowExW(
            0x00000008, // WS_EX_TOPMOST only
            name,
            name,
            0x80000000 | 0x10000000, // WS_POPUP | WS_VISIBLE
            100, 100, 800, 600, // give it a real size!
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"CreateWindowExW error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        return (hwnd, wc);
    }

    private delegate IntPtr CreateWindowInBandDelegate(
        uint dwExStyle, ushort atom, string lpWindowName, uint dwStyle,
        int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam,
        uint band);

    public static (IntPtr hwnd, WNDCLASSEXW wc) CreateWindowWithBand(string name)
    {
        WNDCLASSEXW wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0x0001 /* CS_CLASSDC */,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Overlay.WndProc),
            hInstance = Kernel32.GetModuleHandle(null),
            lpszClassName = name
        };

        ushort atom = User32.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"RegisterClassExW error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        IntPtr hLib = Kernel32.LoadLibrary("user32.dll");
        IntPtr pCreateWindowInBand = Kernel32.GetProcAddress(hLib, "CreateWindowInBand");
        if (pCreateWindowInBand == IntPtr.Zero)
        {
#if DEBUG
            Log.Error("Failed to get CreateWindowInBand");
#endif
            return (IntPtr.Zero, default);
        }

        var createWindow = Marshal.GetDelegateForFunctionPointer<CreateWindowInBandDelegate>(pCreateWindowInBand);

        IntPtr hwnd = createWindow(
            0x00000020 | 0x00000080, // WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
            atom, name, 0x80000000, // WS_POPUP
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, (IntPtr)atom, 2); // ZBID_UIACCESS

        if (hwnd == IntPtr.Zero)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"CreateWindowInBand error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        return (hwnd, wc);
    }

    public static (IntPtr hwnd, WNDCLASSEXW wc) CreateWindowExW(string name)
    {
        WNDCLASSEXW wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0x0001 /* CS_CLASSDC */,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Overlay.WndProc),
            hInstance = Kernel32.GetModuleHandle(null),
            lpszClassName = name
        };

        if (User32.RegisterClassExW(ref wc) == 0)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"RegisterClassExW error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }

        IntPtr hwnd = User32.CreateWindowExW(
            0x00000020 | 0x00000080, // WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
            name, "BeanerBot", 0x80000000, // WS_POPUP
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            int dwError = Marshal.GetLastWin32Error();
#if DEBUG
            Log.Error($"CreateWindowExW error: 0x{dwError:X8}");
#endif
            return (IntPtr.Zero, default);
        }
#if DEBUG
        Log.Error($"Window created hwnd:{hwnd}");
#endif
        return (hwnd, wc);
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}


public static class ImGuiDX11Impl
{
    private static IntPtr _renderNamePtr;
    private static Device _device;
    private static DeviceContext _deviceContext;
    private static ShaderResourceView _fontResourceView;
    private static SamplerState _fontSampler;
    private static VertexShader _vertexShader;
    private static PixelShader _pixelShader;
    private static InputLayout _inputLayout;
    private static Buffer _vertexConstantBuffer;
    private static BlendState _blendState;
    private static RasterizerState _rasterizerState;
    private static DepthStencilState _depthStencilState;
    private static Buffer _vertexBuffer;
    private static Buffer _indexBuffer;
    private static int _vertexBufferSize;
    private static int _indexBufferSize;
    private static VertexBufferBinding _vertexBinding;
    // so we don't make a temporary object every frame
    private static RawColor4 _blendColor = new RawColor4(0, 0, 0, 0);

    public unsafe struct VERTEX_CONSTANT_BUFFER_DX11
    {
        public fixed float mvp[4 * 4];
    }

    // Functions
    static unsafe void ImGui_ImplDX11_SetupRenderState(ImDrawData* draw_data, IntPtr ID3D11DeviceContextPtr)
    {
        var deviceContext = new DeviceContext(ID3D11DeviceContextPtr);

        // Setup viewport
        deviceContext.Rasterizer.SetViewport(0, 0, draw_data->DisplaySize.x, draw_data->DisplaySize.y);


        // Setup shader and vertex buffers
        deviceContext.InputAssembler.InputLayout = _inputLayout;

        deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding()
        {
            Stride = sizeof(ImDrawVert),
            Offset = 0,
            Buffer = _vertexBuffer
        });
        deviceContext.InputAssembler.SetIndexBuffer(_indexBuffer, SharpDX.DXGI.Format.R16_UInt, 0);
        deviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
        deviceContext.VertexShader.SetShader(_vertexShader, null, 0);
        deviceContext.VertexShader.SetConstantBuffer(0, _vertexConstantBuffer);
        deviceContext.PixelShader.SetShader(_pixelShader, null, 0);
        deviceContext.PixelShader.SetSampler(0, _fontSampler);
        deviceContext.GeometryShader.SetShader(null, null, 0);
        deviceContext.HullShader.SetShader(null, null, 0);
        deviceContext.DomainShader.SetShader(null, null, 0);
        deviceContext.ComputeShader.SetShader(null, null, 0);

        // Setup blend state
        RawColor4 blendFactor = new(0.0f, 0.0f, 0.0f, 0.0f);
        deviceContext.OutputMerger.SetBlendState(_blendState, blendFactor, 0xffffffff);
        deviceContext.OutputMerger.SetDepthStencilState(_depthStencilState, 0);
        deviceContext.Rasterizer.State = _rasterizerState;
    }

    private unsafe delegate void ImDrawUserCallBack(ImDrawList* a, ImDrawCmd* b);

    // Render function
    public static unsafe void RenderDrawData(ImDrawData* draw_data)
    {
        // Avoid rendering when minimized
        if (draw_data->DisplaySize.x <= 0.0f || draw_data->DisplaySize.y <= 0.0f)
            return;

        DeviceContext ctx = _deviceContext;

        // Create and grow vertex/index buffers if needed
        if (_vertexBuffer == null || _vertexBufferSize < draw_data->TotalVtxCount)
        {
            _vertexBuffer?.Dispose();

            _vertexBufferSize = draw_data->TotalVtxCount + 5000;

            _vertexBuffer = new Buffer(_device, new BufferDescription
            {
                Usage = ResourceUsage.Dynamic,
                SizeInBytes = _vertexBufferSize * Unsafe.SizeOf<ImDrawVert>(),
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None
            });

            // (Re)make this here rather than every frame
            _vertexBinding = new VertexBufferBinding
            {
                Buffer = _vertexBuffer,
                Stride = Unsafe.SizeOf<ImDrawVert>(),
                Offset = 0
            };
        }

        if (_indexBuffer == null || _indexBufferSize < draw_data->TotalIdxCount)
        {
            _indexBuffer?.Dispose();

            _indexBufferSize = draw_data->TotalIdxCount + 10000;

            _indexBuffer = new Buffer(_device, new BufferDescription
            {
                Usage = ResourceUsage.Dynamic,
                SizeInBytes = _indexBufferSize * sizeof(ushort),
                BindFlags = BindFlags.IndexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
            });
        }

        // Upload vertex/index data into a single contiguous GPU buffer
        ctx.MapSubresource(_vertexBuffer, MapMode.WriteDiscard, MapFlags.None, out var vtx_resource);
        ctx.MapSubresource(_indexBuffer, MapMode.WriteDiscard, MapFlags.None, out var idx_resource);

        ImDrawVert* vtx_dst = (ImDrawVert*)vtx_resource.DataPointer;
        ushort* idx_dst = (ushort*)idx_resource.DataPointer;
        for (int n = 0; n < draw_data->CmdListsCount; n++)
        {
            var cmd_list = (ImDrawList*)draw_data->CmdLists.Ref<IntPtr>(n);

            var len = cmd_list->VtxBuffer.Size * sizeof(ImDrawVert);
            System.Buffer.MemoryCopy((void*)cmd_list->VtxBuffer.Data, vtx_dst, len, len);

            len = cmd_list->IdxBuffer.Size * sizeof(ushort);
            System.Buffer.MemoryCopy((void*)cmd_list->IdxBuffer.Data, idx_dst, len, len);

            vtx_dst += cmd_list->VtxBuffer.Size;
            idx_dst += cmd_list->IdxBuffer.Size;
        }

        ctx.UnmapSubresource(_vertexBuffer, 0);
        ctx.UnmapSubresource(_indexBuffer, 0);

        // Setup orthographic projection matrix into our constant buffer
        // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        ctx.MapSubresource(_vertexConstantBuffer, MapMode.WriteDiscard, MapFlags.None, out var mapped_resource);

        VERTEX_CONSTANT_BUFFER_DX11* constant_buffer = (VERTEX_CONSTANT_BUFFER_DX11*)mapped_resource.DataPointer;
        float L = draw_data->DisplayPos.x;
        float R = draw_data->DisplayPos.x + draw_data->DisplaySize.x;
        float T = draw_data->DisplayPos.y;
        float B = draw_data->DisplayPos.y + draw_data->DisplaySize.y;

        constant_buffer->mvp[0] = 2.0f / (R - L);
        constant_buffer->mvp[1] = 0.0f;
        constant_buffer->mvp[2] = 0.0f;
        constant_buffer->mvp[3] = 0.0f;

        constant_buffer->mvp[4] = 0.0f;
        constant_buffer->mvp[5] = 2.0f / (T - B);
        constant_buffer->mvp[6] = 0.0f;
        constant_buffer->mvp[7] = 0.0f;

        constant_buffer->mvp[8] = 0.0f;
        constant_buffer->mvp[9] = 0.0f;
        constant_buffer->mvp[10] = 0.5f;
        constant_buffer->mvp[11] = 0.0f;

        constant_buffer->mvp[12] = (R + L) / (L - R);
        constant_buffer->mvp[13] = (T + B) / (B - T);
        constant_buffer->mvp[14] = 0.5f;
        constant_buffer->mvp[15] = 1.0f;

        ctx.UnmapSubresource(_vertexConstantBuffer, 0);

        var old = BackupRenderState(ctx);

        ImGui_ImplDX11_SetupRenderState(draw_data, ctx.NativePointer);

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        int global_idx_offset = 0;
        int global_vtx_offset = 0;
        var clip_off = draw_data->DisplayPos;
        for (int n = 0; n < draw_data->CmdListsCount; n++)
        {
            var cmd_list = (ImDrawList*)draw_data->CmdLists.Ref<IntPtr>(n);
            for (int cmd_i = 0; cmd_i < cmd_list->CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmd_list->CmdBuffer.Ref<ImDrawCmd>(cmd_i);
                if (pcmd.UserCallback != IntPtr.Zero)
                {
                    var userCallback = Marshal.GetDelegateForFunctionPointer<ImDrawUserCallBack>(pcmd.UserCallback);

                    // User callback, registered via ImDrawList::AddCallback()
                    // (ImDrawCallback_ResetRenderState is a special callback value used by the user to request the renderer to reset render state.)
                    if (pcmd.UserCallback == new IntPtr(-1))
                        ImGui_ImplDX11_SetupRenderState(draw_data, ctx.NativePointer);
                    else
                        userCallback(cmd_list, &pcmd);
                }
                else
                {
                    // Project scissor/clipping rectangles into framebuffer space
                    var clip_min = new System.Numerics.Vector2(pcmd.ClipRect.x - clip_off.x, pcmd.ClipRect.y - clip_off.y);
                    var clip_max = new System.Numerics.Vector2(pcmd.ClipRect.z - clip_off.x, pcmd.ClipRect.w - clip_off.y);
                    if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
                        continue;

                    // Apply scissor/clipping rectangle
                    RawRectangle r = new((int)clip_min.X, (int)clip_min.Y, (int)clip_max.X, (int)clip_max.Y);
                    ctx.Rasterizer.SetScissorRectangles(r);

                    ctx.PixelShader.SetShaderResource(0, new(pcmd.TextureId));
                    ctx.DrawIndexed((int)pcmd.ElemCount, (int)(pcmd.IdxOffset + global_idx_offset), (int)(pcmd.VtxOffset + global_vtx_offset));
                }
            }
            global_idx_offset += cmd_list->IdxBuffer.Size;
            global_vtx_offset += cmd_list->VtxBuffer.Size;
        }

        RestoreRenderState(ctx, old);
    }

    private static StateBackup BackupRenderState(DeviceContext ctx)
    {
        const int D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE = 16;

        var backup = new StateBackup
        {
            ScissorRects = new Rectangle[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE],
            Viewports = new RawViewportF[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE],
            VertexBuffers = new Buffer[InputAssemblerStage.VertexInputResourceSlotCount],
            VertexBufferStrides = new int[InputAssemblerStage.VertexInputResourceSlotCount],
            VertexBufferOffsets = new int[InputAssemblerStage.VertexInputResourceSlotCount],

            // IA
            InputLayout = ctx.InputAssembler.InputLayout
        };
        ctx.InputAssembler.GetIndexBuffer(out backup.IndexBuffer, out backup.IndexBufferFormat, out backup.IndexBufferOffset);
        backup.PrimitiveTopology = ctx.InputAssembler.PrimitiveTopology;
        ctx.InputAssembler.GetVertexBuffers(0, InputAssemblerStage.VertexInputResourceSlotCount, backup.VertexBuffers, backup.VertexBufferStrides, backup.VertexBufferOffsets);

        // RS
        backup.RS = ctx.Rasterizer.State;
        ctx.Rasterizer.GetScissorRectangles<Rectangle>(backup.ScissorRects);
        ctx.Rasterizer.GetViewports<RawViewportF>(backup.Viewports);

        // OM
        backup.BlendState = ctx.OutputMerger.GetBlendState(out backup.BlendFactor, out backup.SampleMask);
        backup.DepthStencilState = ctx.OutputMerger.GetDepthStencilState(out backup.DepthStencilRef);
        backup.RenderTargetViews = ctx.OutputMerger.GetRenderTargets(OutputMergerStage.SimultaneousRenderTargetCount, out backup.DepthStencilView);

        // VS
        backup.VS = ctx.VertexShader.Get();
        backup.VSSamplers = ctx.VertexShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.VSConstantBuffers = ctx.VertexShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.VSResourceViews = ctx.VertexShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

        // HS
        backup.HS = ctx.HullShader.Get();
        backup.HSSamplers = ctx.HullShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.HSConstantBuffers = ctx.HullShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.HSResourceViews = ctx.HullShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

        // DS
        backup.DS = ctx.DomainShader.Get();
        backup.DSSamplers = ctx.DomainShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.DSConstantBuffers = ctx.DomainShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.DSResourceViews = ctx.DomainShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

        // GS
        backup.GS = ctx.GeometryShader.Get();
        backup.GSSamplers = ctx.GeometryShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.GSConstantBuffers = ctx.GeometryShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.GSResourceViews = ctx.GeometryShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

        // PS
        backup.PS = ctx.PixelShader.Get();
        backup.PSSamplers = ctx.PixelShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.PSConstantBuffers = ctx.PixelShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.PSResourceViews = ctx.PixelShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);

        // CS
        backup.CS = ctx.ComputeShader.Get();
        backup.CSSamplers = ctx.ComputeShader.GetSamplers(0, CommonShaderStage.SamplerSlotCount);
        backup.CSConstantBuffers = ctx.ComputeShader.GetConstantBuffers(0, CommonShaderStage.ConstantBufferApiSlotCount);
        backup.CSResourceViews = ctx.ComputeShader.GetShaderResources(0, CommonShaderStage.InputResourceSlotCount);
        backup.CSUAVs = ctx.ComputeShader.GetUnorderedAccessViews(0, ComputeShaderStage.UnorderedAccessViewSlotCount);   // should be register count and not slot, but the value is correct

        return backup;
    }

    private static void RestoreRenderState(DeviceContext ctx, StateBackup old)
    {
        // IA
        ctx.InputAssembler.InputLayout = old.InputLayout;
        ctx.InputAssembler.SetIndexBuffer(old.IndexBuffer, old.IndexBufferFormat, old.IndexBufferOffset);
        ctx.InputAssembler.PrimitiveTopology = old.PrimitiveTopology;
        ctx.InputAssembler.SetVertexBuffers(0, old.VertexBuffers, old.VertexBufferStrides, old.VertexBufferOffsets);

        // RS
        ctx.Rasterizer.State = old.RS;
        ctx.Rasterizer.SetScissorRectangles(old.ScissorRects);
        ctx.Rasterizer.SetViewports(old.Viewports, old.Viewports.Length);

        // OM
        ctx.OutputMerger.SetBlendState(old.BlendState, old.BlendFactor, old.SampleMask);
        ctx.OutputMerger.SetDepthStencilState(old.DepthStencilState, old.DepthStencilRef);
        ctx.OutputMerger.SetRenderTargets(old.DepthStencilView, old.RenderTargetViews);

        // VS
        ctx.VertexShader.Set(old.VS);
        ctx.VertexShader.SetSamplers(0, old.VSSamplers);
        ctx.VertexShader.SetConstantBuffers(0, old.VSConstantBuffers);
        ctx.VertexShader.SetShaderResources(0, old.VSResourceViews);

        // HS
        ctx.HullShader.Set(old.HS);
        ctx.HullShader.SetSamplers(0, old.HSSamplers);
        ctx.HullShader.SetConstantBuffers(0, old.HSConstantBuffers);
        ctx.HullShader.SetShaderResources(0, old.HSResourceViews);

        // DS
        ctx.DomainShader.Set(old.DS);
        ctx.DomainShader.SetSamplers(0, old.DSSamplers);
        ctx.DomainShader.SetConstantBuffers(0, old.DSConstantBuffers);
        ctx.DomainShader.SetShaderResources(0, old.DSResourceViews);

        // GS
        ctx.GeometryShader.Set(old.GS);
        ctx.GeometryShader.SetSamplers(0, old.GSSamplers);
        ctx.GeometryShader.SetConstantBuffers(0, old.GSConstantBuffers);
        ctx.GeometryShader.SetShaderResources(0, old.GSResourceViews);

        // PS
        ctx.PixelShader.Set(old.PS);
        ctx.PixelShader.SetSamplers(0, old.PSSamplers);
        ctx.PixelShader.SetConstantBuffers(0, old.PSConstantBuffers);
        ctx.PixelShader.SetShaderResources(0, old.PSResourceViews);

        // CS
        ctx.ComputeShader.Set(old.CS);
        ctx.ComputeShader.SetSamplers(0, old.CSSamplers);
        ctx.ComputeShader.SetConstantBuffers(0, old.CSConstantBuffers);
        ctx.ComputeShader.SetShaderResources(0, old.CSResourceViews);
        ctx.ComputeShader.SetUnorderedAccessViews(0, old.CSUAVs);
    }

    private class StateBackup
    {
        // IA
        public InputLayout InputLayout;
        public PrimitiveTopology PrimitiveTopology;
        public Buffer IndexBuffer;
        public SharpDX.DXGI.Format IndexBufferFormat;
        public int IndexBufferOffset;
        public Buffer[] VertexBuffers;
        public int[] VertexBufferStrides;
        public int[] VertexBufferOffsets;

        // RS
        public RasterizerState RS;
        public Rectangle[] ScissorRects;
        public RawViewportF[] Viewports;

        // OM
        public BlendState BlendState;
        public RawColor4 BlendFactor;
        public int SampleMask;
        public DepthStencilState DepthStencilState;
        public int DepthStencilRef;
        public DepthStencilView DepthStencilView;
        public RenderTargetView[] RenderTargetViews;

        // VS
        public VertexShader VS;
        public Buffer[] VSConstantBuffers;
        public SamplerState[] VSSamplers;
        public ShaderResourceView[] VSResourceViews;

        // HS
        public HullShader HS;
        public Buffer[] HSConstantBuffers;
        public SamplerState[] HSSamplers;
        public ShaderResourceView[] HSResourceViews;

        // DS
        public DomainShader DS;
        public Buffer[] DSConstantBuffers;
        public SamplerState[] DSSamplers;
        public ShaderResourceView[] DSResourceViews;

        // GS
        public GeometryShader GS;
        public Buffer[] GSConstantBuffers;
        public SamplerState[] GSSamplers;
        public ShaderResourceView[] GSResourceViews;

        // PS
        public PixelShader PS;
        public Buffer[] PSConstantBuffers;
        public SamplerState[] PSSamplers;
        public ShaderResourceView[] PSResourceViews;

        public SharpDX.Direct3D11.ComputeShader CS;
        public Buffer[] CSConstantBuffers;
        public SamplerState[] CSSamplers;
        public ShaderResourceView[] CSResourceViews;
        public UnorderedAccessView[] CSUAVs;
    }

    public static unsafe void CreateFontsTexture()
    {
        var io = ImGui.GetIO();

        // Build texture atlas
        io.Fonts.GetTexDataAsRGBA32(out IntPtr fontPixels, out int fontWidth, out int fontHeight, out int fontBytesPerPixel);

        // Upload texture to graphics system
        var texDesc = new Texture2DDescription
        {
            Width = fontWidth,
            Height = fontHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        };

        using (var fontTexture = new SharpDX.Direct3D11.Texture2D(_device, texDesc, new DataRectangle(fontPixels, fontWidth * fontBytesPerPixel)))
        {
            // Create texture view
            _fontResourceView = new ShaderResourceView(_device, fontTexture, new ShaderResourceViewDescription
            {
                Format = texDesc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = { MipLevels = texDesc.MipLevels }
            });
        }

        // Store our identifier
        io.Fonts.SetTexID(_fontResourceView.NativePointer);

        // Create texture sampler
        _fontSampler = new SamplerState(_device, new SamplerStateDescription
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MipLodBias = 0,
            ComparisonFunction = Comparison.Always,
            MinimumLod = 0,
            MaximumLod = 0
        });
    }

    public static unsafe bool CreateDeviceObjects()
    {
        if (_device == null)
        {
            return false;
        }

        if (_fontSampler != null)
        {
            InvalidateDeviceObjects();
        }

        var shadersFolder = Path.Combine(Overlay.AssetsFolderPath, "Shaders");

        var vertexShaderPath = Path.Combine(shadersFolder, "imgui-vertex.hlsl.bytes");
        byte[] vertexShaderBytes = File.ReadAllBytes(vertexShaderPath);
        _vertexShader = new VertexShader(_device, vertexShaderBytes);

        // Create the input layout
        _inputLayout = new InputLayout(_device, vertexShaderBytes, new[]
        {
            new InputElement("POSITION", 0, Format.R32G32_Float, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
        });

        // Create the constant buffer
        _vertexConstantBuffer = new Buffer(_device, new BufferDescription
        {
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ConstantBuffer,
            CpuAccessFlags = CpuAccessFlags.Write,
            OptionFlags = ResourceOptionFlags.None,
            SizeInBytes = 16 * sizeof(float)
        });

        var pixelShaderPath = Path.Combine(shadersFolder, "imgui-frag.hlsl.bytes");
        byte[] pixelShaderBytes = File.ReadAllBytes(pixelShaderPath);
        _pixelShader = new PixelShader(_device, pixelShaderBytes);

        // Create the blending setup
        // ...of course this was setup in a way that can't be done inline
        var blendStateDesc = new BlendStateDescription
        {
            AlphaToCoverageEnable = false
        };
        blendStateDesc.RenderTarget[0].IsBlendEnabled = true;
        blendStateDesc.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
        blendStateDesc.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
        blendStateDesc.RenderTarget[0].BlendOperation = BlendOperation.Add;
        // https://github.com/ocornut/imgui/issues/892
        //blendStateDesc.RenderTarget[0].SourceAlphaBlend = BlendOption.InverseSourceAlpha;
        blendStateDesc.RenderTarget[0].SourceAlphaBlend = BlendOption.InverseDestinationAlpha;
        // https://github.com/ocornut/imgui/issues/892
        //blendStateDesc.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
        blendStateDesc.RenderTarget[0].DestinationAlphaBlend = BlendOption.One;
        blendStateDesc.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
        blendStateDesc.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
        _blendState = new BlendState(_device, blendStateDesc);

        // Create the rasterizer state
        _rasterizerState = new RasterizerState(_device, new RasterizerStateDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            IsScissorEnabled = true,
            IsDepthClipEnabled = true
        });

        // Create the depth-stencil State
        _depthStencilState = new DepthStencilState(_device, new DepthStencilStateDescription
        {
            IsDepthEnabled = false,
            DepthWriteMask = DepthWriteMask.All,
            DepthComparison = Comparison.Always,
            IsStencilEnabled = false,
            FrontFace =
            {
                FailOperation = StencilOperation.Keep,
                DepthFailOperation = StencilOperation.Keep,
                PassOperation = StencilOperation.Keep,
                Comparison = Comparison.Always
            },
            BackFace =
            {
                FailOperation = StencilOperation.Keep,
                DepthFailOperation = StencilOperation.Keep,
                PassOperation = StencilOperation.Keep,
                Comparison = Comparison.Always
            }
        });

        CreateFontsTexture();

        return true;
    }

    public static void InvalidateDeviceObjects()
    {
        if (_device == null)
        {
            return;
        }

        _fontSampler?.Dispose();
        _fontSampler = null;

        _fontResourceView?.Dispose();
        _fontResourceView = null;
        ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero);

        _indexBuffer?.Dispose();
        _indexBuffer = null;

        _vertexBuffer?.Dispose();
        _vertexBuffer = null;

        _blendState?.Dispose();
        _blendState = null;

        _depthStencilState?.Dispose();
        _depthStencilState = null;

        _rasterizerState?.Dispose();
        _rasterizerState = null;

        _pixelShader?.Dispose();
        _pixelShader = null;

        _vertexConstantBuffer?.Dispose();
        _vertexConstantBuffer = null;

        _inputLayout?.Dispose();
        _inputLayout = null;

        _vertexShader?.Dispose();
        _vertexShader = null;
    }

    public static void Shutdown()
    {
        InvalidateDeviceObjects();

        // we don't own these, so no Dispose()
        _device = null;
        _deviceContext = null;

        if (_renderNamePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_renderNamePtr);
            _renderNamePtr = IntPtr.Zero;
        }
    }

    public static void NewFrame()
    {
        if (_fontSampler == null)
        {
            CreateDeviceObjects();
        }
    }

    internal static unsafe void Init(void* device, void* deviceContext)
    {
        ImGui.GetIO().BackendFlags = ImGui.GetIO().BackendFlags | ImGuiBackendFlags.RendererHasVtxOffset;

        _renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_dx11_c#");
        ImGui.GetIO().NativePtr->BackendRendererName = (byte*)_renderNamePtr.ToPointer();

        _device = new((IntPtr)device);
        _deviceContext = new((IntPtr)deviceContext);
    }
}

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

    private static bool _hasGamepad;
    private static bool _wantUpdateHasGamepad;
    private static IntPtr _xInputDLL;
    private static XInputGetCapabilitiesDelegate _xInputGetCapabilities;
    private static XInputGetStateDelegate _xInputGetState;

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
        io.NativePtr->BackendPlatformName = (byte*)Marshal.StringToHGlobalAnsi("imgui_impl_win32");
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;         // We can honor GetMouseCursor() values (optional)
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;          // We can honor io.WantSetMousePos requests (optional, rarely used)

        _windowHandle = (IntPtr)windowHandle;
        _ticksPerSecond = perf_frequency;
        _time = perf_counter;
        _lastMouseCursor = ImGuiMouseCursor.COUNT;

        // Set platform dependent data in viewport
        ImGui.GetMainViewport().NativePtr->PlatformHandleRaw = windowHandle;
        //IM_UNUSED(platform_has_own_dc); // Used in 'docking' branch

        // Dynamically load XInput library
        _wantUpdateHasGamepad = true;
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

        io.NativePtr->BackendPlatformName = null;
        io.NativePtr->BackendPlatformUserData = null;
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
                case ImGuiMouseCursor.ResizeEW: win32_cursor = IDC_SIZEWE; break;
                case ImGuiMouseCursor.ResizeNS: win32_cursor = IDC_SIZENS; break;
                case ImGuiMouseCursor.ResizeNESW: win32_cursor = IDC_SIZENESW; break;
                case ImGuiMouseCursor.ResizeNWSE: win32_cursor = IDC_SIZENWSE; break;
                case ImGuiMouseCursor.Hand: win32_cursor = IDC_HAND; break;
                case ImGuiMouseCursor.NotAllowed: win32_cursor = IDC_NO; break;
            }
            User32.SetCursor(User32.LoadCursor(IntPtr.Zero, win32_cursor));
        }
        return true;
    }

    static bool IsVkDown(User32.VirtualKey vk)
    {
        return (User32.GetKeyState(vk) & 0x8000) != 0;
    }

    static void ImGui_ImplWin32_AddKeyEvent(ImGuiKey key, bool down, User32.VirtualKey native_keycode, int native_scancode = -1)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(key, down);
        io.SetKeyEventNativeData(key, (int)native_keycode, native_scancode); // To support legacy indexing (<1.87 user code)
    }

    static void ImGui_ImplWin32_ProcessKeyEventsWorkarounds()
    {
        // Left & right Shift keys: when both are pressed together, Windows tend to not generate the WM_KEYUP event for the first released one.
        if (ImGui.IsKeyDown(ImGuiKey.LeftShift) && !IsVkDown(User32.VirtualKey.VK_LSHIFT))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftShift, false, User32.VirtualKey.VK_LSHIFT);
        if (ImGui.IsKeyDown(ImGuiKey.RightShift) && !IsVkDown(User32.VirtualKey.VK_RSHIFT))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightShift, false, User32.VirtualKey.VK_RSHIFT);

        // Sometimes WM_KEYUP for Win key is not passed down to the app (e.g. for Win+V on some setups, according to GLFW).
        if (ImGui.IsKeyDown(ImGuiKey.LeftSuper) && !IsVkDown(User32.VirtualKey.VK_LWIN))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftSuper, false, User32.VirtualKey.VK_LWIN);
        if (ImGui.IsKeyDown(ImGuiKey.RightSuper) && !IsVkDown(User32.VirtualKey.VK_RWIN))
            ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightSuper, false, User32.VirtualKey.VK_RWIN);
    }

    public static void ImGui_ImplWin32_UpdateKeyModifiers()
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, IsVkDown(User32.VirtualKey.VK_CONTROL));
        io.AddKeyEvent(ImGuiKey.ModShift, IsVkDown(User32.VirtualKey.VK_SHIFT));
        io.AddKeyEvent(ImGuiKey.ModAlt, IsVkDown(User32.VirtualKey.VK_MENU));
        io.AddKeyEvent(ImGuiKey.ModSuper, IsVkDown(User32.VirtualKey.VK_APPS));
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
                User32.POINT pos = new User32.POINT((int)io.MousePos.x, (int)io.MousePos.y);
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
        io.NativePtr->DisplaySize = new UnityEngine.Vector2(rect.Right - rect.Left, rect.Bottom - rect.Top);

        // Setup time step
        Kernel32.QueryPerformanceCounter(out var current_time);
        io.NativePtr->DeltaTime = (float)(current_time - _time) / _ticksPerSecond;
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

    public const User32.VirtualKey IM_VK_KEYPAD_ENTER = (User32.VirtualKey)((int)User32.VirtualKey.VK_RETURN + 256);

    // Map VK_xxx to ImGuiKey_xxx.
    public static ImGuiKey ImGui_ImplWin32_VirtualKeyToImGuiKey(User32.VirtualKey wParam)
    {
        switch (wParam)
        {
            case User32.VirtualKey.VK_TAB: return ImGuiKey.Tab;
            case User32.VirtualKey.VK_LEFT: return ImGuiKey.LeftArrow;
            case User32.VirtualKey.VK_RIGHT: return ImGuiKey.RightArrow;
            case User32.VirtualKey.VK_UP: return ImGuiKey.UpArrow;
            case User32.VirtualKey.VK_DOWN: return ImGuiKey.DownArrow;
            case User32.VirtualKey.VK_PRIOR: return ImGuiKey.PageUp;
            case User32.VirtualKey.VK_NEXT: return ImGuiKey.PageDown;
            case User32.VirtualKey.VK_HOME: return ImGuiKey.Home;
            case User32.VirtualKey.VK_END: return ImGuiKey.End;
            case User32.VirtualKey.VK_INSERT: return ImGuiKey.Insert;
            case User32.VirtualKey.VK_DELETE: return ImGuiKey.Delete;
            case User32.VirtualKey.VK_BACK: return ImGuiKey.Backspace;
            case User32.VirtualKey.VK_SPACE: return ImGuiKey.Space;
            case User32.VirtualKey.VK_RETURN: return ImGuiKey.Enter;
            case User32.VirtualKey.VK_ESCAPE: return ImGuiKey.Escape;
            case User32.VirtualKey.VK_OEM_7: return ImGuiKey.Apostrophe;
            case User32.VirtualKey.VK_OEM_COMMA: return ImGuiKey.Comma;
            case User32.VirtualKey.VK_OEM_MINUS: return ImGuiKey.Minus;
            case User32.VirtualKey.VK_OEM_PERIOD: return ImGuiKey.Period;
            case User32.VirtualKey.VK_OEM_2: return ImGuiKey.Slash;
            case User32.VirtualKey.VK_OEM_1: return ImGuiKey.Semicolon;
            case User32.VirtualKey.VK_OEM_PLUS: return ImGuiKey.Equal;
            case User32.VirtualKey.VK_OEM_4: return ImGuiKey.LeftBracket;
            case User32.VirtualKey.VK_OEM_5: return ImGuiKey.Backslash;
            case User32.VirtualKey.VK_OEM_6: return ImGuiKey.RightBracket;
            case User32.VirtualKey.VK_OEM_3: return ImGuiKey.GraveAccent;
            case User32.VirtualKey.VK_CAPITAL: return ImGuiKey.CapsLock;
            case User32.VirtualKey.VK_SCROLL: return ImGuiKey.ScrollLock;
            case User32.VirtualKey.VK_NUMLOCK: return ImGuiKey.NumLock;
            case User32.VirtualKey.VK_SNAPSHOT: return ImGuiKey.PrintScreen;
            case User32.VirtualKey.VK_PAUSE: return ImGuiKey.Pause;
            case User32.VirtualKey.VK_NUMPAD0: return ImGuiKey.Keypad0;
            case User32.VirtualKey.VK_NUMPAD1: return ImGuiKey.Keypad1;
            case User32.VirtualKey.VK_NUMPAD2: return ImGuiKey.Keypad2;
            case User32.VirtualKey.VK_NUMPAD3: return ImGuiKey.Keypad3;
            case User32.VirtualKey.VK_NUMPAD4: return ImGuiKey.Keypad4;
            case User32.VirtualKey.VK_NUMPAD5: return ImGuiKey.Keypad5;
            case User32.VirtualKey.VK_NUMPAD6: return ImGuiKey.Keypad6;
            case User32.VirtualKey.VK_NUMPAD7: return ImGuiKey.Keypad7;
            case User32.VirtualKey.VK_NUMPAD8: return ImGuiKey.Keypad8;
            case User32.VirtualKey.VK_NUMPAD9: return ImGuiKey.Keypad9;
            case User32.VirtualKey.VK_DECIMAL: return ImGuiKey.KeypadDecimal;
            case User32.VirtualKey.VK_DIVIDE: return ImGuiKey.KeypadDivide;
            case User32.VirtualKey.VK_MULTIPLY: return ImGuiKey.KeypadMultiply;
            case User32.VirtualKey.VK_SUBTRACT: return ImGuiKey.KeypadSubtract;
            case User32.VirtualKey.VK_ADD: return ImGuiKey.KeypadAdd;
            case IM_VK_KEYPAD_ENTER: return ImGuiKey.KeypadEnter;
            case User32.VirtualKey.VK_LSHIFT: return ImGuiKey.LeftShift;
            case User32.VirtualKey.VK_LCONTROL: return ImGuiKey.LeftCtrl;
            case User32.VirtualKey.VK_LMENU: return ImGuiKey.LeftAlt;
            case User32.VirtualKey.VK_LWIN: return ImGuiKey.LeftSuper;
            case User32.VirtualKey.VK_RSHIFT: return ImGuiKey.RightShift;
            case User32.VirtualKey.VK_RCONTROL: return ImGuiKey.RightCtrl;
            case User32.VirtualKey.VK_RMENU: return ImGuiKey.RightAlt;
            case User32.VirtualKey.VK_RWIN: return ImGuiKey.RightSuper;
            case User32.VirtualKey.VK_APPS: return ImGuiKey.Menu;
            case (User32.VirtualKey)'0': return ImGuiKey._0;
            case (User32.VirtualKey)'1': return ImGuiKey._1;
            case (User32.VirtualKey)'2': return ImGuiKey._2;
            case (User32.VirtualKey)'3': return ImGuiKey._3;
            case (User32.VirtualKey)'4': return ImGuiKey._4;
            case (User32.VirtualKey)'5': return ImGuiKey._5;
            case (User32.VirtualKey)'6': return ImGuiKey._6;
            case (User32.VirtualKey)'7': return ImGuiKey._7;
            case (User32.VirtualKey)'8': return ImGuiKey._8;
            case (User32.VirtualKey)'9': return ImGuiKey._9;
            case (User32.VirtualKey)'A': return ImGuiKey.A;
            case (User32.VirtualKey)'B': return ImGuiKey.B;
            case (User32.VirtualKey)'C': return ImGuiKey.C;
            case (User32.VirtualKey)'D': return ImGuiKey.D;
            case (User32.VirtualKey)'E': return ImGuiKey.E;
            case (User32.VirtualKey)'F': return ImGuiKey.F;
            case (User32.VirtualKey)'G': return ImGuiKey.G;
            case (User32.VirtualKey)'H': return ImGuiKey.H;
            case (User32.VirtualKey)'I': return ImGuiKey.I;
            case (User32.VirtualKey)'J': return ImGuiKey.J;
            case (User32.VirtualKey)'K': return ImGuiKey.K;
            case (User32.VirtualKey)'L': return ImGuiKey.L;
            case (User32.VirtualKey)'M': return ImGuiKey.M;
            case (User32.VirtualKey)'N': return ImGuiKey.N;
            case (User32.VirtualKey)'O': return ImGuiKey.O;
            case (User32.VirtualKey)'P': return ImGuiKey.P;
            case (User32.VirtualKey)'Q': return ImGuiKey.Q;
            case (User32.VirtualKey)'R': return ImGuiKey.R;
            case (User32.VirtualKey)'S': return ImGuiKey.S;
            case (User32.VirtualKey)'T': return ImGuiKey.T;
            case (User32.VirtualKey)'U': return ImGuiKey.U;
            case (User32.VirtualKey)'V': return ImGuiKey.V;
            case (User32.VirtualKey)'W': return ImGuiKey.W;
            case (User32.VirtualKey)'X': return ImGuiKey.X;
            case (User32.VirtualKey)'Y': return ImGuiKey.Y;
            case (User32.VirtualKey)'Z': return ImGuiKey.Z;
            case User32.VirtualKey.VK_F1: return ImGuiKey.F1;
            case User32.VirtualKey.VK_F2: return ImGuiKey.F2;
            case User32.VirtualKey.VK_F3: return ImGuiKey.F3;
            case User32.VirtualKey.VK_F4: return ImGuiKey.F4;
            case User32.VirtualKey.VK_F5: return ImGuiKey.F5;
            case User32.VirtualKey.VK_F6: return ImGuiKey.F6;
            case User32.VirtualKey.VK_F7: return ImGuiKey.F7;
            case User32.VirtualKey.VK_F8: return ImGuiKey.F8;
            case User32.VirtualKey.VK_F9: return ImGuiKey.F9;
            case User32.VirtualKey.VK_F10: return ImGuiKey.F10;
            case User32.VirtualKey.VK_F11: return ImGuiKey.F11;
            case User32.VirtualKey.VK_F12: return ImGuiKey.F12;
            default: return ImGuiKey.None;
        }
    }

    // See https://learn.microsoft.com/en-us/windows/win32/tablet/system-events-and-mouse-messages
    // Prefer to call this at the top of the message handler to avoid the possibility of other Win32 calls interfering with this.
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
        if (ImGui.GetCurrentContext() == IntPtr.Zero)
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
                        User32.VirtualKey vk = (User32.VirtualKey)wParam;


                        bool isEnter = (User32.VirtualKey)wParam == User32.VirtualKey.VK_RETURN;
                        bool hasExtendedKeyFlag = (HIWORD(lParam) & (ushort)User32.KeyFlag.KF_EXTENDED) != 0;
                        if (isEnter && hasExtendedKeyFlag)
                            vk = IM_VK_KEYPAD_ENTER;

                        // Submit key event
                        ImGuiKey key = ImGui_ImplWin32_VirtualKeyToImGuiKey(vk);
                        int scancode = LOBYTE(HIWORD(lParam));
                        if (key != ImGuiKey.None)
                            ImGui_ImplWin32_AddKeyEvent(key, is_key_down, vk, scancode);

                        // Submit individual left/right modifier events
                        if (vk == User32.VirtualKey.VK_SHIFT)
                        {
                            // Important: Shift keys tend to get stuck when pressed together, missing key-up events are corrected in ImGui_ImplWin32_ProcessKeyEventsWorkarounds()
                            if (IsVkDown(User32.VirtualKey.VK_LSHIFT) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftShift, is_key_down, User32.VirtualKey.VK_LSHIFT, scancode); }
                            if (IsVkDown(User32.VirtualKey.VK_RSHIFT) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightShift, is_key_down, User32.VirtualKey.VK_RSHIFT, scancode); }
                        }
                        else if (vk == User32.VirtualKey.VK_CONTROL)
                        {
                            if (IsVkDown(User32.VirtualKey.VK_LCONTROL) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftCtrl, is_key_down, User32.VirtualKey.VK_LCONTROL, scancode); }
                            if (IsVkDown(User32.VirtualKey.VK_RCONTROL) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightCtrl, is_key_down, User32.VirtualKey.VK_RCONTROL, scancode); }
                        }
                        else if (vk == User32.VirtualKey.VK_MENU)
                        {
                            if (IsVkDown(User32.VirtualKey.VK_LMENU) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.LeftAlt, is_key_down, User32.VirtualKey.VK_LMENU, scancode); }
                            if (IsVkDown(User32.VirtualKey.VK_RMENU) == is_key_down) { ImGui_ImplWin32_AddKeyEvent(ImGuiKey.RightAlt, is_key_down, User32.VirtualKey.VK_RMENU, scancode); }
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
                const int DBT_DEVNODES_CHANGED = 0x0007;
                if ((uint)wParam == DBT_DEVNODES_CHANGED)
                    _wantUpdateHasGamepad = true;
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
