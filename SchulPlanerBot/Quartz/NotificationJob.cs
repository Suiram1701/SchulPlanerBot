using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Quartz;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.UI;
using SchulPlanerBot.Discord.UI.Models;

namespace SchulPlanerBot.Quartz;

internal sealed class NotificationJob(
    ILogger<NotificationJob> logger,
    IMemoryCache cache,
    IStringLocalizer<NotificationJob> localizer,
    SchulPlanerManager manager,
    HomeworkManager homeworkManager,
    DiscordSocketClient client,
    EmbedsService embedsService,
    ComponentService componentService) : IJob
{
    private readonly ILogger _logger = logger;
    private readonly IMemoryCache _cache = cache;
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
            if (!(context.MergedJobDataMap.TryGetValue(Keys.NotificationData, out object value) && value is Notification notification))
                throw new JobExecutionException($"Unable to retrieve the required job data '{Keys.NotificationData}'!") { UnscheduleFiringTrigger = true };     // Should never happen

            SocketGuild socketGuild = _client.GetGuild(notification.GuildId);
            Guild guild = await _manager.GetGuildAsync(notification.GuildId, context.CancellationToken).ConfigureAwait(false);
            Utils.SetCulture(guild.NotificationCulture ?? socketGuild.PreferredCulture);

            ITextChannel textChannel = await ThrowWhenNotFoundAsync(socketGuild, notification.ChannelId).ConfigureAwait(false);
            
            // Real notification part
            DateTimeOffset startDateTime = DateTimeOffset.Now;
            DateTimeOffset endDateTime = notification.ObjectsIn is not null
                ? DateTimeOffset.UtcNow + notification.ObjectsIn.Value
                : notification.GetNextFiring();
            
            Homework[] homeworks = await _homeworkManager.GetHomeworksAsync(
                    guildId: notification.GuildId,
                    start: startDateTime,
                    end: endDateTime,
                    ct: context.CancellationToken)
                .ConfigureAwait(false);

            if (homeworks.Length != 0)
            {
                IEnumerable<HomeworkSubscription> userSubscriptions = await _homeworkManager.GetSubscriptionsAsync(notification.GuildId, context.CancellationToken).ConfigureAwait(false);
                ulong[] usersToMention = [.. userSubscriptions
                    .Where(s => ShouldNotify(s, homeworks))
                    .Select(s => s.UserId)];

                var message = string.Empty;
                if (usersToMention.Length > 0)
                {
                    string mentionStr = string.Join(", ", usersToMention.Select(MentionUtils.MentionUser));
                    message += $"{_localizer["homeworksNotify", mentionStr]} ";
                }
                message += _localizer["homeworks", TimestampTag.FromDateTimeOffset(endDateTime.ToLocalTime(), TimestampTagStyles.Relative)];
                
                var cacheId = Guid.NewGuid().ToString();
                HomeworkOverview overview = new(homeworks, startDateTime, endDateTime,
                    ComponentIds.CreateGetHomeworksSelectComponent(cacheId));
                _cache.Set(key: cacheId, value: overview, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(7)     // Should be enough for the user to interact with
                });
                
                Embed overviewEmbed = _embedsService.HomeworksOverview(overview);
                MessageComponent component = _componentService.HomeworkOverviewSelect(overview, cacheId);
                await textChannel.SendMessageAsync(text: message, embeds: [overviewEmbed], components: component)
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

    private static async Task<ITextChannel> ThrowWhenNotFoundAsync(IGuild guild, ulong textChannelId)
    {
        ITextChannel? channel = await guild.GetTextChannelAsync(textChannelId).ConfigureAwait(false);
        if (channel is null)
        {
            throw new JobExecutionException("Unable to retrieve notification channel for guild!")
            {
                UnscheduleFiringTrigger = true
            };
        }

        return channel;
    }

    private bool ShouldNotify(HomeworkSubscription subscription, IEnumerable<Homework> homeworks)
    {
        return homeworks.Any(h =>
        {
            return subscription.AnySubject
                ? !subscription.Exclude.Any(s => _manager.SubjectNameComparer.Equals(s, h.Subject))     // True when any homework doesn't have a excluded subject
                : subscription.Include.Any(s => _manager.SubjectNameComparer.Equals(s, h.Subject));     // True when any homework contains the included subject
        });
    }
}
