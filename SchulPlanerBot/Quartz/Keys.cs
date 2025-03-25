using Quartz;

namespace SchulPlanerBot.Quartz;

public static class Keys
{
    public static readonly JobKey NotificationJob = new("notify-channel", group: "discord");

    public static TriggerKey NotificationTrigger(ulong guildId) => new($"notify-{guildId}", group: "discord");
}
