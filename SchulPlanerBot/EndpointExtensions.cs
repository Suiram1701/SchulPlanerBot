using SchulPlanerBot.Services;

namespace SchulPlanerBot;

internal static class EndpointExtensions
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/ignoredGuilds", GetIgnoredGuilds);
        endpoints.MapPut("/api/ignoredGuilds", IgnoreGuilds);
        endpoints.MapDelete("/api/ignoredGuilds/{guildId}", RemoveIgnoredGuild);
        
        endpoints.MapGet("/api/ignoredUsers", GetIgnoredUsers);
        endpoints.MapPut("/api/ignoredUsers", IgnoreUsers);
        endpoints.MapDelete("/api/ignoredUsers/{userId}", RemoveIgnoredUser);
    }

    private static IResult GetIgnoredGuilds(IgnoringService service) => Results.Ok(service.GetIgnoredGuilds());
    
    private static IResult IgnoreGuilds(IgnoringService service, ulong[] guildIds)
    {
        foreach (ulong id in guildIds)
            service.AddIgnoredGuild(id);
        return Results.Ok();
    }
    
    private static IResult RemoveIgnoredGuild(IgnoringService service, ulong guildId)
    {
        return service.RemoveIgnoredGuild(guildId)
            ? Results.Ok()
            : Results.NotFound();
    }

    private static IResult GetIgnoredUsers(IgnoringService service) => Results.Ok(service.GetIgnoredUsers());
    
    private static IResult IgnoreUsers(IgnoringService service, ulong[] userIds)
    {
        foreach (ulong id in userIds)
            service.AddIgnoredUser(id);
        return Results.Ok();
    }
    
    private static IResult RemoveIgnoredUser(IgnoringService service, ulong userId)
    {
        return service.RemoveIgnoredUser(userId)
            ? Results.Ok()
            : Results.NotFound();
    }
}