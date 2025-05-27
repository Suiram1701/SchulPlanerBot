namespace SchulPlanerBot.Business.Models;

public class Homework
{
    public Guid Id { get; set; }

    public ulong GuildId { get; set; }

    public DateTimeOffset Due { get; set; }

    public string? Subject { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ulong CreatedBy { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }

    public ulong? LastModifiedBy { get; set; }
}
