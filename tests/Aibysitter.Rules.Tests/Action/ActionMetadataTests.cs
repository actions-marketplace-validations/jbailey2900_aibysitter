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

    [Fact]
    public void Branding_IconAndColor()
    {
        Assert.Equal("check-square", Branding("icon"));
        Assert.Contains(Branding("color"), MarketplaceColors);
    }
}
