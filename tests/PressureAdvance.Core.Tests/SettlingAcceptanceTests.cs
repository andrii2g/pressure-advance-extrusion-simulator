using PressureAdvance.Core;

namespace PressureAdvance.Core.Tests;

[TestClass]
public sealed class SettlingAcceptanceTests
{
    [TestMethod]
    public void ToleranceBoundaryCountsAsInBand()
    {
        var result = Result((0, 0), (0.1, 0.02), (0.15, -0.02), (0.2, 0));
        var settling = SettlingAnalyzer.Analyze(result, new());
        Assert.IsTrue(settling[0].Settled);
        Assert.AreEqual(0, settling[0].SettlingTimeSeconds!.Value, 1e-12);
        Assert.AreEqual(0, settling[0].SettlingDistanceMm!.Value, 1e-12);
    }

    [TestMethod]
    public void OutOfBandSampleResetsCandidate()
    {
        var result = Result((0, 0), (0.1, 0), (0.14, 0.021), (0.15, 0), (0.2, 0));
        var settling = SettlingAnalyzer.Analyze(result, new());
        Assert.IsTrue(settling[0].Settled);
        Assert.AreEqual(0.05, settling[0].SettlingTimeSeconds!.Value, 1e-12);
        Assert.AreEqual(0.05, settling[0].SettlingDistanceMm!.Value, 1e-12);
    }

    [TestMethod]
    public void EndBeforeHoldWindowIsUnavailable()
    {
        var result = Result((0, 0), (0.1, 0), (0.14, 0.021), (0.15, 0), (0.18, 0));
        var settling = SettlingAnalyzer.Analyze(result, new());
        Assert.IsFalse(settling[0].Settled);
        Assert.IsNull(settling[0].SettlingTimeSeconds);
        Assert.IsNull(settling[0].SettlingDistanceMm);
    }

    private static SimulationResult Result(params (double Time, double Error)[] values)
    {
        var profile = new MotionProfileBuilder("settling", "synthetic")
            .Add(new ConstantVelocitySegment(1, 0.1))
            .Add(new ConstantVelocitySegment(1, 0.2), "transition", "test").Build();
        var options = new SimulationOptions(0.01, new(0.2, 0.45), new(0.04, 1), new(0));
        var samples = values.Select(x => new SimulationSample(x.Time, x.Time, 0, 0, 0, 0, 0, 0, 0,
            false, x.Error, 0, x.Error, x.Error)).ToArray();
        return new("settling", options, profile, samples);
    }
}
