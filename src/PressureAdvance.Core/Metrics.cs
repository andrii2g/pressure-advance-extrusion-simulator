namespace PressureAdvance.Core;

public sealed record SettlingOptions
{
    public SettlingOptions(double relativeTolerance = 0.02, double absoluteToleranceFloorMm3PerSecond = 0.02, double holdSeconds = 0.05)
    {
        RelativeTolerance = Validation.NonNegative(relativeTolerance, nameof(relativeTolerance));
        AbsoluteToleranceFloorMm3PerSecond = Validation.NonNegative(absoluteToleranceFloorMm3PerSecond, nameof(absoluteToleranceFloorMm3PerSecond));
        HoldSeconds = Validation.Positive(holdSeconds, nameof(holdSeconds));
    }
    public double RelativeTolerance { get; }
    public double AbsoluteToleranceFloorMm3PerSecond { get; }
    public double HoldSeconds { get; }
}

public sealed record TransitionSettlingResult(int TransitionIndex, string Name, string Kind,
    double TransitionTimeSeconds, double TransitionDistanceMm, double ToleranceMm3PerSecond,
    bool Settled, double? SettlingTimeSeconds, double? SettlingDistanceMm);

public sealed record RunMetrics(double IntegratedAbsoluteFlowErrorMm3, double MinimumSignedErrorMm3PerSecond,
    double MaximumSignedErrorMm3PerSecond, double PeakUnderFlowMm3PerSecond, double PeakOverFlowMm3PerSecond,
    double RmseMm3PerSecond, double UnderExtrusionVolumeMm3, double OverExtrusionVolumeMm3, int ClampCount,
    int TransitionCount, int SettledTransitionCount, int UnsettledTransitionCount,
    double? WorstSettlingTimeSeconds, double? WorstSettlingDistanceMm,
    IReadOnlyList<TransitionSettlingResult> Transitions);

public static class RunMetricsCalculator
{
    public static RunMetrics Calculate(SimulationResult result, SettlingOptions? settlingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var samples = result.Samples;
        if (samples.Count < 2 || samples[^1].TimeSeconds <= samples[0].TimeSeconds)
            throw new ArgumentException("Metrics require at least two samples spanning positive time.", nameof(result));
        var iafe = 0.0; var squared = 0.0; var under = 0.0; var over = 0.0;
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var first = samples[i]; var second = samples[i + 1];
            var dt = second.TimeSeconds - first.TimeSeconds;
            iafe += 0.5 * (Math.Abs(first.FlowErrorMm3PerSecond) + Math.Abs(second.FlowErrorMm3PerSecond)) * dt;
            squared += 0.5 * (first.FlowErrorMm3PerSecond * first.FlowErrorMm3PerSecond + second.FlowErrorMm3PerSecond * second.FlowErrorMm3PerSecond) * dt;
            under += 0.5 * (Math.Max(0, -first.FlowErrorMm3PerSecond) + Math.Max(0, -second.FlowErrorMm3PerSecond)) * dt;
            over += 0.5 * (Math.Max(0, first.FlowErrorMm3PerSecond) + Math.Max(0, second.FlowErrorMm3PerSecond)) * dt;
        }
        var minimum = samples.Min(x => x.FlowErrorMm3PerSecond);
        var maximum = samples.Max(x => x.FlowErrorMm3PerSecond);
        var settling = SettlingAnalyzer.Analyze(result, settlingOptions ?? new SettlingOptions());
        var settled = settling.Where(x => x.Settled).ToArray();
        return new(iafe, minimum, maximum, Math.Max(0, -minimum), Math.Max(0, maximum),
            Math.Sqrt(squared / (samples[^1].TimeSeconds - samples[0].TimeSeconds)), under, over,
            samples.Count(x => x.DriveWasClamped), settling.Count, settled.Length, settling.Count - settled.Length,
            settled.Length == 0 ? null : settled.Max(x => x.SettlingTimeSeconds),
            settled.Length == 0 ? null : settled.Max(x => x.SettlingDistanceMm), settling);
    }
}

public static class SettlingAnalyzer
{
    public static IReadOnlyList<TransitionSettlingResult> Analyze(SimulationResult result, SettlingOptions options)
    {
        var output = new List<TransitionSettlingResult>();
        foreach (var transition in result.Profile.Transitions)
        {
            var start = 0;
            while (start < result.Samples.Count - 1 && result.Samples[start].TimeSeconds < transition.TimeSeconds - 1e-12) start++;
            var postFlow = result.Samples[start].RequestedFlowMm3PerSecond;
            var tolerance = Math.Max(options.RelativeTolerance * Math.Abs(postFlow), options.AbsoluteToleranceFloorMm3PerSecond);
            TransitionSettlingResult? match = null;
            for (var candidate = start; candidate < result.Samples.Count; candidate++)
            {
                var holdEnd = result.Samples[candidate].TimeSeconds + options.HoldSeconds;
                if (holdEnd > result.Samples[^1].TimeSeconds + 1e-12) break;
                var valid = true; var proven = false;
                for (var i = candidate; i < result.Samples.Count; i++)
                {
                    var sample = result.Samples[i];
                    if (Math.Abs(sample.FlowErrorMm3PerSecond) > tolerance) { valid = false; break; }
                    if (sample.TimeSeconds >= holdEnd - 1e-12) { proven = true; break; }
                }
                if (valid && proven)
                {
                    var sample = result.Samples[candidate];
                    match = new(transition.Index, transition.Name, transition.Kind, transition.TimeSeconds,
                        transition.DistanceMm, tolerance, true, sample.TimeSeconds - transition.TimeSeconds,
                        sample.DistanceMm - transition.DistanceMm);
                    break;
                }
            }
            output.Add(match ?? new(transition.Index, transition.Name, transition.Kind, transition.TimeSeconds,
                transition.DistanceMm, tolerance, false, null, null));
        }
        return output.AsReadOnly();
    }
}
