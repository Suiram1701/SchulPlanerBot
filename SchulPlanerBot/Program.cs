using SchulPlanerBot.ServiceDefaults;
using System.Globalization;

namespace SchulPlanerBot;

public class Program
{
    private const string _commandsLocalizationResource = "SchulplanerBot.Localization.ApplicationCommands";

    private static readonly CultureInfo[] _supportedCultures = [new("en-US"), new("de")];

    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.AddBotDatabase(ResourceNames.BotDatabase);

        builder.Services.AddDatabaseManagers();

        builder.Services.AddDiscordSocketClient("DiscordClient")
            .AddInteractionFramework()
            .AddInteractionResxLocalization<ISchulPlanerBot>(_commandsLocalizationResource, _supportedCultures);

        builder.Services.AddLocalization(options => options.ResourcesPath = "Localization");

        builder.Services.AddOpenTelemetry()
            .WithTracing(provider => provider
                .AddBotDatabaseInstrumentation()
                .AddDiscordClientInstrumentation());

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();

        app.Run();
    }
}
