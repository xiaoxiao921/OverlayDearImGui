using BepInEx.Configuration;

namespace OverlayDearImGui.BepInEx5;

public class ConfigEntryBepInEx5<T> : IConfigEntry<T>
{
    private ConfigEntry<T> _configEntry;

    public ConfigEntryBepInEx5(ConfigEntry<T> configEntry)
    {
        _configEntry = configEntry;
    }

    public T Get() => _configEntry.Value;
    public void Set(T value) => _configEntry.Value = value;
}