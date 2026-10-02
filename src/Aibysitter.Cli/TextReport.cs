using System.Text;
using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <summary>Plain text: a header line, one line per finding with its fix indented below, then suppressed findings.</summary>
internal static class TextReport
{
    public static string Write(string label, LintReport report)
    {
        var text = new StringBuilder();
        var format = RulesFormats.DisplayName(Enum.Parse<RulesFormat>(report.DetectedFormat));
        text.Append($"{label}: {report.Score}/100 {report.Grade} ({format}, ruleset v{report.RulesetVersion})\n");
        if (report.Disabled.Count > 0)
        {
            text.Append($"Rules off: {string.Join(", ", report.Disabled)}.\n");
        }

        var all = report.Findings.Concat(report.Suppressed).ToList();
        var locationWidth = all.Count == 0 ? 0 : all.Max(f => Location(label, f).Length);
        var ruleWidth = all.Count == 0 ? 0 : all.Max(f => RuleLabel(f).Length);

        if (report.Findings.Count == 0)
        {
            text.Append("No findings.\n");
        }

        foreach (var f in report.Findings)
        {
            text.Append($"{Location(label, f).PadRight(locationWidth)}  {f.Severity,-7}  {RuleLabel(f).PadRight(ruleWidth)}  {f.Message}\n");
            text.Append($"    Fix: {f.FixHint}\n");
        }

        if (report.Suppressed.Count > 0)
        {
            text.Append($"Suppressed ({report.Suppressed.Count}), not scored:\n");
            foreach (var f in report.Suppressed)
            {
                text.Append($"{Location(label, f).PadRight(locationWidth)}  {f.Severity,-7}  {RuleLabel(f).PadRight(ruleWidth)}  {f.Message}\n");
            }
        }

        return text.ToString();
    }

    private static string Location(string label, ReportFinding f) => $"{label}:{f.Line}";

    private static string RuleLabel(ReportFinding f) => $"{f.Rule} {RuleDocs.Find(f.Rule)?.Name}";
}
