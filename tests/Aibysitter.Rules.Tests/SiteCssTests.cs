namespace Aibysitter.Rules.Tests;

/// <summary>Narrow-screen wrapping rules. Layout at 375 px is verified in session, not in CI.</summary>
public class SiteCssTests
{
    private static readonly string Css = File.ReadAllText(
        Path.Combine(Parity.NodeRunner.RepoRoot, "src", "Aibysitter.Web", "wwwroot", "css", "site.css")).ReplaceLineEndings("\n");

    [Fact]
    public void InlineCode_WrapsAnywhere_InMain() =>
        Assert.Contains("main :not(pre) > code { overflow-wrap: anywhere; }", Css);

    [Fact]
    public void PerPageInlineCodeRules_AreGone()
    {
        Assert.DoesNotContain(".pack-sections .rendered code", Css);
        Assert.DoesNotContain(".error-page code", Css);
    }

    [Fact]
    public void Headings_BreakLongWords()
    {
        var rule = Css[Css.IndexOf("h1, h2, h3 {", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}')];

        Assert.Contains("overflow-wrap: break-word;", rule);
    }
}
