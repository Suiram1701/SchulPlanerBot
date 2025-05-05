namespace SchulPlanerBot.Business.Models;

public class HomeworkSubscription
{
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public bool AnySubject { get; set; }

    public string?[] Include { get; set; } = [];

    public string?[] Exclude { get; set; } = [];
}
