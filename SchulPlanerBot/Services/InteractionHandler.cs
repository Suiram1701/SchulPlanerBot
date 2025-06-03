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

internal sealed class InteractionHandler : BackgroundService
{
    public const string ActivitySourceName = "Discord.InteractionHandler";

    private readonly IHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly ILogger _interactionLogger;
    private readonly IStringLocalizer _localizer;
    private readonly DiscordClientOptions _options;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interaction;
    private readonly IgnoringService _ignoringService;
    private readonly DatabaseMigrator _dbMigrator;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    
    public InteractionHandler(
        IHostEnvironment environment,
        IServiceScopeFactory scopeFactory,
        ILogger<InteractionHandler> logger,
        ILogger<InteractionService> interactionLogger,
        IStringLocalizer<InteractionHandler> localizer,
        IOptions<DiscordClientOptions> optionsAccessor,
        DiscordSocketClient client,
        InteractionService interaction,
        IgnoringService ignoringService,
        DatabaseMigrator dbMigrator)
    {
        _environment = environment;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interactionLogger = interactionLogger;
        _localizer = localizer;
        _options = optionsAccessor.Value;
        _client = client;
        _interaction = interaction;
        _ignoringService = ignoringService;
        _dbMigrator = dbMigrator;
        
        _client.InteractionCreated += Client_InteractionCreatedAsync;
        _interaction.Log += Interaction_Log;
        _interaction.InteractionExecuted += Interaction_InteractionExecutedAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _dbMigrator.MigrationCompleted.ConfigureAwait(false);
        
        using Activity? activity = _activitySource.StartActivity("Initialize interaction handler");
        using IServiceScope scope = _scopeFactory.CreateScope();

        try
        {
            IEnumerable<ModuleInfo> modules = await _interaction.AddModulesAsync(typeof(ISchulPlanerBot).Assembly, scope.ServiceProvider).ConfigureAwait(false);
            _logger.LogInformation("{modulesCount} interaction modules added", modules.Count());

            TaskCompletionSource tcs = new();
            Task onClientReady()
            {
                _client.Ready -= onClientReady;
                tcs.SetResult();

                return Task.CompletedTask;
            }
            _client.Ready += onClientReady;
            await tcs.Task.ConfigureAwait(false);     // Wait for the client to be ready

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

            throw;
        }
    }
    
    private async Task Client_InteractionCreatedAsync(SocketInteraction interaction)
    {
        Activity.Current = null;     // This activity doesn't have a parent
        Activity? activity = _activitySource.StartActivity("Discord Interaction", ActivityKind.Server, parentId: null, tags: new Dictionary<string, object?>     // Activity disposed by event via context.
        {
            { "interaction.id", interaction.Id },
            { "interaction.type", interaction.Type },
            { "interaction.guild", interaction.GuildId },
            { "interaction.user", interaction.User.ToString() },
        });

        if (_environment.IsDevelopment() && _options.TestGuild is not null && interaction.GuildId != _options.TestGuild)
        {
            _logger.LogInformation("Interaction cancelled! Sent from non-test guild {guildId} during development!", interaction.GuildId);
            
            activity?.Dispose();
            return;
        }

        if (_ignoringService.IsIgnoredUser(interaction.User.Id))
        {
            _logger.LogInformation("Interaction cancelled! Sent from ignored user {userId}!", interaction.User.Id);
            
            activity?.Dispose();
            return;
        }
        
        if (interaction.GuildId is not null && _ignoringService.IsIgnoredGuild(interaction.GuildId.Value))
        {
            _logger.LogInformation("Interaction cancelled! Sent from ignored user {guildId}!", interaction.GuildId);
            
            activity?.Dispose();
            return;
        }
        
        // Required by IStringLocalizer and Humanizer
        CultureInfo userCulture = new(interaction.UserLocale);
        Utils.SetCulture(userCulture);

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
        if (result is { IsSuccess: false, Error: not null })
        {
            string responseMessage = MessageWithEmote("exclamation", _localizer["errorResponse.unknown"]);
            var responseColor = Utils.AnsiColor.Red;
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

    public override void Dispose()
    {
        base.Dispose();
        _activitySource.Dispose();

        _client.InteractionCreated -= Client_InteractionCreatedAsync;
        _interaction.Log -= Interaction_Log;
        _interaction.InteractionExecuted -= Interaction_InteractionExecutedAsync;
    }
}
