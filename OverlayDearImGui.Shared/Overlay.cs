using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using OverlayDearImGui.Windows;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

using SharedList = OverlayDearImGui.DisposableList<OverlayDearImGui.ClonedDrawData>;

[assembly: InternalsVisibleTo("OverlayDearImGui.BepInEx5")]

namespace OverlayDearImGui;

public class Overlay
{
    private SharpDX.Direct3D11.Device _device;
    private DeviceContext _deviceContext;
    private SwapChain _swapChain;
    private RenderTargetView _mainRenderTargetView;

    public static event Action OnRender;

    /// Key for switching the overlay visibility.
    /// </summary>
    public static IConfigEntry<VirtualKey> OverlayToggle { get; internal set; }
    internal const VirtualKey OverlayToggleDefault = VirtualKey.Insert;

    public static string AssetsFolderPath { get; private set; } = "";
    public static string ImGuiIniConfigPath { get; private set; }
    private const string IniFileName = "iDeathHD.OverlayDearImGui_imgui.ini";

    public static bool IsOpen { get; private set; }

    private static RECT _gameRect;
    public static RECT GameRect
    {
        get { return _gameRect; }

        private set
        {
            _gameRect = value;
        }
    }

    public static IntPtr GameHwnd { get; private set; }

    private static uint _resizeWidth;
    private static uint _resizeHeight;

    // freshly cloned by non-render thread
    private static SharedList _nextFrameDrawData;
    // being rendered by render thread
    private static SharedList _currentRenderDrawData;
    // sync lock
    private static readonly object _drawDataLock = new();

