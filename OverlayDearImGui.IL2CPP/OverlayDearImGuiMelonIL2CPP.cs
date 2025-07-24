using System.Diagnostics;
using HarmonyLib;
using Hexa.NET.ImGui;
using MelonLoader;
using MonoMod.RuntimeDetour;
using OverlayDearImGui.MelonIL2CPP;
using OverlayDearImGui.Windows;
using UnityEngine;

[assembly: MelonInfo(typeof(OverlayDearImGuiMelonIL2CPP), "OverlayDearImGui_MelonIL2CPP", "2.1.0", "iDeathHD", "https://github.com/xiaoxiao921/OverlayDearImGui")]
[assembly: MelonGame]

namespace OverlayDearImGui.MelonIL2CPP;

public class OverlayDearImGuiMelonIL2CPP : MelonMod
{
    internal static bool VerifyFile(ref (bool, string) __result)
    {
        __result = (true, null);

        return false;
    }

    public override void OnInitializeMelon()
    {
        Log.Init(new LogMelon(LoggerInstance));

        var category = MelonPreferences.CreateCategory("OverlayDearImGui_MelonIL2CPP");
        var toggleKey = category.CreateEntry("OverlayToggle", Overlay.OverlayToggleDefault, "Key for toggling the overlay.");

        while (!Debugger.IsAttached)
        {
            Log.Info("Waiting for debugger attach.");
            Thread.Sleep(1000);
        }

        var m1 = AccessTools.Method("MelonLoader.Utils.AssemblyVerifier:VerifyFile");
        var m2 = AccessTools.Method("OverlayDearImGui.MelonIL2CPP.OverlayDearImGuiMelonIL2CPP:VerifyFile");

        var res = this.HarmonyInstance.Patch(m1, new HarmonyMethod(m2));
        if (res != null)
            Log.Info(res);

        _renderThread = new Thread(() =>
        {
            try
            {
                new Overlay().Render(null, "UnityWndClass",
                    Path.Combine(Path.GetDirectoryName(MelonAssembly.Location), "Assets"),
                    Path.GetDirectoryName(MelonAssembly.Location),
                    new ConfigEntryMelonIL2CPP<VirtualKey>(toggleKey),
                    Path.Combine(Path.GetDirectoryName(MelonAssembly.Location), "cimgui.dll")
                );
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
        _renderThread.Start();

        //Overlay.OnRender += MyUI;
    }

    public override void OnUpdate()
    {
        Overlay.UpdateOverlayDrawData();

        if (ImGui.IsKeyPressed(ImGuiKey.F10))
        {
        }
    }

    private static bool _isMyUIOpen = true;

    private static float _lastRefreshTime = -Mathf.Infinity;
    private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<GameObject> _cachedInstances;
    private Hook _hook;
    private Thread _renderThread;

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

        if (ImGui.GetTime() - _lastRefreshTime >= 2f)
        {
            _cachedInstances = Resources.FindObjectsOfTypeAll<GameObject>();
            _lastRefreshTime = (float)ImGui.GetTime();
        }

        if (ImGui.Begin("GameObject Debug Viewer", ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_cachedInstances == null)
            {
                ImGui.Text($"Found 0 GameObject instances:");
            }
            else
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
        }
        ImGui.End();
    }
}
