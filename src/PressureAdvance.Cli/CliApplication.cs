using System.Globalization;
using System.Text.Json;
using PressureAdvance.Core;
using PressureAdvance.Reporting;

namespace PressureAdvance.Cli;

public static class CliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help") { PrintHelp(); return 0; }
        try
        {
            var command = args[0].ToLowerInvariant();
            if (command == "list-scenarios") { ListScenarios(); return 0; }
            var config = ResolvedConfiguration.Load(Arguments.Parse(args.Skip(1).ToArray()));
            return command switch { "simulate" => Simulate(config), "compare" => Compare(config), "sweep" => Sweep(config), _ => Invalid($"Unknown command '{args[0]}'. Run with --help for usage.") };
        }
        catch (ArgumentException ex) { Console.Error.WriteLine($"Configuration error: {ex.Message}"); return 2; }
        catch (JsonException ex) { Console.Error.WriteLine($"Configuration error: invalid JSON: {ex.Message}"); return 2; }
        catch (IOException ex) { Console.Error.WriteLine($"Reporting error: {ex.Message}"); return 3; }
        catch (UnauthorizedAccessException ex) { Console.Error.WriteLine($"Reporting error: {ex.Message}"); return 3; }
        catch (Exception ex) { Console.Error.WriteLine($"Simulation error: {ex.Message}"); return 4; }
    }

    private static int Simulate(ResolvedConfiguration c)
    {
        var result = c.Run(c.KSeconds); var metrics = RunMetricsCalculator.Calculate(result, c.Settling);
        var output = c.OutputDirectory ?? Path.Combine("artifacts", $"{c.Scenario}-run");
        ReportWriter.WriteRun(output, result, metrics); PrintMetrics($"Simulation: {c.Scenario}, K={F(c.KSeconds)} s", metrics);
        Console.WriteLine($"Artifacts: {Path.GetFullPath(output)}"); Warn(c); return 0;
    }

    private static int Compare(ResolvedConfiguration c)
    {
        var baseline = c.Run(0); var selected = c.Run(c.KSeconds);
        var baselineMetrics = RunMetricsCalculator.Calculate(baseline, c.Settling); var selectedMetrics = RunMetricsCalculator.Calculate(selected, c.Settling);
        var output = c.OutputDirectory ?? Path.Combine("artifacts", $"{c.Scenario}-compare");
        ReportWriter.WriteComparison(output, baseline, baselineMetrics, selected, selectedMetrics);
        PrintMetrics("Baseline: K=0 s", baselineMetrics); PrintMetrics($"Selected: K={F(c.KSeconds)} s", selectedMetrics);
        if (baselineMetrics.IntegratedAbsoluteFlowErrorMm3 != 0) Console.WriteLine($"IAFE reduction: {F(100 * (1 - selectedMetrics.IntegratedAbsoluteFlowErrorMm3 / baselineMetrics.IntegratedAbsoluteFlowErrorMm3))}%");
        Console.WriteLine($"Artifacts: {Path.GetFullPath(output)}"); Warn(c); return 0;
    }

    private static int Sweep(ResolvedConfiguration c)
    {
        var sweep = KSweepRunner.Run(BuiltInScenarios.Get(c.Scenario), c.Options(c.KSeconds), new(c.KStartSeconds, c.KEndSeconds, c.KStepSeconds), c.Settling);
        var output = c.OutputDirectory ?? Path.Combine("artifacts", $"{c.Scenario}-sweep"); ReportWriter.WriteSweep(output, sweep, c.Plant);
        Console.WriteLine($"Sweep: {c.Scenario}, {sweep.Points.Count} points"); Console.WriteLine($"Best K: {F(sweep.Best.KSeconds)} s; IAFE: {F(sweep.Best.Metrics.IntegratedAbsoluteFlowErrorMm3)} mm³");
        Console.WriteLine($"Artifacts: {Path.GetFullPath(output)}"); Warn(c); return 0;
    }

    private static void ListScenarios() { foreach (var s in BuiltInScenarios.List()) Console.WriteLine($"{s.Name,-14} {s.Description}"); }
    private static void PrintMetrics(string heading, RunMetrics m)
    {
        Console.WriteLine(heading); Console.WriteLine($"  IAFE: {F(m.IntegratedAbsoluteFlowErrorMm3)} mm³");
        Console.WriteLine($"  peak under/over: {F(m.PeakUnderFlowMm3PerSecond)} / {F(m.PeakOverFlowMm3PerSecond)} mm³/s");
        Console.WriteLine($"  RMSE: {F(m.RmseMm3PerSecond)} mm³/s; clamps: {m.ClampCount}");
    }
    private static void Warn(ResolvedConfiguration c) { if (c.Options(c.KSeconds).HasTimeStepQualityWarning) Console.Error.WriteLine("Warning: dt is greater than tau/10; explicit Euler accuracy may be poor."); }
    private static int Invalid(string message) { Console.Error.WriteLine(message); return 2; }
    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static void PrintHelp() => Console.WriteLine("""
Pressure Advance Extrusion Simulator (.NET 10)

Usage:
  PressureAdvance.Cli simulate [options]
  PressureAdvance.Cli compare [options]
  PressureAdvance.Cli sweep [options]
  PressureAdvance.Cli list-scenarios

Options:
  --scenario NAME          Built-in scenario (default: corner)
  --config PATH            JSON configuration file
  --output PATH            Artifact output directory
  --k SECONDS              Pressure-advance coefficient (default: 0.04 s)
  --tau SECONDS            Plant time constant (default: 0.04 s)
  --gain VALUE             Plant pressure gain (default: 1)
  --dt SECONDS             Fixed Euler step (default: 0.001 s)
  --layer-height MM        Positive layer height (default: 0.20 mm)
  --line-width MM          Positive extrusion width (default: 0.45 mm)
  --drive-policy POLICY    clamp or allow-negative (default: clamp)
  --k-start SECONDS        Sweep start (default: 0)
  --k-end SECONDS          Sweep end (default: 0.10)
  --k-step SECONDS         Sweep step (default: 0.005)

Configuration precedence: CLI override -> JSON file -> built-in defaults.
This educational first-order model is not printer calibration or control software.
""");
}

