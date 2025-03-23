using Discord.Rest;
using Discord.WebSocket;
using System.Diagnostics.Metrics;

namespace SchulPlanerBot.Services;

internal sealed class DiscordClientMetrics : IHostedService, IDisposable
{
    public const string MeterName = "Discord.Client";

    private readonly DiscordSocketClient _client;

    private readonly Meter _meter;
    private readonly Histogram<double> _latency;
    private readonly UpDownCounter<int> _guilds;

    public DiscordClientMetrics(IMeterFactory factory, DiscordSocketClient client)
    {
        _client = client;
        _client.LatencyUpdated += Client_LatencyUpdated;
        _client.JoinedGuild += Client_JoinedGuild;
        _client.LeftGuild += Client_LeftGuild;

        _meter = factory.Create(MeterName);
        _guilds = _meter.CreateUpDownCounter<int>("Bot.Guilds", description: "The count of guild the bot is a member of.", unit: "Guilds");
        _latency = _meter.CreateHistogram<double>("Gateway.Latency", description: "The latency to the event gateway.", unit: "Seconds", advice: new()
        {
            HistogramBucketBoundaries = [0.1, 3]
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<RestGuild> guilds = await _client.Rest.GetGuildsAsync().ConfigureAwait(false);
        _guilds.Add(guilds.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task Client_LatencyUpdated(int old, int value)
    {
        _latency.Record((double)value / 1000);     // Milliseconds to seconds
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild guild)
    {
        _guilds.Add(1);
        return Task.CompletedTask;
    }

    private Task Client_LeftGuild(SocketGuild guild)
    {
        _guilds.Add(-1);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _meter.Dispose();
        _client.LatencyUpdated -= Client_LatencyUpdated;
        _client.JoinedGuild -= Client_JoinedGuild;
        _client.LeftGuild -= Client_LeftGuild;
    }
}
