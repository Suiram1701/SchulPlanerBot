using OpenTelemetry.Trace;
using Quartz;
using Quartz.AspNetCore;
using SchulPlanerBot.Discord.TypeConverters;
using SchulPlanerBot.Quartz;
using SchulPlanerBot.ServiceDefaults;
using System.Globalization;

namespace SchulPlanerBot;

public class Program
{
    private const string _commandsLocalizationResource = "SchulplanerBot.Localization.ApplicationCommands";

    public static readonly CultureInfo[] SupportedCultures = [new("en-US"), new("de")];

    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.AddBotDatabase(ResourceNames.BotDatabase);

        builder.Services.AddDatabaseManagers();

        builder.Services
            .AddQuartz(ConfigureQuartz)
            .AddQuartzServer(options =>
            {
                options.AwaitApplicationStarted = true;
                options.WaitForJobsToComplete = true;
            });

        builder.Services.AddDiscordSocketClient("DiscordClient")
            .AddInteractionFramework(service =>
            {
                service.AddTypeConverter<string[]>(new StringArrayConverter());
                service.AddTypeConverter<CultureInfo>(new CultureInfoConverter(cultures: SupportedCultures));
                service.AddTypeConverter<DateTimeOffset>(new DateTimeOffsetConverter());
                service.AddComponentTypeConverter<DateTimeOffset>(new DateTimeOffsetComponentConverter());
            })
            .AddInteractionResXLocalization<ISchulPlanerBot>(_commandsLocalizationResource, SupportedCultures);

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

    private static void ConfigureQuartz(IServiceCollectionQuartzConfigurator options)
    {
        options.AddJob<NotificationJob>(job => job
            .WithIdentity(Keys.NotificationJob)
            .WithDescription("Notifies users in a text channel at specific times")
            .StoreDurably());
        options.AddJob<DeleteHomeworksJob>(job => job
            .WithIdentity(Keys.DeleteHomeworksJob)
            .WithDescription("Deletes homeworks of guilds a specific amount of time after their due.")
            .DisallowConcurrentExecution());

        options.AddTrigger(trigger => trigger
            .WithIdentity(Keys.DeleteHomeworksKey)
            .WithDescription("Triggers this job every 30m.")
            .ForJob(Keys.DeleteHomeworksJob)
            .StartNow()
            .WithSimpleSchedule(scheduler => scheduler
                .WithIntervalInMinutes(30)
                .RepeatForever()
                .WithMisfireHandlingInstructionFireNow()));

        options.UsePersistentStore(ado =>
        {
            ado.UseNewtonsoftJsonSerializer();
            ado.UsePostgres(pg =>
            {
                pg.ConnectionStringName = ResourceNames.BotDatabase;
                pg.TablePrefix = "quartz.qrtz_";     // Configure the used database schema
            });
        });
    }
}
