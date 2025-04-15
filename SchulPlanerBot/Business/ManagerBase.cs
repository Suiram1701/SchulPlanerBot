using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Business;

public abstract class ManagerBase(ILogger logger, IOptions<ManagerOptions> optionsAccessor, BotDbContext dbContext, ErrorService errorService)
{
    protected readonly ILogger _logger = logger;
    protected readonly BotDbContext _dbContext = dbContext;
    protected readonly ErrorService _errorService = errorService;

    public ManagerOptions Options => optionsAccessor.Value;

    protected virtual async Task<Guild> GetOrAddGuildAsync(ulong guildId, CancellationToken ct = default)
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
}
