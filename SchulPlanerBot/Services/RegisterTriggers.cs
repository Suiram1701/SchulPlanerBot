using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using System.Diagnostics;
using Quartz;
using SchulPlanerBot.Quartz;

namespace SchulPlanerBot.Services;

internal sealed class RegisterTriggers(ILogger<RegisterTriggers> logger, IServiceScopeFactory scopeFactory, IConfiguration config, DatabaseMigrator migrator) : BackgroundService
{
    public const string ActivitySourceName = "Bot.RegisterJobTriggers";

    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConfiguration _config = config;
    private readonly DatabaseMigrator _dbMigrator = migrator;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _dbMigrator.MigrationCompleted.ConfigureAwait(false);
        
        using Activity? activity = _activitySource.StartActivity("Register job triggers");
        using IServiceScope scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<SchulPlanerManager>();
        
        await RegisterRemovalTrigger(scope, ct).ConfigureAwait(false);

        IEnumerable<Guild> guilds = await manager.GetGuildsAsync(ct).ConfigureAwait(false);
        foreach ((ulong guildId, Notification notification) in guilds.SelectMany(g => g.Notifications.Select(n => (g.Id, n))))
        {
            KeyValuePair<string, object?>[] tags = [
                KeyValuePair.Create<string, object?>("guild.id", guildId),
                KeyValuePair.Create<string, object?>("notification.startAt", notification.StartAt)
            ];

            try
            {
                await manager.AddNotificationToSchedulerAsync(guildId, notification, ct).ConfigureAwait(false);
                activity?.AddEvent(new ActivityEvent(name: "Added trigger", tags: [.. tags]));
            }
            catch (Exception ex)
            {
                activity?.AddException(ex, new TagList(tags));
                _logger.LogCritical(ex, "An error occurred while adding a notification trigger!");
            }
        }
    }

    private async Task RegisterRemovalTrigger(IServiceScope scope, CancellationToken ct)
    {
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        IScheduler scheduler = await schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        
        var interval = _config.GetValue<TimeSpan>("DeleteHomeworksJobInterval");

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(Keys.DeleteHomeworksKey)
            .WithDescription("Triggers this job every in a certain interval.")
            .ForJob(Keys.DeleteHomeworksJob)
            .StartNow()
            .WithSimpleSchedule(scheduleBuilder => scheduleBuilder
                .WithInterval(interval)
                .RepeatForever()
                .WithMisfireHandlingInstructionFireNow())
            .Build();
        DateTimeOffset nextFiring = await scheduler.ScheduleJob(trigger, ct).ConfigureAwait(false);
        
        _logger.LogInformation("Added homework removal trigger. Next firing at {nextFiring}.", nextFiring);
    }

    public override void Dispose()
    {
        base.Dispose();
        _activitySource.Dispose();
    }
}
