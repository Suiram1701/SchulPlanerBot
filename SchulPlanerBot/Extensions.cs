using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using OpenTelemetry.Trace;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Database;
using SchulPlanerBot.Options;
using SchulPlanerBot.ServiceDefaults;
using SchulPlanerBot.Services;

namespace SchulPlanerBot;

public static class Extensions
{
    public static IHostApplicationBuilder AddBotDatabase(this IHostApplicationBuilder builder, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        builder.AddNpgsqlDbContext<BotDbContext>(ResourceNames.BotDatabase, configureDbContextOptions: options =>
        {
            if (builder.Environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        builder.Services.AddHostedService<DatabaseStartup>();

        return builder;
    }

    public static IServiceCollection AddDiscordSocketClient(this IServiceCollection services, string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DiscordClientOptions>()
            .BindConfiguration(configurationKey)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddSingleton<DiscordSocketConfig>(_ =>
            {
                return new()
                {
                    DefaultRetryMode = RetryMode.AlwaysRetry,
                    GatewayIntents = GatewayIntents.AllUnprivileged,
                    LogGatewayIntentWarnings = true,

                    /*
                     * The snowflake of an object is always specified in UTC while the server could use a local time.
                     * To prevent the response cancellation because of the three second timeout (a different time zone causes a difference of one hour) the time
                     * where the message were received have to be used.
                    */
                    UseInteractionSnowflakeDate = false
                };
            })
            .AddSingleton<DiscordSocketClient>()
            .AddHostedService<DiscordClientStartup>();
    }

    public static IServiceCollection AddInteractionFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddSingleton(sp =>
            {
                DiscordSocketClient client = sp.GetRequiredService<DiscordSocketClient>();
                return new InteractionService(client, new InteractionServiceConfig
                {
                    DefaultRunMode = RunMode.Sync,     // Idk exactly why but async always fails
                    AutoServiceScopes = false,     // Scopes managed by DiscordInteractionHandler
                    UseCompiledLambda = true
                });
            })
            .AddHostedService<DiscordInteractionHandler>();
    }

    public static IServiceCollection AddDatabaseManagers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<SchulPlanerManager>();
    }

    public static TracerProviderBuilder AddDiscordClientInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DiscordClientStartup.ActivitySourceName, DiscordInteractionHandler.ActivitySourceName);
    }

    public static TracerProviderBuilder AddBotDatabaseInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DatabaseStartup.ActivitySourceName);
    }
}
