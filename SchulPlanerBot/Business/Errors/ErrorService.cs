using Humanizer;
using Microsoft.Extensions.Localization;

namespace SchulPlanerBot.Business.Errors;

public class ErrorService(IStringLocalizer<ErrorService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    public UpdateResult NotificationAlreadyExists() =>
        UpdateResult.Failed(nameof(NotificationAlreadyExists), _localizer["notificationAlreadyExists"]);

    public UpdateResult NotificationNotFound() =>
        UpdateResult.Failed(nameof(NotificationNotFound), _localizer["notificationNotFound"]);

    public UpdateResult LowTimeBetween(TimeSpan minimum) =>
        UpdateResult.Failed(nameof(LowTimeBetween), _localizer["lowTimeBetween", minimum.Humanize()]);

    public UpdateResult DeleteAfterDueTooHigh(TimeSpan maximum) =>
        UpdateResult.Failed(nameof(DeleteAfterDueTooHigh), _localizer["deleteAfterDueTooHigh", maximum.Humanize()]);

    public UpdateResult DueMustInFuture(TimeSpan atLeastInFuture) =>
        UpdateResult.Failed(nameof(DueMustInFuture), _localizer["dueTooLow", atLeastInFuture.Humanize()]);

    public UpdateResult HomeworkNotFound() =>
        UpdateResult.Failed(nameof(HomeworkNotFound), _localizer["homeworkNotFound"]);
}
