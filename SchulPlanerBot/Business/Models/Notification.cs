using Quartz;

namespace SchulPlanerBot.Business.Models;

public class Notification
{
    public ulong GuildId { get; set; }
    
    public ulong ChannelId { get; set; }
    
    public string CronExpression { get; set; } = string.Empty;

    public DateTimeOffset GetNextFiring() =>
        new CronExpression(CronExpression).GetNextValidTimeAfter(DateTimeOffset.Now)!.Value;
    
    public TimeSpan? ObjectsIn { get; set; }
}
