namespace PressureAdvance.Core;

public sealed record KSweepOptions
{
    public KSweepOptions(double startKSeconds, double endKSeconds, double stepKSeconds)
    {
        StartKSeconds = Validation.NonNegative(startKSeconds, nameof(startKSeconds));
        EndKSeconds = Validation.NonNegative(endKSeconds, nameof(endKSeconds));
        StepKSeconds = Validation.Positive(stepKSeconds, nameof(stepKSeconds));
        if (endKSeconds < startKSeconds) throw new ArgumentOutOfRangeException(nameof(endKSeconds), endKSeconds, "Sweep end must be at least the start.");
    }
    public double StartKSeconds { get; }
    public double EndKSeconds { get; }
    public double StepKSeconds { get; }
}

public sealed record KSweepPoint(double KSeconds, RunMetrics Metrics);
public sealed record KSweepResult(IReadOnlyList<KSweepPoint> Points, KSweepPoint Best);

public static class KSweepRunner
{
    public static KSweepResult Run(MotionProfile profile, SimulationOptions baseOptions, KSweepOptions sweep, SettlingOptions? settling = null)
    {
        var points = new List<KSweepPoint>();
        for (var index = 0; ; index++)
        {
            var calculated = sweep.StartKSeconds + index * sweep.StepKSeconds;
            if (calculated > sweep.EndKSeconds + 1e-12) break;
            var k = Math.Abs(calculated - sweep.EndKSeconds) <= 1e-12 ? sweep.EndKSeconds : calculated;
            points.Add(RunPoint(profile, baseOptions, k, settling));
        }
        if (points.Count == 0 || points[^1].KSeconds < sweep.EndKSeconds - 1e-12)
            points.Add(RunPoint(profile, baseOptions, sweep.EndKSeconds, settling));
        var best = points[0];
        foreach (var point in points.Skip(1))
            if (point.Metrics.IntegratedAbsoluteFlowErrorMm3 < best.Metrics.IntegratedAbsoluteFlowErrorMm3 - 1e-12) best = point;
        return new(points.AsReadOnly(), best);
    }

    private static KSweepPoint RunPoint(MotionProfile profile, SimulationOptions source, double k, SettlingOptions? settling)
    {
        var options = new SimulationOptions(source.TimeStepSeconds, source.Geometry, source.Plant,
            new PressureAdvanceParameters(k, source.PressureAdvance.DriveFlowPolicy), source.InitialPressure);
        return new(k, RunMetricsCalculator.Calculate(new SimulationEngine().Run(profile, options), settling));
    }
}
