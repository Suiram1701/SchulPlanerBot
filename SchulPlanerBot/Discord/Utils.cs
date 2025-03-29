using Discord;

namespace SchulPlanerBot.Discord;

public static class Utils
{
    public static LogLevel ConvertLogLevel(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => throw new NotImplementedException()
        };
    }

    public static LogSeverity ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical => LogSeverity.Critical,
            LogLevel.Error => LogSeverity.Error,
            LogLevel.Warning => LogSeverity.Warning,
            LogLevel.Information => LogSeverity.Info,
            LogLevel.Trace => LogSeverity.Verbose,
            LogLevel.Debug => LogSeverity.Debug,
            _ => throw new NotImplementedException()
        };
    }

    public static string Timestamp(DateTimeOffset dateTime, TimestampKind kind)
    {
        string suffix = kind switch
        {
            TimestampKind.Default => string.Empty,
            TimestampKind.ShortTime => ":t",
            TimestampKind.LongTime => ":T",
            TimestampKind.ShortDate => ":d",
            TimestampKind.LongDate => ":D",
            TimestampKind.ShortDateTime => ":f",
            TimestampKind.LongDateTime => ":F",
            TimestampKind.Relative => ":R",
            _ => throw new NotImplementedException()
        };
        return $"<t:{dateTime.ToUnixTimeSeconds()}{suffix}>";
    }

    public enum TimestampKind
    {
        Default,
        ShortTime,
        LongTime,
        ShortDate,
        LongDate,
        ShortDateTime,
        LongDateTime,
        Relative
    }
}
