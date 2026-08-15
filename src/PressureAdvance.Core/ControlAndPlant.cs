namespace PressureAdvance.Core;

public static class ExtrusionDemandCalculator
{
    public static ExtrusionDemand Calculate(MotionState motion, ExtrusionGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return new(motion.VelocityMmPerSecond * geometry.AreaMm2,
            motion.AccelerationMmPerSecondSquared * geometry.AreaMm2);
    }
}

public interface IExtrusionFeedForward { DriveCommand Calculate(double requestedFlow, double requestedFlowDerivative); }

public sealed class NoCompensationFeedForward : IExtrusionFeedForward
{
    public DriveCommand Calculate(double requestedFlow, double requestedFlowDerivative)
    {
        Validation.Finite(requestedFlow, nameof(requestedFlow));
        Validation.Finite(requestedFlowDerivative, nameof(requestedFlowDerivative));
        return new(requestedFlow, 0, requestedFlow, requestedFlow, false);
    }
}

public sealed class PressureAdvanceFeedForward(PressureAdvanceParameters parameters) : IExtrusionFeedForward
{
    private readonly PressureAdvanceParameters parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public DriveCommand Calculate(double requestedFlow, double requestedFlowDerivative)
    {
        Validation.Finite(requestedFlow, nameof(requestedFlow));
        Validation.Finite(requestedFlowDerivative, nameof(requestedFlowDerivative));
        var advance = parameters.KSeconds * requestedFlowDerivative;
        var raw = requestedFlow + advance;
        var clamped = parameters.DriveFlowPolicy == DriveFlowPolicy.ClampToZero && raw < 0;
        return new(requestedFlow, advance, raw, clamped ? 0 : raw, clamped);
    }
}

public interface IExtrusionPlant
{
    PlantParameters Parameters { get; }
    double ActualFlow(PlantState state);
    PlantState Advance(PlantState state, double driveFlow, double timeStepSeconds);
}

public sealed class FirstOrderExtrusionPlant(PlantParameters parameters) : IExtrusionPlant
{
    public PlantParameters Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));
    public double ActualFlow(PlantState state) => state.NozzlePressure / Parameters.PressureGain;

    public PlantState Advance(PlantState state, double driveFlow, double timeStepSeconds)
    {
        Validation.Finite(state.NozzlePressure, nameof(state));
        Validation.Finite(driveFlow, nameof(driveFlow));
        Validation.Positive(timeStepSeconds, nameof(timeStepSeconds));
        var derivative = (Parameters.PressureGain * driveFlow - state.NozzlePressure) / Parameters.TimeConstantSeconds;
        return new(state.NozzlePressure + timeStepSeconds * derivative);
    }
}
