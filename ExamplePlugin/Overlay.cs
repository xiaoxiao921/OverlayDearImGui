using System;
using System.IO;
using System.Reflection;
using ImGuiNET;
using OverlayDearImGui.Windows;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

namespace OverlayDearImGui;

public class Overlay
{
    private SharpDX.Direct3D11.Device _device;
    private DeviceContext _deviceContext;
    private SwapChain _swapChain;
    private RenderTargetView _mainRenderTargetView;

    public static string AssetsFolderPath = "";

    public static bool IsOpen = true;

    public static RECT GameRect;

    public static IntPtr GameHwnd;

    public static uint g_ResizeWidth;
    public static uint g_ResizeHeight;

    void CreateRenderTarget()
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

    public unsafe void Render(string windowName, string windowClass)
    {
        GameHwnd = User32.FindWindowW(windowClass, windowName);

        AssetsFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets");

        User32.GetWindowRect(GameHwnd, out GameRect);

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

        ImGui.StyleColorsDark();

        ImGuiWin32Impl.Init(hwnd);

        ImGuiDX11Impl.Init((void*)_device.NativePointer, (void*)_deviceContext.NativePointer);

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

            bool insertKeyDown = (User32.GetAsyncKeyState(User32.VirtualKey.VK_INSERT) & 0x8000) != 0;

            if (insertKeyDown && !insertKeyPreviouslyDown)
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

    public static IntPtr WndProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        if (ImGuiWin32Impl.WndProcHandler(hWnd, msg, wParam, lParam) != IntPtr.Zero)
        {
            return new IntPtr(1);
        }

        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
