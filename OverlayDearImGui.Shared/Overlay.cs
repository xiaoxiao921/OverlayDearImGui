using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using HexaGen.Runtime;
using OverlayDearImGui.Windows;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

using SharedList = OverlayDearImGui.DisposableList<OverlayDearImGui.ClonedDrawData>;

[assembly: InternalsVisibleTo("OverlayDearImGui.BepInEx5")]

[assembly: InternalsVisibleTo("OverlayDearImGui.MelonIL2CPP")]

namespace OverlayDearImGui;

public class Overlay
{
    private SharpDX.Direct3D11.Device _device;
    private DeviceContext _deviceContext;
    private SwapChain _swapChain;
    private RenderTargetView _mainRenderTargetView;

    public static event Action OnRender;

    /// <summary>
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

            newData.Add(new(vp, new(vp.DrawData)));
        }

        lock (_drawDataLock)
        {
            _nextFrameDrawData?.Dispose();
            _nextFrameDrawData = newData;
        }
    }

    internal static void UpdateOverlayDrawData()
    {
        if (ImGui.GetCurrentContext().IsNull)
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
        //ImGui.UpdatePlatformWindows();
        CloneRenderData();
    }

    //private static unsafe void InitPlatformInterface()
    //{
    //ImGuiPlatformIOPtr platform_io = ImGui.GetPlatformIO();
    //platform_io.RendererCreateWindow = (void*)Marshal.GetFunctionPointerForDelegate<RendererCreateWindow>(CreateWindow);
    //platform_io.RendererDestroyWindow = (void*)Marshal.GetFunctionPointerForDelegate<RendererDestroyWindow>(DestroyWindow);
    //platform_io.RendererSetWindowSize = (void*)Marshal.GetFunctionPointerForDelegate<RendererSetWindowSize>(SetWindowSize);
    //}

    //internal static unsafe void SetWindowSize(nint viewport_, Vector2 size)
    //{
    //ImGuiViewportPtr viewport = (ImGuiViewport*)viewport_;
    //var data = (ImGuiViewportDataDx11*)viewport.RendererUserData;

    // Delete our existing view
    //new RenderTargetView(data->View).Dispose();
    //var tmpSwap = new SwapChain(data->SwapChain);

    // Resize buffers and recreate view
    //tmpSwap.ResizeBuffers(1, (int)size.X, (int)size.Y, Format.Unknown, SwapChainFlags.None);
    //using (var backbuffer = tmpSwap.GetBackBuffer<Texture2D>(0))
    //data->View = new RenderTargetView(_device, backbuffer).NativePointer;
    //}

    //internal static unsafe void DestroyWindow(nint viewport_)
    //{
    //ImGuiViewportPtr viewport = (ImGuiViewport*)viewport_;
    // This is also called on the main viewport for some reason, and we never set that viewport's RendererUserData
    //if (viewport.RendererUserData == null) return;

    //var data = (ImGuiViewportDataDx11*)viewport.RendererUserData;

    //new SwapChain(data->SwapChain).Dispose();
    //new RenderTargetView(data->View).Dispose();
    //data->SwapChain = IntPtr.Zero;
    //data->View = IntPtr.Zero;

    //Marshal.FreeHGlobal((IntPtr)viewport.RendererUserData);
    //viewport.RendererUserData = null;
    //}

    /// <summary>
    /// Renderer data
    /// </summary>
    //private struct RendererData
    //{
    //    public IntPtr device;
    //    public IntPtr context;
    //    public IntPtr factory;
    //    public IntPtr vertexBuffer;
    //    public IntPtr indexBuffer;
    //    public IntPtr vertexShader;
    //    public IntPtr inputLayout;
    //    public IntPtr constantBuffer;
    //    public IntPtr pixelShader;
    //    public IntPtr fontSampler;
    //    public IntPtr fontTextureView;
    //    public IntPtr rasterizerState;
    //    public IntPtr blendState;
    //    public IntPtr depthStencilState;
    //    public int vertexBufferSize = 5000, indexBufferSize = 10000;

    //    public RendererData()
    //    {
    //    }
    //}

    /** Viewport support **/
    //private struct ImGuiViewportDataDx11
    //{
    //    public IntPtr SwapChain;
    //    public IntPtr View;
    //}

    //private static IntPtr CreateSwapChain(SwapChainDescription desc)
    //{
    //    // Create a swapchain using the existing game hardware (I think)
    //    using (var dxgi = _device.QueryInterface<SharpDX.DXGI.Device>())
    //    using (var adapter = dxgi.Adapter)
    //    using (var factory = adapter.GetParent<Factory>())
    //    {
    //        return new SwapChain(factory, _device, desc).NativePointer;
    //    }
    //}

    // Viewport functions
    //internal static unsafe void CreateWindow(nint viewport_)
    //{
    //    ImGuiViewportPtr viewport = (ImGuiViewport*)viewport_;
    //    var data = (ImGuiViewportDataDx11*)Marshal.AllocHGlobal(Marshal.SizeOf<ImGuiViewportDataDx11>());

    //    // PlatformHandleRaw should always be a HWND, whereas PlatformHandle might be a higher-level handle (e.g. GLFWWindow*, SDL_Window*).
    //    // Some backend will leave PlatformHandleRaw NULL, in which case we assume PlatformHandle will contain the HWND.
    //    IntPtr hWnd = (IntPtr)viewport.PlatformHandleRaw;
    //    if (hWnd == IntPtr.Zero)
    //        hWnd = (IntPtr)viewport.PlatformHandle;

    //    // Create swapchain
    //    SwapChainDescription desc = new SwapChainDescription
    //    {
    //        ModeDescription = new ModeDescription
    //        {
    //            Width = 0,
    //            Height = 0,
    //            Format = Format.R8G8B8A8_UNorm,
    //            RefreshRate = new Rational(0, 0)
    //        },
    //        SampleDescription = new SampleDescription
    //        {
    //            Count = 1,
    //            Quality = 0
    //        },
    //        Usage = Usage.RenderTargetOutput,
    //        BufferCount = 1,
    //        OutputHandle = hWnd,
    //        IsWindowed = true,
    //        SwapEffect = SwapEffect.Discard,
    //        Flags = SwapChainFlags.None
    //    };

    //    data->SwapChain = CreateSwapChain(desc);

    //    // Create the render target view
    //    using (var backbuffer = new SwapChain(data->SwapChain).GetBackBuffer<Texture2D>(0))
    //        data->View = new RenderTargetView(_device, backbuffer).NativePointer;

    //    viewport.RendererUserData = (void*)(IntPtr)data;
    //}

    public unsafe void Render(string windowName, string windowClass, string assetsFolderPath, string imguiIniConfigFolderPath, IConfigEntry<VirtualKey> overlayToggleKeybind, string cimguiDllFilePath)
    {
        ImGui.InitApi(new NativeLibraryContext(Kernel32.LoadLibrary(cimguiDllFilePath)));

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
        io.ConfigFlags |=
            ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad |
            //ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.ViewportsEnable;
            ImGuiConfigFlags.DockingEnable;

        //if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        //InitPlatformInterface();

        io.IniFilename = (byte*)Marshal.StringToHGlobalAnsi(ImGuiIniConfigPath);

        ImGui.StyleColorsDark();

        var fontPath = Path.Combine(AssetsFolderPath, "Fonts", "Bombardier-Regular.ttf");
        var font = io.Fonts.AddFontFromFileTTF(fontPath, 16);
        io.FontDefault = font;

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


            //ref var viewports = ref ImGui.GetPlatformIO().Viewports;
            //for (int i = 0; i < viewports.Size; i++)
            //{
            //    var viewport = viewports[i];

            //    if ((viewport.Flags & ImGuiViewportFlags.IsMinimized) != 0)
            //        continue;

            //    var data = (ImGuiViewportDataDx11*)viewport.RendererUserData;
            //    if (data == null)
            //        continue;
            //    if (data->View == null)
            //        continue;

            //    var tmpRtv = new RenderTargetView(data->View);
            //    _deviceContext.OutputMerger.SetTargets(tmpRtv);
            //    if ((viewport.Flags & ImGuiViewportFlags.NoRendererClear) != ImGuiViewportFlags.NoRendererClear)
            //        _deviceContext.ClearRenderTargetView(tmpRtv, new(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));

            //    if (newRenderData != null && newRenderData.Count > 0)
            //    {
            //        foreach (var renderData in newRenderData)
            //        {
            //            if (renderData.ViewportPtr == viewport)
            //            {
            //                ImGuiDX11Impl.RenderDrawData(renderData.Data);
            //                break;
            //            }
            //        }
            //    }

            //    new SwapChain(data->SwapChain).Present(1, PresentFlags.None);
            //}
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

    public delegate IntPtr WndProcDelegate(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr WndProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        if (ImGuiWin32Impl.WndProcHandler(hWnd, msg, wParam, lParam) != IntPtr.Zero)
        {
            return new IntPtr(1);
        }

        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
