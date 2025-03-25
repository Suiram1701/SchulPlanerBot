using Microsoft.EntityFrameworkCore;
using Quartz;
using SchulPlanerBot.Business.Database;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Quartz;

namespace SchulPlanerBot.Business;

public class SchulPlanerManager(IHostEnvironment environment, ILogger<SchulPlanerManager> logger, ISchedulerFactory schedulerFactory, BotDbContext dbContext, ErrorService errorService)
{
    private readonly IHostEnvironment _environment = environment;
    private readonly ILogger _logger = logger;
    private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
    private readonly BotDbContext _dbContext = dbContext;
    private readonly ErrorService _errorService = errorService;

    public async Task<Guild> GetGuildAsync(ulong guildId, CancellationToken ct)
    {
        Guild? guild = await _dbContext.Guilds
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == guildId, ct)
            .ConfigureAwait(false);
        if (guild is null)
        {
            guild = new() { Id = guildId };
            await _dbContext.Guilds.AddAsync(guild, ct).AsTask().ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return guild;
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

    public async Task<UpdateResult> EnableNotificationsAsync(ulong guildId, DateTimeOffset start, TimeSpan between, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);

        if (guild.ChannelId is null)
            return _errorService.NoChannel();
        if (between < TimeSpan.FromMinutes(10) && !_environment.IsDevelopment())     // Disable minimum time for dev purpose
            return _errorService.LowTimeBetween(TimeSpan.FromMinutes(10));

        guild.NotificationsEnabled = true;
        guild.StartNotifications = start.ToUniversalTime();
        guild.BetweenNotifications = between;

        await UpdateSchedulerForGuildAsync(guild, ct).ConfigureAwait(false);     // Call before SaveChanges to ensure exceptions are called before the changes are persisted
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

    public async Task<Homework?> GetHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.OwnerId == guildId)
            .SingleOrDefaultAsync(h => h.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Homework>> GetHomeworksAsync(ulong guildId, DateTimeOffset? start = null, DateTimeOffset? end = null, string? subject = null, CancellationToken ct = default)
    {
        start = start?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        end = end?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddDays(7);

        IQueryable<Homework> query = _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.OwnerId == guildId)
            .Where(h => h.Due >= start && h.Due <= end);
        if (string.IsNullOrEmpty(subject))
            return await query.ToListAsync(ct).ConfigureAwait(false);
        else
            return await query.Where(h => h.Subject == subject).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<(Homework? homework, UpdateResult result)> CreateHomeworkAsync(ulong guildId, ulong userId, DateTimeOffset due, string? subject, string title, string? details, CancellationToken ct = default)
    {
        due = due.ToUniversalTime();
        if (due <= DateTimeOffset.UtcNow.AddMinutes(10) && !_environment.IsDevelopment())     // Disable minimum time for dev purpose
            return (null, _errorService.DueMustInFuture(TimeSpan.FromMinutes(10)));

        _ = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);     // Ensure the a guild with this id exists

        Homework homework = new()
        {
            OwnerId = guildId,
            Due = due,
            Subject = subject,
            Title = title,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };
        await _dbContext.Homeworks.AddAsync(homework, ct).AsTask().ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return (homework, UpdateResult.Succeeded());
    }

    public async Task<UpdateResult> DeleteHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        int count = await _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.OwnerId == guildId && h.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return count != 0
            ? UpdateResult.Succeeded()
            : _errorService.HomeworkNotFound();
    }

    private async Task<Guild> GetOrAddGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild? guild = await _dbContext.Guilds.FindAsync([guildId], ct).AsTask().ConfigureAwait(false);
        if (guild is null)
        {
            guild = new() { Id = guildId };
            await _dbContext.Guilds.AddAsync(guild, ct).AsTask().ConfigureAwait(false);
        }

        return guild;
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
            .UsingJobData(DataKeys.GuildId, guild.Id.ToString())
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
