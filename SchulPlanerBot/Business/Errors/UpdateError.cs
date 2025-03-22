namespace SchulPlanerBot.Business.Errors;

public record UpdateError(string Name, string Description)
{
    public override string ToString() => $"{Name}: {Description}";
}