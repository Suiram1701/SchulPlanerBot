using System.ComponentModel.DataAnnotations;

namespace SchulPlanerBot.Options;

public class HelpOptions
{
    [Required]
    public string Maintainer { get; set; } = default!;

    public string? ProjectWebsite { get; set; }

    public string? SupportDiscordGuild { get; set; }
}
