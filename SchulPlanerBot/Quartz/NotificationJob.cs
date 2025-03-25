using Discord;
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
    DiscordSocketClient client,
    EmbedsService embedsService) : IJob
{
    private readonly ILogger _logger = logger;
    private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly SchulPlanerManager _manager = manager;
    private readonly DiscordSocketClient _client = client;
    private readonly EmbedsService _embedsService = embedsService;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (!(context.MergedJobDataMap.TryGetString(DataKeys.GuildId, out string? guildIdStr) && ulong.TryParse(guildIdStr, out ulong guildId)))
            {
                throw new JobExecutionException($"Unable to retrieve the required job data '{DataKeys.GuildId}'!");
            }

            Guild guild = await _manager.GetGuildAsync(guildId, context.CancellationToken).ConfigureAwait(false);
            if (!guild.NotificationsEnabled || guild.ChannelId is null)
            {
                _logger.LogWarning("Notifications not right configured for guild! Removing notification trigger...");
                await UnscheduleTriggerAsync(context).ConfigureAwait(false);

                throw new JobExecutionException("Notifications not right configured for guild!");
            }

            IChannel? channel = await _client.GetChannelAsync(guild.ChannelId.Value).ConfigureAwait(false);
            if (channel is not ITextChannel textChannel)
            {
                _logger.LogWarning("Unable to retrieve the notification text channel for guild! Removing notification trigger...");
                await UnscheduleTriggerAsync(context).ConfigureAwait(false);

                throw new JobExecutionException("Unable to retrieve notification channel for guild!");
            }

            // Real notification starts
            DateTimeOffset endDateTime = DateTimeOffset.UtcNow + guild.BetweenNotifications.Value;
            IEnumerable<Homework> homeworks = await _manager.GetHomeworksAsync(guildId, start: DateTime.UtcNow, end: endDateTime, ct: context.CancellationToken).ConfigureAwait(false);

            Embed[] embeds = [.. homeworks.Select(_embedsService.Homework)];
            if (embeds.Length > 0)
            {
                await textChannel.SendMessageAsync(_localizer["homeworks", Utilities.Timestamp(endDateTime, TimestampKind.Relative)]).ConfigureAwait(false);

                int sentEmbeds = 0;
                do
                {
                    Embed[] embedPart = [.. embeds.Skip(sentEmbeds).Take(DiscordConfig.MaxEmbedsPerMessage)];
                    await textChannel.SendMessageAsync(embeds: embedPart, allowedMentions: AllowedMentions.None).ConfigureAwait(false);

                    sentEmbeds += embedPart.Length;
                }
                while (sentEmbeds < embeds.Length);
            }
            else
            {
                await textChannel.SendMessageAsync(_localizer["noHomeworks", Utilities.Timestamp(endDateTime, TimestampKind.Relative)]).ConfigureAwait(false);
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
}
