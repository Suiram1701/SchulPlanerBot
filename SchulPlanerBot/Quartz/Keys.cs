using Quartz;

namespace SchulPlanerBot.Quartz;

public static class Keys
{
    public static readonly JobKey NotificationJob = new("notify-channel", group: "notifications");

    public static TriggerKey NotificationTrigger(ulong guildId, ulong channelId) => new($"notify-{guildId}-{channelId}", group: "notifications");

    public static readonly JobKey DeleteHomeworksJob = new("delete", group: "homeworks");

    public static readonly TriggerKey DeleteHomeworksKey = new("delete", group: "homeworks");

    #region Data
    public const string NotificationData = nameof(NotificationData);
    #endregion
}
