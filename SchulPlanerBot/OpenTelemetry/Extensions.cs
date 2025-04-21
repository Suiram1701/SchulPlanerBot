using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SchulPlanerBot.Services;

namespace SchulPlanerBot.OpenTelemetry;

public static class Extensions
{
    public static TracerProviderBuilder AddBotInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DatabaseStartup.ActivitySourceName, RegisterTriggers.ActivitySourceName);
    }

    public static TracerProviderBuilder AddDiscordNetInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DiscordClientManager.ActivitySourceName, DiscordClientMetrics.ActivitySourceName, InteractionHandler.ActivitySourceName);
    }

    public static MeterProviderBuilder AddDiscordNetInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .ConfigureServices(services => services
                .AddSingleton<DiscordClientMetrics>()
                .AddSingleton<InteractionFrameworkMetrics>())
            .AddInstrumentation(sp => sp.GetRequiredService<DiscordClientMetrics>())
            .AddInstrumentation(sp => sp.GetRequiredService<InteractionFrameworkMetrics>())
            .AddMeter(DiscordClientMetrics.MeterName, InteractionFrameworkMetrics.MeterName);
    }
}
