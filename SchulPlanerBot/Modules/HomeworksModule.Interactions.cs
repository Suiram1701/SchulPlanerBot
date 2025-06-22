using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Caching.Memory;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.UI.Models;
using SchulPlanerBot.Modals;

namespace SchulPlanerBot.Modules;

public partial class HomeworksModule 
{
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworkPageComponent, ignoreGroupNames: true)]
    public Task GetHomeworks_SwitchPageAsync(int newIndex, string cacheId)
    {
        var overview = (HomeworkOverview?)_cache.Get(cacheId);
        if (overview is not null)
            return UpdateHomeworkSearchAsync(overview with { PageIndex = newIndex }, cacheId);
        
        _logger.LogWarning("Received component containing missing cache ID");
        this.RespondWithWarningAsync(_localizer["cacheIdMissing"]);
            
        return Task.CompletedTask;
    }
    
    // Components created by global::SchulPlanerBot.Discord.ComponentService
    [ComponentInteraction(ComponentIds.GetHomeworksSelectComponent, ignoreGroupNames: true)]
    public Task GetHomeworks_SelectAsync(string cacheId, string newHomework)
    {
        var overview = (HomeworkOverview?)_cache.Get(cacheId);
        if (overview is not null)
            return UpdateHomeworkSearchAsync(
                overview with { DisplayedHomeworkId = Guid.Parse(newHomework) }, cacheId);
        
        _logger.LogWarning("Received component containing missing cache ID");
        this.RespondWithWarningAsync(_localizer["cacheIdMissing"]);
            
        return Task.CompletedTask;
    }

    private async Task UpdateHomeworkSearchAsync(HomeworkOverview overview, string cacheId)
    {
        _cache.Set(cacheId, overview, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(7)     // Should be enough for the user to interact with
        });
        
        Homework? displayHomework = overview.DisplayedHomeworkId is not null
            ? await _homeworkManager
                .GetHomeworkAsync(Guild.Id, overview.DisplayedHomeworkId.Value, CancellationToken)
                .ConfigureAwait(false)
            : null;
        
        await this.ModifyComponentMessageAsync(msg =>
        {
            List<Embed> embeds = [ _embedsService.HomeworksOverview(overview) ];
            if (displayHomework is not null)
                embeds.Add(_embedsService.Homework(displayHomework));
            
            msg.Embeds = embeds.ToArray();
            msg.Components = _componentService.HomeworkOverviewSelect(overview, cacheId);
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
    public async Task ModifyHomework_SubmitAsync(string id, ulong? triggerMessage, HomeworkModal homeworkModal)
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
        {
            if (triggerMessage is not null && triggerMessage != 0)     // Modal was created by a component
            {
                await DeferAsync().ConfigureAwait(false);     // Signalise Discord that we've received the message
                
                var sourceMsg = (IUserMessage)await Context.Channel.GetMessageAsync(triggerMessage.Value).ConfigureAwait(false);
                await sourceMsg.ModifyAsync(msg =>
                {
                    msg.Content = _localizer["modify.updated"].ToString();
                    msg.Embeds = new[] { _embedsService.Homework(homework) };
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(_localizer["modify.updated"], embed: _embedsService.Homework(homework)).ConfigureAwait(false);
            }
        }
        else
        {
            await this.RespondWithErrorAsync(modifyResult.Errors, _logger).ConfigureAwait(false);
        }
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

        var componentInteraction = (IComponentInteraction)Context.Interaction;
        ulong sourceMessageId = componentInteraction.Message.Id;
        
        HomeworkModal modal = new()
        {
            Due = homework.Due.ToLocalTime(),
            Subject = homework.Subject,
            Title = homework.Title,
            Details = homework.Details
        };
        await RespondWithModalAsync(
                customId: ComponentIds.CreateModifyHomeworkModal(homework.Id.ToString(), triggerMessage: sourceMessageId),
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
        {
            await this.ModifyComponentMessageAsync(msg =>
            {
                msg.Content = _localizer["delete.deleted", homework.Title].ToString();
                msg.Embeds = Array.Empty<Embed>();
                msg.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);
        }
        else
        {
            await this.RespondWithErrorAsync(deleteResult.Errors, _logger).ConfigureAwait(false);
        }
    }
}