using Discord;
using Discord.Interactions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using IResult = Discord.Interactions.IResult;

namespace SchulPlanerBot.Services;

public sealed class InteractionFrameworkMetrics : IDisposable
{
    public const string MeterName = "Discord.InteractionFramework";

    private readonly InteractionService _interaction;

    private readonly Meter _meter;
    private readonly Counter<long> _totalInteractions;
    private readonly Histogram<double> _interactionDuration;

    private readonly ConcurrentDictionary<ulong, Stopwatch> _watches = new();

    public InteractionFrameworkMetrics(IMeterFactory factory, InteractionService interaction)
    {
        _interaction = interaction;
        _interaction.InteractionExecuted += Interaction_InteractionExecuted;

        _meter = factory.Create(MeterName);
        _totalInteractions = _meter.CreateCounter<long>("Interactions.Total", description: "The total amount of executed interactions.", unit: "Interactions");
        _interactionDuration = _meter.CreateHistogram<double>("Interactions.Duration", description: "The time interactions took to execute.", unit: "seconds", advice: new()
        {
            HistogramBucketBoundaries = [0.1, 3]
        });
    }

    private Task Interaction_InteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
    {
        _totalInteractions.Add(1);

        TimeSpan duration = DateTimeOffset.UtcNow - context.Interaction.CreatedAt;
        _interactionDuration.Record(duration.TotalSeconds, [
            new("Type", context.Interaction.Type),
            new("Module", command.Module.Name),
            new("Command", command.Name),
            new("Succeeded", result.IsSuccess),
            new("ErrorType", result.Error)
            ]);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _meter.Dispose();
        _interaction.InteractionExecuted -= Interaction_InteractionExecuted;
    }
}
