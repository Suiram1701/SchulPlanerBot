namespace SchulPlanerBot.AppHost;

public class Program
{
    public static void Main(string[] args)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
        builder.AddProject<Projects.SchulPlanerBot>("discord-bot");

        builder.Build().Run();
    }
}