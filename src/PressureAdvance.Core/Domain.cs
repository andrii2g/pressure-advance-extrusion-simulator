namespace PressureAdvance.Core;

/// <summary>Deposited bead dimensions in millimeters.</summary>
public sealed record ExtrusionGeometry
{
    public ExtrusionGeometry(double layerHeightMm, double extrusionWidthMm)
    {
        LayerHeightMm = Validation.Positive(layerHeightMm, nameof(layerHeightMm));
        ExtrusionWidthMm = Validation.Positive(extrusionWidthMm, nameof(extrusionWidthMm));
    }

    public double LayerHeightMm { get; }
    public double ExtrusionWidthMm { get; }
    /// <summary>Rectangular bead area in mm².</summary>
    public double AreaMm2 => LayerHeightMm * ExtrusionWidthMm;
}

/// <summary>First-order plant parameters: time constant in seconds and pressure gain per (mm³/s).</summary>
public sealed record PlantParameters
{
    public PlantParameters(double timeConstantSeconds, double pressureGain)
    {
        TimeConstantSeconds = Validation.Positive(timeConstantSeconds, nameof(timeConstantSeconds));
        PressureGain = Validation.Positive(pressureGain, nameof(pressureGain));
    }

    public double TimeConstantSeconds { get; }
    public double PressureGain { get; }
}

public enum DriveFlowPolicy { ClampToZero, AllowNegative }

/// <summary>Pressure-advance K in seconds and the post-command drive policy.</summary>
public sealed record PressureAdvanceParameters
{
    public PressureAdvanceParameters(double kSeconds, DriveFlowPolicy driveFlowPolicy = DriveFlowPolicy.ClampToZero)
    {
        KSeconds = Validation.NonNegative(kSeconds, nameof(kSeconds));
        DriveFlowPolicy = driveFlowPolicy;
    }

    public double KSeconds { get; }
    public DriveFlowPolicy DriveFlowPolicy { get; }
}

/// <summary>Fixed-step options. Null initial pressure means requested-flow steady state.</summary>
public sealed record SimulationOptions
{
    public SimulationOptions(double timeStepSeconds, ExtrusionGeometry geometry, PlantParameters plant,
        PressureAdvanceParameters pressureAdvance, double? initialPressure = null)
    {
        TimeStepSeconds = Validation.Positive(timeStepSeconds, nameof(timeStepSeconds));
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Plant = plant ?? throw new ArgumentNullException(nameof(plant));
        PressureAdvance = pressureAdvance ?? throw new ArgumentNullException(nameof(pressureAdvance));
        if (initialPressure.HasValue) Validation.Finite(initialPressure.Value, nameof(initialPressure));
        InitialPressure = initialPressure;
    }

    public double TimeStepSeconds { get; }
    public ExtrusionGeometry Geometry { get; }
    public PlantParameters Plant { get; }
    public PressureAdvanceParameters PressureAdvance { get; }
    public double? InitialPressure { get; }
    public bool HasTimeStepQualityWarning => TimeStepSeconds > Plant.TimeConstantSeconds / 10.0;
}

/// <summary>Plant pressure state in model pressure units.</summary>
public readonly record struct PlantState(double NozzlePressure);
/// <summary>Motion in seconds, mm, mm/s, and mm/s².</summary>
public readonly record struct MotionState(double TimeSeconds, double DistanceMm, double VelocityMmPerSecond, double AccelerationMmPerSecondSquared);
/// <summary>Demand in mm³/s and derivative in mm³/s².</summary>
public readonly record struct ExtrusionDemand(double RequestedFlow, double RequestedFlowDerivative);
public readonly record struct DriveCommand(double RequestedFlow, double AdvanceFlow, double RawDriveFlow, double DriveFlow, bool WasClamped);

public sealed record MotionTransition(int Index, string Name, string Kind, double TimeSeconds, double DistanceMm,
    double VelocityBeforeMmPerSecond, double VelocityAfterMmPerSecond);

/// <summary>Complete pre-integration state at one deterministic timestamp.</summary>
public sealed record SimulationSample(
    double TimeSeconds, double DistanceMm, double VelocityMmPerSecond, double AccelerationMmPerSecondSquared,
    double RequestedFlowMm3PerSecond, double RequestedFlowDerivativeMm3PerSecondSquared,
    double AdvanceFlowMm3PerSecond, double RawDriveFlowMm3PerSecond, double DriveFlowMm3PerSecond,
    bool DriveWasClamped, double NozzlePressure, double EquilibriumPressure,
    double ActualFlowMm3PerSecond, double FlowErrorMm3PerSecond);

public sealed record SimulationResult(string ScenarioName, SimulationOptions Options, MotionProfile Profile,
    IReadOnlyList<SimulationSample> Samples);
