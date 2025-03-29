using Discord;
using Discord.Interactions;
using SchulPlanerBot.Business.Errors;

namespace SchulPlanerBot.Discord;

public static class Extensions
{
    public static async Task RespondWithErrorAsync<TContext>(this InteractionModuleBase<TContext> module, UpdateError[] errors, ILogger? logger = null)
        where TContext : class, IInteractionContext
    {
        string message;
        if (errors.Length == 0)
        {
            throw new ArgumentException("At least one element have to provided!", nameof(errors));
        }
        else if (errors.Length == 1)
        {
            UpdateError error = errors[0];

            logger?.LogTrace("Update error occurred: {error}", error.Name);
            message = error.Description;
        }
        else
        {
            logger?.LogTrace("Multiple update error occurred: {errors}", string.Join(',', errors.Select(e => e.Name)));
            message = string.Join('\n', errors.Select(e => e.Description));
        }

        message = Utils.UseAnsiFormat($"{Emoji.Parse(":warning:")} {message}", Utils.AnsiColor.Yellow);
        await module.Context.Interaction.RespondAsync(message, ephemeral: true).ConfigureAwait(false);
    }
}
