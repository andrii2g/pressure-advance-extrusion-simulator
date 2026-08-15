using System.Globalization;
using System.Security;
using System.Text;

namespace PressureAdvance.Reporting;

public static class SvgChart
{
    public static string Render(string title, string xLabel, IReadOnlyList<Panel> panels, IReadOnlyList<Marker>? markers = null)
    {
        if (panels.Count == 0) throw new ArgumentException("At least one panel is required.", nameof(panels));
        const double width = 1000; const double left = 85; const double right = 30; const double top = 60; const double bottom = 60;
        var panelHeight = 300.0; var gap = 35.0; var height = top + bottom + panels.Count * panelHeight + (panels.Count - 1) * gap;
        var all = panels.SelectMany(x => x.Series).SelectMany(x => x.Points).ToArray();
        if (all.Length == 0) throw new ArgumentException("At least one point is required.", nameof(panels));
        var (xMin, xMax) = Range(all.Select(x => x.X));
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(width).Append("\" height=\"").Append(F(height))
            .Append("\" viewBox=\"0 0 ").Append(width).Append(' ').Append(F(height)).Append("\">\n<title>").Append(E(title)).Append("</title>\n")
            .Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/><style>text{font-family:Segoe UI,Arial,sans-serif;fill:#111827}.grid{stroke:#e5e7eb;stroke-width:1}.axis{stroke:#374151;stroke-width:1}.marker{stroke:#94a3b8;stroke-dasharray:4 4}</style>\n")
            .Append("<text x=\"500\" y=\"30\" text-anchor=\"middle\" font-size=\"20\">").Append(E(title)).Append("</text>\n");
        for (var p = 0; p < panels.Count; p++)
        {
            var panel = panels[p]; var yTop = top + p * (panelHeight + gap); var yBottom = yTop + panelHeight;
            var values = panel.Series.SelectMany(x => x.Points).Select(x => x.Y).ToArray(); var (yMin, yMax) = Range(values, includeZero: values.Any(x => x <= 0) && values.Any(x => x >= 0));
            for (var tick = 0; tick <= 5; tick++)
            {
                var y = yBottom - panelHeight * tick / 5.0; var value = yMin + (yMax - yMin) * tick / 5.0;
                sb.Append("<line class=\"grid\" x1=\"").Append(left).Append("\" x2=\"").Append(width - right).Append("\" y1=\"").Append(F(y)).Append("\" y2=\"").Append(F(y)).Append("\"/>")
                    .Append("<text x=\"").Append(left - 8).Append("\" y=\"").Append(F(y + 4)).Append("\" text-anchor=\"end\" font-size=\"11\">").Append(Tick(value)).Append("</text>\n");
            }
            for (var tick = 0; tick <= 5; tick++)
            {
                var x = left + (width - left - right) * tick / 5.0; var value = xMin + (xMax - xMin) * tick / 5.0;
                sb.Append("<line class=\"grid\" x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(yTop)).Append("\" y2=\"").Append(F(yBottom)).Append("\"/>");
                if (p == panels.Count - 1) sb.Append("<text x=\"").Append(F(x)).Append("\" y=\"").Append(F(yBottom + 18)).Append("\" text-anchor=\"middle\" font-size=\"11\">").Append(Tick(value)).Append("</text>");
                sb.Append('\n');
            }
            sb.Append("<line class=\"axis\" x1=\"").Append(left).Append("\" x2=\"").Append(width - right).Append("\" y1=\"").Append(F(yBottom)).Append("\" y2=\"").Append(F(yBottom)).Append("\"/>")
                .Append("<line class=\"axis\" x1=\"").Append(left).Append("\" x2=\"").Append(left).Append("\" y1=\"").Append(F(yTop)).Append("\" y2=\"").Append(F(yBottom)).Append("\"/>")
                .Append("<text x=\"18\" y=\"").Append(F((yTop + yBottom) / 2)).Append("\" transform=\"rotate(-90 18 ").Append(F((yTop + yBottom) / 2)).Append(")\" text-anchor=\"middle\" font-size=\"13\">").Append(E(panel.Label)).Append(" (").Append(E(panel.Units)).Append(")</text>\n");
            if (yMin <= 0 && yMax >= 0)
            {
                var zero = Map(0, yMin, yMax, yBottom, yTop); sb.Append("<line x1=\"").Append(left).Append("\" x2=\"").Append(width - right).Append("\" y1=\"").Append(F(zero)).Append("\" y2=\"").Append(F(zero)).Append("\" stroke=\"#64748b\"/>\n");
            }
            foreach (var marker in markers ?? [])
            {
                var x = Map(marker.X, xMin, xMax, left, width - right); sb.Append("<line class=\"marker\" data-transition=\"").Append(E(marker.Label)).Append("\" x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(yTop)).Append("\" y2=\"").Append(F(yBottom)).Append("\"/>\n");
            }
            var legendX = left + 8;
            foreach (var series in panel.Series)
            {
                sb.Append("<polyline data-series=\"").Append(E(series.Name)).Append("\" fill=\"none\" stroke=\"").Append(E(series.Color)).Append("\" stroke-width=\"2\" points=\"");
                foreach (var point in series.Points) sb.Append(F(Map(point.X, xMin, xMax, left, width - right))).Append(',').Append(F(Map(point.Y, yMin, yMax, yBottom, yTop))).Append(' ');
                sb.Append("\"/><line x1=\"").Append(F(legendX)).Append("\" x2=\"").Append(F(legendX + 18)).Append("\" y1=\"").Append(F(yTop + 15)).Append("\" y2=\"").Append(F(yTop + 15)).Append("\" stroke=\"").Append(E(series.Color)).Append("\" stroke-width=\"3\"/>")
                    .Append("<text x=\"").Append(F(legendX + 23)).Append("\" y=\"").Append(F(yTop + 19)).Append("\" font-size=\"11\">").Append(E(series.Name)).Append("</text>\n");
                legendX += 150;
            }
        }
        sb.Append("<text x=\"500\" y=\"").Append(F(height - 15)).Append("\" text-anchor=\"middle\" font-size=\"13\">").Append(E(xLabel)).Append("</text>\n</svg>\n");
        var output = sb.ToString();
        if (output.Contains("NaN", StringComparison.Ordinal) || output.Contains("Infinity", StringComparison.Ordinal)) throw new InvalidOperationException("SVG contains invalid values.");
        return output;
    }

    private static (double Min, double Max) Range(IEnumerable<double> source, bool includeZero = false)
    {
        var values = source.ToArray(); if (values.Length == 0 || values.Any(x => !double.IsFinite(x))) throw new ArgumentException("Chart values must be finite and non-empty.");
        var min = values.Min(); var max = values.Max(); if (includeZero) { min = Math.Min(0, min); max = Math.Max(0, max); }
        if (min == max) { var padding = Math.Max(Math.Abs(min) * 0.05, 1); min -= padding; max += padding; }
        else { var padding = (max - min) * 0.05; min -= padding; max += padding; }
        return (min, max);
    }
    private static double Map(double value, double min, double max, double start, double end) => start + (value - min) / (max - min) * (end - start);
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Tick(double value) => Math.Abs(value) is >= 10000 or (< 0.001 and > 0) ? value.ToString("0.###E+0", CultureInfo.InvariantCulture) : value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
