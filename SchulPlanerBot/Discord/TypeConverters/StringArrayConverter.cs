using Discord;
using Discord.Interactions;

namespace SchulPlanerBot.Discord.TypeConverters;

public sealed class StringArrayConverter(params string[] separators) : TypeConverter<string[]>
{
    private readonly string[] _separators = separators.Length == 0 ? [",", ";"] : separators;

    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string value = option.Value.ToString()!;
        string[] items = value.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Task.FromResult(TypeConverterResult.FromSuccess(items));
    }
}
