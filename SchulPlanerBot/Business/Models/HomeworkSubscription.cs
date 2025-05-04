namespace SchulPlanerBot.Business.Models;

public class HomeworkSubscription
{
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public bool AnySubject { get; set; }

    public HashSet<string?> Include { get; set; } = [];

    public HashSet<string?> Exclude { get; set; } = [];
}
