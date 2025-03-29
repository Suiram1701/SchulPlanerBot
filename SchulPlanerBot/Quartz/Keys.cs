using Quartz;

namespace SchulPlanerBot.Quartz;

public static class Keys
{
    public static readonly JobKey NotificationJob = new("notify-channel", group: "homeworks");

    public static TriggerKey NotificationTrigger(ulong guildId) => new($"notify-{guildId}", group: "homeworks");

    public static readonly JobKey DeleteHomeworksJob = new("delete", group: "homeworks");

    public static readonly TriggerKey DeleteHomeworksKey = new("delete", group: "homeworks");

    #region Data
    public const string GuildIdData = nameof(GuildIdData);
    #endregion
}
