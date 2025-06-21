using Discord;
using Humanizer;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord.UI.Models;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Discord.UI;

public class ComponentService(IStringLocalizer<ComponentService> loc, IOptionsSnapshot<ResponseOptions> optionsSnapshot)
{
    private readonly IStringLocalizer _loc = loc;
    private readonly ResponseOptions _options = optionsSnapshot.Value;

    public void LocalizeHomeworkModal(ModalBuilder builder, bool createHomework)
    {
        builder
            .WithTitle(loc[createHomework
                ? "homeworkModal.titleCreate"
                : "homeworkModal.titleModify"])
            .UpdateTextInput(ComponentIds.HomeworkModal.DueDate, input => input
                .WithLabel(loc["homeworkModal.dueDate"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Subject, input => input
                .WithLabel(loc["homeworkModal.subject"])
                .WithPlaceholder(loc["homeworkModal.subject.placeholder"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Title, input => input
                .WithLabel(loc["homeworkModal.title"])
                .WithPlaceholder(loc["homeworkModal.title.placeholder"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Details, input => input
                .WithLabel(loc["homeworkModal.details"])
                .WithPlaceholder(loc["homeworkModal.details.placeholder"]));
    }
    
    public MessageComponent HomeworkOverviewSelect(HomeworkOverview overview, string cacheId)
    {
        SelectMenuBuilder menuBuilder = new SelectMenuBuilder()
            .WithCustomId(overview.SelectCustomId)
            .WithPlaceholder(_loc["selectHomework.placeholder"]);
        foreach (Homework homework in overview.Homeworks
                     .Skip(overview.PageIndex * _options.MaxObjectsPerSelect)
                     .Take(_options.MaxObjectsPerSelect))
        {
            string label = string.IsNullOrEmpty(homework.Subject)
                ? homework.Title
                : $"{homework.Title} ({homework.Subject})";     // No need to check for MaxLength: 64 (Title) + 32 (Subject) + 3 (chars between) = 99 < 100
            menuBuilder.AddOption(
                label: label,
                description: !string.IsNullOrEmpty(homework.Details)
                    ? homework.Details.Truncate(SelectMenuOptionBuilder.MaxDescriptionLength)
                    : null,
                value: homework.Id.ToString(),
                isDefault: homework.Id == overview.DisplayedHomeworkId);
        }

        if (menuBuilder.Options.Count == 0)
        {
            menuBuilder
                .AddOption(label: _loc["selectHomework.noHomework"], value: "-1", isDefault: true)
                .WithDisabled(true);
        }
        
        var pages = (int)Math.Ceiling((float)overview.Homeworks.Length / _options.MaxObjectsPerSelect);
        
        ActionRowBuilder buttonRow = new ActionRowBuilder()
            .WithButton(
                label: _loc["selectHomework.pageBack"],
                customId: ComponentIds.CreateGetHomeworkPageComponent(pages - 1, cacheId),
                style: ButtonStyle.Secondary,
                disabled: overview.PageIndex <= 0)
            .WithButton(
                label: $"{overview.PageIndex + 1}/{Math.Max(pages, 1)}",     // At least one should be displayed
                customId: "0",
                style: ButtonStyle.Secondary,
                disabled: true)     // customId never used
            .WithButton(
                label: _loc["selectHomework.pageForward"],
                customId: ComponentIds.CreateGetHomeworkPageComponent(pages + 1, cacheId),
                style: ButtonStyle.Secondary,
                disabled: overview.PageIndex + 1 >= pages);

        return new ComponentBuilder()
            .AddRow(buttonRow)
            .WithSelectMenu(menuBuilder)
            .Build();
    }
}
