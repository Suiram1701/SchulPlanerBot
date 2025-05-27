using Quartz;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Quartz;

internal sealed class DeleteHomeworksJob(ILogger<DeleteHomeworksJob> logger, SchulPlanerManager manager, HomeworkManager homeworkManager) : IJob
{
    private readonly ILogger _logger = logger;
    private readonly SchulPlanerManager _manager = manager;
    private readonly HomeworkManager _homeworkManager = homeworkManager;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            foreach (Guild guild in await _manager.GetGuildsAsync(context.CancellationToken).ConfigureAwait(false))
            {
                DateTimeOffset olderThan = DateTimeOffset.UtcNow - guild.DeleteHomeworksAfterDue;

                try
                {
                    (int? count, UpdateResult deleteResult) = await _homeworkManager.DeleteHomeworksWithDueOlderAsync(guild.Id, olderThan, context.CancellationToken).ConfigureAwait(false);
                    if (deleteResult.Success && count is not null)
                    {
                        _logger.LogTrace("Deleted {deleted} obsolete homeworks for guild {guildId}.", count, guild.Id);
                    }
                    else
                    {
                        string errors = string.Join(", ", deleteResult.Errors.Select(e => e.Name));
                        _logger.LogError("An error occurred while deleting obsolete homeworks for guild {guildId}. Errors: {errors}", guild.Id, errors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing old homework deletion for guild {guildId}!", guild.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not JobExecutionException)
        {
            _logger.LogError(ex, "An unexpected error occurred during execution!");
            throw new JobExecutionException("An unexpected error occurred during job execution!", ex);
        }
    }
}
