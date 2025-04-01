using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace SchulPlanerBot.Modals;

public class HomeworkModal : IModal
{
    string IModal.Title => string.Empty;

    [RequiredInput]
    [InputLabel("Due date")]
    [ModalTextInput(nameof(Due), placeholder: "20.01.2020", maxLength: 19)]     // 19 is the max length using the format 'dd.mm.yyyy hh:MM:ss'
    public string Due { get; set; } = default!;

    [RequiredInput(isRequired: false)]
    [InputLabel("Subject")]
    [ModalTextInput(nameof(Subject), placeholder: "Math", minLength: 0, maxLength: 32)]
    public string? Subject { get; set; } = default!;

    [RequiredInput]
    [InputLabel("Title")]
    [ModalTextInput(nameof(Title), placeholder: "Do page 97 task 2d", maxLength: 64)]
    public string Title { get; set; } = default!;

    [RequiredInput(isRequired: false)]
    [InputLabel("Details")]
    [ModalTextInput(nameof(Details), TextInputStyle.Paragraph, placeholder: "Solve quadratic equation", minLength: 0)]
    public string? Details { get; set; } = string.Empty;

    internal static void LocalizeModal(ModalBuilder builder, IStringLocalizer localizer, bool create = true)
    {
        builder
            .WithTitle(localizer[create ? "modalTitle.create" : "modalTitle.modify"])
            .UpdateTextInput(nameof(Due), input => input
                .WithLabel(localizer["due_date"]))
            .UpdateTextInput(nameof(Subject), input => input
                .WithLabel(localizer["subject"])
                .WithPlaceholder(localizer["subject.placeholder"]))
            .UpdateTextInput(nameof(Title), input => input
                .WithLabel(localizer["title"])
                .WithPlaceholder(localizer["title.placeholder"]))
            .UpdateTextInput(nameof(Details), input => input
                .WithLabel(localizer["details"])
                .WithPlaceholder(localizer["details.placeholder"]));
    }
}
