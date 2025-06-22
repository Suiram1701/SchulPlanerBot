using Discord;
using Discord.Interactions;
using System.Diagnostics.Metrics;
using IResult = Discord.Interactions.IResult;

namespace SchulPlanerBot.OpenTelemetry;

internal sealed class InteractionFrameworkMetrics : IDisposable
{
    public const string MeterName = "Discord.InteractionFramework";

    private readonly InteractionService _interaction;

    private readonly Meter _meter;
    private readonly Counter<long> _totalInteractions;
    private readonly Histogram<double> _interactionDuration;

    public InteractionFrameworkMetrics(IMeterFactory factory, InteractionService interaction)
    {
        _interaction = interaction;
        _interaction.InteractionExecuted += Interaction_InteractionExecuted;

        _meter = factory.Create(MeterName);
        _totalInteractions = _meter.CreateCounter<long>(
            name: "Interactions.Total",
            description: "The total amount of executed interactions.",
            unit: "Interactions");
        _interactionDuration = _meter.CreateHistogram<double>(
            name: "Interactions.Duration",
            description: "The time interactions took to execute.",
            unit: "Seconds",
            advice: new()
            {
                HistogramBucketBoundaries = [0.1, 0.25, 0.5, 0.6, 0.7, 0.8, 0.9, 1, 1.1, 1.2, 1.3, 1.4, 1.5, 1.75, 2, 2.25, 2.5]
            });
    }

    private Task Interaction_InteractionExecuted(ICommandInfo? command, IInteractionContext context, IResult result)
    {
        _totalInteractions.Add(1);

        TimeSpan duration = DateTimeOffset.UtcNow - context.Interaction.CreatedAt;
        _interactionDuration.Record(duration.TotalSeconds, [
            KeyValuePair.Create<string, object?>("Type", context.Interaction.Type),
            KeyValuePair.Create<string, object?>("Module", command?.Module.Name),
            KeyValuePair.Create<string, object?>("Command", command?.Name),
            KeyValuePair.Create<string, object?>("Succeeded", result.IsSuccess),
            KeyValuePair.Create<string, object?>("ErrorType", result.Error)
        ]);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _meter.Dispose();
        _interaction.InteractionExecuted -= Interaction_InteractionExecuted;
    }
}
