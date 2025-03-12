namespace SchulPlanerBot;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.AddDiscordClient("DiscordClient");

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();

        app.Run();
    }
}
