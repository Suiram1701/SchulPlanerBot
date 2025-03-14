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
                    UseCompiledLambda = true
                });
            })
            .AddHostedService<DiscordInteractionHandler>();
    }

    public static TracerProviderBuilder AddDiscordClientInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DiscordClientStartup.ActivitySourceName, DiscordInteractionHandler.ActivitySourceName);
    }
}
