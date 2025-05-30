
namespace SchulPlanerBot.Services;

public sealed class IgnoringService
{
    private readonly HashSet<ulong> _ignoredGuilds = [];
    private readonly HashSet<ulong> _ignoredUsers = [];
    
    public IEnumerable<ulong> GetIgnoredGuilds() => _ignoredGuilds;
    
    public bool IsIgnoredGuild(ulong guildId) => _ignoredGuilds.Contains(guildId);
    
    public void AddIgnoredGuild(ulong guildId) => _ignoredGuilds.Add(guildId);
    
    public bool RemoveIgnoredGuild(ulong guildId) => _ignoredGuilds.Remove(guildId);
    
    public IEnumerable<ulong> GetIgnoredUsers() => _ignoredUsers;
    
    public bool IsIgnoredUser(ulong userId) => _ignoredUsers.Contains(userId);
    
    public void AddIgnoredUser(ulong userId) => _ignoredUsers.Add(userId);
    
    public bool RemoveIgnoredUser(ulong userId) => _ignoredUsers.Remove(userId);
}