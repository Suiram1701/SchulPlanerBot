using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SchulPlanerBot.Options;

namespace SchulPlanerBot.Services;

public sealed class DiscordClientStartup(
    ILogger<DiscordSocketClient> logger,
    IOptions<DiscordClientOptions> clientOptionsAccessor,
    DiscordSocketClient client)
    : IHostedService, IAsyncDisposable
{
    private readonly ILogger _logger = logger;
    private readonly DiscordClientOptions _clientOptions = clientOptionsAccessor.Value;
    private readonly DiscordSocketClient _client = client;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += Client_Log;

        await _client.LoginAsync(_clientOptions.TokenType, _clientOptions.Token).ConfigureAwait(false);
        if (_client.LoginState != LoginState.LoggedIn)
        {
            _logger.LogCritical("Unable to login client");
            return;
        }

        await _client.StartAsync().ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.LogoutAsync().ConfigureAwait(false);
        await _client.StopAsync().ConfigureAwait(false);

        _client.Log -= Client_Log;
    }

    private Task Client_Log(LogMessage arg)
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

    public async ValueTask DisposeAsync() => await _client.DisposeAsync().ConfigureAwait(false);
}
