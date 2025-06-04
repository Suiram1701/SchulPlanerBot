using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Services;

internal sealed class PmMessageService : IDisposable
{
    private readonly ILogger _logger;
    private readonly IStringLocalizer _loc;
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    
    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public const string ActivitySourceName = nameof(PmMessageService);

    public PmMessageService(ILogger<PmMessageService> logger, IStringLocalizer<PmMessageService> loc, DiscordSocketClient client, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _loc = loc;
        _client = client;
        _scopeFactory = scopeFactory;
        
        _client.MessageReceived += Client_MessageReceivedAsync;
        _client.ChannelDestroyed += Client_ChannelDestroyedAsync;
    }

    private async Task Client_ChannelDestroyedAsync(SocketChannel channel)
    {
        if (channel is not SocketTextChannel textChannel)
            return;
        SocketGuild guild = textChannel.Guild;
        
        using Activity? activity =
            _activitySource.StartActivity(name: "Textchannel destroyed received", kind: ActivityKind.Consumer, tags: [
                KeyValuePair.Create<string, object?>("channel.id", textChannel.Id),
                KeyValuePair.Create<string, object?>("channel.name", textChannel.Name),
                KeyValuePair.Create<string, object?>("guild.id", guild.Id),
                KeyValuePair.Create<string, object?>("guild.name", guild.Name)
            ]);
        
        using IServiceScope scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<SchulPlanerManager>();

        IEnumerable<Notification> notifications =
            await manager.GetNotificationsAsync(guild.Id).ConfigureAwait(false);
        Notification? notification = notifications.FirstOrDefault(n => n.ChannelId == textChannel.Id);
        if (notification is not null)
        {
            _logger.LogWarning(
                "Textchannel {channelId} destroyed received! Channel is being used in a notification of guild {guildId}",
                textChannel.Id,
                guild.Id);
            
            await manager.RemoveNotificationFromSchedulerAsync(notification, CancellationToken.None).ConfigureAwait(false);
            UpdateResult removeResult = await manager.RemoveNotificationAsync(guild.Id, notification.ChannelId).ConfigureAwait(false);
            if (!removeResult.Success)
            {
                string errorsStr = string.Join(", ", removeResult.Errors.Select(e => e.Name));
                _logger.LogError("An error occurred while removing notification! Errors: {errors}", errorsStr);
            }
            
            await guild.Owner.SendMessageAsync(_loc[
                "notificationChannelRemoved",
                guild.Name,
                guild.Id,
                textChannel.Mention
            ]).ConfigureAwait(false);
            _logger.LogInformation("Guild owner {ownerId} of guild {guildId} notified, notification removed", guild.OwnerId, guild.Id);
        }
        else
        {
            _logger.LogTrace("Textchannel {channelId} destroyed received! Channel not being used", textChannel.Id);
        }
    }

    private async Task Client_MessageReceivedAsync(SocketMessage message)
    {
        Activity.Current = null;     // Activity doesn't have a parent
        
        // Message is sent by a user (except for the bot its self) in a private way (not from a guild)
        if (message is SocketUserMessage { Author.IsBot: false } && message.Channel is IPrivateChannel)
        {
            using Activity? activity =
                _activitySource.StartActivity(name: "PM message received", kind: ActivityKind.Consumer, tags:
                [
                    KeyValuePair.Create<string, object?>("user.id", message.Author.Id),
                    KeyValuePair.Create<string, object?>("user.name", message.Author.GlobalName)
                ]);
            
            await message.Channel.SendMessageAsync(_loc["dmResponse"], messageReference: new MessageReference(messageId: message.Id)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _client.MessageReceived -= Client_MessageReceivedAsync;
        _client.ChannelDestroyed -= Client_ChannelDestroyedAsync;
        
        _activitySource.Dispose();
    }
}