internal sealed record ResolvedConfiguration(string Scenario, string? OutputDirectory, double KSeconds, double TimeStepSeconds,
    ExtrusionGeometry Geometry, PlantParameters Plant, DriveFlowPolicy DrivePolicy, double? InitialPressure,
    SettlingOptions Settling, double KStartSeconds, double KEndSeconds, double KStepSeconds)
{
    public SimulationOptions Options(double k) => new(TimeStepSeconds, Geometry, Plant, new(k, DrivePolicy), InitialPressure);
    public SimulationResult Run(double k) => new SimulationEngine().Run(BuiltInScenarios.Get(Scenario), Options(k));

    public static ResolvedConfiguration Load(Arguments args)
    {
        ConfigDto file = new();
        if (args.Get("config") is { } path) file = JsonSerializer.Deserialize<ConfigDto>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Configuration file is empty.");
        var scenario = args.Get("scenario") ?? file.Scenario ?? "corner";
        var policyText = args.Get("drive-policy") ?? file.PressureAdvance?.DriveFlowPolicy ?? "ClampToZero";
        var policy = policyText.ToLowerInvariant() switch { "clamp" or "clamptozero" => DriveFlowPolicy.ClampToZero, "allow-negative" or "allownegative" => DriveFlowPolicy.AllowNegative, _ => throw new ArgumentException($"Invalid drive policy '{policyText}'. Use clamp or allow-negative.") };
        double? pressure = file.Simulation?.InitialPressure.ValueKind switch
        {
            JsonValueKind.Number => file.Simulation.InitialPressure.GetDouble(),
            JsonValueKind.String when !string.Equals(file.Simulation.InitialPressure.GetString(), "steady-state", StringComparison.OrdinalIgnoreCase) => throw new ArgumentException("initialPressure string must be 'steady-state'."),
            _ => null,
        };
        _ = BuiltInScenarios.Get(scenario);
        return new(scenario, args.Get("output"), args.Double("k") ?? file.PressureAdvance?.KSeconds ?? 0.04,
            args.Double("dt") ?? file.Simulation?.TimeStepSeconds ?? 0.001,
            new(args.Double("layer-height") ?? file.Geometry?.LayerHeightMm ?? 0.20, args.Double("line-width") ?? file.Geometry?.ExtrusionWidthMm ?? 0.45),
            new(args.Double("tau") ?? file.Plant?.TimeConstantSeconds ?? 0.04, args.Double("gain") ?? file.Plant?.PressureGain ?? 1), policy, pressure,
            new(file.Settling?.RelativeTolerance ?? 0.02, file.Settling?.AbsoluteToleranceFloorMm3PerSecond ?? 0.02, file.Settling?.HoldSeconds ?? 0.05),
            args.Double("k-start") ?? file.Sweep?.StartKSeconds ?? 0, args.Double("k-end") ?? file.Sweep?.EndKSeconds ?? 0.10,
            args.Double("k-step") ?? file.Sweep?.StepKSeconds ?? 0.005);
    }
}

internal sealed class Arguments
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    public static Arguments Parse(string[] args)
    {
        var parsed = new Arguments();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            var name = args[i][2..]; if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Option --{name} requires a value.");
            if (!parsed.values.TryAdd(name, args[++i])) throw new ArgumentException($"Option --{name} was specified more than once.");
        }
        return parsed;
    }
    public string? Get(string name) => values.GetValueOrDefault(name);
    public double? Double(string name)
    {
        var value = Get(name); if (value is null) return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed)) throw new ArgumentException($"Invalid value for --{name}: '{value}'. A finite invariant-culture number is required.");
        return parsed;
    }
}

internal sealed class ConfigDto { public string? Scenario { get; set; } public GeometryDto? Geometry { get; set; } public PlantDto? Plant { get; set; } public SimulationDto? Simulation { get; set; } public PressureAdvanceDto? PressureAdvance { get; set; } public SettlingDto? Settling { get; set; } public SweepDto? Sweep { get; set; } }
internal sealed class GeometryDto { public double? LayerHeightMm { get; set; } public double? ExtrusionWidthMm { get; set; } }
internal sealed class PlantDto { public double? TimeConstantSeconds { get; set; } public double? PressureGain { get; set; } }
internal sealed class SimulationDto { public double? TimeStepSeconds { get; set; } public JsonElement InitialPressure { get; set; } }
internal sealed class PressureAdvanceDto { public double? KSeconds { get; set; } public string? DriveFlowPolicy { get; set; } }
internal sealed class SettlingDto { public double? RelativeTolerance { get; set; } public double? AbsoluteToleranceFloorMm3PerSecond { get; set; } public double? HoldSeconds { get; set; } }
internal sealed class SweepDto { public double? StartKSeconds { get; set; } public double? EndKSeconds { get; set; } public double? StepKSeconds { get; set; } }
