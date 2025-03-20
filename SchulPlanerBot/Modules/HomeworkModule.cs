using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SchulPlanerBot.Business;
using SchulPlanerBot.Business.Models;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Modals;

namespace SchulPlanerBot.Modules;

[RequireContext(ContextType.Guild)]
[CommandContextType(InteractionContextType.Guild)]
[Group("homeworks", "Manages homeworks on the server.")]
public sealed class HomeworkModule(ILogger<HomeworkModule> logger, SchulPlanerManager manager) : InteractionModuleBase<ExtendedSocketContext>
{
    private readonly ILogger _logger = logger;
    private readonly SchulPlanerManager _manager = manager;

    public SocketUser User => Context.User;

    private SocketGuild Guild => Context.Guild;

    private CancellationToken CancellationToken => Context.CancellationToken;

    [SlashCommand("list", "Gets all homeworks within the specified range or homeworks of a specific subject.")]
    public async Task GetHomeworksAsync(
        [Summary(description: "The start date time of the range. By default the current date. E.g.: 01.01.2020 10:00 or just 10:00.")] DateTime? start = null,
        [Summary(description: "The end date time of the range. By default one week into the future. E.g.: 01.01.2020 10:00")] DateTime? end = null,
        [Summary(description: "The subject to retrieve the homeworks of. Leave empty to not filter.")] string? subject = null)
    {
        IEnumerable<Homework> homeworks = await _manager.GetHomeworksAsync(Guild.Id, start, end, subject, CancellationToken).ConfigureAwait(false);

        Embed[] embeds = [.. homeworks.Select(HomeworkToEmbed)];
        if (embeds.Length > 0)
        {
            int sentEmbeds = 0;
            do
            {
                Embed[] embedPart = [.. embeds.Skip(sentEmbeds).Take(10)];
                if (!Context.Interaction.HasResponded)
                    await RespondAsync(embeds: embedPart, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
                else
                    await ReplyAsync(embeds: embedPart, allowedMentions: AllowedMentions.None).ConfigureAwait(false);

                sentEmbeds += embedPart.Length;
            }
            while (sentEmbeds < embeds.Length);
        }
        else
        {
            await RespondAsync("No homeworks available").ConfigureAwait(false);
        }
    }

    [SlashCommand("create", "Creates a new homework.")]
    public async Task CreateHomeworkAsync() => await RespondWithModalAsync<CreateHomeworkModal>(ComponentIds.CreateHomeworkModal).ConfigureAwait(false);

    [ModalInteraction(ComponentIds.CreateHomeworkModal, ignoreGroupNames: true)]
    public async Task CreateHomework_SubmitAsync(CreateHomeworkModal homeworkModal)
    {
        if (!DateTimeOffset.TryParse(homeworkModal.Due, out DateTimeOffset due))
        {
            await RespondAsync("Unable to parse due.").ConfigureAwait(false);
            return;
        }

        (Homework? homework, UpdateResult result) = await _manager.CreateHomeworkAsync(
            Guild.Id,
            User.Id,
            due,
            homeworkModal.Subject,
            homeworkModal.Title,
            homeworkModal.Details,
            CancellationToken).ConfigureAwait(false);
        if (result.Success && homework is not null)
            await RespondAsync("Homework created:", embeds: [HomeworkToEmbed(homework)], allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        else
            await RespondAsync(result.Errors[0].Description).ConfigureAwait(false);
    }

    [SlashCommand("delete", "Removes a homework by its ID")]
    public async Task DeleteHomeworkAsync([Summary(description: "The ID of the homework to remove.")] string id)
    {
        if (!Guid.TryParse(id, out Guid homeworkId))
        {
            await RespondAsync("Invalid id provided!").ConfigureAwait(false);
            return;
        }

        SocketGuildUser guildUser = Guild.GetUser(User.Id);
        if (!guildUser.GuildPermissions.Has(GuildPermission.ModerateMembers))
        {
            Homework? homework = await _manager.GetHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
            if (homework is null)
            {
                await RespondAsync("Homework not found").ConfigureAwait(false);
                return;
            }
            else if (homework.CreatedBy != User.Id)
            {
                await RespondAsync("Unauthorized to remove this homework").ConfigureAwait(false);
                return;
            }
        }

        UpdateResult deleteResult = await _manager.DeleteHomeworkAsync(Guild.Id, homeworkId, CancellationToken).ConfigureAwait(false);
        if (deleteResult.Success)
            await RespondAsync("Deleted").ConfigureAwait(false);
        else
            await RespondAsync(deleteResult.Errors[0].Description).ConfigureAwait(false);
    }

    private static Embed HomeworkToEmbed(Homework homework)
    {
        return new EmbedBuilder()
            .WithAuthor(new EmbedAuthorBuilder().WithName(homework.Subject))
            .WithTitle(homework.Title)
            .WithDescription(homework.Details)
            .AddField("Due", Utilities.Timestamp(homework.Due, TimestampKind.Relative))
            .AddField("Creator", Utilities.Mention(homework.CreatedBy, MentionType.User))
            .AddField("Created", Utilities.Timestamp(homework.CreatedAt, TimestampKind.ShortDate))
            .AddField("Id", $"||{homework.Id}||")
            .Build();
    }
}