using System.Text.RegularExpressions;
using Aibysitter.Rules.Browser;

namespace Aibysitter.Rules.Tests.Parity;

public class JsRegexTranslatorTests
{
    private const string Word = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";
    private const string Boundary = Word + @"\u200C\u200D";

    [Theory]
    [InlineData(@"\w+", "[" + Word + "]+")]
    [InlineData(@"[\w.-]", "[" + Word + ".-]")]
    [InlineData(@"\d{3}", @"\p{Nd}{3}")]
    [InlineData(@"a\sb", @"a[\f\n\r\t\v\x85\p{Z}]b")]
    [InlineData(@"\bx", "(?:(?<=[" + Boundary + "])(?![" + Boundary + "])|(?<![" + Boundary + "])(?=[" + Boundary + "]))x")]
    [InlineData("a.b", @"a[^\n]b")]
    [InlineData(@"a\.b", @"a\.b")]
    [InlineData("[.]", "[.]")]
    [InlineData(@"\-\:\#", "-:#")]
    [InlineData(@"[\-a]", @"[\-a]")]
    [InlineData("a{2,3}b{4}", "a{2,3}b{4}")]
    [InlineData("{x}", @"\{x\}")]
    [InlineData("[]a]", @"[\]a]")]
    [InlineData("[[a]", @"[\[a]")]
    [InlineData(@"(?<v>a)\k<v>", @"(?<v>a)\k<v>")]
    public void Translate_Source(string pattern, string expected)
    {
        Assert.Equal(expected, JsRegexTranslator.Translate(pattern, RegexOptions.None).Source);
    }

    [Theory]
    [InlineData(RegexOptions.None, "u")]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "iu")]
    [InlineData(RegexOptions.Multiline | RegexOptions.Singleline, "msu")]
    public void Translate_Flags(RegexOptions options, string expected)
    {
        Assert.Equal(expected, JsRegexTranslator.Translate("a", options).Flags);
    }

    [Fact]
    public void Singleline_KeepsDot()
    {
        Assert.Equal("a.b", JsRegexTranslator.Translate("a.b", RegexOptions.Singleline).Source);
    }

    [Theory]
    [InlineData("(?>a)")]
    [InlineData("(?i)a")]
    [InlineData("(?i:a)")]
    [InlineData(@"\Aa")]
    [InlineData(@"a\z")]
    [InlineData(@"a\Z")]
    [InlineData(@"\Ga")]
    [InlineData(@"[\W]")]
    [InlineData(@"\q")]
    public void UnsupportedConstruct_Throws(string pattern)
    {
        Assert.Throws<NotSupportedException>(() => JsRegexTranslator.Translate(pattern, RegexOptions.None));
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.ExplicitCapture)]
    [InlineData(RegexOptions.IgnorePatternWhitespace)]
    [InlineData(RegexOptions.ECMAScript)]
    public void UnsupportedOption_Throws(RegexOptions options)
    {
        Assert.Throws<NotSupportedException>(() => JsRegexTranslator.Translate("a", options));
    }

    [Fact]
    public void Export_HasEveryLintRule_AndNotR006()
    {
        var js = PatternExport.Build();

        Assert.StartsWith("// Generated at build time", js, StringComparison.Ordinal);
        Assert.All(new LintEngine().Rules, r => Assert.Contains($"\"id\": \"{r.Id}\"", js, StringComparison.Ordinal));
        Assert.DoesNotContain("\"R006\"", js, StringComparison.Ordinal);
    }
}
