using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using System.Globalization;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
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
                ? _localizer["settings.channel", MentionUtils.MentionChannel(guild.ChannelId!.Value)]
                : _localizer["settings.noChannel"],
            '\n',
            guild.NotificationsEnabled
                ? _localizer["settings.notifications",
                    guild.BetweenNotifications.Value.Humanize(),
                    TimestampTag.FromDateTimeOffset(guild.StartNotifications.Value, TimestampTagStyles.ShortDateTime)]
                : _localizer["settings.noNotifications"],
            '\n',
            guild.NotificationCulture is not null
                ? _localizer["settings.locale", guild.NotificationCulture.DisplayName]
                : _localizer["settings.noLocale"],
            "\n\n",
            _localizer["settings.homeworksDeleteAfterDue", guild.DeleteHomeworksAfterDue.Humanize()]
            ]);
        await RespondAsync(message).ConfigureAwait(false);
    }

    [SlashCommand("notifications", "Configures when notifications will be made.")]
    public async Task SetNotificationsAsync([ChannelTypes(ChannelType.Text)] IChannel channel, DateTime start, TimeSpan between, CultureInfo? locale = null)
    {
        await _manager.SetChannelAsync(Guild.Id, channel.Id, CancellationToken).ConfigureAwait(false);
        await _manager.SetNotificationCultureAsync(Guild.Id, locale).ConfigureAwait(false);
        UpdateResult enableResult = await _manager.EnableNotificationsAsync(Guild.Id, start, between, CancellationToken).ConfigureAwait(false);

        if (enableResult.Success)
        {
            Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
            DateTimeOffset startOffset = new(start);

            await RespondAsync(_localizer[
                    "notifications.updated",
                    MentionUtils.MentionChannel(guild.ChannelId!.Value),
                    between.Humanize(),
                    TimestampTag.FromDateTimeOffset(startOffset, TimestampTagStyles.ShortDateTime)])
                .ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(enableResult.Errors, _logger).ConfigureAwait(false);
        }
    }

    [SlashCommand("disable-notifications", "Disables any automated notifications.")]
    public async Task DisableNotificationsAsync()
    {
        UpdateResult disableResult = await _manager.DisableNotificationsAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
        if (disableResult.Success)
            await RespondAsync(_localizer["disable-notifications.disabled"]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(disableResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("delete-homeworks", "Sets the time a homework gets deleted after its due.")]
    public async Task SetDeleteHomeworksAfterDueAsync(TimeSpan after)
    {
        UpdateResult setResult = await _manager.SetDeleteHomeworkAfterDueAsync(Guild.Id, after, CancellationToken).ConfigureAwait(false);
        if (setResult.Success)
            await RespondAsync(_localizer["homeworksDeletedAfter.updated", after.Humanize()]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(setResult.Errors, _logger).ConfigureAwait(false);
    }
}