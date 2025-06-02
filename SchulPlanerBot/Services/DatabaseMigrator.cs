using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SchulPlanerBot.Business;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

internal sealed class DatabaseMigrator(IServiceScopeFactory scopeFactory, ILogger<DatabaseMigrator> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger _logger = logger;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly TaskCompletionSource _completionSource = new();
    
    public Task MigrationCompleted => _completionSource.Task;
    
    public const string ActivitySourceName = "Bot.DatabaseMigration";
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using Activity? activity = _activitySource.StartActivity("Migrate database");
        using IServiceScope scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        DatabaseFacade database = dbContext.Database;

        string[] pendingMigrations = [.. await database.GetPendingMigrationsAsync(ct).ConfigureAwait(false)];
        if (pendingMigrations.Length > 0)
        {
            _logger.LogInformation("{pendingMigrationsCount} migrations aren't applied to the database.", pendingMigrations.Length);
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

        _completionSource.SetResult();
    }

    public override void Dispose()
    {
        base.Dispose();
        _activitySource.Dispose();
    }
}
