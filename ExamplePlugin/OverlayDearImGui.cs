using System;
using System.Threading;
using BepInEx;
using ImGuiNET;
using OverlayDearImGui.Windows;
using UnityEngine;

using SharedList = OverlayDearImGui.DisposableList<OverlayDearImGui.ClonedDrawData>;

namespace OverlayDearImGui;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class OverlayDearImGui : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "iDeathHD";
    public const string PluginName = "OverlayDearImGui";
    public const string PluginVersion = "2.0.0";

    private static Thread _renderThread;

    private static bool _isMyUIOpen;

    private void Awake()
    {
        Log.Init(Logger);

        User32.MessageBox(IntPtr.Zero, "qd", "q", 0);

        //this.gameObject.AddComponent<UnityMainThreadDispatcher>();

        _renderThread = new Thread(() =>
        {
            try
            {
                new Overlay().Render(null, "UnityWndClass");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
        _renderThread.Start();

    }

    public void Update()
    {
        if (ImGui.GetCurrentContext() == null)
        {
            return;
        }

        ImGuiDX11Impl.NewFrame();
        ImGuiWin32Impl.NewFrame();
        ImGui.NewFrame();

        MyUI();

        if (Overlay.IsOpen && Overlay.OnRender != null)
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

        ImGui.Render();
        CloneRenderData();
    }

    // freshly cloned by Unity thread
    internal static SharedList? NextFrameDrawData;
    // being rendered by render thread
    internal static SharedList? CurrentRenderDrawData;
    // sync lock
    internal static readonly object DrawDataLock = new();

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

        lock (DrawDataLock)
        {
            NextFrameDrawData?.Dispose();
            NextFrameDrawData = newData;
        }
    }

    private static void MyUI()
    {
        if (Overlay.IsOpen)
        {
            var dummy = true;
            ImGui.ShowDemoWindow(ref dummy);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("MainBar", true))
                {
                    if (ImGui.MenuItem("MyTestPlugin", null, false, true))
                    {
                        _isMyUIOpen ^= true;
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("MainBar", true))
                {
                    if (ImGui.MenuItem("MyTestPlugin2", null, false, true))
                    {
                        _isMyUIOpen ^= true;
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }
        }

        if (_isMyUIOpen)
        {
            var dummy2 = true;
            if (ImGui.Begin("My Mod Window", ref dummy2, (int)ImGuiWindowFlags.None))
            {
                ImGui.Text("hello there");

                if (ImGui.Button("Click me"))
                {
                    // Interacting with the unity api must be done from the unity main thread
                    // Can just use the dispatcher shipped with the library for that
                    //UnityMainThreadDispatcher.Enqueue(() =>
                    //{
                    var go = new GameObject();
                    go.AddComponent<Stuff>();
                    //});
                }
            }

            ImGui.End();
        }
    }
}

public class Stuff : MonoBehaviour
{
    private void Awake()
    {
        Log.Info("hello  from stuff!");
    }
}
