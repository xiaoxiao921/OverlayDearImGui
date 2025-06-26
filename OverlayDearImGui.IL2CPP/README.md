# OverlayDearImGui.MelonIL2CPP

Creates an overlay window that renders [Dear ImGui](https://github.com/ocornut/imgui) over the process of your choice.

Does not work when the target application is exclusively full-screen.

## Usage

The keybind for bringing up the cursor for interaction is by default the `Insert` key, which can be modified in the configuration file.

## Mod Developers

Download this package and the `OverlayDearImGui.Shared` dependency. Add a reference to `Hexa.NET.ImGui.dll` (from Shared) and `OverlayDearImGui.MelonIL2CPP.dll` (from this package) in your C# project.

```csharp
OverlayDearImGui.Overlay.Render += MyUI;
```

Example Render Function:

```csharp
private static float _lastRefreshTime = -Mathf.Infinity;
private static GameObject[] _cachedInstances = Array.Empty<GameObject>();

private static void MyUI()
{
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
    {
        return;
    }

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
            if (stuff == null)
            {
                continue;
            }

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

```
