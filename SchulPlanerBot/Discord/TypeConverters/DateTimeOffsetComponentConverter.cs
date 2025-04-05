using Discord;
using Discord.Interactions;
using System;

namespace SchulPlanerBot.Discord.TypeConverters;

public class DateTimeOffsetComponentConverter(TimeZoneInfo timeZone): ComponentTypeConverter<DateTimeOffset>
{
    private readonly Func<TimeSpan> _defaultTimeOffsetProvider = () => timeZone.GetUtcOffset(DateTime.Now);

    public DateTimeOffsetComponentConverter() : this(TimeZoneInfo.Local) { }

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IComponentInteractionData option, IServiceProvider services)
    {
        TimeSpan defaultOffset = _defaultTimeOffsetProvider();
        return Task.FromResult(Conversion.ToDateTimeOffset(option.Value, defaultOffset));
    }
}
