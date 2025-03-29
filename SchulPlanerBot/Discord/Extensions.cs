using Discord;
using Discord.Interactions;
using SchulPlanerBot.Business.Errors;

namespace SchulPlanerBot.Discord;

public static class Extensions
{
    public static async Task RespondWithErrorAsync<TContext>(this InteractionModuleBase<TContext> module, UpdateError[] errors, ILogger? logger = null)
        where TContext : class, IInteractionContext
    {
        if (errors.Length == 0)
        {
            throw new ArgumentException("At least one element have to provided!", nameof(errors));
        }
        else if (errors.Length == 1)
        {
            UpdateError error = errors[0]; 

            logger?.LogTrace("Update error occurred: {error}", error.Name);
            await module.Context.Interaction.RespondAsync(error.Description, ephemeral: true).ConfigureAwait(false);
        }
        else
        {
            logger?.LogTrace("Multiple update error occurred: {errors}", string.Join(',', errors.Select(e => e.Name)));

            string responseMessage = string.Join('\n', errors.Select(e => e.Description));
            await module.Context.Interaction.RespondAsync(responseMessage, ephemeral: true).ConfigureAwait(false);
        }
    }
}
