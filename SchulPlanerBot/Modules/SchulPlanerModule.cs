using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using System.Text;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[Group("schulplaner", "Manages settings of the bot on the guild.")]
public sealed class SchulPlanerModule(ILogger<SchulPlanerModule> logger, IStringLocalizer<SchulPlanerModule> localizer, SchulPlanerManager manager) : InteractionModuleBase<ExtendedSocketContext>
{
    private readonly ILogger _logger = logger;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly SchulPlanerManager _manager = manager;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("settings", "Retrieves the settings configured for the guild.")]
    public async Task GetSettingsAsync()
    {
        Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);

        string message = string.Concat([
            guild.ChannelId is not null
                ? _localizer["settings.response.channel", Utilities.Mention(guild.ChannelId.Value, MentionType.Channel)]
                : _localizer["settings.response.noChannel"],
            '\n',
            guild.NotificationsEnabled
                ? _localizer["settings.response.notifications",
                    guild.BetweenNotifications.Value.Humanize(),
                    guild.StartNotifications.Value.Timestamp(TimestampKind.ShortDateTime)]
                : _localizer["settings.response.noNotifications"]
            ]);
        await RespondAsync(message).ConfigureAwait(false);
    }

    [SlashCommand("channel", "Configures the channel that will be used for notifications and interactions.")]
    public async Task SetChannelAsync([ChannelTypes(ChannelType.Text)] IChannel? channel = null)
    {
        if (channel is not null)
        {
            await _manager.SetChannelAsync(Guild.Id, channel.Id, CancellationToken).ConfigureAwait(false);
            await RespondAsync(_localizer["channel.updated", channel.Mention()]).ConfigureAwait(false);
        }
        else
        {
            await _manager.RemoveChannelAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
            await RespondAsync(_localizer["channel.removed"]).ConfigureAwait(false);
        }
    }

    [SlashCommand("notifications", "Configures when notifications will be made.")]
    public async Task SetNotificationsAsync(DateTime start, TimeSpan between)
    {
        if (between < TimeSpan.FromMinutes(10))
            await RespondAsync(_localizer["notifications.tooShort", between.Humanize()]).ConfigureAwait(false);

        UpdateResult result = await _manager.EnableNotificationsAsync(Guild.Id, start, between, CancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
            DateTimeOffset startOffset = new(start);

            await RespondAsync(_localizer[
                "notifications.updated",
                Utilities.Mention(guild.ChannelId!.Value, MentionType.Channel),
                between.Humanize(),
                startOffset.Timestamp(TimestampKind.ShortDateTime)])
                .ConfigureAwait(false);
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

    [SlashCommand("disable-notifications", "Disables any automated notifications.")]
    public async Task DisableNotificationsAsync()
    {
        await _manager.DisableNotificationsAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
        await RespondAsync(_localizer["disable-notifications.disabled"]).ConfigureAwait(false);
    }
}