using System.Globalization;
using System.Xml.Linq;
using PressureAdvance.Core;
using PressureAdvance.Reporting;

namespace PressureAdvance.Reporting.Tests;

[TestClass]
public sealed class ReportingAcceptanceTests
{
    [TestMethod]
    public void CsvIsInvariantCompleteAndFinite()
    {
        var result = Result();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            var csv = ReportWriter.Csv(result.Samples);
            Assert.IsTrue(csv.StartsWith("time_s,distance_mm", StringComparison.Ordinal));
            Assert.AreEqual(result.Samples.Count + 1, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.IsFalse(csv.Contains("NaN", StringComparison.Ordinal));
            Assert.IsFalse(csv.Contains("Infinity", StringComparison.Ordinal));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public void SvgIsDeterministicXmlAndHandlesConstantRangesAndEscaping()
    {
        var panels = new[] { new Panel("Error < signed", "mm³/s", new Series("A & B", "#000", [new(0, 0), new(1, 0)])) };
        var first = SvgChart.Render("Title < test", "Distance (mm)", panels, [new(0.5, "x & y")]);
        var second = SvgChart.Render("Title < test", "Distance (mm)", panels, [new(0.5, "x & y")]);
        Assert.AreEqual(first, second);
        var document = XDocument.Parse(first);
        Assert.AreEqual("svg", document.Root?.Name.LocalName);
        Assert.IsTrue(first.Contains("<title>", StringComparison.Ordinal));
        Assert.IsTrue(first.Contains("data-transition", StringComparison.Ordinal));
        Assert.IsFalse(first.Contains("NaN", StringComparison.Ordinal));
        Assert.IsFalse(first.Contains("Infinity", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RunWriterProducesAllRequiredParseableArtifacts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pressure-advance-reporting-acceptance");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        var result = Result();
        ReportWriter.WriteRun(directory, result, RunMetricsCalculator.Calculate(result));
        foreach (var name in new[] { "run.json", "metrics.json", "samples.csv", "speed.svg", "flow.svg", "pressure.svg", "flow-error.svg" })
            Assert.IsTrue(File.Exists(Path.Combine(directory, name)), name);
        foreach (var name in new[] { "speed.svg", "flow.svg", "pressure.svg", "flow-error.svg" })
            _ = XDocument.Load(Path.Combine(directory, name));
        using var run = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "run.json")));
        Assert.AreEqual("corner", run.RootElement.GetProperty("scenario").GetString());
        Directory.Delete(directory, true);
    }

    private static SimulationResult Result()
    {
        var options = new SimulationOptions(0.001, new(0.2, 0.45), new(0.04, 1), new(0.04));
        return new SimulationEngine().Run(BuiltInScenarios.Get("corner"), options);
    }
}
