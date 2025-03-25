using Discord;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Discord;

public class EmbedsService(IStringLocalizer<EmbedsService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    public Embed Homework(Homework homework)
    {
        return new EmbedBuilder()
            .WithAuthor(new EmbedAuthorBuilder().WithName(homework.Subject))
            .WithTitle(homework.Title)
            .WithDescription(homework.Details)
            .AddField(_localizer["homeworkEmbed.due"], Utilities.Timestamp(homework.Due, TimestampKind.Relative))
            .AddField(_localizer["homeworkEmbed.creator"], Utilities.Mention(homework.CreatedBy, MentionType.User))
            .AddField(_localizer["homeworkEmbed.created"], Utilities.Timestamp(homework.CreatedAt, TimestampKind.ShortDate))
            .AddField(_localizer["homeworkEmbed.id"], $"||{homework.Id}||")
            .Build();
    }
}
