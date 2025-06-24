using System;
using System.Threading;
using BepInEx;
using ImGuiNET;
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

    private static bool _isMyUIOpen = true;

    private void Awake()
    {
        Log.Init(Logger);

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
    internal static SharedList NextFrameDrawData;
    // being rendered by render thread
    internal static SharedList CurrentRenderDrawData;
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

    private static float _lastRefreshTime = -Mathf.Infinity;
    private static GameObject[] _cachedInstances = Array.Empty<GameObject>();

    private static void MyUI()
    {
        if (!Overlay.IsOpen)
            return;

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Debug", true))
            {
                if (ImGui.MenuItem("Open Debug Window", null, _isMyUIOpen))
                {
                    _isMyUIOpen ^= true;
                }

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }

        if (!_isMyUIOpen)
            return;

        if (Time.realtimeSinceStartup - _lastRefreshTime >= 2f)
        {
            _cachedInstances = UnityEngine.Object.FindObjectsOfType<GameObject>();
            _lastRefreshTime = Time.realtimeSinceStartup;
        }

        if (ImGui.Begin("GameObject Debug Viewer", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"Found {_cachedInstances.Length} GameObject instances:");

            for (int i = 0; i < _cachedInstances.Length; i++)
            {
                var stuff = _cachedInstances[i];
                if (stuff == null) continue;

                var name = stuff.gameObject.name;
                var pos = stuff.transform.position;
                var active = stuff.gameObject.activeInHierarchy;

                ImGui.Separator();
                ImGui.Text($"[{i}] Name: {name}");
                ImGui.Text($"    Active: {active}");
                ImGui.Text($"    Position: {pos}");
            }

            ImGui.End();
        }
    }
}
