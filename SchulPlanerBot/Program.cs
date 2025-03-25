using OpenTelemetry.Trace;
using Quartz;
using Quartz.AspNetCore;
using SchulPlanerBot.Quartz;
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

        builder.Services
            .AddQuartz(options =>
            {
                options.AddJob<NotificationJob>(job => job
                    .WithIdentity(Keys.NotificationJob)
                    .WithDescription("Notifies users in a text channel at specific times")
                    .StoreDurably());

                options.UsePersistentStore(ado =>
                {
                    ado.UseNewtonsoftJsonSerializer();
                    ado.UsePostgres(pg =>
                    {
                        pg.ConnectionStringName = ResourceNames.BotDatabase;
                        pg.TablePrefix = "quartz.qrtz_";     // Configure the used database schema
                    });
                });
            })
            .AddQuartzServer(options =>
            {
                options.AwaitApplicationStarted = true;
                options.WaitForJobsToComplete = true;
            });

        builder.Services.AddDiscordSocketClient("DiscordClient")
            .AddInteractionFramework()
            .AddInteractionResXLocalization<ISchulPlanerBot>(_commandsLocalizationResource, _supportedCultures);

        builder.Services.AddLocalization(options => options.ResourcesPath = "Localization");

        builder.Services.AddOpenTelemetry()
            .WithTracing(provider => provider
                .AddBotDatabaseInstrumentation()
                .AddQuartzInstrumentation(options => options.RecordException = true)
                .AddDiscordNetInstrumentation())
            .WithMetrics(provider => provider
                .AddDiscordNetInstrumentation());

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();

        app.Run();
    }
}
