using Microsoft.EntityFrameworkCore;
using SchulPlanerBot.Business.Database;
using SchulPlanerBot.Business.Errors;
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
            await _dbContext.Guilds.AddAsync(guild, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return guild;
    }

    public async Task<UpdateResult> SetChannelAsync(ulong guildId, ulong channelId, CancellationToken ct)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.ChannelId = channelId;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> RemoveChannelAsync(ulong guildId, CancellationToken ct)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.ChannelId = null;
        guild.NotificationsEnabled = false;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> EnableNotificationsAsync(ulong guildId, DateTimeOffset start, TimeSpan between, CancellationToken ct)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        if (guild.ChannelId is null)
            return UpdateResult.Failed(new UpdateError("NoChannel", "An interaction channel must be set!"));

        guild.NotificationsEnabled = true;
        guild.StartNotifications = start.ToUniversalTime();
        guild.BetweenNotifications = between;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    public async Task<UpdateResult> DisableNotificationsAsync(ulong guildId, CancellationToken ct)
    {
        Guild guild = await GetOrAddGuildAsync(guildId, ct).ConfigureAwait(false);
        guild.NotificationsEnabled = false;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return UpdateResult.Succeeded();
    }

    private async Task<Guild> GetOrAddGuildAsync(ulong guildId, CancellationToken ct)
    {
        Guild? guild = await _dbContext.Guilds.FindAsync([guildId], ct).AsTask().ConfigureAwait(false);
        if (guild is null)
        {
            guild = new() { Id = guildId };
            await _dbContext.Guilds.AddAsync(guild, ct).ConfigureAwait(false);
        }

        return guild;
    }
}
