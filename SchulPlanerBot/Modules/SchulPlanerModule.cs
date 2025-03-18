using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord.Interactions;
using System.Text;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[Group("schulplaner", "Manages settings of this app on the server.")]
public sealed class SchulPlanerModule(ILogger<SchulPlanerModule> logger, SchulPlanerManager manager) : InteractionModuleBase<CancellableSocketContext>
{
    private readonly ILogger _logger = logger;
    private readonly SchulPlanerManager _manager = manager;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("settings", "Retrieves the settings configured for the guild.")]
    public async Task GetSettingsAsync()
    {
        Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);

        StringBuilder messageBuilder = new();

        messageBuilder.Append("Text channel: ");
        if (guild.ChannelId is not null)
            messageBuilder.AppendLine($"<#{guild.ChannelId}>");
        else
            messageBuilder.AppendLine("Not yet configured");

        messageBuilder.Append("Notification: ");
        if (guild.NotificationsEnabled)
        {
            messageBuilder.Append(
                $"Every {guild.BetweenNotifications!.Value.Humanize()} " +
                $"starting on <t:{guild.StartNotifications!.Value.ToUnixTimeSeconds()}:f>");
        }
        else
        {
            messageBuilder.Append("Not yet enabled");
        }

        await RespondAsync(messageBuilder.ToString()).ConfigureAwait(false);
    }

    [SlashCommand("channel", "Configures the channel that will be used for notifications and interactions.")]
    public async Task SetChannelAsync(
        [ChannelTypes(ChannelType.Text)]
        [Summary(description: "The target channel where interactions with the bot will happen. Leave empty to remove the channel.")]
        IChannel? channel = null)
    {
        if (channel is not null)
        {
            await _manager.SetChannelAsync(Guild.Id, channel.Id, CancellationToken).ConfigureAwait(false);
            await RespondAsync($"User interaction channel successfully updated to <#{channel.Id}>").ConfigureAwait(false);
        }
        else
        {
            await _manager.RemoveChannelAsync(Guild.Id, CancellationToken).ConfigureAwait(false);

            string message =
                "User interaction channel removed. If automated notifications were enabled " +
                "they are also disabled because there is not channel to notify in.";
            await RespondAsync(message).ConfigureAwait(false);
        }
    }

    [SlashCommand("notify", "Configures when notifications will be made.")]
    public async Task SetNotificationsAsync(
        [Summary(description: "The date time where the bot should start with notifications. E.g.: 01.01.2020 10:00 or just 10:00.")] DateTime start,
        [Summary(description: "The time to wait between notifications. E.g.: 1d, 2h etc.")] TimeSpan between)
    {
        if (between < TimeSpan.FromMinutes(10))
            await RespondAsync("The provided time in between the notifications must be at least 10 min!").ConfigureAwait(false);

        UpdateResult result = await _manager.EnableNotificationsAsync(Guild.Id, start, between, CancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);

            DateTimeOffset startOffset = new(start);
            await RespondAsync($"Notifying in channel <#{guild.ChannelId}> every {between.Humanize()} starting at <t:{startOffset.ToUnixTimeSeconds()}:f>").ConfigureAwait(false);
        }
        else
        {
            switch (result.Errors.First())
            {
                case { Name: "NoChannel" }:
                    await RespondAsync("An interaction channel must be configured before enabling notifications!").ConfigureAwait(false);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    [SlashCommand("disable-notification", "Disables any automated notifications.")]
    public async Task DisableNotificationsAsync()
    {
        await _manager.DisableNotificationsAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
        await RespondAsync("Automated notifications disabled.").ConfigureAwait(false);
    }
}