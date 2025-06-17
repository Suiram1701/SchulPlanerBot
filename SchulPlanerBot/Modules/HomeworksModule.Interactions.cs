using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Caching.Memory;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Modals;

namespace SchulPlanerBot.Modules;

public partial class HomeworksModule 
{
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworkPageComponent, ignoreGroupNames: true)]
    public Task GetHomeworks_SwitchPageAsync(int newIndex, string cacheId)
    {
        var searchOptions = (HomeworkSearchMessage?)_cache.Get(cacheId);
        if (searchOptions is not null)
            return UpdateHomeworkSearchAsync(searchOptions with { PageIndex = newIndex }, cacheId);
        
        _logger.LogWarning("Received component containing missing cache ID");
        this.RespondWithWarningAsync(_localizer["cacheIdMissing"]);
            
        return Task.CompletedTask;

    }
    
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworksSelectComponent, ignoreGroupNames: true)]
    public Task GetHomeworks_SelectAsync(string cacheId, string newHomework)
    {
        var searchOptions = (HomeworkSearchMessage?)_cache.Get(cacheId);
        if (searchOptions is not null)
            return UpdateHomeworkSearchAsync(
                searchOptions with { DisplayedHomeworkId = Guid.Parse(newHomework) },
                cacheId);
        
        _logger.LogWarning("Received component containing missing cache ID");
        this.RespondWithWarningAsync(_localizer["cacheIdMissing"]);
            
        return Task.CompletedTask;

    }

    private async Task UpdateHomeworkSearchAsync(HomeworkSearchMessage searchOptions, string cacheId)
    {
        _cache.Set(cacheId, searchOptions, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(7)     // Should be enough for the user to interact with
        });
        
        Homework[] homeworks = await _homeworkManager.GetHomeworksAsync(Guild.Id, searchOptions.Search,
            searchOptions.Subject, searchOptions.Start, searchOptions.End, CancellationToken).ConfigureAwait(false);

        Homework? displayHomework = searchOptions.DisplayedHomeworkId is not null
            ? await _homeworkManager
                .GetHomeworkAsync(Guild.Id, searchOptions.DisplayedHomeworkId.Value, CancellationToken)
                .ConfigureAwait(false)
            : null;
        
        await this.ModifyComponentMessageAsync(msg =>
        {
            List<Embed> embeds =
            [
                _embedsService.HomeworksOverview(homeworks, searchOptions.PageIndex, searchOptions.Start,
                    searchOptions.End, selectedHomeworkId: searchOptions.DisplayedHomeworkId)
            ];
            if (displayHomework is not null)
                embeds.Add(_embedsService.Homework(displayHomework));
            
            msg.Embeds = embeds.ToArray();
            msg.Components = _componentService.SelectOverviewHomework(
                homeworks, searchOptions.PageIndex,
                cacheId: cacheId,
                selectedHomeworkId: searchOptions.DisplayedHomeworkId);
        }).ConfigureAwait(false);
    }
    
    [ModalInteraction(ComponentIds.CreateHomeworkModal, ignoreGroupNames: true)]
    public async Task CreateHomework_SubmitAsync(HomeworkModal homeworkModal)
    {
        (Homework? homework, UpdateResult creationResult) = await _homeworkManager.CreateHomeworkAsync(
            Guild.Id,
            User.Id,
            homeworkModal.Due.ToLocalTime(),
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken).ConfigureAwait(false);

        if (creationResult.Success && homework is not null)
            await RespondAsync(_localizer["create.created"], embed: _embedsService.Homework(homework), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(creationResult.Errors, _logger).ConfigureAwait(false);
    }
    
    [ModalInteraction(ComponentIds.ModifyHomeworkModal, ignoreGroupNames: true)]
    public async Task ModifyHomework_SubmitAsync(string id, HomeworkModal homeworkModal)
    {
        Guid homeworkId = Guid.Parse(id);
        (Homework? homework, UpdateResult modifyResult) = await _homeworkManager.ModifyHomeworkAsync(
            homeworkId,
            User.Id,
            homeworkModal.Due.ToUniversalTime(),
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken).ConfigureAwait(false);

        if (modifyResult.Success && homework is not null)
            await RespondAsync(_localizer["modify.updated"], embed: _embedsService.Homework(homework), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(modifyResult.Errors, _logger).ConfigureAwait(false);
    }
    
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.ModifyHomeworkSelectComponent, ignoreGroupNames: true)]
    public async Task ModifyHomework_SelectAsync(string value)
    {
        Guid homeworkId = Guid.Parse(value);
        Homework? homework = await _homeworkManager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (homework is null)
        {
            await this.RespondWithErrorAsync(_errorService.HomeworkNotFound().Errors, _logger).ConfigureAwait(false);
            return;
        }
        
        if (!HomeworkEditAllowed(homework, Guild.GetUser(User.Id)))
        {
            await RespondAsync(_localizer["modify.selectUnauthorized"], ephemeral: true).ConfigureAwait(false);
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
    
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.DeleteHomeworkSelectComponent, ignoreGroupNames: true)]
    public async Task DeleteHomework_SelectAsync(string value)
    {
        Guid homeworkId = Guid.Parse(value);
        Homework? homework = await _homeworkManager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (homework is null)
        {
            await this.RespondWithErrorAsync(_errorService.HomeworkNotFound().Errors, _logger).ConfigureAwait(false);
            return;
        }
        
        if (!HomeworkEditAllowed(homework, Guild.GetUser(User.Id)))
        {
            await RespondAsync(_localizer["delete.selectUnauthorized"], ephemeral: true).ConfigureAwait(false);
            return;
        }
        
        UpdateResult deleteResult = await _homeworkManager.DeleteHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (deleteResult.Success)
            await RespondAsync(_localizer["delete.deleted", homework.Title]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(deleteResult.Errors, _logger).ConfigureAwait(false);
    }
    
    public record HomeworkSearchMessage(
        string? Search,
        string? Subject,
        DateTimeOffset Start,
        DateTimeOffset? End,
        int PageIndex = 0,
        Guid? DisplayedHomeworkId = null);
}