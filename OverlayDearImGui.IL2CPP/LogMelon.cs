using MelonLoader;

namespace OverlayDearImGui.MelonIL2CPP;

internal class LogMelon : ILog
{
    private MelonLogger.Instance _logSource;

    internal LogMelon(MelonLogger.Instance logSource)
    {
        _logSource = logSource;
    }

    private static string Format(object data, string file, string member, int line)
    {
        return $"[{file}:{line} ({member})] {data}";
    }

    void ILog.Debug(object data, string file, string member, int line) => _logSource.Msg(Format(data, file, member, line));
    void ILog.Error(object data, string file, string member, int line) => _logSource.Error(Format(data, file, member, line));
    void ILog.Fatal(object data, string file, string member, int line) => _logSource.BigError(Format(data, file, member, line));
    void ILog.Info(object data, string file, string member, int line) => _logSource.MsgPastel(Format(data, file, member, line));
    void ILog.Message(object data, string file, string member, int line) => _logSource.Msg(Format(data, file, member, line));
    void ILog.Warning(object data, string file, string member, int line) => _logSource.Warning(Format(data, file, member, line));
}