using System.Runtime.CompilerServices;

namespace OverlayDearImGui;

internal interface ILog
{
    internal void Debug(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);

    internal void Error(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);

    internal void Fatal(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);

    internal void Info(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);

    internal void Message(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);

    internal void Warning(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0);
}

internal static class Log
{
    private static ILog _log;

    internal static void Init(ILog log)
    {
        _log = log;
    }

    internal static void Debug(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Debug(data, file, member, line);

    internal static void Error(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Error(data, file, member, line);

    internal static void Fatal(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Fatal(data, file, member, line);

    internal static void Info(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Info(data, file, member, line);

    internal static void Message(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Message(data, file, member, line);

    internal static void Warning(object data,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) => _log.Warning(data, file, member, line);
}