using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SchulPlanerBot.Business.Models;

public class Guild
{
    public ulong Id { get; set; }

    public ulong? ChannelId { get; set; }

    [MemberNotNullWhen(true, nameof(ChannelId), nameof(StartNotifications), nameof(BetweenNotifications))]
    public bool NotificationsEnabled { get; set; }

    public DateTimeOffset? StartNotifications { get; set; }

    public TimeSpan? BetweenNotifications { get; set; }

    public string? NotificationLocale { get; set; }

    [NotMapped]
    public CultureInfo? NotificationCulture
    {
        get => !string.IsNullOrEmpty(NotificationLocale) ? new(NotificationLocale) : null;
        set => NotificationLocale = value?.ToString();
    }
}
