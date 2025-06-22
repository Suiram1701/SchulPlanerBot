using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Discord.UI;
using SchulPlanerBot.Discord.UI.Models;
using SchulPlanerBot.Modals;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[Group("homeworks", "Manages homeworks on the guild.")]
public sealed partial class HomeworksModule(
    ILogger<HomeworksModule> logger,
    IMemoryCache cache,
    IStringLocalizer<HomeworksModule> localizer,
    SchulPlanerManager manager,
    HomeworkManager homeworkManager,
    ErrorService errorService,
    EmbedsService embedsService,
    ComponentService componentService) : InteractionModuleBase<ExtendedSocketContext>
{
    private readonly ILogger _logger = logger;
    private readonly IMemoryCache _cache = cache;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly SchulPlanerManager _manager = manager;
    private readonly HomeworkManager _homeworkManager = homeworkManager;
    private readonly ErrorService _errorService = errorService;
    private readonly EmbedsService _embedsService = embedsService;
    private readonly ComponentService _componentService = componentService;

    private SocketUser User => Context.User;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("list", "Gets all homeworks within the specified range or homeworks of a specific subject.")]
    public async Task GetHomeworksAsync(string? search = null, string? subject = null, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        start ??= DateTimeOffset.Now;
        
        Homework[] homeworks = await _homeworkManager.GetHomeworksAsync(Guild.Id, search, subject, start, end, CancellationToken).ConfigureAwait(false);

        var cacheId = Guid.NewGuid().ToString();
        HomeworkOverview options = new(homeworks, start, end, ComponentIds.CreateGetHomeworksSelectComponent(cacheId));
        await RespondWithHomeworkOverviewAsync(_localizer["list.listed"], options, cacheId).ConfigureAwait(false);
    }
    
    [SlashCommand("create", "Opens the form to create a new homework.")]
    public Task CreateHomeworkAsync()
    {
        return RespondWithModalAsync<HomeworkModal>(
            customId: ComponentIds.CreateHomeworkModal,
            modifyModal: builder => _componentService.LocalizeHomeworkModal(builder, createHomework: true));
    }
    
    [SlashCommand("modify", "Modifies an existing homework.")]
    public async Task ModifyHomeworkAsync(string? id = null)
    {
        if (id is null)
        {
            SocketGuildUser user = Guild.GetUser(User.Id);
            Homework[] homeworks = await _homeworkManager.GetHomeworksAsync(Guild.Id, ct: CancellationToken).ConfigureAwait(false);

            HomeworkOverview options = new(
                [.. homeworks.Where(h => HomeworkEditAllowed(h, user))], 
                null, 
                null,
                ComponentIds.ModifyHomeworkSelectComponent);
            await RespondWithHomeworkOverviewAsync(_localizer["modify.select"], options).ConfigureAwait(false);
            return;
        }
        
        if (!Guid.TryParse(id, out Guid homeworkId))
        {
            await RespondAsync(_localizer["modify.parseIdFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        Homework? homework = await _homeworkManager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (homework is null)
        {
            await this.RespondWithErrorAsync(_errorService.HomeworkNotFound().Errors, _logger).ConfigureAwait(false);
            return;
        }
        
        if (!HomeworkEditAllowed(homework, Guild.GetUser(User.Id)))
        {
            await RespondAsync(_localizer["modify.unauthorized"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        HomeworkModal modal = new()
        {
            Due = homework.Due.ToLocalTime(),
            Subject = homework.Subject,
            Title = homework.Title,
            Details = homework.Details
        };
        await RespondWithModalAsync(
                customId: ComponentIds.CreateModifyHomeworkModal(homework.Id.ToString()),
                modal: modal,
                modifyModal: builder => _componentService.LocalizeHomeworkModal(builder, createHomework: false))
            .ConfigureAwait(false);
    }
    
    [SlashCommand("delete", "Deletes a homework by its ID. A homework can only be deleted by its creator or a mod.")]
    public async Task DeleteHomeworkAsync(string? id = null)
    {
        if (id is null)
        {
            SocketGuildUser user = Guild.GetUser(User.Id);
            IEnumerable<Homework> homeworks = await _homeworkManager.GetHomeworksAsync(Guild.Id, ct: CancellationToken).ConfigureAwait(false);
            
            HomeworkOverview options = new(
                [.. homeworks.Where(h => HomeworkEditAllowed(h, user))], 
                null, 
                null,
                ComponentIds.DeleteHomeworkSelectComponent);
            await RespondWithHomeworkOverviewAsync(_localizer["delete.select"], options).ConfigureAwait(false);
            return;
        }
        
        if (!Guid.TryParse(id, out Guid homeworkId))
        {
            await RespondAsync(_localizer["delete.parseIdFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        Homework? homework = await _homeworkManager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (homework is null)
        {
            await this.RespondWithErrorAsync(_errorService.HomeworkNotFound().Errors, _logger).ConfigureAwait(false);
            return;
        }

        if (!HomeworkEditAllowed(homework, Guild.GetUser(User.Id)))
        {
            await RespondAsync(_localizer["delete.unauthorized"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        UpdateResult deleteResult = await _homeworkManager.DeleteHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (deleteResult.Success)
            await RespondAsync(_localizer["delete.deleted", homework.Title]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(deleteResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("subscriptions", "Shows your homework notification subscriptions.")]
    public async Task GetSubscriptionsAsync()
    {
        HomeworkSubscription? subscriptions = await _homeworkManager.GetHomeworkSubscriptionAsync(Guild.Id, User.Id, CancellationToken).ConfigureAwait(false);
        if (subscriptions is null)
            await RespondAsync(_localizer["subscriptions.notConfigured"]).ConfigureAwait(false);
        else
            await RespondAsync(LocalizeSubscriptions(subscriptions)).ConfigureAwait(false);
    }

    [SlashCommand("subscribe-all", "Subscribes to notifications for all subjects.")]
    public async Task SubscribeToAllSubjectsAsync()
    {
        (UpdateResult updateResult, HomeworkSubscription? subscription) = await _homeworkManager.SetSubscribeToAllSubjectsAsync(Guild.Id, User.Id, subscribe: true, CancellationToken).ConfigureAwait(false);

        await HandleSubscriptionsUpdatedAsync(updateResult, subscription).ConfigureAwait(false);
    }

    [SlashCommand("unsubscribe-all", "Unsubscribes from notifications of all subjects. Manuel added subjects will remain.")]
    public async Task UnsubscribeFromAllSubjectsAsync()
    {
        (UpdateResult updateResult, _) = await _homeworkManager.SetSubscribeToAllSubjectsAsync(Guild.Id, User.Id, subscribe: false, CancellationToken).ConfigureAwait(false);
        if (updateResult.Success)
            await RespondAsync(_localizer["subscriptions.updated"]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(updateResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("subscribe", "Subscribes to notifications from specific subjects or no subject.")]
    public async Task SubscribeToSubjectsAsync(string[] subjects, [Summary(name: "no-subject")] bool noSubject = false)
    {
        if (!noSubject)
            subjects = [.. subjects, null!];
        (UpdateResult updateResult, HomeworkSubscription? subscription) = await _homeworkManager.SubscribeToSubjectsAsync(Guild.Id, User.Id, subjects, CancellationToken).ConfigureAwait(false);

        await HandleSubscriptionsUpdatedAsync(updateResult, subscription).ConfigureAwait(false);
    }

    [SlashCommand("unsubscribe", "Unsubscribes from notifications of specific subjects or no subject.")]
    public async Task UnsubscribeFromSubjectsAsync(string[] subjects, [Summary(name: "no-subject")] bool noSubject = false)
    {
        if (!noSubject)
            subjects = [.. subjects, null!];
        (UpdateResult updateResult, HomeworkSubscription? subscription) = await _homeworkManager.UnsubscribeFromSubjectsAsync(Guild.Id, User.Id, subjects, CancellationToken).ConfigureAwait(false);

        await HandleSubscriptionsUpdatedAsync(updateResult, subscription).ConfigureAwait(false);
    }

    private Task RespondWithHomeworkOverviewAsync(string message, HomeworkOverview options, string? cacheId = null)
    {
        cacheId ??= Guid.NewGuid().ToString();
        _cache.Set(key: cacheId, value: options, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(7)     // Should be enough for the user to interact with
        });
            
        Embed overview = _embedsService.HomeworksOverview(options);
        MessageComponent components = _componentService.HomeworkOverviewSelect(options, cacheId);
        return RespondAsync(message, embeds: [overview], components: components);
    }
    
    private bool HomeworkEditAllowed(Homework homework, SocketGuildUser user) =>
        user.GuildPermissions.Has(GuildPermission.ModerateMembers) || homework.CreatedBy == User.Id;
    
    private async Task HandleSubscriptionsUpdatedAsync(UpdateResult result, HomeworkSubscription? newSubscription)
    {
        if (result.Success && newSubscription is not null)
        {
            Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);
            var message = $"{_localizer["subscriptions.updated"]} {LocalizeSubscriptions(newSubscription)}";

            if (guild.Notifications.Count == 0)
                message += $" {_localizer["subscriptions.notificationNotEnabled"]}";
            await RespondAsync(message).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(result.Errors, _logger).ConfigureAwait(false);
        }
    }

    private string LocalizeSubscriptions(HomeworkSubscription subscription)
    {
        string formatSubject(string? str) => $"`{str ?? _localizer["empty"]}`";

        if (subscription.AnySubject)
        {
            return subscription.Exclude.Length == 0
                ? _localizer["subscriptions.anySubject"]
                : _localizer["subscriptions.anySubjectExcept", subscription.Exclude.Humanize(displayFormatter: formatSubject)];
        }
        else
        {
            return _localizer["subscriptions.subjects", subscription.Include.Humanize(displayFormatter: formatSubject)];
        }
    }
}