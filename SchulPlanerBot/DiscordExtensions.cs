using Discord;
using Discord.WebSocket;
using SchulPlanerBot.Options;
using SchulPlanerBot.Services;

namespace SchulPlanerBot;

public static class DiscordExtensions
{
    public static IHostApplicationBuilder AddDiscordClient(this IHostApplicationBuilder builder, string configurationKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<DiscordClientOptions>()
            .BindConfiguration(configurationKey)
            .ValidateDataAnnotations();

        builder.Services
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
        return builder;
    }
}
