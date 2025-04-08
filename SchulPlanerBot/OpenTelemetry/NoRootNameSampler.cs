using OpenTelemetry.Trace;

namespace SchulPlanerBot.OpenTelemetry;

internal sealed class NoRootNameSampler : Sampler
{
    private readonly Sampler _rootSampler;
    private readonly string[] _namesNotSample;

    public NoRootNameSampler(Sampler rootSampler, params string[] namesNotSample)
    {
        _rootSampler = rootSampler;
        _namesNotSample = namesNotSample;

        Description = "Drops samples with a specific name and which are also root activities.";
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        string activityName = samplingParameters.Name;
        if (_namesNotSample.Any(activityName.Contains) && samplingParameters.ParentContext.TraceId == default)
        {
            return new SamplingResult(SamplingDecision.Drop);
        }
        else
        {
            return _rootSampler.ShouldSample(samplingParameters);
        }
    }
}
