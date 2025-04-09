using Discord;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Discord;

public class ComponentService(IStringLocalizer<ComponentService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    /// <summary>
    /// Creates a new select menu for showing more information about a specific homework.
    /// </summary>
    /// <remarks>
    /// Handled by <see cref="Modules.HomeworksModule.GetHomeworks_InteractAsync(string[])"/>
    /// </remarks>
    /// <param name="homeworks">The homeworks to show as an option.</param>
    /// <returns>The build component</returns>
    public MessageComponent SelectHomework(IEnumerable<Homework> homeworks)
    {
        SelectMenuBuilder menuBuilder = new SelectMenuBuilder()
                .WithCustomId(ComponentIds.ChangeSelectedHomeworkSelection)
                .WithPlaceholder(_localizer["selectHomework.placeholder"]);
        foreach (Homework homework in homeworks)
        {
            menuBuilder.AddOption(
                label: homework.Title,
                description: homework.Details,
                value: homework.Id.ToString());
        }

        return new ComponentBuilder()
            .WithSelectMenu(menuBuilder)
            .Build();
    }
}
