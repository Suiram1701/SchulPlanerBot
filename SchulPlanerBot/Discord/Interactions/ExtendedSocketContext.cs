using Discord.Interactions;
using Discord.WebSocket;
using System.Diagnostics;

namespace SchulPlanerBot.Discord.Interactions;

public class ExtendedSocketContext(
    DiscordSocketClient client,
    SocketInteraction interaction,
    Activity? activity = null,
    CancellationToken? cancellationToken = null) : SocketInteractionContext(client, interaction)
{
    public Activity? Activity => activity;

    public CancellationToken CancellationToken => cancellationToken ?? CancellationToken.None;
}
