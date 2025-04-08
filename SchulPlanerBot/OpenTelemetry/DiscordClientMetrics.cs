using Discord.Rest;
using Discord.WebSocket;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SchulPlanerBot.OpenTelemetry;

internal sealed class DiscordClientMetrics : IDisposable
{
    public const string ActivitySourceName = "Discord.ClientMetrics";
    public const string MeterName = "Discord.Client";

    private readonly DiscordSocketClient _client;

    private readonly Meter _meter;
    private readonly Histogram<double> _latency;
    private readonly Gauge<int> _guilds;

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public DiscordClientMetrics(IMeterFactory factory, DiscordSocketClient client)
    {
        _client = client;
        _client.Ready += Client_ReadyAsync;
        _client.LatencyUpdated += Client_LatencyUpdated;
        _client.JoinedGuild += Client_JoinedGuildAsync;
        _client.LeftGuild += Client_LeftGuildAsync;

        _meter = factory.Create(MeterName);
        _guilds = _meter.CreateGauge<int>(
            name: "Bot.Guilds",
            description: "The count of guild the bot is a member of.",
            unit: "Guilds");
        _latency = _meter.CreateHistogram<double>(
            name: "Gateway.Latency",
            description: "The latency to the event gateway.",
            unit: "Seconds",
            advice: new()
            {
                /* 
                 * OTel bucket boundary recommendation for 'http.request.duration':
                 * https://github.com/open-telemetry/semantic-conventions/blob/release/v1.23.x/docs/http/http-metrics.md#metric-httpclientrequestduration
                 * [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
                */
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.125, 0.15, 0.175, 0.2, 0.225, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]     // Higher resolution in the area from 0.1 to 0.25 in 0.025 steps
            });
    }

    private async Task Client_ReadyAsync() => await UpdateGuildsAmountAsync().ConfigureAwait(false);

    private Task Client_LatencyUpdated(int old, int value)
    {
        _latency.Record((double)value / 1000);     // Milliseconds to seconds
        return Task.CompletedTask;
    }

    private async Task Client_JoinedGuildAsync(SocketGuild guild) => await UpdateGuildsAmountAsync().ConfigureAwait(false);

    private async Task Client_LeftGuildAsync(SocketGuild guild) => await UpdateGuildsAmountAsync().ConfigureAwait(false);

    private async Task UpdateGuildsAmountAsync()
    {
        using Activity? activity = _activitySource.StartActivity("Update guilds", kind: ActivityKind.Client);

        IReadOnlyCollection<RestGuild> guilds = await _client.Rest.GetGuildsAsync().ConfigureAwait(false);
        _guilds.Record(guilds.Count);
    }

    public void Dispose()
    {
        _meter.Dispose();
        _client.Ready -= Client_ReadyAsync;
        _client.LatencyUpdated -= Client_LatencyUpdated;
        _client.JoinedGuild -= Client_JoinedGuildAsync;
        _client.LeftGuild -= Client_LeftGuildAsync;

        _activitySource.Dispose();
    }
}
