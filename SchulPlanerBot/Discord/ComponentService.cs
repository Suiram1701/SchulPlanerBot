using Discord;
using Humanizer;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Discord;

public class ComponentService(IStringLocalizer<ComponentService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    public void LocalizeHomeworkModal(ModalBuilder builder, bool createHomework)
    {
        builder
            .WithTitle(localizer[createHomework
                ? "homeworkModal.titleCreate"
                : "homeworkModal.titleModify"])
            .UpdateTextInput(ComponentIds.HomeworkModal.DueDate, input => input
                .WithLabel(localizer["homeworkModal.dueDate"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Subject, input => input
                .WithLabel(localizer["homeworkModal.subject"])
                .WithPlaceholder(localizer["homeworkModal.subject.placeholder"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Title, input => input
                .WithLabel(localizer["homeworkModal.title"])
                .WithPlaceholder(localizer["homeworkModal.title.placeholder"]))
            .UpdateTextInput(ComponentIds.HomeworkModal.Details, input => input
                .WithLabel(localizer["homeworkModal.details"])
                .WithPlaceholder(localizer["homeworkModal.details.placeholder"]));
    }

    /// <summary>
    /// Creates a new select menu for showing more information about a specific homework.
    /// </summary>
    /// <remarks>
    /// Handled by <see cref="Modules.HomeworksModule.GetHomeworks_InteractAsync(string[])"/>
    /// </remarks>
    /// <param name="homeworks">The homeworks to show as an option.</param>
    /// <returns>The build component</returns>
    public MessageComponent SelectHomework(IEnumerable<Homework> homeworks, string cacheId)
    {
        SelectMenuBuilder menuBuilder = new SelectMenuBuilder()
                .WithCustomId(ComponentIds.CreateGetHomeworksSelectComponent(cacheId))
                .WithPlaceholder(_localizer["selectHomework.placeholder"]);
        foreach (Homework homework in homeworks)
        {
            menuBuilder.AddOption(
                label: homework.Title,
                description: !string.IsNullOrEmpty(homework.Details)
                    ? homework.Details.Truncate(SelectMenuOptionBuilder.MaxDescriptionLength)
                    : null,
                value: homework.Id.ToString());
        }

        ButtonBuilder buttonBuilder = new ButtonBuilder()
            .WithCustomId(ComponentIds.CreateGetHomeworksReloadComponent(cacheId))
            .WithLabel(_localizer["selectHomework.reloadBtn"])
            .WithStyle(ButtonStyle.Secondary);

        return new ComponentBuilder()
            .WithSelectMenu(menuBuilder)
            .WithButton(buttonBuilder)
            .Build();
    }
}
