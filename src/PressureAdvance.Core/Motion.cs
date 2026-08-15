namespace PressureAdvance.Core;

public interface IMotionSegment
{
    double DurationSeconds { get; }
    double StartVelocityMmPerSecond { get; }
    double EndVelocityMmPerSecond { get; }
    double DistanceMm { get; }
    MotionState Evaluate(double localTimeSeconds);
}

public sealed class ConstantVelocitySegment : IMotionSegment
{
    public ConstantVelocitySegment(double velocityMmPerSecond, double durationSeconds)
    {
        StartVelocityMmPerSecond = Validation.NonNegative(velocityMmPerSecond, nameof(velocityMmPerSecond));
        DurationSeconds = Validation.Positive(durationSeconds, nameof(durationSeconds));
    }

    public double DurationSeconds { get; }
    public double StartVelocityMmPerSecond { get; }
    public double EndVelocityMmPerSecond => StartVelocityMmPerSecond;
    public double DistanceMm => StartVelocityMmPerSecond * DurationSeconds;

    public MotionState Evaluate(double localTimeSeconds)
    {
        var t = Math.Clamp(Validation.Finite(localTimeSeconds, nameof(localTimeSeconds)), 0, DurationSeconds);
        return new MotionState(t, StartVelocityMmPerSecond * t, StartVelocityMmPerSecond, 0);
    }
}

public sealed class ConstantAccelerationSegment : IMotionSegment
{
    public ConstantAccelerationSegment(double startVelocityMmPerSecond, double endVelocityMmPerSecond,
        double accelerationMagnitudeMmPerSecondSquared)
    {
        StartVelocityMmPerSecond = Validation.NonNegative(startVelocityMmPerSecond, nameof(startVelocityMmPerSecond));
        EndVelocityMmPerSecond = Validation.NonNegative(endVelocityMmPerSecond, nameof(endVelocityMmPerSecond));
        var magnitude = Validation.Positive(accelerationMagnitudeMmPerSecondSquared, nameof(accelerationMagnitudeMmPerSecondSquared));
        if (startVelocityMmPerSecond == endVelocityMmPerSecond)
            throw new ArgumentException("Acceleration segment velocities must differ.", nameof(endVelocityMmPerSecond));
        AccelerationMmPerSecondSquared = Math.CopySign(magnitude, endVelocityMmPerSecond - startVelocityMmPerSecond);
        DurationSeconds = Math.Abs(endVelocityMmPerSecond - startVelocityMmPerSecond) / magnitude;
    }

    public double DurationSeconds { get; }
    public double StartVelocityMmPerSecond { get; }
    public double EndVelocityMmPerSecond { get; }
    public double AccelerationMmPerSecondSquared { get; }
    public double DistanceMm => (StartVelocityMmPerSecond + EndVelocityMmPerSecond) * 0.5 * DurationSeconds;

    public MotionState Evaluate(double localTimeSeconds)
    {
        var t = Math.Clamp(Validation.Finite(localTimeSeconds, nameof(localTimeSeconds)), 0, DurationSeconds);
        var velocity = StartVelocityMmPerSecond + AccelerationMmPerSecondSquared * t;
        var distance = StartVelocityMmPerSecond * t + 0.5 * AccelerationMmPerSecondSquared * t * t;
        return new MotionState(t, distance, velocity, AccelerationMmPerSecondSquared);
    }
}

public sealed class MotionProfile
{
    private readonly Entry[] entries;

    internal MotionProfile(string name, string description, IReadOnlyList<IMotionSegment> segments,
        IReadOnlyList<MotionTransition> transitions)
    {
        Name = name;
        Description = description;
        Segments = segments.ToArray();
        Transitions = transitions.ToArray();
        entries = new Entry[Segments.Count];
        var time = 0.0;
        var distance = 0.0;
        for (var i = 0; i < Segments.Count; i++)
        {
            entries[i] = new Entry(Segments[i], time, distance);
            time += Segments[i].DurationSeconds;
            distance += Segments[i].DistanceMm;
        }
        DurationSeconds = time;
        DistanceMm = distance;
    }

    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<IMotionSegment> Segments { get; }
    public IReadOnlyList<MotionTransition> Transitions { get; }
    public double DurationSeconds { get; }
    public double DistanceMm { get; }

    public MotionState Evaluate(double timeSeconds)
    {
        Validation.Finite(timeSeconds, nameof(timeSeconds));
        if (timeSeconds < 0 || timeSeconds > DurationSeconds + 1e-12)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds, "Time must be within the motion profile.");
        var time = Math.Min(timeSeconds, DurationSeconds);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var end = entry.StartTime + entry.Segment.DurationSeconds;
            if (time < end || i == entries.Length - 1)
            {
                var state = entry.Segment.Evaluate(time - entry.StartTime);
                return state with { TimeSeconds = time, DistanceMm = entry.StartDistance + state.DistanceMm };
            }
        }
        throw new InvalidOperationException("Motion profile evaluation failed.");
    }

    private sealed record Entry(IMotionSegment Segment, double StartTime, double StartDistance);
}

public sealed class MotionProfileBuilder
{
    private readonly string name;
    private readonly string description;
    private readonly List<IMotionSegment> segments = [];
    private readonly List<MotionTransition> transitions = [];
    private double time;
    private double distance;

