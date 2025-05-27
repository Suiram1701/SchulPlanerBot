using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SchulPlanerBot.Business.Models;

public class Guild
{
    public ulong Id { get; set; }

    internal string? NotificationLocale { get; set; }

    [NotMapped]
    public CultureInfo? NotificationCulture
    {
        get => !string.IsNullOrEmpty(NotificationLocale) ? new CultureInfo(NotificationLocale) : null;
        set => NotificationLocale = value?.ToString();
    }

    public IList<Notification> Notifications { get; set; } = [];

    public TimeSpan DeleteHomeworksAfterDue { get; set; }
}
