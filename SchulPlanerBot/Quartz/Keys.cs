using Quartz;

namespace SchulPlanerBot.Quartz;

public static class Keys
{
    public static readonly JobKey NotificationJob = new("notify-channel", group: "homeworks");

    public static TriggerKey NotificationTrigger(ulong guildId, DateTimeOffset startAt) => new($"notify-{guildId}-{startAt.ToUnixTimeSeconds()}", group: "homeworks");

    public static readonly JobKey DeleteHomeworksJob = new("delete", group: "homeworks");

    public static readonly TriggerKey DeleteHomeworksKey = new("delete", group: "homeworks");

    #region Data
    public const string GuildIdData = nameof(GuildIdData);

    public const string NotificationData = nameof(NotificationData);
    #endregion
}
