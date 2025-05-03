using Discord;
using Discord.Interactions;
using SchulPlanerBot.Discord;

namespace SchulPlanerBot.Modals;

public class HomeworkModal : IModal
{
    string IModal.Title => string.Empty;

    [RequiredInput(isRequired: false)]
    [InputLabel("Subject")]
    [ModalTextInput(ComponentIds.HomeworkModal.Subject, placeholder: "Math", minLength: 0, maxLength: 32)]
    public string? Subject { get; set; } = default!;

    [RequiredInput]
    [InputLabel("Due date")]
    [ModalTextInput(ComponentIds.HomeworkModal.DueDate, placeholder: "20.01.2020", maxLength: 19)]     // 19 is the max length using the format 'dd.mm.yyyy hh:MM:ss'
    public DateTimeOffset Due { get; set; } = default!;

    [RequiredInput]
    [InputLabel("Title")]
    [ModalTextInput(ComponentIds.HomeworkModal.Title, placeholder: "Do page 97 task 2d", maxLength: 64)]
    public string Title { get; set; } = default!;

    [RequiredInput(isRequired: false)]
    [InputLabel("Details")]
    [ModalTextInput(ComponentIds.HomeworkModal.Details, TextInputStyle.Paragraph, placeholder: "Solve quadratic equation", minLength: 0)]
    public string? Details { get; set; } = string.Empty;
}
