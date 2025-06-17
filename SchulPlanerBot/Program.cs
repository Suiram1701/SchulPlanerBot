using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.AspNetCore;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.TypeConverters;
using SchulPlanerBot.OpenTelemetry;
using SchulPlanerBot.Options;
using SchulPlanerBot.Quartz;
using SchulPlanerBot.Services;
using System.Globalization;
using System.Reflection;

namespace SchulPlanerBot;

public static class Program
{
    private const string _commandsLocalizationResource = "SchulplanerBot.Localization.ApplicationCommands";

    public static readonly CultureInfo[] SupportedCultures = [new("en-US"), new("de")];

    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.AddBotDatabase(KnownResourceNames.BotDatabase);
        builder.Services
            .AddDatabaseManagers()
            .AddSingleton<IgnoringService>();

        builder.Services
            .AddQuartz(ConfigureQuartz)
            .AddQuartzServer(options =>
            {
                options.AwaitApplicationStarted = true;
                options.WaitForJobsToComplete = true;
            })
            .AddHostedService<RegisterTriggers>();

        builder.Services.AddLocalization(options => options.ResourcesPath = "Localization");
        builder.Services.AddDiscordSocketClient("DiscordClient")
            .AddInteractionFramework(service =>
            {
                service.AddTypeConverter<string[]>(new StringArrayConverter());
                service.AddTypeConverter<CultureInfo>(new CultureInfoConverter(cultures: SupportedCultures));
                service.AddTypeConverter<DateTimeOffset>(new DateTimeOffsetConverter());
                service.AddComponentTypeConverter<DateTimeOffset>(new DateTimeOffsetComponentConverter());
            })
            .AddInteractionResXLocalization<ISchulPlanerBot>(_commandsLocalizationResource, SupportedCultures)
            .AddActivatedSingleton<PmMessageService>()
            .AddTransient<EmbedsService>()
            .AddTransient<ComponentService>();
        builder.Services.AddMemoryCache();

        builder.Services.AddOptions<HelpOptions>()
            .BindConfiguration("Help")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<ResponseOptions>()
            .BindConfiguration("Response")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOpenTelemetry()
            .WithTracing(provider => provider
                .SetSampler(new NoRootNameSampler(new AlwaysOnSampler(), KnownResourceNames.BotDatabase))
                .AddBotInstrumentation()
                .AddQuartzInstrumentation(options => options.RecordException = true)
                .AddDiscordNetInstrumentation())
            .WithMetrics(provider => provider
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddDiscordNetInstrumentation());

        WebApplication app = builder.Build();
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            AssemblyName appName = typeof(ISchulPlanerBot).Assembly.GetName();
            app.Logger.LogInformation(
                "Application {appName} v{version} started",
                appName.Name,
#if !DEBUG
                appName.Version
#else
                $"{appName.Version}-dev"
#endif
                );
        });

        app.MapDefaultEndpoints(always: true);     // Ok to register every endpoint because this container isn't exposed
        app.MapApiEndpoints();
        
        app.Run();
    }

    private static void ConfigureQuartz(IServiceCollectionQuartzConfigurator options)
    {
        options.AddJob<NotificationJob>(job => job
            .WithIdentity(Keys.NotificationJob)
            .WithDescription("Notifies users in a text channel at specific times.")
            .StoreDurably());
        options.AddJob<DeleteHomeworksJob>(job => job
            .WithIdentity(Keys.DeleteHomeworksJob)
            .WithDescription("Deletes homeworks of guilds a specific amount of time after their due.")
            .StoreDurably()
            .DisallowConcurrentExecution());
    }
}
