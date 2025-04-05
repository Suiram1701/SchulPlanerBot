using Discord;
using Discord.Interactions;

namespace SchulPlanerBot.Discord.TypeConverters;

public class DateTimeOffsetConverter(TimeZoneInfo timeZone) : TypeConverter<DateTimeOffset>
{
    private readonly Func<TimeSpan> _defaultTimeOffsetProvider = () => timeZone.GetUtcOffset(DateTime.Now);

    public DateTimeOffsetConverter() : this(TimeZoneInfo.Local) { }

    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        TimeSpan defaultOffset = _defaultTimeOffsetProvider();
        return Task.FromResult(Conversion.ToDateTimeOffset(option.Value.ToString(), defaultOffset));
    }
}
