namespace SchulPlanerBot;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();

        app.Run();
    }
}
