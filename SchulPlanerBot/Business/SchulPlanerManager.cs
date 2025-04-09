using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;
using SchulPlanerBot.Quartz;
using System.Globalization;
using System.Linq.Expressions;

namespace SchulPlanerBot.Business;

public class SchulPlanerManager(IHostEnvironment environment, ILogger<SchulPlanerManager> logger, ISchedulerFactory schedulerFactory, IOptions<ManagerOptions> optionsAccessor, BotDbContext dbContext, ErrorService errorService)
{
    private readonly IHostEnvironment _environment = environment;
    private readonly ILogger _logger = logger;
    private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
    private readonly BotDbContext _dbContext = dbContext;
    private readonly ErrorService _errorService = errorService;

    public ManagerOptions Options => optionsAccessor.Value;

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
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);

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
        if (deleteAfter > Options.MaxDeleteHomeworksAfterDue && !_environment.IsDevelopment())     // Disable maximum time for dev purpose
            return _errorService.DeleteAfterDueTooHigh(Options.MaxDeleteHomeworksAfterDue);

        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.DeleteHomeworksAfterDue = deleteAfter;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<Homework?> GetHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.GuildId == guildId)
            .SingleOrDefaultAsync(h => h.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Homework>> GetHomeworksAsync(ulong guildId, string? search = null, string? subject = null, DateTimeOffset? start = null, DateTimeOffset? end = null, CancellationToken ct = default)
    {
        start ??= DateTimeOffset.MinValue;
        end ??= DateTimeOffset.MaxValue;

        IQueryable<Homework> query = _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.GuildId == guildId)
            .Where(h => h.Due >= start && h.Due <= end);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            Expression<Func<Homework, bool>> predicate = Options.SubjectsCaseSensitive
                ? h => EF.Functions.Like(h.Subject, subject)
                : h => EF.Functions.ILike(h.Subject!, subject);
            query = query.Where(predicate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchPattern = $"%{search.Replace(' ', '%')}%";
            query = query.Where(h =>
                EF.Functions.ILike(h.Title, searchPattern) ||
                EF.Functions.ILike(h.Details!, searchPattern));
        }
        
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<(Homework? homework, UpdateResult result)> CreateHomeworkAsync(ulong guildId, ulong userId, DateTimeOffset due, string? subject, string title, string? details, CancellationToken ct = default)
    {
        if (due <= DateTimeOffset.UtcNow.Add(Options.MinDueInFuture) && !_environment.IsDevelopment())     // Disable minimum time for dev purpose
            return (null, _errorService.DueMustInFuture(Options.MinDueInFuture));

        _ = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);     // Ensure the a guild with this id exists

        Homework homework = new()
        {
            GuildId = guildId,
            Due = due,
            Subject = subject,
            Title = title,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };
        _dbContext.Homeworks.Add(homework);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return (homework, UpdateResult.Succeeded());
    }

    public async Task<(Homework? homework, UpdateResult result)> ModifyHomeworkAsync(Guid homeworkId, ulong userId, DateTimeOffset newDue, string? newSubject, string newTitle, string? newDetails, CancellationToken ct = default)
    {
        if (newDue <= DateTimeOffset.UtcNow.Add(Options.MinDueInFuture) && !_environment.IsDevelopment())     // Disable minimum time for dev purpose
            return (null, _errorService.DueMustInFuture(Options.MinDueInFuture));

        Homework? homework = await _dbContext.Homeworks.FindAsync([homeworkId], ct).AsTask().ConfigureAwait(false);
        if (homework is null)
            return (null, _errorService.HomeworkNotFound());

        homework.Due = newDue;
        homework.Subject = newSubject;
        homework.Title = newTitle;
        homework.Details = newDetails;
        homework.LastModifiedAt = DateTimeOffset.UtcNow;
        homework.LastModifiedBy = userId;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return (homework, UpdateResult.Succeeded());
    }

    public async Task<UpdateResult> DeleteHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        int count = await _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.GuildId == guildId && h.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return count != 0
            ? UpdateResult.Succeeded()
            : _errorService.HomeworkNotFound();
    }

    public async Task<(int? deleted, UpdateResult)> DeleteHomeworksWithDueOlderAsync(ulong guildId, DateTimeOffset dateTime, CancellationToken ct = default)
    {
        int count = await _dbContext.Homeworks
            .Where(h => h.GuildId == guildId && h.Due <= dateTime)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        return (count, UpdateResult.Succeeded());
    }

    public async Task<HomeworkSubscription?> GetHomeworkSubscriptionAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        return await _dbContext.HomeworkSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.GuildId == guildId && s.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<HomeworkSubscription>> GetSubscriptionsAsync(ulong guildId, CancellationToken ct = default)
    {
        return await _dbContext.HomeworkSubscriptions
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<UpdateResult> SetSubscribeToAllSubjectsAsync(ulong guildId, ulong userId, bool subscribe, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        subscription.AnySubject = subscribe;
        if (IsSubscriptionNotNeeded(subscription))
            _dbContext.HomeworkSubscriptions.Remove(subscription);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> SubscribeToSubjectsAsync(ulong guildId, ulong userId, bool noSubject, string[] subjects, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        subscription.AnySubject = false;
        subscription.NoSubject = noSubject || subscription.NoSubject;     // Sets NoSubject to true when noSubject true
        subscription.Include = [.. subscription.Include, .. subjects.Except(subscription.Include, SubjectNameComparer)];

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> UnsubscribeFromSubjectsAsync(ulong guildId, ulong userId, bool noSubject, string[] subjects, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        subscription.AnySubject = false;
        subscription.NoSubject = !noSubject && subscription.NoSubject;     // Sets NoSubject to false when noSubject true
        subscription.Include = [.. subscription.Include.Except(subjects, SubjectNameComparer)];

        if (IsSubscriptionNotNeeded(subscription))
            _dbContext.HomeworkSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return UpdateResult.Succeeded();
    }

    private async Task<Guild> GetOrAddGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild? guild = await _dbContext.Guilds.FindAsync([guildId], ct).AsTask().ConfigureAwait(false);
        if (guild is null)
        {
            guild = new()
            {
                Id = guildId,
                DeleteHomeworksAfterDue = Options.MaxDeleteHomeworksAfterDue
            };
            _dbContext.Guilds.Add(guild);
        }

        return guild;
    }

    private async Task<HomeworkSubscription> GetOrAddSubscriptionAsync(ulong guildId, ulong userId, CancellationToken ct)
    {
        _ = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);

        HomeworkSubscription? subscription = await _dbContext.HomeworkSubscriptions
            .FindAsync([guildId, userId], ct)
            .AsTask()
            .ConfigureAwait(false);
        if (subscription is null)
        {
            subscription = new()
            {
                GuildId = guildId,
                UserId = userId
            };
            _dbContext.HomeworkSubscriptions.Add(subscription);
        }

        return subscription;
    }

    private static bool IsSubscriptionNotNeeded(HomeworkSubscription subscription) => subscription is { AnySubject: false, NoSubject: false, Include.Length: 0 };

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
