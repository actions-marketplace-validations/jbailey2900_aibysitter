using System.Buffers.Binary;
using System.Xml.Linq;
using Aibysitter.Rules.Tests.Parity;

namespace Aibysitter.Rules.Tests.Cli;

public class CliPackageTests
{
    private static string CliDir => Path.Combine(NodeRunner.RepoRoot, "src", "Aibysitter.Cli");

    private static XDocument Project => XDocument.Load(Path.Combine(CliDir, "Aibysitter.Cli.csproj"));

    private static string? Property(string name) => Project.Descendants(name).SingleOrDefault()?.Value;

    [Theory]
    [InlineData("PackageId", "Aibysitter.Cli")]
    [InlineData("ToolCommandName", "aibysitter")]
    [InlineData("PackageLicenseExpression", "MIT")]
    [InlineData("PackageReadmeFile", "README.md")]
    [InlineData("PackageIcon", "icon.png")]
    [InlineData("RepositoryUrl", "https://github.com/jbailey2900/aibysitter")]
    [InlineData("RepositoryType", "git")]
    public void Project_HasPackageMetadata(string name, string expected) => Assert.Equal(expected, Property(name));

    [Theory]
    [InlineData("README.md")]
    [InlineData("icon.png")]
    public void PackedFile_IsIncludedAtPackageRoot(string file)
    {
        var item = Project.Descendants("None").SingleOrDefault(e => (string?)e.Attribute("Include") == file);

        Assert.NotNull(item);
        Assert.Equal("true", (string?)item.Attribute("Pack"));
        Assert.Equal("\\", (string?)item.Attribute("PackagePath"));
        Assert.True(File.Exists(Path.Combine(CliDir, file)));
    }

    [Fact]
    public void Icon_IsPng128()
    {
        var bytes = File.ReadAllBytes(Path.Combine(CliDir, "icon.png"));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Fact]
    public void Readme_FixExample_DisablesAFixableRule()
    {
        var readme = File.ReadAllText(Path.Combine(CliDir, "README.md"));

        Assert.Contains("aibysitter fix <file|-> [--dry-run] [--disable R011]", readme, StringComparison.Ordinal);
    }
}
