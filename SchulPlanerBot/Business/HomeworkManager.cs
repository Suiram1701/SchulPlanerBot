using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;
using System.Linq.Expressions;

namespace SchulPlanerBot.Business;

public class HomeworkManager(ILogger<SchulPlanerManager> logger, IOptions<ManagerOptions> optionsAccessor, BotDbContext dbContext, ErrorService errorService)
    : ManagerBase(logger, optionsAccessor, dbContext, errorService)
{
    private StringComparer SubjectNameComparer => Options.SubjectsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public Task<Homework?> GetHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        return _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.GuildId == guildId)
            .SingleOrDefaultAsync(h => h.Id == id, ct);
    }

    public async Task<Homework[]> GetHomeworksAsync(ulong guildId, string? search = null, string? subject = null, DateTimeOffset? start = null, DateTimeOffset? end = null, CancellationToken ct = default)
    {
        start = start?.ToUniversalTime();
        start ??= DateTimeOffset.MinValue;
        
        end = end?.ToUniversalTime();
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
            var searchPattern = $"%{search.Replace(' ', '%')}%";
            query = query.Where(h =>
                EF.Functions.ILike(h.Title, searchPattern) ||
                EF.Functions.ILike(h.Details!, searchPattern));
        }

        return [.. await query.OrderBy(h => h.Due).ToListAsync(ct).ConfigureAwait(false)];
    }

    public async Task<(Homework? homework, UpdateResult result)> CreateHomeworkAsync(ulong guildId, ulong userId, DateTimeOffset due, string? subject, string title, string? details, CancellationToken ct = default)
    {
        due = due.ToUniversalTime();
        
        if (due <= DateTimeOffset.UtcNow.Add(Options.MinDueInFuture))
            return (null, _errorService.DueMustInFuture(Options.MinDueInFuture));

        _ = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);     // Ensure a guild with this id exists

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
        newDue = newDue.ToUniversalTime();
        
        if (newDue <= DateTimeOffset.UtcNow.Add(Options.MinDueInFuture))
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
        dateTime = dateTime.ToUniversalTime();
        
        int count = await _dbContext.Homeworks
            .IgnoreQueryFilters()
            .Where(h => h.GuildId == guildId && h.Due <= dateTime)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        return (count, UpdateResult.Succeeded());
    }

    public Task<HomeworkSubscription?> GetHomeworkSubscriptionAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        return _dbContext.HomeworkSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.GuildId == guildId && s.UserId == userId, ct);
    }

    public async Task<HomeworkSubscription[]> GetSubscriptionsAsync(ulong guildId, CancellationToken ct = default)
    {
        return [.. await _dbContext.HomeworkSubscriptions
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .ToListAsync(ct)
            .ConfigureAwait(false)];
    }

    public async Task<(UpdateResult, HomeworkSubscription? newSubscription)> SetSubscribeToAllSubjectsAsync(ulong guildId, ulong userId, bool subscribe, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        subscription.AnySubject = subscribe;
        RemoveNotNeededData(subscription);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return (UpdateResult.Succeeded(), subscription);
    }

    public async Task<(UpdateResult, HomeworkSubscription? newSubscription)> SubscribeToSubjectsAsync(ulong guildId, ulong userId, string?[] subjects, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        if (subscription.AnySubject)
            subscription.Exclude = [.. subscription.Exclude.Except(subjects, SubjectNameComparer)];
        else
            subscription.Include = ConcatSubjects(subscription.Include, subjects);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return (UpdateResult.Succeeded(), subscription);
    }

    public async Task<(UpdateResult, HomeworkSubscription? newSubscription)> UnsubscribeFromSubjectsAsync(ulong guildId, ulong userId, string?[] subjects, CancellationToken ct = default)
    {
        HomeworkSubscription subscription = await GetOrAddSubscriptionAsync(guildId, userId, ct).ConfigureAwait(false);

        subjects = new HashSet<string?>(subjects, SubjectNameComparer).ToArray();     // Ensure only one of each subject
        if (subscription.AnySubject)
            subscription.Exclude = ConcatSubjects(subscription.Exclude, subjects);
        else
            subscription.Include = [.. subscription.Include.Except(subjects, SubjectNameComparer)];
        RemoveNotNeededData(subscription);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return (UpdateResult.Succeeded(), subscription);
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
            subscription = new HomeworkSubscription()
            {
                GuildId = guildId,
                UserId = userId
            };
            _dbContext.HomeworkSubscriptions.Add(subscription);
        }

        return subscription;
    }

    private string?[] ConcatSubjects(string?[] a, string?[] b) => [.. ((string?[])[.. a, .. b]).Distinct(SubjectNameComparer)];     // concat both arrays and distinct same subjects

    private void RemoveNotNeededData(HomeworkSubscription subscription)
    {
        if (subscription.AnySubject)
            subscription.Include = [];
        else
            subscription.Exclude = [];

        if (subscription is { AnySubject: false, Include.Length: 0 })
            _dbContext.HomeworkSubscriptions.Remove(subscription);
    }
}
