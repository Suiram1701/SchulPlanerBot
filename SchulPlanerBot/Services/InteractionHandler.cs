using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Options;
using System.Diagnostics;
using System.Globalization;
using IResult = Discord.Interactions.IResult;

namespace SchulPlanerBot.Services;

internal sealed class InteractionHandler(
    IHost host,
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<InteractionHandler> logger,
    ILogger<InteractionService> interactionLogger,
    IStringLocalizer<InteractionHandler> localizer,
    IOptions<DiscordClientOptions> optionsAccessor,
    DiscordSocketClient client,
    InteractionService interaction)
    : IHostedService, IDisposable
{
    public const string ActivitySourceName = "Discord.InteractionHandler";

    private readonly IHost _host = host;
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

        try
        {
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
        catch (Exception ex)     // Errors here have to be logged explicitly because this part is critical
        {
            _logger.LogCritical(ex, "An critical error occurred while registered interaction modules!");
            activity?.AddException(ex);

            await _host.StopAsync().ConfigureAwait(false);     // An error on registration can disturb the whole app
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

        if (_environment.IsDevelopment() && _options.TestGuild is not null && interaction.GuildId != _options.TestGuild)
        {
            _logger.LogWarning("Interaction cancelled! Sent from non-test channel {guildId} during development!", interaction.GuildId);
            await interaction.RespondAsync(Utils.UseAnsiFormat(
                MessageWithEmote("warning", "Cannot execute interactions outside of the development guild during dev environment!"),
                Utils.AnsiColor.Yellow)).ConfigureAwait(false);

            activity?.Dispose();
            return;
        }

        // Required by IStringLocalizer
        CultureInfo userCulture = new(interaction.UserLocale);
        CultureInfo.CurrentCulture = userCulture;
        CultureInfo.CurrentUICulture = userCulture;

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
            activity?.Dispose();
        }
    }

    private Task Interaction_Log(LogMessage arg)
    {
        _interactionLogger.Log(Utils.ConvertLogLevel(arg.Severity), arg.Exception, "{LogSource}: {LogMessage}", arg.Source, arg.Message);
        return Task.CompletedTask;
    }

    private async Task Interaction_InteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess && result.Error is not null)
        {
            string responseMessage = MessageWithEmote("exclamation", _localizer["errorResponse.unknown"]);
            Utils.AnsiColor responseColor = Utils.AnsiColor.Red;
            switch (result)
            {
                case ExecuteResult executeResult and { Error: InteractionCommandError.Exception }:
                    _logger.LogError(executeResult.Exception, "Reason: {errorReason}", executeResult.ErrorReason);
                    responseMessage = MessageWithEmote("exclamation", _localizer["errorResponse.exception"]);
                    break;
                case PreconditionResult preconditionResult and { Error: InteractionCommandError.UnmetPrecondition }:
                    responseMessage = MessageWithEmote("exclamation", _localizer["errorResponse.precondition", preconditionResult.ErrorReason]);
                    break;
                case ParseResult parseResult:
                    responseMessage = MessageWithEmote("warning", _localizer["errorResponse.typeConverter", parseResult.ErrorReason]);
                    responseColor = Utils.AnsiColor.Yellow;
                    break;
                case TypeConverterResult typeConverterResult:
                    responseMessage = MessageWithEmote("warning", _localizer["errorResponse.typeConverter", typeConverterResult.ErrorReason]);
                    responseColor = Utils.AnsiColor.Yellow;
                    break;
            }

            Activity? activity = Activity.Current;
            responseMessage += $"\nTraceId: {activity?.TraceId}\nSpanId: {activity?.SpanId}";

            if (!context.Interaction.HasResponded)
                await context.Interaction.RespondAsync(Utils.UseAnsiFormat(responseMessage, responseColor), ephemeral: true).ConfigureAwait(false);
        }

        if (context is ExtendedSocketContext extendedContext)
            extendedContext.Activity?.Dispose();
    }

    private static string MessageWithEmote(string emote, string message) => $"{Emoji.Parse($":{emote}:")} {message}";

    public void Dispose() => _activitySource.Dispose();
}
