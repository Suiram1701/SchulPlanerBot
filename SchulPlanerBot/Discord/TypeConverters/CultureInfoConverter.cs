using Discord;
using Discord.Interactions;
using System.Globalization;

namespace SchulPlanerBot.Discord.TypeConverters;

public class CultureInfoConverter(bool supportDisplayName = true, CultureInfo[]? cultures = null) : TypeConverter<CultureInfo>
{
    private readonly bool _supportDisplayName = supportDisplayName && (cultures?.Length ?? 0) == 0;
    private readonly CultureInfo[]? _cultures = cultures;

    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        if (_cultures is not null)
        {
            properties.Choices.Clear();
            foreach (CultureInfo culture in _cultures.OrderBy(c => c.Name))
            {
                properties.Choices.Add(new()
                {
                    Name = culture.NativeName,
                    Value = culture.Name
                });
            }
        }

        base.Write(properties, parameter);
    }

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string value = option.Value.ToString()!;

        try
        {
            CultureInfo info = CultureInfo.GetCultureInfo(value);
            return Task.FromResult(TypeConverterResult.FromSuccess(info));
        }
        catch (CultureNotFoundException)
        {
        }

        if (_supportDisplayName)
        {
            CultureInfo? info = CultureInfo
                .GetCultures(CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
                .SingleOrDefault(ci => ci.DisplayName == value || ci.NativeName == value);
            if (info is not null)
                return Task.FromResult(TypeConverterResult.FromSuccess(info!));
        }

        return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, "Provided language could not be recognized."));
    }
}
