using System.Text;
using Discord;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord.UI.Models;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Discord.UI;

public class EmbedsService(IStringLocalizer<EmbedsService> localizer, IOptionsSnapshot<ResponseOptions> optionsSnapshot)
{
    private readonly IStringLocalizer _localizer = localizer;
    private readonly ResponseOptions _options = optionsSnapshot.Value;

    private const string _invisibleChar = "\u200B";

    public Embed Homework(Homework homework)
    {
        EmbedBuilder builder = new EmbedBuilder()
            .WithColor(Color.LightGrey)
            .WithAuthor(a => a.WithName(homework.Subject))
            .WithTitle(homework.Title)
            .WithDescription(homework.Details)
            .AddField(_localizer["homeworkEmbed.due"], TimestampTag.FromDateTimeOffset(homework.Due.ToLocalTime(), TimestampTagStyles.Relative))
            .AddField(_localizer["homeworkEmbed.creator"], MentionUtils.MentionUser(homework.CreatedBy), inline: true)
            .AddField(_localizer["homeworkEmbed.created"], TimestampTag.FromDateTimeOffset(homework.CreatedAt.ToLocalTime(), TimestampTagStyles.ShortDate), inline: true);

        if (homework.LastModifiedBy is not null && homework.LastModifiedAt is not null)
        {
            builder = builder
                .AddField(_invisibleChar, _invisibleChar, inline: false)     // Improved layout
                .AddField(_localizer["homeworkEmbed.lastEditor"], MentionUtils.MentionUser(homework.LastModifiedBy.Value), inline: true)
                .AddField(_localizer["homeworkEmbed.lastEdited"], TimestampTag.FromDateTimeOffset(homework.LastModifiedAt.Value.ToLocalTime(), TimestampTagStyles.ShortDate), inline: true);
        }

        return builder
            .WithFooter(homework.Id.ToString())
            .Build();
    }

    public Embed HomeworksOverview(HomeworkOverview overview)
    {
        StringBuilder descBuilder = new();
        foreach (Homework homework in overview.Homeworks
                     .Skip(overview.PageIndex * _options.MaxObjectsPerSelect)
                     .Take(_options.MaxObjectsPerSelect))
        {
            TimestampTag dueTag =
                TimestampTag.FromDateTimeOffset(homework.Due.ToLocalTime(), TimestampTagStyles.ShortDate);
            descBuilder.Append($"**{dueTag}**: ");

            if (homework.Id == overview.DisplayedHomeworkId)     // Starts bold
                descBuilder.Append("**");
                
            if (string.IsNullOrEmpty(homework.Subject))
                descBuilder.Append($"({homework.Subject})");
            else
                descBuilder.Append($"{homework.Title} ({homework.Subject})");
            
            if (homework.Id == overview.DisplayedHomeworkId)     // Ends bold
                descBuilder.Append("**");
            
            descBuilder.AppendLine();
        }
        
        if (descBuilder.Length == 0)
            descBuilder.Append(_localizer["homeworkOverviewEmbed.placeholder"]);

        var title = string.Empty;
        if (overview.Start is not null)
        {
            title = overview.End is not null
                ? _localizer["homeworkOverviewEmbed.title", overview.Start.Value.ToString("d"),
                    overview.End.Value.ToString("d")]
                : _localizer["homeworkOverviewEmbed.titleNoEnd", overview.Start.Value.ToString("d")];
        }
        return new EmbedBuilder()
            .WithColor(Color.LightGrey)
            .WithAuthor(a => a.WithName(title))
            .WithDescription(descBuilder.ToString())
            .Build();
    }
}
