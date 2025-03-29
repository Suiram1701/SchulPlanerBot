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
            .AddField(_localizer["homeworkEmbed.due"], TimestampTag.FromDateTimeOffset(homework.Due, TimestampTagStyles.Relative))
            .AddField(_localizer["homeworkEmbed.creator"], MentionUtils.MentionUser(homework.CreatedBy), inline: true)
            .AddField(_localizer["homeworkEmbed.created"], TimestampTag.FromDateTimeOffset(homework.CreatedAt, TimestampTagStyles.ShortDate), inline: true);

        if (homework.LastModifiedBy is not null && homework.LastModifiedAt is not null)
        {
            builder = builder
                .AddField(_invisibleChar, _invisibleChar, inline: false)     // Improved layout
                .AddField(_localizer["homeworkEmbed.lastEditor"], MentionUtils.MentionUser(homework.LastModifiedBy.Value), inline: true)
                .AddField(_localizer["homeworkEmbed.lastEdited"], TimestampTag.FromDateTimeOffset(homework.LastModifiedAt.Value, TimestampTagStyles.ShortDate), inline: true);
        }

        return builder
            .WithFooter(homework.Id.ToString())
            .Build();
    }
}
