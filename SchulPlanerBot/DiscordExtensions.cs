using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using OpenTelemetry.Trace;
using SchulPlanerBot.Options;
using SchulPlanerBot.Services;

namespace SchulPlanerBot;

public static class DiscordExtensions
{
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
                    LogGatewayIntentWarnings = true
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
                return new InteractionService(client);
            })
            .AddHostedService<DiscordInteractionHandler>();
    }

    public static TracerProviderBuilder AddDiscordClientInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DiscordClientStartup.ActivitySourceName, DiscordInteractionHandler.ActivitySourceName);
    }
}
