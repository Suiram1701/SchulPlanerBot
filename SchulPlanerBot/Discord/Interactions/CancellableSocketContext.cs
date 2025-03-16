using Discord.Interactions;
using Discord.WebSocket;

namespace SchulPlanerBot.Discord.Interactions;

public class CancellableSocketContext(
    DiscordSocketClient client,
    SocketInteraction interaction,
    CancellationToken? cancellationToken = null) : SocketInteractionContext(client, interaction)
{
    public CancellationToken CancellationToken => cancellationToken ?? CancellationToken.None;
}
