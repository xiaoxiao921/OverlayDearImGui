using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace OverlayDearImGui
{
    internal static class Log
    {
        private static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        private static string Format(object data, string file, string member, int line)
        {
            return $"[{file}:{line} ({member})] {data}";
        }

        internal static void Debug(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogDebug(Format(data, file, member, line));

        internal static void Error(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogError(Format(data, file, member, line));

        internal static void Fatal(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogFatal(Format(data, file, member, line));

        internal static void Info(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogInfo(Format(data, file, member, line));

        internal static void Message(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogMessage(Format(data, file, member, line));

        internal static void Warning(object data,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
            => _logSource.LogWarning(Format(data, file, member, line));
    }
}