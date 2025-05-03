namespace SchulPlanerBot.Options;

public class ManagerOptions
{
    public TimeSpan MinBetweenNotifications { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan MinDueInFuture { get; set; } = TimeSpan.FromMinutes(30);

    public bool SubjectsCaseSensitive { get; set; } = false;

    public TimeSpan MaxDeleteHomeworksAfterDue { get; set; } = TimeSpan.FromDays(31);

    public bool MessageWhenNoHomework { get; set; } = false;
}
