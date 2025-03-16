using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Discord.Interactions;
using SchulPlanerBot.Options;
using System.Diagnostics;
using IResult = Discord.Interactions.IResult;

namespace SchulPlanerBot.Services;

internal sealed class DiscordInteractionHandler(
    IHostEnvironment environment,
    ILogger<DiscordInteractionHandler> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<DiscordClientOptions> optionsAccessor,
    DiscordSocketClient client,
    InteractionService interaction)
    : IHostedService, IDisposable
{
    public const string ActivitySourceName = "Discord.InteractionHandler";

    private readonly IHostEnvironment _environment = environment;
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
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
        _interaction.InteractionExecuted += Interaction_InteractionExecuted;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= Client_ReadyAsync;
        _client.InteractionCreated -= Client_InteractionCreatedAsync;
        _interaction.Log -= Interaction_Log;
        _interaction.InteractionExecuted -= Interaction_InteractionExecuted;

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
        using Activity? activity = _activitySource.StartActivity("Discord Interaction", ActivityKind.Server, parentId: null, tags: new Dictionary<string, object?>
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

            CancellableSocketContext context = new(_client, interaction, tokenSource.Token);
            IResult executionResult = await _interaction.ExecuteCommandAsync(context, scope.ServiceProvider).ConfigureAwait(false);     // AutoScope is enabled

            // Due to async nature of InteractionFramework, the result here may always be success.
            // That's why we also need to handle the InteractionExecuted event.
            if (!executionResult.IsSuccess)
            {
                _logger.LogError("A interaction failed to execute: {error} = {reason}", executionResult.Error, executionResult.ErrorReason);
            }
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
        LogLevel level = arg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => throw new NotImplementedException()
        };
        _logger.Log(level, arg.Exception, "{LogSource}: {LogMessage}", arg.Source, arg.Message);

        return Task.CompletedTask;
    }

    private Task Interaction_InteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
            _logger.LogError("A interaction failed to execute: {error} = {reason}", result.Error, result.ErrorReason);
        return Task.CompletedTask;
    }

    public void Dispose() => _activitySource.Dispose();
}
