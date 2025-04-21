
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

public sealed class RegisterTriggers(ILogger<RegisterTriggers> logger, IServiceScopeFactory scopeFactory) : IHostedService, IDisposable
{
    public const string ActivitySourceName = "Bot.RegisterJobTriggers";

    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = _activitySource.StartActivity("Register notification triggers");
        using IServiceScope scope = _scopeFactory.CreateScope();
        SchulPlanerManager manager = scope.ServiceProvider.GetRequiredService<SchulPlanerManager>();

        IEnumerable<Guild> guilds = await manager.GetGuildsAsync(cancellationToken).ConfigureAwait(false);
        foreach ((ulong guildId, Notification notification) in guilds.SelectMany(g => g.Notifications.Select(n => (g.Id, n))))
        {
            KeyValuePair<string, object?>[] tags = [
                KeyValuePair.Create<string, object?>("guild.id", guildId),
                KeyValuePair.Create<string, object?>("notification.startAt", notification.StartAt)
            ];

            try
            {
                await manager.AddNotificationToSchedulerAsync(guildId, notification, cancellationToken).ConfigureAwait(false);
                activity?.AddEvent(new(name: "Added trigger", tags: [.. tags]));
            }
            catch (Exception ex)
            {
                activity?.AddException(ex, new(tags));
                _logger.LogCritical(ex, "An error occurred while adding a notification trigger!");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _activitySource.Dispose();
}
