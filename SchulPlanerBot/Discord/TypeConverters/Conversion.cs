using Discord.Interactions;

namespace SchulPlanerBot.Discord.TypeConverters;

internal static class Conversion
{
    public static TypeConverterResult ToDateTimeOffset(string? value, TimeSpan defaultOffset)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, "A value were expected!");

        if (DateTime.TryParse(value, out DateTime dateTime))
        {
            DateTimeOffset dateTimOffset = dateTime.Kind == DateTimeKind.Unspecified
                ? new(dateTime, defaultOffset)
                : new(dateTime);
            return TypeConverterResult.FromSuccess(dateTimOffset);
        }
        else
        {
            return TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, $"The provided value \"{value}\" were not recognized as a valid date time!");
        }
    }
}
