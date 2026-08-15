using System.Text.Json;
using PressureAdvance.Cli;

namespace PressureAdvance.Reporting.Tests;

[TestClass]
public sealed class CliAcceptanceTests
{
    [TestMethod]
    public void HelpListAndInvalidConfigurationReturnExpectedCodes()
    {
        Assert.AreEqual(0, CliApplication.Run([]));
        Assert.AreEqual(0, CliApplication.Run(["list-scenarios"]));
        Assert.AreEqual(2, CliApplication.Run(["simulate", "--tau", "0"]));
    }

    [TestMethod]
    public void CliOverridesJsonAndSimulateCreatesArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "pressure-advance-cli-acceptance");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        var config = Path.Combine(root, "config.json");
        var output = Path.Combine(root, "run");
        File.WriteAllText(config, """
        {
          "scenario": "acceleration",
          "plant": { "timeConstantSeconds": 0.08 },
          "pressureAdvance": { "kSeconds": 0.01 }
        }
        """);
        Assert.AreEqual(0, CliApplication.Run(["simulate", "--config", config, "--k", "0.03", "--output", output]));
        foreach (var name in new[] { "run.json", "metrics.json", "samples.csv", "speed.svg", "flow.svg", "pressure.svg", "flow-error.svg" })
            Assert.IsTrue(File.Exists(Path.Combine(output, name)), name);
        using var run = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "run.json")));
        Assert.AreEqual(0.03, run.RootElement.GetProperty("pressureAdvance").GetProperty("kSeconds").GetDouble(), 1e-12);
        Assert.AreEqual(0.08, run.RootElement.GetProperty("plant").GetProperty("timeConstantSeconds").GetDouble(), 1e-12);
        Directory.Delete(root, true);
    }
}
