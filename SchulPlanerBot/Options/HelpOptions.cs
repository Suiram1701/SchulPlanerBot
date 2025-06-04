using System.ComponentModel.DataAnnotations;

namespace SchulPlanerBot.Options;

public class HelpOptions
{
    [Required]
    public string Maintainer { get; set; } = string.Empty;

    public string? ProjectWebsite { get; set; }

    public string? SupportDiscordGuild { get; set; }
    
    public string? CronHelpPage { get; set; }
}
