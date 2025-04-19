namespace SchulPlanerBot.Business.Models;

public record Notification(DateTimeOffset StartAt, TimeSpan Between, ulong ChannelId);
