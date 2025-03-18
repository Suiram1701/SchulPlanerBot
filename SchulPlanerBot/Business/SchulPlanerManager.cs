using Microsoft.EntityFrameworkCore;
using SchulPlanerBot.Business.Database;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Business;

public class SchulPlanerManager(ILogger<SchulPlanerManager> logger, BotDbContext dbContext)
{
    private readonly ILogger _logger = logger;
    private readonly BotDbContext _dbContext = dbContext;

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

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> EnableNotificationsAsync(ulong guildId, DateTimeOffset start, TimeSpan between, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        if (guild.ChannelId is null)
            return UpdateResult.Failed("NoChannel", "An interaction channel must be set!");

        if (between < TimeSpan.FromMinutes(10))
            return UpdateResult.Failed("LowTimeBetween", "The provided time in between the notifications must be at least 10 min!");

        guild.NotificationsEnabled = true;
        guild.StartNotifications = start.ToUniversalTime();
        guild.BetweenNotifications = between;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> DisableNotificationsAsync(ulong guildId, CancellationToken ct = default)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationsEnabled = false;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<Homework?> GetHomeworkAsync(ulong guildId, Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Homeworks
            .AsNoTracking()
            .Where(h => h.OwnerId == guildId)
            .SingleOrDefaultAsync(h => h.Id == id, ct).ConfigureAwait(false);
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
        if (due <= DateTimeOffset.UtcNow.AddMinutes(10))
            return (null, UpdateResult.Failed("DueInPast", "The provided due have to be at least 10 minutes in the future!"));

        _ = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);     // Ensure the a guild with this id exists

        Homework homework = new()
        {
            OwnerId = guildId,
            Due = due.ToUniversalTime(),
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
        if (count == 0)
            return UpdateResult.Failed("HomeworkNotFound", "The specified homework doesn't exists.");
        return UpdateResult.Succeeded();
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
}
