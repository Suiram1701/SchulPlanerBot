using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;
using Quartz;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;

namespace SchulPlanerBot.Quartz;

internal sealed class NotificationJob(
    ILogger<NotificationJob> logger,
    ISchedulerFactory schedulerFactory,
    IStringLocalizer<NotificationJob> localizer,
    SchulPlanerManager manager,
    HomeworkManager homeworkManager,
    DiscordSocketClient client,
    EmbedsService embedsService,
    ComponentService componentService) : IJob
{
    private readonly ILogger _logger = logger;
    private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly SchulPlanerManager _manager = manager;
    private readonly HomeworkManager _homeworkManager = homeworkManager;
    private readonly DiscordSocketClient _client = client;
    private readonly EmbedsService _embedsService = embedsService;
    private readonly ComponentService _componentService = componentService;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // Preparing
            if (!(context.MergedJobDataMap.TryGetString(Keys.GuildIdData, out string? guildIdStr) && ulong.TryParse(guildIdStr, out ulong guildId)))
            {
                throw new JobExecutionException($"Unable to retrieve the required job data '{Keys.GuildIdData}'!");
            }

            Guild guild = await _manager.GetGuildAsync(guildId, context.CancellationToken).ConfigureAwait(false);
            if (!guild.NotificationsEnabled || guild.ChannelId is null)
            {
                _logger.LogWarning("Notifications not right configured for guild! Removing notification trigger...");
                await UnscheduleTriggerAsync(context).ConfigureAwait(false);

                throw new JobExecutionException("Notifications not right configured for guild!");
            }

            if (guild.NotificationCulture is not null)
            {
                Utils.SetCulture(guild.NotificationCulture);
            }
            else
            {
                RestGuild restGuild = await _client.Rest.GetGuildAsync(guildId).ConfigureAwait(false);
                Utils.SetCulture(restGuild.PreferredCulture);
            }

            IChannel? channel = await _client.GetChannelAsync(guild.ChannelId.Value).ConfigureAwait(false);
            if (channel is not ITextChannel textChannel)
            {
                _logger.LogWarning("Unable to retrieve the notification text channel for guild! Removing notification trigger...");
                await UnscheduleTriggerAsync(context).ConfigureAwait(false);

                throw new JobExecutionException("Unable to retrieve notification channel for guild!");
            }

            // Real notification part
            DateTimeOffset endDateTime = DateTimeOffset.UtcNow + guild.BetweenNotifications.Value;
            IEnumerable<Homework> homeworks = await _homeworkManager.GetHomeworksAsync(guildId, start: DateTime.UtcNow, end: endDateTime, ct: context.CancellationToken).ConfigureAwait(false);
            homeworks = [.. homeworks.OrderBy(h => h.Due)];

            if (homeworks.Any())
            {
                IEnumerable<HomeworkSubscription> userSubscriptions = await _homeworkManager.GetSubscriptionsAsync(guildId, context.CancellationToken).ConfigureAwait(false);
                ulong[] usersToMention = [.. userSubscriptions
                    .Where(s => ShouldNotifyUser(s, homeworks))
                    .Select(s => s.UserId)];

                string message = string.Empty;
                if (usersToMention.Length > 0)
                {
                    string mentionStr = string.Join(", ", usersToMention.Select(MentionUtils.MentionUser));
                    message += $"{_localizer["homeworksNotify", mentionStr]} ";
                }
                message += _localizer["homeworks", TimestampTag.FromDateTimeOffset(endDateTime.ToLocalTime(), TimestampTagStyles.Relative)];

                Embed overviewEmbed = _embedsService.HomeworksOverview(homeworks, DateTimeOffset.UtcNow, endDateTime);
                MessageComponent selectComp = _componentService.SelectHomework(homeworks, cacheId: Guid.NewGuid().ToString());
                await textChannel.SendMessageAsync(message, embeds: [overviewEmbed], components: selectComp, allowedMentions: AllowedMentions.All).ConfigureAwait(false);
            }
            else
            {
                await textChannel.SendMessageAsync(_localizer["noHomeworks", TimestampTag.FromDateTimeOffset(endDateTime.ToLocalTime(), TimestampTagStyles.Relative)]).ConfigureAwait(false);
            }

        }
        catch (Exception ex) when (ex is not JobExecutionException)
        {
            _logger.LogError(ex, "An unexpected error occurred during execution!");
            throw new JobExecutionException("An unexpected error occurred during job execution!", ex);
        }
    }

    private async Task UnscheduleTriggerAsync(IJobExecutionContext context)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(context.CancellationToken).ConfigureAwait(false);
        await scheduler.UnscheduleJob(context.RecoveringTriggerKey, context.CancellationToken).ConfigureAwait(false);
    }

    private bool ShouldNotifyUser(HomeworkSubscription subscription, IEnumerable<Homework> homeworks)
    {
        if (subscription.AnySubject)
            return true;
        if (subscription.NoSubject && homeworks.Any(h => string.IsNullOrWhiteSpace(h.Subject)))
            return true;
        if (subscription.Include.Any(s => homeworks.Select(h => h.Subject).Contains(s, _manager.SubjectNameComparer)))
            return true;
        return false;
    }
}
