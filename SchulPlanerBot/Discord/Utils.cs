﻿using Discord;
using System.Globalization;

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

    public static string UseAnsiFormat(string text, AnsiColor color, AnsiFormat format = AnsiFormat.Normal) =>
        $"```ansi\n\e[{(int)format};{(int)color}m{text}\e[0m\n```";

    public enum AnsiFormat
    {
        Normal = 0,
        Bold = 1,
        Underline = 2
    }

    public enum AnsiColor
    {
        Gray = 30,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Pink = 35,
        Cyan = 36,
        White = 37
    }

    internal static void SetCulture(CultureInfo info)
    {
        if (!Program.SupportedCultures
            .Select(c => c.IsNeutralCulture ? c : c.Parent)
            .Contains(info.IsNeutralCulture ? info : info.Parent))
        {
            info = Program.SupportedCultures[0];     // 0 is English
        }

        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
        Thread.CurrentThread.CurrentCulture = info;
        Thread.CurrentThread.CurrentUICulture = info;
    }
}
