using Discord;
using Discord.Interactions;

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
}
