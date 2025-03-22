using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Options;
using System.Diagnostics;
using IResult = Discord.Interactions.IResult;

namespace SchulPlanerBot.Services;

internal sealed class DiscordInteractionHandler(
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordInteractionHandler> logger,
    ILogger<InteractionService> interactionLogger,
    IStringLocalizer<DiscordInteractionHandler> localizer,
    IOptions<DiscordClientOptions> optionsAccessor,
    DiscordSocketClient client,
    InteractionService interaction)
    : IHostedService, IDisposable
{
    public const string ActivitySourceName = "Discord.InteractionHandler";

    private readonly IHostEnvironment _environment = environment;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger _logger = logger;
    private readonly ILogger _interactionLogger = interactionLogger;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly DiscordClientOptions _options = optionsAccessor.Value;
    private readonly DiscordSocketClient _client = client;
    private readonly InteractionService _interaction = interaction;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    private bool _modulesAdded = false;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Ready += Client_ReadyAsync;
        _client.InteractionCreated += Client_InteractionCreatedAsync;
        _interaction.Log += Interaction_Log;
        _interaction.InteractionExecuted += Interaction_InteractionExecutedAsync;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= Client_ReadyAsync;
        _client.InteractionCreated -= Client_InteractionCreatedAsync;
        _interaction.Log -= Interaction_Log;
        _interaction.InteractionExecuted -= Interaction_InteractionExecutedAsync;

        return Task.CompletedTask;
    }

    private async Task Client_ReadyAsync()
    {
        using Activity? activity = _activitySource.StartActivity("Register commands");
        using IServiceScope scope = _scopeFactory.CreateScope();

        if (!_modulesAdded)
        {
            IEnumerable<ModuleInfo> modules = await _interaction.AddModulesAsync(typeof(ISchulPlanerBot).Assembly, scope.ServiceProvider).ConfigureAwait(false);
            _logger.LogInformation("{modulesCount} interaction modules added", modules.Count());
            _modulesAdded = true;
        }

        if (_environment.IsDevelopment() && _options.TestGuild is not null)
        {
            await _interaction.RegisterCommandsToGuildAsync(_options.TestGuild.Value).ConfigureAwait(false);
            _logger.LogInformation("Commands registered for test guild {guildId}", _options.TestGuild);
        }
        else
        {
            await _interaction.RegisterCommandsGloballyAsync().ConfigureAwait(false);
            _logger.LogInformation("Commands registered globally");
        }
    }

    private async Task Client_InteractionCreatedAsync(SocketInteraction interaction)
    {
        Activity.Current = null;     // This activity doesn't have a parent
        Activity? activity = _activitySource.StartActivity("Discord Interaction", ActivityKind.Server, parentId: null, tags: new Dictionary<string, object?>     // Activity disposed by event via context.
        {
            { "Id", interaction.Id },
            { "Type", interaction.Type },
            { "User", interaction.User.Username },
            { "UserId", interaction.User.Id },
            { "GuildId", interaction.GuildId },
        });

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            using CancellationTokenSource tokenSource = new(TimeSpan.FromSeconds(3));

            ExtendedSocketContext context = new(_client, interaction, activity, tokenSource.Token);
            await _interaction.ExecuteCommandAsync(context, scope.ServiceProvider).ConfigureAwait(false);     // AutoScope is enabled; result handled by event
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected exception occurred while executing a interaction");

            // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
            {
                RestInteractionMessage? msg = await interaction.GetOriginalResponseAsync().ConfigureAwait(false);
                if (msg is not null)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
        }
    }

    private Task Interaction_Log(LogMessage arg)
    {
        _interactionLogger.Log(Utilities.ConvertLogLevel(arg.Severity), arg.Exception, "{LogSource}: {LogMessage}", arg.Source, arg.Message);
        return Task.CompletedTask;
    }

    private async Task Interaction_InteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess && result.Error is not null)
        {
            string responseMessage = _localizer["errorResponse.unknown"];
            switch (result)
            {
                case ExecuteResult executeResult and { Error: InteractionCommandError.Exception }:
                    _logger.LogError(executeResult.Exception, "Reason: {errorReason}", executeResult.ErrorReason);
                    responseMessage = _localizer["errorResponse.exception"];
                    break;
                case PreconditionResult preconditionResult and { Error: InteractionCommandError.UnmetPrecondition }:
                    responseMessage = _localizer["errorResponse.precondition", preconditionResult.ErrorReason];
                    break;
                case TypeConverterResult typeConverterResult:
                    responseMessage = _localizer["errorResponse.typeConverter", typeConverterResult.ErrorReason];
                    break;
            }

            if (!context.Interaction.HasResponded)
            {
                Activity? activity = Activity.Current;
                responseMessage += $"\n||TraceId: {activity?.TraceId}\nSpanId: {activity?.SpanId}||";

                await context.Interaction.RespondAsync(responseMessage, ephemeral: true).ConfigureAwait(false);
            }
        }

        if (context is ExtendedSocketContext extendedContext)
            extendedContext.Activity?.Dispose();
    }

    public void Dispose() => _activitySource.Dispose();
}