    public MotionProfileBuilder(string name, string description)
    {
        this.name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        this.description = description;
    }

    public MotionProfileBuilder Add(IMotionSegment segment, string transitionName = "", string transitionKind = "segment-boundary")
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (segments.Count > 0)
        {
            var previous = segments[^1];
            if (Math.Abs(previous.EndVelocityMmPerSecond - segment.StartVelocityMmPerSecond) > 1e-9)
                throw new ArgumentException("Adjacent motion segments must be velocity-continuous.", nameof(segment));
            transitions.Add(new MotionTransition(transitions.Count,
                string.IsNullOrWhiteSpace(transitionName) ? $"transition-{transitions.Count}" : transitionName,
                transitionKind, time, distance, previous.EndVelocityMmPerSecond, segment.StartVelocityMmPerSecond));
        }
        segments.Add(segment);
        time += segment.DurationSeconds;
        distance += segment.DistanceMm;
        return this;
    }

    public MotionProfile Build() => segments.Count == 0
        ? throw new InvalidOperationException("A motion profile must contain at least one segment.")
        : new MotionProfile(name, description, segments, transitions);
}

public static class BuiltInScenarios
{
    private static readonly IReadOnlyDictionary<string, Func<MotionProfile>> Factories =
        new Dictionary<string, Func<MotionProfile>>(StringComparer.OrdinalIgnoreCase)
        {
            ["acceleration"] = Acceleration, ["deceleration"] = Deceleration, ["trapezoid"] = Trapezoid,
            ["corner"] = Corner, ["multi-change"] = MultiChange,
        };

    public static IReadOnlyList<(string Name, string Description)> List() =>
        Factories.Values.Select(factory => factory()).Select(profile => (profile.Name, profile.Description)).ToArray();

    public static MotionProfile Get(string name) => Factories.TryGetValue(name, out var factory) ? factory()
        : throw new ArgumentException($"Unknown scenario '{name}'. Available: {string.Join(", ", Factories.Keys)}.", nameof(name));

    private static MotionProfile Acceleration() => new MotionProfileBuilder("acceleration", "Steady motion, acceleration, and cruise.")
        .Add(new ConstantVelocitySegment(20, 0.2)).Add(new ConstantAccelerationSegment(20, 100, 2000), "acceleration-start", "acceleration-start")
        .Add(new ConstantVelocitySegment(100, 0.4), "cruise-start", "cruise-start").Build();
    private static MotionProfile Deceleration() => new MotionProfileBuilder("deceleration", "Steady motion, deceleration, and low-speed cruise.")
        .Add(new ConstantVelocitySegment(100, 0.2)).Add(new ConstantAccelerationSegment(100, 20, 2000), "deceleration-start", "slowdown-start")
        .Add(new ConstantVelocitySegment(20, 2), "cruise-start", "cruise-start").Build();
    private static MotionProfile Trapezoid() => new MotionProfileBuilder("trapezoid", "Acceleration, cruise, deceleration, and settling.")
        .Add(new ConstantVelocitySegment(20, 0.2)).Add(new ConstantAccelerationSegment(20, 120, 2000), "acceleration-start", "acceleration-start")
        .Add(new ConstantVelocitySegment(120, 0.25), "high-cruise", "cruise-start").Add(new ConstantAccelerationSegment(120, 20, 2000), "deceleration-start", "slowdown-start")
        .Add(new ConstantVelocitySegment(20, 0.5), "settle", "cruise-start").Build();
    private static MotionProfile Corner() => new MotionProfileBuilder("corner", "Speed reduction and recovery around an illustrative corner.")
        .Add(new ConstantVelocitySegment(30, 0.2)).Add(new ConstantAccelerationSegment(30, 120, 2000), "initial-acceleration", "acceleration-start")
        .Add(new ConstantVelocitySegment(120, 0.25), "high-cruise", "cruise-start").Add(new ConstantAccelerationSegment(120, 35, 2000), "corner-entry", "corner-entry")
        .Add(new ConstantVelocitySegment(35, 2.0 / 35.0), "corner-low-speed", "cruise-start").Add(new ConstantAccelerationSegment(35, 120, 2000), "corner-exit", "corner-exit")
        .Add(new ConstantVelocitySegment(120, 0.25), "exit-cruise", "cruise-start").Add(new ConstantAccelerationSegment(120, 0, 2000), "final-deceleration", "stop-start")
        .Add(new ConstantVelocitySegment(0, 0.2), "stopped", "stop").Build();
    private static MotionProfile MultiChange()
    {
        var builder = new MotionProfileBuilder("multi-change", "Repeated deterministic speed changes.").Add(new ConstantVelocitySegment(40, 0.15));
        var current = 40.0;
        foreach (var next in new[] { 120.0, 40, 80, 30, 100, 20 })
        {
            builder.Add(new ConstantAccelerationSegment(current, next, 2000), $"change-to-{next:0}", next > current ? "acceleration-start" : "slowdown-start")
                .Add(new ConstantVelocitySegment(next, 0.15), $"plateau-{next:0}", "cruise-start");
            current = next;
        }
        return builder.Build();
    }
}
