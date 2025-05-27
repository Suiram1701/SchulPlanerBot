using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
public sealed class RootModule(IStringLocalizer<RootModule> localizer, IOptionsSnapshot<HelpOptions> optionsSnapshot) : InteractionModuleBase<ExtendedSocketContext>
{
    private readonly IStringLocalizer _localizer = localizer;
    private readonly HelpOptions _helpOptions = optionsSnapshot.Value;

    [SlashCommand("help", "Retrieves information to help with this bot")]
    public Task GetHelpAsync()
    {
        return RespondAsync(
            _localizer[
                "help",
                _helpOptions.Maintainer,
                _helpOptions.ProjectWebsite ?? _localizer["help.notAvailable"],
                _helpOptions.SupportDiscordGuild ?? _localizer["help.notAvailable"]
            ]);
    }
}
