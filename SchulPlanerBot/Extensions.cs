﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Database;
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
                config.GatewayIntents = GatewayIntents.AllUnprivileged;
                config.LogGatewayIntentWarnings = true;

                /*
                 * The snowflake of an object is always specified in UTC while the server could use a local time.
                 * To prevent the response cancellation because of the three second timeout (a different time zone causes a difference of one hour) the time
                 * where the message were received have to be used.
                */
                config.UseInteractionSnowflakeDate = false;
            })
            .AddSingleton<DiscordSocketClient>(sp =>
            {
                IOptionsMonitor<DiscordSocketConfig> monitor = sp.GetRequiredService<IOptionsMonitor<DiscordSocketConfig>>();
                return new(monitor.CurrentValue);
            })
            .AddHostedService<DiscordClientManager>();
    }

    public static IServiceCollection AddInteractionFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .Configure<InteractionServiceConfig>(config =>
            {
                config.LogLevel = LogSeverity.Debug;     // Managed by ILogger<InteractionService>
                config.DefaultRunMode = RunMode.Sync;     // Idk exactly why but async always fails
                config.AutoServiceScopes = false;     // Scopes managed by DiscordInteractionHandler
                config.UseCompiledLambda = true;
            })
            .AddSingleton(sp =>
            {
                DiscordSocketClient client = sp.GetRequiredService<DiscordSocketClient>();
                IOptionsMonitor<InteractionServiceConfig> monitor = sp.GetRequiredService<IOptionsMonitor<InteractionServiceConfig>>();

                return new InteractionService(client, monitor.CurrentValue);
            })
            .AddHostedService<DiscordInteractionHandler>();
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


    public static IServiceCollection AddDatabaseManagers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddTransient<ErrorService>()
            .AddScoped<SchulPlanerManager>();
    }

    public static TracerProviderBuilder AddDiscordNetInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DiscordClientManager.ActivitySourceName, DiscordInteractionHandler.ActivitySourceName);
    }

    public static TracerProviderBuilder AddBotDatabaseInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DatabaseStartup.ActivitySourceName);
    }

    public static MeterProviderBuilder AddDiscordNetInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureServices(services => services
            .AddHostedService<DiscordClientMetrics>()
            .AddActivatedSingleton<InteractionFrameworkMetrics>());
        return builder.AddMeter(DiscordClientMetrics.MeterName, InteractionFrameworkMetrics.MeterName);
    }
}
