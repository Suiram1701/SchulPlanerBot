using Discord;
using System.ComponentModel.DataAnnotations;

namespace SchulPlanerBot.Options;

public class DiscordClientOptions
{
    [Required]
    public TokenType TokenType { get; set; }

    [Required]
    public string Token { get; set; } = default!;
}
