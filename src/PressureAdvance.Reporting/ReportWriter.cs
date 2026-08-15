using System.Globalization;
using System.Text;
using System.Text.Json;
using PressureAdvance.Core;

namespace PressureAdvance.Reporting;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void WriteRun(string outputDirectory, SimulationResult result, RunMetrics metrics)
    {
        Directory.CreateDirectory(outputDirectory);
        WriteText(Path.Combine(outputDirectory, "samples.csv"), Csv(result.Samples));
        WriteJson(Path.Combine(outputDirectory, "metrics.json"), metrics);
        WriteJson(Path.Combine(outputDirectory, "run.json"), RunContract(result));
        var markers = result.Profile.Transitions.Select(x => new Marker(x.DistanceMm, x.Name)).ToArray();
        WriteText(Path.Combine(outputDirectory, "speed.svg"), SvgChart.Render("Motion profile", "Distance (mm)",
            [new("Velocity", "mm/s", new Series("Velocity", "#2563eb", result.Samples.Select(x => new Point(x.DistanceMm, x.VelocityMmPerSecond)).ToArray())),
             new("Acceleration", "mm/s²", new Series("Acceleration", "#dc2626", result.Samples.Select(x => new Point(x.DistanceMm, x.AccelerationMmPerSecondSquared)).ToArray()))], markers));
        WriteText(Path.Combine(outputDirectory, "flow.svg"), SvgChart.Render("Flow", "Distance (mm)",
            [new("Flow", "mm³/s",
                new Series("Requested", "#2563eb", result.Samples.Select(x => new Point(x.DistanceMm, x.RequestedFlowMm3PerSecond)).ToArray()),
                new Series("Raw drive", "#f59e0b", result.Samples.Select(x => new Point(x.DistanceMm, x.RawDriveFlowMm3PerSecond)).ToArray()),
                new Series("Drive", "#dc2626", result.Samples.Select(x => new Point(x.DistanceMm, x.DriveFlowMm3PerSecond)).ToArray()),
                new Series("Actual", "#16a34a", result.Samples.Select(x => new Point(x.DistanceMm, x.ActualFlowMm3PerSecond)).ToArray()))], markers));
        WriteText(Path.Combine(outputDirectory, "pressure.svg"), SvgChart.Render("Nozzle pressure", "Distance (mm)",
            [new("Pressure", "model pressure units",
                new Series("Nozzle pressure", "#7c3aed", result.Samples.Select(x => new Point(x.DistanceMm, x.NozzlePressure)).ToArray()),
                new Series("Requested equilibrium", "#0891b2", result.Samples.Select(x => new Point(x.DistanceMm, x.EquilibriumPressure)).ToArray()))], markers));
        WriteText(Path.Combine(outputDirectory, "flow-error.svg"), SvgChart.Render("Signed flow error (negative = under-flow; positive = over-flow)", "Distance (mm)",
            [new("Actual - requested", "mm³/s", new Series("Flow error", "#be123c",
                result.Samples.Select(x => new Point(x.DistanceMm, x.FlowErrorMm3PerSecond)).ToArray()))], markers));
    }

    public static void WriteComparison(string outputDirectory, SimulationResult baseline, RunMetrics baselineMetrics,
        SimulationResult selected, RunMetrics selectedMetrics)
    {
        Directory.CreateDirectory(outputDirectory);
        WriteRun(Path.Combine(outputDirectory, "no-pa"), baseline, baselineMetrics);
        WriteRun(Path.Combine(outputDirectory, "selected-k"), selected, selectedMetrics);
        WriteRun(outputDirectory, selected, selectedMetrics);
        var markers = selected.Profile.Transitions.Select(x => new Marker(x.DistanceMm, x.Name)).ToArray();
        var label = selected.Options.PressureAdvance.KSeconds.ToString("0.######", CultureInfo.InvariantCulture);
        WriteText(Path.Combine(outputDirectory, "comparison.svg"), SvgChart.Render("Pressure advance comparison", "Distance (mm)",
            [new("Flow", "mm³/s",
                new Series("Requested", "#2563eb", selected.Samples.Select(x => new Point(x.DistanceMm, x.RequestedFlowMm3PerSecond)).ToArray()),
                new Series("Actual K=0 s", "#dc2626", baseline.Samples.Select(x => new Point(x.DistanceMm, x.ActualFlowMm3PerSecond)).ToArray()),
                new Series($"Actual K={label} s", "#16a34a", selected.Samples.Select(x => new Point(x.DistanceMm, x.ActualFlowMm3PerSecond)).ToArray()))], markers));
    }

    public static void WriteSweep(string outputDirectory, KSweepResult sweep, PlantParameters plant)
    {
        Directory.CreateDirectory(outputDirectory);
        var csv = new StringBuilder("k_s,iafe_mm3,peak_under_mm3_s,peak_over_mm3_s,rmse_mm3_s,under_volume_mm3,over_volume_mm3,clamp_count,worst_settling_distance_mm\n");
        foreach (var point in sweep.Points)
        {
            var m = point.Metrics;
            csv.AppendJoin(',', F(point.KSeconds), F(m.IntegratedAbsoluteFlowErrorMm3), F(m.PeakUnderFlowMm3PerSecond),
                F(m.PeakOverFlowMm3PerSecond), F(m.RmseMm3PerSecond), F(m.UnderExtrusionVolumeMm3),
                F(m.OverExtrusionVolumeMm3), m.ClampCount.ToString(CultureInfo.InvariantCulture),
                m.WorstSettlingDistanceMm.HasValue ? F(m.WorstSettlingDistanceMm.Value) : string.Empty).Append('\n');
        }
        WriteText(Path.Combine(outputDirectory, "k-sweep.csv"), csv.ToString());
        WriteJson(Path.Combine(outputDirectory, "k-sweep.json"), new { bestKSeconds = sweep.Best.KSeconds, timeConstantSeconds = plant.TimeConstantSeconds, points = sweep.Points });
        WriteText(Path.Combine(outputDirectory, "k-sweep.svg"), SvgChart.Render("K sweep: integrated absolute flow error", "K (s)",
            [new("IAFE", "mm³", new Series("IAFE", "#2563eb", sweep.Points.Select(x => new Point(x.KSeconds, x.Metrics.IntegratedAbsoluteFlowErrorMm3)).ToArray()))],
            [new(sweep.Best.KSeconds, "best K"), new(plant.TimeConstantSeconds, "tau")]));
    }

    public static string Csv(IReadOnlyList<SimulationSample> samples)
    {
        var csv = new StringBuilder("time_s,distance_mm,velocity_mm_s,acceleration_mm_s2,requested_flow_mm3_s,requested_flow_derivative_mm3_s2,advance_flow_mm3_s,raw_drive_flow_mm3_s,drive_flow_mm3_s,drive_clamped,nozzle_pressure,equilibrium_pressure,actual_flow_mm3_s,flow_error_mm3_s\n");
        foreach (var x in samples)
        {
            csv.AppendJoin(',', F(x.TimeSeconds), F(x.DistanceMm), F(x.VelocityMmPerSecond), F(x.AccelerationMmPerSecondSquared),
                F(x.RequestedFlowMm3PerSecond), F(x.RequestedFlowDerivativeMm3PerSecondSquared), F(x.AdvanceFlowMm3PerSecond),
                F(x.RawDriveFlowMm3PerSecond), F(x.DriveFlowMm3PerSecond), x.DriveWasClamped ? "true" : "false",
                F(x.NozzlePressure), F(x.EquilibriumPressure), F(x.ActualFlowMm3PerSecond), F(x.FlowErrorMm3PerSecond)).Append('\n');
        }
        return csv.ToString();
    }

    private static object RunContract(SimulationResult result) => new
    {
        application = "pressure-advance-extrusion-simulator",
        model = "deterministic fixed-step explicit Euler first-order plant",
        scenario = result.ScenarioName,
        geometry = result.Options.Geometry,
        plant = result.Options.Plant,
        pressureAdvance = result.Options.PressureAdvance,
        timeStepSeconds = result.Options.TimeStepSeconds,
        initialPressure = result.Options.InitialPressure.HasValue ? new { mode = "explicit", value = result.Options.InitialPressure } : new { mode = "steady-state", value = (double?)null },
        totalDurationSeconds = result.Profile.DurationSeconds,
        totalDistanceMm = result.Profile.DistanceMm,
        transitions = result.Profile.Transitions,
        artifacts = new[] { "run.json", "metrics.json", "samples.csv", "speed.svg", "flow.svg", "pressure.svg", "flow-error.svg" },
    };

    private static string F(double value)
    {
        if (!double.IsFinite(value)) throw new InvalidOperationException("Report data contains a non-finite number.");
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }
    private static void WriteJson(string path, object value) => WriteText(path, JsonSerializer.Serialize(value, JsonOptions) + "\n");
    private static void WriteText(string path, string value) => File.WriteAllText(path, value, new UTF8Encoding(false));
}

public readonly record struct Point(double X, double Y);
public sealed record Series(string Name, string Color, IReadOnlyList<Point> Points);
public sealed record Panel(string Label, string Units, params Series[] Series);
public readonly record struct Marker(double X, string Label);
