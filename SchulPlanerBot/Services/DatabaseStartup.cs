using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SchulPlanerBot.Business;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

public sealed class DatabaseStartup(IServiceScopeFactory scopeFactory, ILogger<DatabaseStartup> logger) : BackgroundService, IDisposable
{
    public const string ActivitySourceName = "Bot.DatabaseInitialization";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger _logger = logger;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using Activity? activity = _activitySource.StartActivity("Initialize database");
        using IServiceScope scope = _scopeFactory.CreateScope();

        BotDbContext dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        DatabaseFacade database = dbContext.Database;

        IEnumerable<string> pendingMigrations = await database.GetPendingMigrationsAsync(ct).ConfigureAwait(false);
        if (pendingMigrations.Any())
        {
            _logger.LogInformation("{pendingMigrationsCount} migrations aren't applied to the database.", pendingMigrations.Count());
            foreach (string migration in pendingMigrations)
            {
                await database.MigrateAsync(migration, ct).ConfigureAwait(false);
                _logger.LogInformation("Migration '{name}' applied", migration);
            }
        }
        else
        {
            _logger.LogInformation("Database is up to date");
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _activitySource.Dispose();
    }
}