    internal void CreateRenderTarget()
    {
        using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
        {
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

    private static unsafe void CloneRenderData()
    {
        var pio = ImGui.GetPlatformIO();
        var newData = new SharedList(pio.Viewports.Size);

        for (int i = 0; i < pio.Viewports.Size; ++i)
        {
            var vp = pio.Viewports[i];
            if (vp.Flags.HasFlag(ImGuiViewportFlags.IsMinimized))
                continue;

            newData.Add(new(new(vp.DrawData)));
        }

        lock (_drawDataLock)
        {
            _nextFrameDrawData?.Dispose();
            _nextFrameDrawData = newData;
        }
    }

    internal static void UpdateOverlayDrawData()
    {
        if (ImGui.GetCurrentContext() == null)
        {
            return;
        }

        ImGuiDX11Impl.NewFrame();
        ImGuiWin32Impl.NewFrame();
        ImGui.NewFrame();

        if (Overlay.IsOpen)
        {
            if (Overlay.OnRender != null)
            {
                foreach (Action item in Overlay.OnRender.GetInvocationList())
                {
                    try
                    {
                        item();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
        }

        ImGui.Render();
        CloneRenderData();
    }

    public unsafe void Render(string windowName, string windowClass, string assetsFolderPath, string imguiIniConfigFolderPath, IConfigEntry<VirtualKey> overlayToggleKeybind)
    {
        ImGuiIniConfigPath = Path.Combine(imguiIniConfigFolderPath, IniFileName);
        AssetsFolderPath = assetsFolderPath ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets");
        OverlayToggle = overlayToggleKeybind;

        GameHwnd = User32.FindWindowW(windowClass, windowName);


        User32.GetWindowRect(GameHwnd, out _gameRect);

        var (hwnd, wc) = WindowFactory.CreateClassicWindow("OverlayDearImGui");

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        Dwmapi.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (!CreateDeviceD3D(hwnd))
        {
            CleanupDeviceD3D();
            User32.UnregisterClass(wc.lpszClassName, wc.hInstance);
            Log.Error("Failed CreateDeviceD3D");
            return;
        }

        User32.ShowWindow(hwnd, User32.ShowWindowCommand.ShowDefault);
        User32.UpdateWindow(hwnd);

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad;

        io.NativePtr->IniFilename = (byte*)Marshal.StringToHGlobalAnsi(ImGuiIniConfigPath);

        ImGui.StyleColorsDark();

        var fontPath = Path.Combine(AssetsFolderPath, "Fonts", "Bombardier-Regular.ttf");
        var font = io.Fonts.AddFontFromFileTTF(fontPath, 16);
        io.NativePtr->FontDefault = font;

        ImGuiWin32Impl.Init(hwnd);

        ImGuiDX11Impl.Init((void*)_device.NativePointer, (void*)_deviceContext.NativePointer);

        bool openOverlayKeyPreviouslyDown = false;

        ToggleOverlay(hwnd);

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

            bool openOverlayKeyDown = (User32.GetAsyncKeyState(OverlayToggle.Get()) & 0x8000) != 0;

            if (openOverlayKeyDown && !openOverlayKeyPreviouslyDown)
            {
                ToggleOverlay(hwnd);
            }

            openOverlayKeyPreviouslyDown = openOverlayKeyDown;

            if (_resizeWidth != 0 && _resizeHeight != 0)
            {
                CleanupRenderTarget();
                _swapChain.ResizeBuffers(0, (int)_resizeWidth, (int)_resizeHeight, Format.Unknown, 0);
                _resizeWidth = _resizeHeight = 0;
                CreateRenderTarget();
            }

            var currentForegroundWindow = User32.GetForegroundWindow();
            if (!(currentForegroundWindow == hwnd || currentForegroundWindow == GameHwnd))
            {
                Kernel32.Sleep(16);
            }

            bool visible = User32.IsWindowVisible(GameHwnd);

            User32.ShowWindow(hwnd, visible ? User32.ShowWindowCommand.Show : User32.ShowWindowCommand.Hide);

            if (!visible)
                Kernel32.Sleep(16);

            User32.GetWindowRect(GameHwnd, out var TmpRect);

            if (TmpRect.Left != GameRect.Left || TmpRect.Bottom != GameRect.Bottom ||
                TmpRect.Top != GameRect.Top || TmpRect.Right != GameRect.Right
                )
            {
                GameRect = TmpRect;

                User32.SetWindowPos(hwnd, (IntPtr)(-2) /* HWND_NOTOPMOST */, GameRect.X, GameRect.Y, GameRect.Width, GameRect.Height, User32.SWP_NOREDRAW);
            }

            User32.SetWindowDisplayAffinity(hwnd, User32.DisplayAffinity.None);

            _deviceContext.OutputMerger.SetRenderTargets(_mainRenderTargetView);
            //var clearColorWithAlpha = new RawColor4(clearColor.X * clearColor.W, clearColor.Y * clearColor.W, clearColor.Z * clearColor.W, clearColor.W);
            //_deviceContext.ClearRenderTargetView(_mainRenderTargetView, clearColorWithAlpha);
            _deviceContext.ClearRenderTargetView(_mainRenderTargetView, new(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));

            SharedList newRenderData = null;

            lock (_drawDataLock)
            {
                if (_nextFrameDrawData != null)
                {
                    _currentRenderDrawData?.Dispose(); // Done rendering previous frame
                    _currentRenderDrawData = _nextFrameDrawData;
                    _nextFrameDrawData = null;
                }

                newRenderData = _currentRenderDrawData;
            }

            if (newRenderData != null && newRenderData.Count > 0)
            {
                ImGuiDX11Impl.RenderDrawData(newRenderData[0].Data);
            }

            _swapChain.Present(1, PresentFlags.None);
        }

        ImGuiDX11Impl.Shutdown();
        ImGuiWin32Impl.Shutdown();
        ImGui.DestroyContext();
        CleanupDeviceD3D();
        User32.DestroyWindow(hwnd);
        User32.UnregisterClass(wc.lpszClassName, wc.hInstance);
    }

    private static unsafe void ToggleOverlay(IntPtr hwnd)
    {
        Overlay.IsOpen = !Overlay.IsOpen;

        if (Overlay.IsOpen)
        {
            User32.ShowWindow(hwnd, User32.ShowWindowCommand.Restore);
            User32.ShowWindow(hwnd, User32.ShowWindowCommand.Show);
            User32.SetForegroundWindow(hwnd);
        }
        else
        {
            User32.ShowWindow(hwnd, User32.ShowWindowCommand.Hide);
            User32.ShowWindow(hwnd, User32.ShowWindowCommand.Minimize);

            User32.BringWindowToTop(GameHwnd);
            User32.SetForegroundWindow(GameHwnd);
        }
    }

    public static IntPtr WndProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        if (ImGuiWin32Impl.WndProcHandler(hWnd, msg, wParam, lParam) != IntPtr.Zero)
        {
            return new IntPtr(1);
        }

        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
