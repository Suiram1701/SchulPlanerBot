using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;
using System.Linq.Expressions;

namespace SchulPlanerBot.Business;

public class HomeworkManager(IHostEnvironment environment, ILogger<SchulPlanerManager> logger, IOptions<ManagerOptions> optionsAccessor, BotDbContext dbContext, ErrorService errorService)
    : ManagerBase(logger, optionsAccessor, dbContext, errorService)
{
    private readonly IHostEnvironment _environment = environment;

    public StringComparer SubjectNameComparer => Options.SubjectsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

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
}
