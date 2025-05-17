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
using System.Text;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
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

        StringBuilder build = new();
        if (guild.NotificationCulture is not null)
        {
            build.AppendLine(_localizer["settings.locale", guild.NotificationCulture.DisplayName]);
        }
        else
        {
            CultureInfo guildCulture = new(Context.Interaction.GuildLocale);
            build.AppendLine(_localizer["settings.guildLocale", guildCulture.DisplayName]);
        }

        if (guild.Notifications.Count > 0)
        {
            build.AppendLine(_localizer["settings.notification"]);
            foreach (Notification notification in guild.Notifications)
            {
                TimestampTag startTag = new(notification.StartAt, style: TimestampTagStyles.ShortDateTime);
                string betweenStr = notification.Between.Humanize();
                string channelStr = MentionUtils.MentionChannel(notification.ChannelId);

                build.AppendLine($"- {_localizer["settings.notification.item", startTag, betweenStr, channelStr]}");
            }
        }
        else
        {
            build.AppendLine(_localizer["settings.notification.none"]);
        }

        build.AppendLine(_localizer["settings.homeworksDeleteAfterDue", guild.DeleteHomeworksAfterDue.Humanize()]);

        await RespondAsync(build.ToString()).ConfigureAwait(false);
    }

    [SlashCommand("locale", "Sets the locale to use on notifications. By default the guilds locale is used.")]
    public async Task SetGuildLocaleAsync(CultureInfo? culture)
    {
        UpdateResult updateResult = culture is not null
            ? await _manager.SetNotificationCultureAsync(Guild.Id, culture, CancellationToken).ConfigureAwait(false)
            : await _manager.RemoveNotificationCultureAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
        if (updateResult.Success)
        {
            CultureInfo notificationLocale = culture ?? new(Context.Interaction.GuildLocale);
            await RespondAsync(_localizer["locale.updated", notificationLocale.DisplayName]).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(updateResult.Errors, _logger).ConfigureAwait(false);
        }
    }

    [SlashCommand("add-notification", "Adds a notifications to make on this guild.")]
    public async Task SetNotificationsAsync(DateTimeOffset start, TimeSpan between, [ChannelTypes(ChannelType.Text)] IChannel channel, [Summary(name: "objects-in")] TimeSpan? objectsIn = null)
    {
        UpdateResult addResult = await _manager.AddNotificationAsync(Guild.Id, start, between, objectsIn ?? between, channel.Id, CancellationToken).ConfigureAwait(false);
        if (addResult.Success)
        {
            await RespondAsync(_localizer[
                    "notification.added",
                    MentionUtils.MentionChannel(channel.Id),
                    between.Humanize(),
                    TimestampTag.FromDateTimeOffset(start, TimestampTagStyles.ShortDateTime)])
                .ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(addResult.Errors, _logger).ConfigureAwait(false);
        }
    }

    [SlashCommand("remove-notification", "Removes a specific notification for this guild")]
    public async Task DisableNotificationsAsync(DateTimeOffset start)
    {
        UpdateResult disableResult = await _manager.RemoveNotificationAsync(Guild.Id, start, CancellationToken).ConfigureAwait(false);
        if (disableResult.Success)
        {
            TimestampTag startTag = new(start, style: TimestampTagStyles.ShortDateTime);
            await RespondAsync(_localizer["notification.removed", startTag]).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(disableResult.Errors, _logger).ConfigureAwait(false);
        }
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