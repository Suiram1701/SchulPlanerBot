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
using Quartz;

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
                TimestampTag nextTag = new(notification.GetNextFiring(), style: TimestampTagStyles.ShortDateTime);
                string channelStr = MentionUtils.MentionChannel(notification.ChannelId);
                
                if (notification.ObjectsIn is null)
                    build.AppendLine($"- {_localizer["settings.notification.item", notification.CronExpression, nextTag, channelStr]}");
                else
                    build.AppendLine($"- {_localizer["settings.notification.item-objectsIn", notification.CronExpression, nextTag, channelStr, notification.ObjectsIn.Value.Humanize()]}");
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
            CultureInfo notificationLocale = culture ?? new CultureInfo(Context.Interaction.GuildLocale);
            await RespondAsync(_localizer["locale.updated", notificationLocale.DisplayName]).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(updateResult.Errors, _logger).ConfigureAwait(false);
        }
    }

    [SlashCommand("add-notification", "Adds a notifications to make on this guild.")]
    public async Task AddNotificationAsync([ChannelTypes(ChannelType.Text)] IChannel channel, string cron, [Summary(name: "objects-in")] TimeSpan? objectsIn = null)
    {
        UpdateResult addResult = await _manager.AddNotificationAsync(Guild.Id, cron, objectsIn, channel.Id, CancellationToken).ConfigureAwait(false);
        if (addResult.Success)
        {
            DateTimeOffset next = new CronExpression(cron).GetNextValidTimeAfter(DateTimeOffset.Now)!.Value;
            
            await RespondAsync(_localizer[
                    "notification.added",
                    MentionUtils.MentionChannel(channel.Id),
                    TimestampTag.FromDateTimeOffset(next, TimestampTagStyles.ShortDateTime)])
                .ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(addResult.Errors, _logger).ConfigureAwait(false);
        }
    }

    [SlashCommand("remove-notification", "Removes a specific notification for this guild")]
    public async Task RemoveNotificationAsync([ChannelTypes(ChannelType.Text)] IChannel channel)
    {
        UpdateResult disableResult = await _manager.RemoveNotificationAsync(Guild.Id, channel.Id, CancellationToken).ConfigureAwait(false);
        if (disableResult.Success)
            await RespondAsync(_localizer["notification.removed", MentionUtils.MentionChannel(channel.Id)]).ConfigureAwait(false);
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