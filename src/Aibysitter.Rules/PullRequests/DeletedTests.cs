using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Test files removed by the pull request. Removed test methods inside kept files are not checked.
/// Not flagged: the same pull request removes a non-test code file the test is named after
/// (FooTests.cs and Foo.cs, foo.test.ts and foo.ts, test_foo.py and foo.py, foo_test.go and foo.go, foo_spec.rb and foo.rb).
/// </summary>
public sealed partial class DeletedTests : IPullRequestCheck
{
    public string Id => "P008";
    public string Title => "Deleted tests";
    public Severity Severity => Severity.Error;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var removed = context.Files.Where(f => f.Status == FileChangeStatus.Removed).ToList();
        var removedSubjects = removed
            .Where(f => FileKinds.IsCode(f.Path) && !FileKinds.IsTestFile(f.Path))
            .Select(f => Path.GetFileNameWithoutExtension(f.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in removed.Where(f => FileKinds.IsTestFile(f.Path)))
        {
            if (removedSubjects.Contains(Subject(file.Path)))
            {
                continue;
            }

            yield return new PullRequestFinding(
                Id,
                file.Path,
                1,
                $"Test file deleted: {file.Path}",
                "Restore the file, or state in the PR which tests replace it.");
        }
    }

    /// <summary>The file name with test affixes removed: FooTests.cs → Foo; foo.test.ts → foo; test_foo.py → foo.</summary>
    internal static string Subject(string path) => SubjectRegex().Replace(Path.GetFileName(path), string.Empty);

    [GeneratedRegex(@"^test_|(?:(?-i:Tests?)|[._](?:test|spec))?\.[^.]+$", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectRegex();
}
