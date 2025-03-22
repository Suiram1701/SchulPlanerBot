using System.Diagnostics.CodeAnalysis;

namespace SchulPlanerBot.Business.Models;

public class Guild
{
    public ulong Id { get; set; }

    public ulong? ChannelId { get; set; }

    [MemberNotNullWhen(true, nameof(StartNotifications), nameof(BetweenNotifications))]
    public bool NotificationsEnabled { get; set; }

    public DateTimeOffset? StartNotifications { get; set; }

    public TimeSpan? BetweenNotifications { get; set; }
}
