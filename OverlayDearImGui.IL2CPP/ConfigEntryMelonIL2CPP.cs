using MelonLoader;

namespace OverlayDearImGui.MelonIL2CPP;

public class ConfigEntryMelonIL2CPP<T> : IConfigEntry<T>
{
    private MelonPreferences_Entry<T> _configEntry;

    public ConfigEntryMelonIL2CPP(MelonPreferences_Entry<T> configEntry)
    {
        _configEntry = configEntry;
    }

    public T Get() => _configEntry.Value;
    public void Set(T value) => _configEntry.Value = value;
}