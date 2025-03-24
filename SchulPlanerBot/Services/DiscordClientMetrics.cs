using Discord.Rest;
using Discord.WebSocket;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SchulPlanerBot.Services;

internal sealed class DiscordClientMetrics : IHostedService, IDisposable
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
        _client.LatencyUpdated += Client_LatencyUpdated;
        _client.JoinedGuild += Client_JoinedGuildAsync;
        _client.LeftGuild += Client_LeftGuildAsync;

        _meter = factory.Create(MeterName);
        _guilds = _meter.CreateGauge<int>("Bot.Guilds", description: "The count of guild the bot is a member of.", unit: "Guilds");
        _latency = _meter.CreateHistogram<double>("Gateway.Latency", description: "The latency to the event gateway.", unit: "Seconds", advice: new()
        {
            HistogramBucketBoundaries = [0.1, 3]
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken) => await UpdateGuildsAmountAsync().ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
        _client.LatencyUpdated -= Client_LatencyUpdated;
        _client.JoinedGuild -= Client_JoinedGuildAsync;
        _client.LeftGuild -= Client_LeftGuildAsync;

        _activitySource.Dispose();
    }
}
