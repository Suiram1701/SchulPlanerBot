using Microsoft.Extensions.Configuration;

namespace SchulPlanerBot.AppHost;

public static class Program
{
    public static void Main(string[] args)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

        IResourceBuilder<IResourceWithConnectionString> botDb;
        if (!builder.ExecutionContext.IsPublishMode)
        {
            botDb = builder.AddPostgres("postgres-server")
                .WithDataVolume()
                .WithPgAdmin()
                .AddDatabase(KnownResourceNames.BotDatabase);
        }
        else
        {
            botDb = builder.AddConnectionString(KnownResourceNames.BotDatabase);
        }

        IResourceBuilder<ProjectResource> discordBot = builder.AddProject<Projects.SchulPlanerBot>("discord-bot")
            .WithConfiguration(builder.Configuration.GetSection("DiscordClient"), secretKeys: "Token")
            .WithEnvironment("TZ", builder.AddParameterFromConfiguration("TimeZone", "TimeZone"))
            .WithReference(botDb)
            .WaitFor(botDb);

        IConfigurationSection otelSection = builder.Configuration.GetSection("Otel");
        if (builder.ExecutionContext.IsPublishMode && otelSection.Exists())
        {
            discordBot.WithEnvironment("OTEL_SERVICE_NAME",
                builder.AddParameterFromConfiguration("ServiceName", "OTel:ServiceName"));
            
            foreach (string data in new[] { "Traces", "Metrics", "Logs" })
            {
                if (otelSection.GetSection(data).Exists())
                {
                    discordBot
                        .WithEnvironment($"OTEL_EXPORTER_OTLP_{data.ToUpper()}_ENDPOINT",
                            builder.AddParameterFromConfiguration(data + "Endpoint", $"OTel:{data}:Endpoint"))
                        .WithEnvironment($"OTEL_EXPORTER_OTLP_{data.ToUpper()}_PROTOCOL",
                            builder.AddParameterFromConfiguration(data + "Protocol", $"OTel:{data}:Protocol"))
                        .WithEnvironment($"OTEL_EXPORTER_OTLP_{data.ToUpper()}_HEADERS",
                            builder.AddParameterFromConfiguration(data + "Headers", $"OTel:{data}:Headers", secret: true));     // May contains auth
                }
            }
        }

        builder.Build().Run();
    }
}