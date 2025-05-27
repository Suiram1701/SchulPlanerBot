using Discord;
using Discord.Interactions;
using SchulPlanerBot.Business.Errors;

namespace SchulPlanerBot.Discord;

public static class Extensions
{
    public static  Task RespondWithErrorAsync<TContext>(this InteractionModuleBase<TContext> module, UpdateError[] errors, ILogger? logger = null)
        where TContext : class, IInteractionContext
    {
        string message;
        switch (errors.Length)
        {
            case 0:
                throw new ArgumentException("At least one element have to provided!", nameof(errors));
            case 1:
            {
                UpdateError error = errors[0];

                logger?.LogTrace("Update error occurred: {error}", error.Name);
                message = error.Description;
                break;
            }
            default:
                logger?.LogTrace("Multiple update error occurred: {errors}", string.Join(',', errors.Select(e => e.Name)));
                message = string.Join('\n', errors.Select(e => e.Description));
                break;
        }

        message = Utils.UseAnsiFormat($"{Emoji.Parse(":warning:")} {message}", Utils.AnsiColor.Yellow);
        return module.Context.Interaction.RespondAsync(message, ephemeral: true);
    }
}
