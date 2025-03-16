namespace SchulPlanerBot.Business.Models;

public class Guild
{
    public ulong Id { get; set; }

    public ulong? ChannelId { get; set; }

    public bool NotificationsEnabled { get; set; }

    public DateTimeOffset? StartNotifications { get; set; }

    public TimeSpan? BetweenNotifications { get; set; }
}
