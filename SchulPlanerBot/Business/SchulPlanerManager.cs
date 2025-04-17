using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;
using SchulPlanerBot.Quartz;
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

    public async Task<UpdateResult> SetChannelAsync(ulong guildId, ulong channelId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.ChannelId = channelId;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> RemoveChannelAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.ChannelId = null;
        guild.NotificationsEnabled = false;

        await RemoveSchedulerForGuildAsync(guild, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> SetNotificationCultureAsync(ulong guildId, CultureInfo? cultureInfo, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationCulture = cultureInfo;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> EnableNotificationsAsync(ulong guildId, DateTimeOffset start, TimeSpan between, CancellationToken ct = default)
    {
        if (between < Options.MinBetweenNotifications)

        if (guild.ChannelId is null)
            return _errorService.NoChannel();
        if (between < Options.MinBetweenNotifications && !_environment.IsDevelopment())     // Disable minimum time for dev purpose
            return _errorService.LowTimeBetween(Options.MinBetweenNotifications);

        guild.NotificationsEnabled = true;
        guild.StartNotifications = start;
        guild.BetweenNotifications = between;

        await UpdateSchedulerForGuildAsync(guild, ct).ConfigureAwait(false);     // Call before SaveChanges to ensure exceptions are thrown before the changes are persisted
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> DisableNotificationsAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationsEnabled = false;

        await RemoveSchedulerForGuildAsync(guild, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
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

    private async Task UpdateSchedulerForGuildAsync(Guild guild, CancellationToken ct)
    {
        if (!guild.NotificationsEnabled)
            throw new InvalidOperationException();

        IScheduler scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);

        TriggerKey triggerKey = Keys.NotificationTrigger(guild.Id);
        bool triggerExists = await scheduler.CheckExists(triggerKey, ct).ConfigureAwait(false);

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithDescription("Notifies users of a certain server at a configured time")
            .ForJob(Keys.NotificationJob)
            .UsingJobData(Keys.GuildIdData, guild.Id.ToString())
            .StartAt(guild.StartNotifications.Value)
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(guild.BetweenNotifications.Value)
                .RepeatForever()
                .WithMisfireHandlingInstructionNextWithRemainingCount())
            .Build();

        DateTimeOffset? nextFiring = !triggerExists
            ? await scheduler.ScheduleJob(trigger, ct).ConfigureAwait(false)
            : await scheduler.RescheduleJob(triggerKey, trigger, ct).ConfigureAwait(false);
        _logger.LogTrace("Notifications for guild {guildId} scheduled. Next firing at {next}", guild.Id, nextFiring!);
    }

    private async Task RemoveSchedulerForGuildAsync(Guild guild, CancellationToken ct)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);

        TriggerKey triggerKey = Keys.NotificationTrigger(guild.Id);
        if (await scheduler.CheckExists(triggerKey, ct).ConfigureAwait(false))
        {
            await scheduler.UnscheduleJob(triggerKey, ct).ConfigureAwait(false);
            _logger.LogTrace("Notification for guild {guildId} removed from scheduler", guild.Id);
        }
    }
}
