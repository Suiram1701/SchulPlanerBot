using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace SchulPlanerBot.Modals;

public class CreateHomeworkModal : IModal
{
    string IModal.Title => "Create homework";

    [RequiredInput]
    [InputLabel("Due date")]
    [ModalTextInput("due_date", placeholder: "20.01.2020", maxLength: 10)]
    public string Due { get; set; } = default!;

    [InputLabel("Subject")]
    [ModalTextInput("subject", placeholder: "Math", minLength: 0, maxLength: 32)]
    public string? Subject { get; set; } = default!;

    [RequiredInput]
    [InputLabel("Title")]
    [ModalTextInput("title", placeholder: "Do page 97 task 2d", maxLength: 64)]
    public string Title { get; set; } = default!;

    [RequiredInput(isRequired: false)]
    [InputLabel("Details")]
    [ModalTextInput("details", TextInputStyle.Paragraph, placeholder: "Solve quadratic equation", minLength: 0)]
    public string? Details { get; set; } = string.Empty;

    internal static void LocalizeModal(ModalBuilder builder, IStringLocalizer localizer)
    {
        builder
            .WithTitle(localizer["modalTitle"])
            .UpdateTextInput("due_date", input => input
                .WithLabel(localizer["due_date"]))
            .UpdateTextInput("subject", input => input
                .WithLabel(localizer["subject"])
                .WithPlaceholder(localizer["subject.placeholder"]))
            .UpdateTextInput("title", input => input
                .WithLabel(localizer["title"])
                .WithPlaceholder(localizer["title.placeholder"]))
            .UpdateTextInput("details", input => input
                .WithLabel(localizer["details"])
                .WithPlaceholder(localizer["details.placeholder"]));
    }
}
