using System.Text.RegularExpressions;
using Aibysitter.Rules.Tests.Parity;

namespace Aibysitter.Rules.Tests.Action;

public class ActionMetadataTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(NodeRunner.RepoRoot, "action.yml"));

    private static readonly string[] MarketplaceColors = ["white", "black", "yellow", "blue", "green", "orange", "red", "purple", "gray-dark"];

    private static string? TopLevel(string key) =>
        Lines.FirstOrDefault(l => l.StartsWith(key + ":", StringComparison.Ordinal))?[(key.Length + 1)..].Trim();

    private static string? Branding(string key)
    {
        var start = Array.IndexOf(Lines, "branding:");
        return start < 0
            ? null
            : Lines.Skip(start + 1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal))
                .FirstOrDefault(l => l.TrimStart().StartsWith(key + ":", StringComparison.Ordinal))?.Split(':', 2)[1].Trim();
    }

    [Theory]
    [InlineData("name", "Aibysitter rules lint")]
    [InlineData("author", "Jon Bailey")]
    public void TopLevel_Value(string key, string expected) => Assert.Equal(expected, TopLevel(key));

    [Fact]
    public void Description_IsPresent() => Assert.False(string.IsNullOrWhiteSpace(TopLevel("description")));

    [Fact]
    public void Description_UnderMarketplaceLimit() => Assert.InRange(TopLevel("description")!.Length, 1, 124);

    private static string Text => string.Join("\n", Lines);

    private static string ActionTest => File.ReadAllText(Path.Combine(NodeRunner.RepoRoot, ".github", "workflows", "action-test.yml")).ReplaceLineEndings("\n");

    /// <summary>Each "uses: ./" step's body: from the uses line to the next step or job.</summary>
    private static List<string> SelfTestSteps(string workflow) =>
        Regex.Matches(workflow, @"        uses: \./\n(?<body>(?:          .*\n|        with:\n)*)").Select(m => m.Groups["body"].Value).ToList();

    [Fact]
    public void CliVersion_DefaultIsCliProjectVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(NodeRunner.RepoRoot, "src", "Aibysitter.Cli", "Aibysitter.Cli.csproj"));
        var version = Regex.Match(csproj, "<Version>(?<v>[^<]+)</Version>").Groups["v"].Value;

        Assert.Matches(@"\n  cli-version:\n    description: .+\n    required: false\n    default: """ + Regex.Escape(version) + @"""\n", Text);
    }

    [Fact]
    public void Steps_InstallFromNuGet_OrBuildFromSource_ByCliVersion()
    {
        Assert.Contains("      if: ${{ inputs.cli-version != '' }}\n      shell: bash\n      working-directory: ${{ runner.temp }}\n", Text, StringComparison.Ordinal);
        Assert.Contains("dotnet tool update Aibysitter.Cli --version \"$CLI_VERSION\" --tool-path \"$RUNNER_TEMP/aibysitter-tool\"", Text, StringComparison.Ordinal);
        Assert.Contains("      if: ${{ inputs.cli-version == '' }}\n      shell: bash\n      run: dotnet build ", Text, StringComparison.Ordinal);
        Assert.Contains("        AIBYSITTER_CLI: ${{ steps.cli.outputs.command }}\n", Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LocateStep_ExecutableNamePerRunnerOs()
    {
        Assert.Contains("temp=$(cygpath -u \"$RUNNER_TEMP\")", Text, StringComparison.Ordinal);
        Assert.Contains("elif [ \"$RUNNER_OS\" = Windows ]; then command=\"$temp/aibysitter-tool/aibysitter.exe\"", Text, StringComparison.Ordinal);
        Assert.Contains("else command=\"$temp/aibysitter-tool/aibysitter\"", Text, StringComparison.Ordinal);
        Assert.Contains("then command=\"dotnet $temp/aibysitter-cli/Aibysitter.Cli.dll\"", Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionTest_SourceBuildEverywhere_ExceptNuGetJob()
    {
        var workflow = ActionTest;
        var nugetStart = workflow.IndexOf("\n  nuget:\n", StringComparison.Ordinal);
        Assert.True(nugetStart > 0);

        var before = SelfTestSteps(workflow[..nugetStart]);
        var nuget = SelfTestSteps(workflow[nugetStart..]);

        Assert.Equal(6, before.Count);
        Assert.All(before, body => Assert.Contains("          cli-version: \"\"\n", body, StringComparison.Ordinal));
        Assert.Single(nuget);
        Assert.DoesNotContain("cli-version", nuget[0], StringComparison.Ordinal);
        Assert.Contains("os: [ubuntu-latest, windows-latest, macos-latest]", workflow[nugetStart..], StringComparison.Ordinal);
    }

    [Fact]
    public void Branding_IconAndColor()
    {
        Assert.Equal("check-square", Branding("icon"));
        Assert.Contains(Branding("color"), MarketplaceColors);
    }
}
