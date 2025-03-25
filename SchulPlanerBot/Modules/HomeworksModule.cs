using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
    IStringLocalizer<HomeworksModule> localizer,
    IStringLocalizer<CreateHomeworkModal> modalLocalizer,
    SchulPlanerManager manager,
    ErrorService errorService,
    EmbedsService embedsService) : InteractionModuleBase<ExtendedSocketContext>
{
    private readonly ILogger _logger = logger;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly IStringLocalizer _modalLocalizer = modalLocalizer;
    private readonly SchulPlanerManager _manager = manager;
    private readonly ErrorService _errorService = errorService;
    private readonly EmbedsService _embedsService = embedsService;

    public SocketUser User => Context.User;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("list", "Gets all homeworks within the specified range or homeworks of a specific subject.")]
    public async Task GetHomeworksAsync(DateTime? start = null, DateTime? end = null, string? subject = null)
    {
        IEnumerable<Homework> homeworks = await _manager.GetHomeworksAsync(Guild.Id, start, end, subject, CancellationToken).ConfigureAwait(false);

        Embed[] embeds = [.. homeworks.Select(_embedsService.Homework)];
        if (embeds.Length > 0)
        {
            int sentEmbeds = 0;
            do
            {
                Embed[] embedPart = [.. embeds.Skip(sentEmbeds).Take(DiscordConfig.MaxEmbedsPerMessage)];
                if (!Context.Interaction.HasResponded)
                    await RespondAsync(embeds: embedPart, allowedMentions: AllowedMentions.None).ConfigureAwait(false);     // Only one time can be responded directly
                else
                    await FollowupAsync(embeds: embedPart, allowedMentions: AllowedMentions.None).ConfigureAwait(false);

                sentEmbeds += embedPart.Length;
            }
            while (sentEmbeds < embeds.Length);
        }
        else
        {
            await RespondAsync(_localizer["list.noHomeworks"]).ConfigureAwait(false);
        }
    }

    [SlashCommand("create", "Opens the form to create a new homework.")]
    public async Task CreateHomeworkAsync() =>
        await RespondWithModalAsync<CreateHomeworkModal>(
            ComponentIds.CreateHomeworkModal,
            modifyModal: builder => CreateHomeworkModal.LocalizeModal(builder, _modalLocalizer))
        .ConfigureAwait(false);

    [ModalInteraction(ComponentIds.CreateHomeworkModal, ignoreGroupNames: true)]
    public async Task CreateHomework_SubmitAsync(CreateHomeworkModal homeworkModal)
    {
        if (!DateTimeOffset.TryParse(homeworkModal.Due, out DateTimeOffset due))
        {
            await RespondAsync(_localizer["createHomework.parseDueFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        (Homework? homework, UpdateResult creationResult) = await _manager.CreateHomeworkAsync(
            Guild.Id,
            User.Id,
            due,
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken)
            .ConfigureAwait(false);

        if (creationResult.Success && homework is not null)
            await RespondAsync(_localizer["createHomework.created"], embeds: [_embedsService.Homework(homework)], allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(creationResult.Errors, _logger).ConfigureAwait(false);
    }

    [SlashCommand("delete", "Deletes a homework by its ID. A homework can only be deleted by its creator or a mod.")]
    public async Task DeleteHomeworkAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid homeworkId))
        {
            await RespondAsync(_localizer["delete.parseIdFailed"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        Homework? homework = await _manager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (homework is null)
        {
            await this.RespondWithErrorAsync(_errorService.HomeworkNotFound().Errors, _logger).ConfigureAwait(false);
            return;
        }

        SocketGuildUser guildUser = Guild.GetUser(User.Id);
        if (!guildUser.GuildPermissions.Has(GuildPermission.ModerateMembers) && homework?.CreatedBy != User.Id)
        {
            await RespondAsync(_localizer["delete.unauthorized"], ephemeral: true).ConfigureAwait(false);
            return;
        }

        UpdateResult deleteResult = await _manager.DeleteHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (deleteResult.Success)
            await RespondAsync(_localizer["delete.deleted", homework.Title]).ConfigureAwait(false);
        else
            await this.RespondWithErrorAsync(deleteResult.Errors, _logger).ConfigureAwait(false);
    }
}