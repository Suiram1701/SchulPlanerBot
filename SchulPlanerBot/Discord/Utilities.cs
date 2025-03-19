using Discord;

namespace SchulPlanerBot.Discord;

public static class Utilities
{
    public static string Mention(IUser user) => $"<@{user.Id}>";

    public static string Mention(IChannel channel) => $"<#{channel.Id}>";

    public static string Mention(ulong id, MentionType type)
    {
        char prefix = type switch
        {
            MentionType.User => '@',
            MentionType.Channel => '#',
            _ => throw new NotImplementedException()
        };
        return $"<{prefix}{id}>";
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
}
