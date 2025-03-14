using Discord;
using Discord.Interactions;

namespace SchulPlanerBot.Modules;

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
[Group("schulplaner", "Settings of the app on this server.")]
public sealed class ServerModule : InteractionModuleBase<SocketInteractionContext>
{
}
