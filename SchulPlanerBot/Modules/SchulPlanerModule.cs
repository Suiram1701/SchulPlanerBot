using Discord;
using Discord.Interactions;

namespace SchulPlanerBot.Modules;

[CommandContextType(InteractionContextType.Guild)]
[RequireUserPermission(GuildPermission.ManageGuild)]
[Group("schulplaner", "Manages settings of this app on the server.")]
public sealed class SchulPlanerModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("settings", "Retrieves the settings configured for the guild.")]
    public Task GetSettingsAsync()
    {
        throw new NotImplementedException();
    }

    [SlashCommand("channel", "Configures the channel that will be used for notifications and interactions.")]
    public Task SetChannelAsync(
        [ChannelTypes(ChannelType.Text), Summary(description: "The target channel where interactions with the bot will happen.")] IChannel channel)
    {
        throw new NotImplementedException();
    }

    [SlashCommand("notify", "Configures when notifications will be made. ")]
    public Task SetNotifications(
        [Summary(description: "The date time where the bot should start with notifications.")] DateTime start,
        [Summary(description: "The time to wait between notifications.")] TimeSpan between)
    {
        throw new NotImplementedException();
    }
}