using Discord;
using Humanizer;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Discord;

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

    private ActionRowBuilder Pagination(int pageIndex, int pages, Func<int, string> getCustomId)
    {
        return new ActionRowBuilder()
            .WithButton(label: _loc["page.back"], customId: getCustomId(pageIndex - 1), style: ButtonStyle.Secondary, disabled: pageIndex <= 0)
            .WithButton(label: $"{pageIndex + 1}/{pages}", customId: "0", style: ButtonStyle.Secondary, disabled: true)     // customId never used
            .WithButton(label: _loc["page.forward"], customId: getCustomId(pageIndex + 1), style: ButtonStyle.Secondary, disabled: pageIndex + 1 >= pages);
    }
    
    public MessageComponent SelectOverviewHomework(Homework[] homeworks, int currentPage, string cacheId, Guid? selectedHomeworkId = null)
    {
        SelectMenuBuilder menuBuilder = CreateSelectHomeworkComp(
            homeworks: homeworks.Skip(currentPage * _options.MaxObjectsPerSelect).Take(_options.MaxObjectsPerSelect),
            componentId: ComponentIds.CreateGetHomeworksSelectComponent(cacheId), 
            placeholder:_loc["selectHomework.placeholder"],
            selectHomeworkId: selectedHomeworkId);

        var pages = (int)Math.Ceiling((float)homeworks.Length / _options.MaxObjectsPerSelect);
        return new ComponentBuilder()
            .AddRow(Pagination(currentPage, pages, i => ComponentIds.CreateGetHomeworkPageComponent(i, cacheId)))
            .WithSelectMenu(menuBuilder)
            .Build();
    }

    public MessageComponent SelectModifyHomework(IEnumerable<Homework> homeworks)
    {
        return new ComponentBuilder()
            .WithSelectMenu(CreateSelectHomeworkComp(homeworks, ComponentIds.ModifyHomeworkSelectComponent,
                _loc["modifyHomework.placeholder"]))
            .Build();
    }

    public MessageComponent SelectDeleteHomework(IEnumerable<Homework> homeworks)
    {
        return new ComponentBuilder()
            .WithSelectMenu(CreateSelectHomeworkComp(homeworks, ComponentIds.DeleteHomeworkSelectComponent,
                _loc["deleteHomework.placeholder"]))
            .Build();
    }
    
    private SelectMenuBuilder CreateSelectHomeworkComp(IEnumerable<Homework> homeworks, string componentId, string placeholder, Guid? selectHomeworkId = null)
    {
        SelectMenuBuilder menuBuilder = new SelectMenuBuilder()
            .WithCustomId(componentId)
            .WithPlaceholder(placeholder);
        foreach (Homework homework in homeworks)
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
                isDefault: homework.Id == selectHomeworkId);
        }

        if (menuBuilder.Options.Count == 0)
        {
            menuBuilder
                .AddOption(label: _loc["selectHomework.noHomework"], value: "-1", isDefault: true)
                .WithDisabled(true);
        }
        
        return menuBuilder;
    }
}
