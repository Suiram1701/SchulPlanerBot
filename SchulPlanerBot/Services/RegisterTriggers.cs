using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

public sealed class RegisterTriggers(ILogger<RegisterTriggers> logger, IServiceScopeFactory scopeFactory) : BackgroundService
{
    public const string ActivitySourceName = "Bot.RegisterJobTriggers";

    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using Activity? activity = _activitySource.StartActivity("Register notification triggers");
        using IServiceScope scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<SchulPlanerManager>();

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
                activity?.AddEvent(new(name: "Added trigger", tags: [.. tags]));
            }
            catch (Exception ex)
            {
                activity?.AddException(ex, new(tags));
                _logger.LogCritical(ex, "An error occurred while adding a notification trigger!");
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _activitySource.Dispose();
    }
}
