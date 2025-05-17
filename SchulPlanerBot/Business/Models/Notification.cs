namespace SchulPlanerBot.Business.Models;

public record Notification(DateTimeOffset StartAt, TimeSpan Between, TimeSpan ObjectsIn, ulong ChannelId);
