using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Options;
using SchulPlanerBot.ServiceDefaults;
using SchulPlanerBot.Services;
using System.Globalization;
using System.Reflection;

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

    public static IServiceCollection AddDatabaseManagers(this IServiceCollection services, string config = "Manager")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ManagerOptions>()
            .BindConfiguration("Manager")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddTransient<ErrorService>()
            .AddScoped<SchulPlanerManager>()
            .AddScoped<HomeworkManager>();
    }

    public static IServiceCollection AddDiscordSocketClient(this IServiceCollection services, string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DiscordClientOptions>()
            .BindConfiguration(configurationKey)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .Configure<DiscordSocketConfig>(config =>
            {
                config.LogLevel = LogSeverity.Debug;     // Managed by ILogger<DiscordSocketClient>
                config.DefaultRetryMode = RetryMode.AlwaysRetry;
                config.GatewayIntents =
                    GatewayIntents.Guilds | GatewayIntents.GuildMessages
                    | GatewayIntents.DirectMessages;     // Be able to respond to DMs
                config.LogGatewayIntentWarnings = true;
                config.UseInteractionSnowflakeDate = false;     // The DateTime.UtcNow on my device is always about half a minute off the real UTC
                config.ResponseInternalTimeCheck =
#if DEBUG
                false;
#else
                true;
#endif
            })
            .AddSingleton<DiscordSocketClient>(sp =>
            {
                IOptionsMonitor<DiscordSocketConfig> monitor = sp.GetRequiredService<IOptionsMonitor<DiscordSocketConfig>>();
                return new(monitor.CurrentValue);
            })
            .AddHostedService<DiscordClientManager>();
    }

    public static IServiceCollection AddInteractionFramework(this IServiceCollection services, Action<InteractionService>? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .Configure<InteractionServiceConfig>(config =>
            {
                config.LogLevel = LogSeverity.Debug;     // Level managed by ILogger<InteractionService>
                config.DefaultRunMode = RunMode.Sync;     // Idk exactly why but RunMode.Async always fails
                config.AutoServiceScopes = false;     // Scopes managed by DiscordInteractionHandler
                config.UseCompiledLambda = true;
            })
            .AddSingleton(sp =>
            {
                DiscordSocketClient client = sp.GetRequiredService<DiscordSocketClient>();
                IOptionsMonitor<InteractionServiceConfig> monitor = sp.GetRequiredService<IOptionsMonitor<InteractionServiceConfig>>();

                InteractionService service = new(client, monitor.CurrentValue);
                options?.Invoke(service);

                return service;
            })
            .AddHostedService<InteractionHandler>();
    }

    public static IServiceCollection AddInteractionResXLocalization<T>(this IServiceCollection services, string baseResource, params CultureInfo[] supportedLocales) =>
        AddInteractionResXLocalization(services, baseResource, typeof(T).Assembly, supportedLocales);

    public static IServiceCollection AddInteractionResXLocalization(this IServiceCollection services, string baseResource, Assembly resourceAssembly, params CultureInfo[] supportedLocales)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseResource);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        if (supportedLocales.Length == 0)
            throw new ArgumentException("At least one locale is required!", nameof(supportedLocales));

        ResxLocalizationManager localizationManager = new(baseResource, resourceAssembly, supportedLocales);
        services.Configure<InteractionServiceConfig>(config => config.LocalizationManager = localizationManager);

        return services;
    }
}
