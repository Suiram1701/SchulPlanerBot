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
    IStringLocalizer<NotificationJob> localizer,
    SchulPlanerManager manager,
    HomeworkManager homeworkManager,
    DiscordSocketClient client,
    EmbedsService embedsService,
    ComponentService componentService) : IJob
{
    private readonly ILogger _logger = logger;
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
                throw new JobExecutionException($"Unable to retrieve the required job data '{Keys.GuildIdData}'!");

            if (!(context.MergedJobDataMap.TryGetValue(Keys.NotificationData, out object value) && value is Notification notification))
                throw new JobExecutionException($"Unable to retrieve the required job data '{Keys.NotificationData}'!");

            Guild guild = await _manager.GetGuildAsync(guildId, context.CancellationToken).ConfigureAwait(false);
            if (guild.NotificationCulture is not null)
            {
                Utils.SetCulture(guild.NotificationCulture);
            }
            else
            {
                RestGuild restGuild = await _client.Rest.GetGuildAsync(guildId).ConfigureAwait(false);
                Utils.SetCulture(restGuild.PreferredCulture);
            }

            IChannel? channel = await _client.GetChannelAsync(notification.ChannelId).ConfigureAwait(false);
            if (channel is not ITextChannel textChannel)
            {
                _logger.LogWarning("Unable to retrieve the notification text channel for guild! Removing notification trigger...");
                throw new JobExecutionException("Unable to retrieve notification channel for guild!")
                {
                    UnscheduleFiringTrigger = true
                };
            }

            // Real notification part
            DateTimeOffset endDateTime = DateTimeOffset.UtcNow + notification.Between;
            IEnumerable<Homework> homeworks = await _homeworkManager.GetHomeworksAsync(guildId, start: DateTime.UtcNow, end: endDateTime, ct: context.CancellationToken).ConfigureAwait(false);
            homeworks = [.. homeworks.OrderBy(h => h.Due)];

            if (homeworks.Any())
            {
                IEnumerable<HomeworkSubscription> userSubscriptions = await _homeworkManager.GetSubscriptionsAsync(guildId, context.CancellationToken).ConfigureAwait(false);
                ulong[] usersToMention = [.. userSubscriptions
                    .Where(s => ShouldNotify(s, homeworks))
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
                await textChannel.SendMessageAsync(
                    text: message,
                    embeds: [overviewEmbed],
                    components: selectComp,
                    allowedMentions: new AllowedMentions(AllowedMentionTypes.Users),
                    flags: MessageFlags.SuppressNotification)
                    .ConfigureAwait(false);
            }
            else if (_manager.Options.MessageWhenNoHomework)
            {
                await textChannel.SendMessageAsync(
                    text: _localizer["noHomeworks", TimestampTag.FromDateTimeOffset(endDateTime.ToLocalTime(), TimestampTagStyles.Relative)],
                    flags: MessageFlags.SuppressNotification)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not JobExecutionException)
        {
            _logger.LogError(ex, "An unexpected error occurred during execution!");
            throw new JobExecutionException("An unexpected error occurred during job execution!", ex);
        }
    }

    private bool ShouldNotify(HomeworkSubscription subscription, IEnumerable<Homework> homeworks)
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
