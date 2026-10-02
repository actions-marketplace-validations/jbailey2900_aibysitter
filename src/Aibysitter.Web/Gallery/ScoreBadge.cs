using System.Globalization;
using System.Security;
using Aibysitter.Rules;

namespace Aibysitter.Web.Gallery;

/// <summary>Two-part SVG badge: "aibysitter" | "A 100", right side colored by grade. Monospace metrics, no external fonts.</summary>
public static class ScoreBadge
{
    private const string Label = "aibysitter";

    /// <summary>Accessible name and tooltip; the visible label stays short for 20 px badges.</summary>
    private const string Title = "Aibysitter lint score";
    private const double CharWidth = 6.6;
    private const int Padding = 6;
    private const int Height = 20;

    public static string Render(LintScore score)
    {
        var value = $"{score.Grade} {score.Value}";
        var left = Width(Label);
        var right = Width(value);
        var total = left + right;
        var color = score.Grade switch
        {
            "A" or "B" => "#2e7d32",
            "C" or "D" => "#9a6400",
            _ => "#b3261e",
        };

        return string.Create(CultureInfo.InvariantCulture, $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{total}" height="{Height}" role="img" aria-label="{Title}: {Escape(value)}">
              <title>{Title}: {Escape(value)}</title>
              <rect width="{left}" height="{Height}" fill="#1d1c1a"/>
              <rect x="{left}" width="{right}" height="{Height}" fill="{color}"/>
              <g fill="#fbfaf8" font-family="'JetBrains Mono','DejaVu Sans Mono',Menlo,Consolas,monospace" font-size="11">
                <text x="{Padding}" y="14">{Label}</text>
                <text x="{left + Padding}" y="14" font-weight="700">{Escape(value)}</text>
              </g>
            </svg>
            """);
    }

    private static int Width(string text) => (int)Math.Ceiling(text.Length * CharWidth) + (2 * Padding);

    private static string Escape(string text) => SecurityElement.Escape(text);
}
