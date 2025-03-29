using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Discord;
using SchulPlanerBot.Options;
using System.Diagnostics;

namespace SchulPlanerBot.Services;

internal sealed class DiscordClientManager(
    ILogger<DiscordClientManager> logger,
    ILogger<DiscordSocketClient> clientLogger,
    IOptions<DiscordClientOptions> clientOptionsAccessor,
    DiscordSocketClient client)
    : IHostedService, IDisposable
{
    public const string ActivitySourceName = "Discord.ClientManager";

    private readonly ILogger _logger = logger;
    private readonly ILogger _clientLogger = clientLogger;
    private readonly DiscordClientOptions _clientOptions = clientOptionsAccessor.Value;
    private readonly DiscordSocketClient _client = client;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = _activitySource.StartActivity("Discord bot startup");

        _client.Log += Client_Log;

        await _client.LoginAsync(_clientOptions.TokenType, _clientOptions.Token).ConfigureAwait(false);
        if (_client.LoginState == LoginState.LoggedIn)
        {
            _logger.LogInformation("Bot successfully logged in");
        }
        else
        {
            _logger.LogCritical("Unable to login client");
            return;
        }

        await _client.StartAsync().ConfigureAwait(false);
        _logger.LogInformation("Bot successfully started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Activity.Current = null;     // This activity doesn't have a parent
        using Activity? activity = _activitySource.StartActivity("Discord bot shutdown");

        await _client.StopAsync().ConfigureAwait(false);
        _logger.LogInformation("Bot stopped");

        await _client.LogoutAsync().ConfigureAwait(false);
        _logger.LogInformation("Bot logged out");

        _client.Log -= Client_Log;
    }

    private Task Client_Log(LogMessage arg)
    {
        _clientLogger.Log(Utils.ConvertLogLevel(arg.Severity), arg.Exception, "{LogSource}: {LogMessage}", arg.Source, arg.Message);
        return Task.CompletedTask;
    }

    public void Dispose() => _activitySource.Dispose();
}
