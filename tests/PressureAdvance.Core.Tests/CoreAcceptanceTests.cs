using PressureAdvance.Core;

namespace PressureAdvance.Core.Tests;

[TestClass]
public sealed class CoreAcceptanceTests
{
    private static readonly ExtrusionGeometry Geometry = new(0.20, 0.45);

    [TestMethod]
    public void ValidationRejectsInvalidNumericInputs()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PlantParameters(0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PlantParameters(0.04, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExtrusionGeometry(-0.2, 0.45));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SimulationOptions(0, Geometry, new(0.04, 1), new(0)));
    }

    [TestMethod]
    public void DemandUsesRectangularAreaAndAnalyticalAcceleration()
    {
        var demand = ExtrusionDemandCalculator.Calculate(new(0, 0, 100, 2000), Geometry);
        Assert.AreEqual(9.0, demand.RequestedFlow, 1e-12);
        Assert.AreEqual(180.0, demand.RequestedFlowDerivative, 1e-12);
        Assert.AreEqual(0, ExtrusionDemandCalculator.Calculate(new(0, 0, 0, 0), Geometry).RequestedFlow);
    }

    [TestMethod]
    public void PressureAdvanceFormsRawCommandBeforePolicy()
    {
        var positive = new PressureAdvanceFeedForward(new(0.04)).Calculate(9, 180);
        Assert.AreEqual(7.2, positive.AdvanceFlow, 1e-12);
        var clamped = new PressureAdvanceFeedForward(new(0.04)).Calculate(1, -50);
        Assert.AreEqual(-1, clamped.RawDriveFlow, 1e-12);
        Assert.AreEqual(0, clamped.DriveFlow);
        Assert.IsTrue(clamped.WasClamped);
        var negative = new PressureAdvanceFeedForward(new(0.04, DriveFlowPolicy.AllowNegative)).Calculate(1, -50);
        Assert.AreEqual(-1, negative.DriveFlow, 1e-12);
        Assert.IsFalse(negative.WasClamped);
    }

    [TestMethod]
    public void KZeroEqualsNoCompensation()
    {
        var pa = new PressureAdvanceFeedForward(new(0)).Calculate(4, -12);
        var none = new NoCompensationFeedForward().Calculate(4, -12);
        Assert.AreEqual(none, pa);
    }

    [TestMethod]
    public void MotionEndpointsContinuityAndCornerMarkersAreDeterministic()
    {
        var segment = new ConstantAccelerationSegment(20, 100, 2000);
        Assert.AreEqual(0.04, segment.DurationSeconds, 1e-12);
        Assert.AreEqual(100, segment.Evaluate(segment.DurationSeconds).VelocityMmPerSecond, 1e-10);
        Assert.AreEqual(2.4, segment.DistanceMm, 1e-12);
        var corner = BuiltInScenarios.Get("corner");
        Assert.IsTrue(corner.Transitions.Any(x => x.Kind == "corner-entry"));
        Assert.IsTrue(corner.Transitions.Any(x => x.Kind == "corner-exit"));
        foreach (var transition in corner.Transitions)
            Assert.AreEqual(transition.VelocityBeforeMmPerSecond, transition.VelocityAfterMmPerSecond, 1e-12);
    }

    [TestMethod]
    public void PlantStepMatchesEulerAndSteadyState()
    {
        var plant = new FirstOrderExtrusionPlant(new(0.04, 2));
        Assert.AreEqual(9, plant.ActualFlow(new(18)), 1e-12);
        var next = plant.Advance(new(0), 9, 0.001);
        Assert.AreEqual(0.45, next.NozzlePressure, 1e-12);
    }

    [TestMethod]
    public void EulerConvergesTowardAnalyticalResponse()
    {
        var errors = new[] { 0.004, 0.002, 0.001, 0.0005 }.Select(EulerError).ToArray();
        for (var i = 1; i < errors.Length; i++) Assert.IsTrue(errors[i] < errors[i - 1]);
        Assert.IsTrue(errors[2] < 0.005);
    }

    [TestMethod]
    public void SimulationIsDeterministicMonotonicAndIncludesExactEndpoint()
    {
        var profile = new MotionProfileBuilder("endpoint", "test")
            .Add(new ConstantVelocitySegment(10, 0.0105)).Build();
        var options = Options(0.001, 0);
        var first = new SimulationEngine().Run(profile, options);
        var second = new SimulationEngine().Run(profile, options);
        CollectionAssert.AreEqual(first.Samples.ToArray(), second.Samples.ToArray());
        Assert.AreEqual(profile.DurationSeconds, first.Samples[^1].TimeSeconds, 1e-15);
        for (var i = 1; i < first.Samples.Count; i++)
        {
            Assert.IsTrue(first.Samples[i].TimeSeconds > first.Samples[i - 1].TimeSeconds);
            Assert.IsTrue(first.Samples[i].DistanceMm >= first.Samples[i - 1].DistanceMm);
        }
    }

    [TestMethod]
    public void ExpectedTransientSignsAndNearTauImprovementOccur()
    {
        var acceleration = new SimulationEngine().Run(BuiltInScenarios.Get("acceleration"), Options(0.001, 0));
        var deceleration = new SimulationEngine().Run(BuiltInScenarios.Get("deceleration"), Options(0.001, 0));
        Assert.IsTrue(acceleration.Samples.Min(x => x.FlowErrorMm3PerSecond) < 0);
        Assert.IsTrue(deceleration.Samples.Max(x => x.FlowErrorMm3PerSecond) > 0);
        var profile = BuiltInScenarios.Get("corner");
        var baseline = RunMetricsCalculator.Calculate(new SimulationEngine().Run(profile, Options(0.001, 0, DriveFlowPolicy.AllowNegative)));
        var optimal = RunMetricsCalculator.Calculate(new SimulationEngine().Run(profile, Options(0.001, 0.04, DriveFlowPolicy.AllowNegative)));
        var excessive = RunMetricsCalculator.Calculate(new SimulationEngine().Run(profile, Options(0.001, 0.10, DriveFlowPolicy.AllowNegative)));
        Assert.IsTrue(optimal.IntegratedAbsoluteFlowErrorMm3 < baseline.IntegratedAbsoluteFlowErrorMm3);
        Assert.IsTrue(excessive.IntegratedAbsoluteFlowErrorMm3 > optimal.IntegratedAbsoluteFlowErrorMm3);
    }

    [TestMethod]
    public void MetricsUseSignedErrorAndTrapezoidalIntegration()
    {
        var result = SyntheticResult([-1, 1, 1]);
        var metrics = RunMetricsCalculator.Calculate(result);
        Assert.AreEqual(2, metrics.IntegratedAbsoluteFlowErrorMm3, 1e-12);
        Assert.AreEqual(0.5, metrics.UnderExtrusionVolumeMm3, 1e-12);
        Assert.AreEqual(1.5, metrics.OverExtrusionVolumeMm3, 1e-12);
        Assert.AreEqual(metrics.IntegratedAbsoluteFlowErrorMm3,
            metrics.UnderExtrusionVolumeMm3 + metrics.OverExtrusionVolumeMm3, 1e-12);
        Assert.AreEqual(1, metrics.PeakUnderFlowMm3PerSecond);
        Assert.AreEqual(1, metrics.PeakOverFlowMm3PerSecond);
        Assert.AreEqual(1, metrics.RmseMm3PerSecond, 1e-12);
    }

    [TestMethod]
    public void SweepIncludesEndpointsAndFindsGridPointNearTau()
    {
        var result = KSweepRunner.Run(BuiltInScenarios.Get("corner"), Options(0.001, 0, DriveFlowPolicy.AllowNegative), new(0, 0.10, 0.005));
        Assert.AreEqual(0, result.Points[0].KSeconds);
        Assert.AreEqual(0.10, result.Points[^1].KSeconds, 1e-12);
        Assert.AreEqual(0.04, result.Best.KSeconds, 0.01);
    }

    private static SimulationOptions Options(double dt, double k, DriveFlowPolicy policy = DriveFlowPolicy.ClampToZero) =>
        new(dt, Geometry, new(0.04, 1), new(k, policy));

    private static double EulerError(double dt)
    {
        var plant = new FirstOrderExtrusionPlant(new(0.04, 1));
        var state = new PlantState(0);
        for (var time = 0.0; time < 0.2 - 1e-12; time += dt) state = plant.Advance(state, 9, dt);
        var exact = 9 * (1 - Math.Exp(-0.2 / 0.04));
        return Math.Abs(state.NozzlePressure - exact);
    }

    private static SimulationResult SyntheticResult(double[] errors)
    {
        var profile = new MotionProfileBuilder("synthetic", "metrics")
            .Add(new ConstantVelocitySegment(1, errors.Length - 1)).Build();
        var samples = errors.Select((error, index) => new SimulationSample(index, index, 1, 0, 0, 0, 0, 0, 0,
            false, error, 0, error, error)).ToArray();
        return new("synthetic", Options(1, 0), profile, samples);
    }
}
