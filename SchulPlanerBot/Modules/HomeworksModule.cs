﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Errors;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Modals;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[Group("homeworks", "Manages homeworks on the guild.")]
public sealed class HomeworksModule(
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

    public SocketUser User => Context.User;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("list", "Gets all homeworks within the specified range or homeworks of a specific subject.")]
    public async Task GetHomeworksAsync(string? search = null, string? subject = null, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        start ??= DateTimeOffset.UtcNow;
        end ??= DateTimeOffset.UtcNow.AddDays(7);

        IEnumerable<Homework> homeworks = await _homeworkManager.GetHomeworksAsync(Guild.Id, search, subject, start?.ToUniversalTime(), end?.ToUniversalTime(), CancellationToken).ConfigureAwait(false);
        homeworks = [.. homeworks.OrderBy(h => h.Due)];

        if (homeworks.Any())
        {
            Embed overview = _embedsService.HomeworksOverview(homeworks, start!.Value, end!.Value);
            MessageComponent select = _componentService.SelectHomework(homeworks, cacheId: Guid.NewGuid().ToString());
            await RespondAsync(_localizer["list.listed"], embeds: [overview], components: select).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(_localizer["list.noHomeworks"]).ConfigureAwait(false);
        }
    }

    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworksSelectComponent, ignoreGroupNames: true)]
    public async Task GetHomeworks_InteractAsync(string cacheId, string? value = null)
    {
        Guid? homeworkId;
        if (value is not null)
        {
            homeworkId = Guid.Parse(value);
            _cache.Set(cacheId, homeworkId, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(7)     // Should be enough for the user to interact with
            });
        }
        else
        {
            homeworkId = (Guid?)_cache.Get(cacheId);
            if (homeworkId is null)
            {
                await RespondAsync(_localizer["list.selectBeforeReload"], ephemeral: true).ConfigureAwait(false);
                return;
            }
        }

        Homework? homework = await _homeworkManager.GetHomeworkAsync(Guild.Id, homeworkId.Value, CancellationToken).ConfigureAwait(false);
        if (homework is not null)
        {
            Embed embed = _embedsService.Homework(homework);
            await RespondAsync(embeds: [embed], allowedMentions: AllowedMentions.None, ephemeral: true).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(_localizer["list.notFound"], ephemeral: true).ConfigureAwait(false);
        }
    }

    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworksReloadComponent, ignoreGroupNames: true)]
    public async Task GetHomeworks_ReloadInteractAsync(string cacheId) => await GetHomeworks_InteractAsync(cacheId, value: null).ConfigureAwait(false);

    [SlashCommand("create", "Opens the form to create a new homework.")]
    public async Task CreateHomeworkAsync()
    {
        await RespondWithModalAsync<HomeworkModal>(
            customId: ComponentIds.CreateHomeworkModal,
            modifyModal: builder => _componentService.LocalizeHomeworkModal(builder, createHomework: true))
            .ConfigureAwait(false);
    }

    [ModalInteraction(ComponentIds.CreateHomeworkModal, ignoreGroupNames: true)]
    public async Task CreateHomework_SubmitAsync(HomeworkModal homeworkModal)
    {
        if (!DateTime.TryParse(homeworkModal.Due, out DateTime due))
        {
            await RespondAsync(_localizer["create.parseDueFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        // ComponentConverters with modals are buggy.
        DateTimeOffset dueWithOffset = due.Kind == DateTimeKind.Unspecified
            ? new(due, TimeSpan.FromHours(2))
            : new(due);

        (Homework? homework, UpdateResult creationResult) = await _homeworkManager.CreateHomeworkAsync(
            Guild.Id,
            User.Id,
            dueWithOffset.ToUniversalTime(),
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken).ConfigureAwait(false);

        if (creationResult.Success && homework is not null)
            await RespondAsync(_localizer["create.created"], embed: _embedsService.Homework(homework), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(creationResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("modify", "Modifies an existing homework.")]
    public async Task ModifyHomeworkAsync(string id)
    {
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

        // Check authorization
        SocketGuildUser guildUser = Guild.GetUser(User.Id);
        if (!guildUser.GuildPermissions.Has(GuildPermission.ModerateMembers) && homework?.CreatedBy != User.Id)
        {
            await RespondAsync(_localizer["modify.unauthorized"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        HomeworkModal modal = new()
        {
            Due = homework.Due.ToLocalTime().ToString("g"),     // g Serializes in similar format than the user input
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

    [ModalInteraction(ComponentIds.ModifyHomeworkModal, ignoreGroupNames: true)]
    public async Task ModifyHomework_SubmitAsync(string id, HomeworkModal homeworkModal)
    {
        Guid homeworkId = Guid.Parse(id);
        if (!DateTime.TryParse(homeworkModal.Due, out DateTime due))
        {
            await RespondAsync(_localizer["modify.parseDueFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        // ComponentConverters with modals are buggy.
        DateTimeOffset dueWithOffset = due.Kind == DateTimeKind.Unspecified
            ? new(due, TimeSpan.FromHours(2))
            : new(due);

        (Homework? homework, UpdateResult modifyResult) = await _homeworkManager.ModifyHomeworkAsync(
            homeworkId,
            User.Id,
            dueWithOffset.ToUniversalTime(),
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken).ConfigureAwait(false);

        if (modifyResult.Success && homework is not null)
            await RespondAsync(_localizer["modify.updated"], embed: _embedsService.Homework(homework), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(modifyResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("delete", "Deletes a homework by its ID. A homework can only be deleted by its creator or a mod.")]
    public async Task DeleteHomeworkAsync(string id)
    {
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

        // Check authorization
        SocketGuildUser guildUser = Guild.GetUser(User.Id);
        if (!guildUser.GuildPermissions.Has(GuildPermission.ModerateMembers) && homework?.CreatedBy != User.Id)
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
        {
            await RespondAsync(_localizer["subscriptions.notConfigured"]).ConfigureAwait(false);
        }
        else if (subscriptions.AnySubject)
        {
            await RespondAsync(_localizer["subscriptions.anySubject"]).ConfigureAwait(false);
        }
        else
        {
            string subjectsStr = string.Join(", ", subscriptions.Include);
            if (subscriptions.NoSubject)
                await RespondAsync(_localizer["subscriptions.subjectsWithNoSubjects", subjectsStr]).ConfigureAwait(false);
            else
                await RespondAsync(_localizer["subscriptions.subjectWithSubject", subjectsStr]).ConfigureAwait(false);
        }
    }

    [SlashCommand("subscribe-all", "Subscribes to notifications for all subjects.")]
    public async Task SubscribeToAllSubjectsAsync()
    {
        UpdateResult updateResult = await _homeworkManager.SetSubscribeToAllSubjectsAsync(Guild.Id, User.Id, subscribe: true, CancellationToken).ConfigureAwait(false);
        await HandleSubscriptionsUpdatedAsync(updateResult).ConfigureAwait(false);
    }

    [SlashCommand("unsubscribe-all", "Unsubscribes from notifications of all subjects. Manuel added subjects will remain.")]
    public async Task UnsubscribeFromAllSubjectsAsync()
    {
        UpdateResult updateResult = await _homeworkManager.SetSubscribeToAllSubjectsAsync(Guild.Id, User.Id, subscribe: false, CancellationToken).ConfigureAwait(false);
        if (updateResult.Success)
            await RespondAsync(_localizer["subscriptions.unsubscribed-all"]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(updateResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("subscribe", "Subscribes to notifications from specific subjects or no subject.")]
    public async Task SubscribeToSubjectsAsync(string[] subjects, [Summary(name: "no-subject")] bool noSubject = false)
    {
        UpdateResult updateResult = await _homeworkManager.SubscribeToSubjectsAsync(Guild.Id, User.Id, noSubject, subjects, CancellationToken).ConfigureAwait(false);
        await HandleSubscriptionsUpdatedAsync(updateResult).ConfigureAwait(false);
    }

    [SlashCommand("unsubscribe", "Unsubscribes from notifications of specific subjects or no subject.")]
    public async Task UnsubscribeFromSubjectsAsync(string[] subjects, [Summary(name: "no-subject")] bool noSubject = false)
    {
        UpdateResult updateResult = await _homeworkManager.UnsubscribeFromSubjectsAsync(Guild.Id, User.Id, noSubject, subjects, CancellationToken).ConfigureAwait(false);
        if (updateResult.Success)
            await RespondAsync(_localizer["subscriptions.updated"]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(updateResult.Errors, _logger).ConfigureAwait(false);
    }

    private async Task HandleSubscriptionsUpdatedAsync(UpdateResult result)
    {
        if (result.Success)
        {
            Guild guild = await _manager.GetGuildAsync(Guild.Id, CancellationToken).ConfigureAwait(false);

            string message = _localizer["subscriptions.updated"];
            if (guild.NotificationsEnabled)
                message += $" {_localizer["subscriptions.notificationNotEnabled"]}";
            await RespondAsync(message).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(result.Errors, _logger).ConfigureAwait(false);
        }
    }
}