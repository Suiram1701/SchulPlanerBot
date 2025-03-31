using SchulPlanerBot.ServiceDefaults;

namespace SchulPlanerBot.AppHost;

public class Program
{
    public static void Main(string[] args)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

        IResourceBuilder<PostgresDatabaseResource> botDb = builder.AddPostgres("postgres-server")
            .WithDataVolume()
            .WithPgAdmin(subBuilder => subBuilder.WithExternalHttpEndpoints())      // Makes the db accessable from outside
            .AddDatabase(ResourceNames.BotDatabase);

        builder.AddProject<Projects.SchulPlanerBot>("discord-bot")
            .WithConfiguration(builder.Configuration.GetSection("DiscordClient"), secretKeys: "Token")
            .WithReference(botDb)
            .WaitFor(botDb);

        builder.Build().Run();
    }
}