namespace SchulPlanerBot.Business.Models;

public class Homework
{
    public Guid Id { get; set; }

    public ulong OwnerId { get; set; }

    public DateTimeOffset Due { get; set; }

    public string? Subject { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Details { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }

    public ulong CreatedBy { get; set; }
}
