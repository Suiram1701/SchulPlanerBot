using Humanizer;
using Microsoft.Extensions.Localization;

namespace SchulPlanerBot.Business.Errors;

public class ErrorService(IStringLocalizer<ErrorService> localizer)
{
    private readonly IStringLocalizer _localizer = localizer;

    public UpdateResult NoChannel() =>
        UpdateResult.Failed(nameof(NoChannel), _localizer["noChannel"]);

    public UpdateResult LowTimeBetween(TimeSpan minimum) =>
        UpdateResult.Failed(nameof(LowTimeBetween), _localizer["lowTimeBetween", minimum.Humanize()]);

    public UpdateResult DueMustInFuture(TimeSpan atLeastInFuture) =>
        UpdateResult.Failed(nameof(DueMustInFuture), _localizer["dueTooLow", atLeastInFuture.Humanize()]);

    public UpdateResult HomeworkNotFound() =>
        UpdateResult.Failed(nameof(HomeworkNotFound), _localizer["homeworkNotFound"]);
}
