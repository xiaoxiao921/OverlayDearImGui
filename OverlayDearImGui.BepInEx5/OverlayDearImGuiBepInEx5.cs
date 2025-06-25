using System;
using System.IO;
using System.Threading;
using BepInEx;
using Hexa.NET.ImGui;
using OverlayDearImGui.Windows;
using UnityEngine;
using static OverlayDearImGui.Windows.User32;

namespace OverlayDearImGui;

#pragma warning disable CS0436 // Type conflicts with imported type
[AutoThunderstoreVersion.AutoVersion]
#pragma warning restore CS0436 // Type conflicts with imported type
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public partial class OverlayDearImGuiBepInEx5 : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "iDeathHD";
    public const string PluginName = "OverlayDearImGui_BepInEx5";

    private static Thread _renderThread;

    private static bool _isMyUIOpen = true;

    private void Awake()
    {
        Log.Init(new LogBepInEx5(Logger));

        var toggleKey = new ConfigEntryBepInEx5<VirtualKey>(
            Config.Bind("Keybinds", "OverlayToggle",
            Overlay.OverlayToggleDefault,
            "Key for toggling the overlay.")
        );

        Overlay.OnRender += MyUI;

        _renderThread = new Thread(() =>
        {
            try
            {
                new Overlay().Render(null, "UnityWndClass",
                    Path.Combine(Path.GetDirectoryName(Info.Location), "Assets"),
                    Paths.ConfigPath,
                    toggleKey,
                    Path.Combine(Path.GetDirectoryName(Info.Location), "cimgui.dll")
                );
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
        _renderThread.Start();
    }

    private void Update()
    {
        Overlay.UpdateOverlayDrawData();
    }

    private static float _lastRefreshTime = -Mathf.Infinity;
    private static GameObject[] _cachedInstances = Array.Empty<GameObject>();

    private static void MyUI()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Debug", true))
            {
                if (ImGui.MenuItem("Open Debug Window", (string)null, _isMyUIOpen))
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
        }
        ImGui.End();
    }
}
