using Humanizer;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Business.Errors;

public class ErrorService(IStringLocalizer<ErrorService> localizer, IOptions<HelpOptions> helpAccessor)
{
    private readonly IStringLocalizer _localizer = localizer;
    private readonly HelpOptions _helpOptions = helpAccessor.Value;

    public UpdateResult InvalidCronExpression(string cron)
    {
        string desc = string.IsNullOrEmpty(_helpOptions.CronHelpPage)
            ? _localizer["notificationCronExpInvalid", cron]
            : _localizer["notificationCronExpInvalid-help", cron, _helpOptions.CronHelpPage];
        return UpdateResult.Failed(nameof(InvalidCronExpression), desc);
    }
    
    public UpdateResult NotificationAlreadyExists() =>
        UpdateResult.Failed(nameof(NotificationAlreadyExists), _localizer["notificationAlreadyExists"]);

    public UpdateResult NotificationNotFound() =>
        UpdateResult.Failed(nameof(NotificationNotFound), _localizer["notificationNotFound"]);
    
    public UpdateResult DeleteAfterDueTooHigh(TimeSpan maximum) =>
        UpdateResult.Failed(nameof(DeleteAfterDueTooHigh), _localizer["deleteAfterDueTooHigh", maximum.Humanize()]);

    public UpdateResult DueMustInFuture(TimeSpan atLeastInFuture) =>
        UpdateResult.Failed(nameof(DueMustInFuture), _localizer["dueTooLow", atLeastInFuture.Humanize()]);

    public UpdateResult HomeworkNotFound() =>
        UpdateResult.Failed(nameof(HomeworkNotFound), _localizer["homeworkNotFound"]);
}
