namespace SchulPlanerBot;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddDiscordSocketClient("DiscordClient")
            .AddInteractionFramework();

        builder.Services.AddOpenTelemetry()
            .WithTracing(provider => provider.AddDiscordClientInstrumentation());

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();

        app.Run();
    }
}
