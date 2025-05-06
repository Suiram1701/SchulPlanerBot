namespace SchulPlanerBot.AppHost;

public class Program
{
    public static void Main(string[] args)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
        IResourceBuilder<ParameterResource> timeZoneResource = builder.AddParameterFromConfiguration("TimeZone", "TimeZone");

        IResourceBuilder<PostgresDatabaseResource> botDb = builder.AddPostgres("postgres-server")
            .WithDataVolume()
            .WithExternalTcpEndpoints()
            .WithPgAdmin()
            .AddDatabase(KnownResourceNames.BotDatabase);

        builder.AddProject<Projects.SchulPlanerBot>("discord-bot")
            .WithConfiguration(builder.Configuration.GetSection("DiscordClient"), secretKeys: "Token")
            .WithEnvironment("TZ", timeZoneResource)
            .WithReference(botDb)
            .WaitFor(botDb);

        builder.Build().Run();
    }
}