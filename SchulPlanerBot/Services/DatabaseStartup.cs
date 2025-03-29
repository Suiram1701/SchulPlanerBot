using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SchulPlanerBot.Business;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

public sealed class DatabaseStartup(IServiceScopeFactory scopeFactory, ILogger<DatabaseStartup> logger) : IHostedService, IDisposable
{
    public const string ActivitySourceName = "BotDatabase.Initialization";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger _logger = logger;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = _activitySource.StartActivity("Initialize database");
        using IServiceScope scope = _scopeFactory.CreateScope();

        BotDbContext dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        DatabaseFacade database = dbContext.Database;

        IEnumerable<string> pendingMigrations = await database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (pendingMigrations.Any())
        {
            _logger.LogInformation("{pendingMigrationsCount} migrations aren't applied to the database.", pendingMigrations.Count());
            foreach (string migration in pendingMigrations)
            {
                await database.MigrateAsync(migration, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Migration '{name}' applied", migration);
            }
        }
        else
        {
            _logger.LogInformation("Database is up to date");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _activitySource.Dispose();
}
