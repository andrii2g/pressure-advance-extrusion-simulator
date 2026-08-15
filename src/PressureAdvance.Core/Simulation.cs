namespace PressureAdvance.Core;

public sealed class SimulationEngine
{
    public SimulationResult Run(MotionProfile profile, SimulationOptions options, IExtrusionFeedForward? feedForward = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        feedForward ??= new PressureAdvanceFeedForward(options.PressureAdvance);
        var plant = new FirstOrderExtrusionPlant(options.Plant);
        var initialDemand = ExtrusionDemandCalculator.Calculate(profile.Evaluate(0), options.Geometry);
        var state = new PlantState(options.InitialPressure ?? options.Plant.PressureGain * initialDemand.RequestedFlow);
        var samples = new List<SimulationSample>();
        var time = 0.0;

        while (true)
        {
            var motion = profile.Evaluate(time);
            var demand = ExtrusionDemandCalculator.Calculate(motion, options.Geometry);
            var command = feedForward.Calculate(demand.RequestedFlow, demand.RequestedFlowDerivative);
            var actual = plant.ActualFlow(state);
            samples.Add(new(time, motion.DistanceMm, motion.VelocityMmPerSecond, motion.AccelerationMmPerSecondSquared,
                demand.RequestedFlow, demand.RequestedFlowDerivative, command.AdvanceFlow, command.RawDriveFlow,
                command.DriveFlow, command.WasClamped, state.NozzlePressure,
                options.Plant.PressureGain * demand.RequestedFlow, actual, actual - demand.RequestedFlow));

            if (time >= profile.DurationSeconds) break;
            var remaining = profile.DurationSeconds - time;
            var step = Math.Min(options.TimeStepSeconds, remaining);
            state = plant.Advance(state, command.DriveFlow, step);
            time = step >= remaining ? profile.DurationSeconds : time + step;
        }

        return new(profile.Name, options, profile, samples.AsReadOnly());
    }
}
