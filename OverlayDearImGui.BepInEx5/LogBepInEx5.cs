using BepInEx.Logging;

namespace OverlayDearImGui
{
    internal class LogBepInEx5 : ILog
    {
        private ManualLogSource _logSource;

        internal LogBepInEx5(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        private static string Format(object data, string file, string member, int line)
        {
            return $"[{file}:{line} ({member})] {data}";
        }

        void ILog.Debug(object data, string file, string member, int line) => _logSource.LogDebug(Format(data, file, member, line));
        void ILog.Error(object data, string file, string member, int line) => _logSource.LogError(Format(data, file, member, line));
        void ILog.Fatal(object data, string file, string member, int line) => _logSource.LogFatal(Format(data, file, member, line));
        void ILog.Info(object data, string file, string member, int line) => _logSource.LogInfo(Format(data, file, member, line));
        void ILog.Message(object data, string file, string member, int line) => _logSource.LogMessage(Format(data, file, member, line));
        void ILog.Warning(object data, string file, string member, int line) => _logSource.LogWarning(Format(data, file, member, line));
    }
}