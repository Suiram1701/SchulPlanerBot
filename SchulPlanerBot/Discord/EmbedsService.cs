using Discord;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Discord;

public class EmbedsService(IStringLocalizer<EmbedsService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    private const string _invisibleChar = "\u200B";

    public Embed Homework(Homework homework)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithColor(Color.LightGrey)
            .WithAuthor(new EmbedAuthorBuilder().WithName(homework.Subject))
            .WithTitle(homework.Title)
            .WithDescription(homework.Details)
            .AddField(_localizer["homeworkEmbed.due"], Utilities.Timestamp(homework.Due, TimestampKind.Relative))
            .AddField(_localizer["homeworkEmbed.creator"], Utilities.Mention(homework.CreatedBy, MentionType.User), inline: true)
            .AddField(_localizer["homeworkEmbed.created"], Utilities.Timestamp(homework.CreatedAt, TimestampKind.ShortDate), inline: true);

        if (homework.LastModifiedBy is not null && homework.LastModifiedAt is not null)
        {
            builder = builder
                .AddField(_invisibleChar, _invisibleChar, inline: false)     // Improved layout
                .AddField(_localizer["homeworkEmbed.lastEditor"], Utilities.Mention(homework.LastModifiedBy.Value, MentionType.User), inline: true)
                .AddField(_localizer["homeworkEmbed.lastEdited"], Utilities.Timestamp(homework.LastModifiedAt.Value, TimestampKind.ShortDate), inline: true);
        }

        return builder
            .WithFooter(homework.Id.ToString())
            .Build();
    }
}
