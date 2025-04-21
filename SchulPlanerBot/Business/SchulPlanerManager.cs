using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;
using SchulPlanerBot.Quartz;
using System.Diagnostics;
using System.Globalization;

namespace SchulPlanerBot.Business;

public class SchulPlanerManager(ILogger<SchulPlanerManager> logger, ISchedulerFactory schedulerFactory, IOptions<ManagerOptions> optionsAccessor, BotDbContext dbContext, ErrorService errorService)
    : ManagerBase(logger, optionsAccessor, dbContext, errorService)
{
    private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;

    public StringComparer SubjectNameComparer => Options.SubjectsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public async Task<Guild> GetGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild? guild = await _dbContext.Guilds
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == guildId, ct)
            .ConfigureAwait(false);
        if (guild is null)
        {
            guild = new()
            {
                Id = guildId,
                DeleteHomeworksAfterDue = Options.MaxDeleteHomeworksAfterDue
            };
            _dbContext.Guilds.Add(guild);

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return guild;
    }

    public async Task<IEnumerable<Guild>> GetGuildsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Guilds
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<UpdateResult> SetNotificationCultureAsync(ulong guildId, CultureInfo cultureInfo, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationCulture = cultureInfo;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> RemoveNotificationCultureAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationCulture = null;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    public async Task<IEnumerable<Notification>> GetNotificationsAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        return guild.Notifications;
    }

    public async Task<UpdateResult> AddNotificationAsync(ulong guildId, DateTimeOffset startAt, TimeSpan between, ulong channelId, CancellationToken ct = default)
    {
        if (between < Options.MinBetweenNotifications)
            return _errorService.LowTimeBetween(Options.MinBetweenNotifications);

        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        if (!guild.Notifications.Any(n => n.StartAt == startAt))
        {
            Notification notification = new(startAt, between, channelId);
            await AddNotificationToSchedulerAsync(guild.Id, notification, ct).ConfigureAwait(false);     // Call before SaveChanges to ensure exceptions are thrown before the changes are persisted

            guild.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return UpdateResult.Succeeded();
        }
        else
        {
            return _errorService.NotificationAlreadyExists();
        }
    }

    public async Task<UpdateResult> RemoveNotificationAsync(ulong guildId, DateTimeOffset startAt, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);

        Notification? notification = guild.Notifications.FirstOrDefault(n => n.StartAt == startAt);
        if (notification is not null)
        {
            await RemoveNotificationFromSchedulerAsync(guild.Id, notification, ct).ConfigureAwait(false);

            guild.Notifications.Remove(notification);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return UpdateResult.Succeeded();
        }
        else
        {
            return _errorService.NotificationNotFound();
        }
    }

    public async Task<UpdateResult> SetDeleteHomeworkAfterDueAsync(ulong guildId, TimeSpan deleteAfter, CancellationToken ct = default)
    {
        if (deleteAfter > Options.MaxDeleteHomeworksAfterDue)
            return _errorService.DeleteAfterDueTooHigh(Options.MaxDeleteHomeworksAfterDue);

        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.DeleteHomeworksAfterDue = deleteAfter;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    internal async Task AddNotificationToSchedulerAsync(ulong guildId, Notification notification, CancellationToken ct)
    {
        Activity? activity = Activity.Current;
        Activity.Current = null;
        IScheduler scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);     // When called the first time no activity must be active because if so it will be the parent of every jobs
        Activity.Current = activity;

        TriggerKey triggerKey = Keys.NotificationTrigger(guildId, notification.StartAt);
        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithDescription("Notifies users of a certain server at a configured time.")
            .ForJob(Keys.NotificationJob)
            .UsingJobData(new JobDataMap
            {
                { Keys.GuildIdData, guildId.ToString() },
                { Keys.NotificationData, notification }
            })
            .StartAt(notification.StartAt)
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(notification.Between)
                .RepeatForever()
                .WithMisfireHandlingInstructionNextWithRemainingCount())
            .Build();

        bool triggerExists = await scheduler.CheckExists(triggerKey, ct).ConfigureAwait(false);
        DateTimeOffset? nextFiring = !triggerExists
            ? await scheduler.ScheduleJob(trigger, ct).ConfigureAwait(false)
            : await scheduler.RescheduleJob(triggerKey, trigger, ct).ConfigureAwait(false);

        _logger.LogInformation("Notifications for guild {guildId} scheduled. Next firing at {next}", guildId, nextFiring!);
    }

    internal async Task RemoveNotificationFromSchedulerAsync(ulong guildId, Notification notification, CancellationToken ct)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);

        TriggerKey triggerKey = Keys.NotificationTrigger(guildId, notification.StartAt);
        if (await scheduler.UnscheduleJob(triggerKey, ct).ConfigureAwait(false))
            _logger.LogTrace("Notification for guild {guildId} removed from scheduler (trigger: {triggerKey}).", guildId, triggerKey.ToString());
        else
            _logger.LogWarning("Failed to remove notification for guild {guildId} from scheduler (trigger: {triggerKey}).", guildId, triggerKey.ToString());
    }
}
