using Aibysitter.Packs;
using Aibysitter.Rules;
using Aibysitter.Web.Content;

namespace Aibysitter.Web.RulesPacks;

/// <param name="Score">The section linted alone as a Markdown rules file.</param>
public sealed record SectionView(PackSection Section, LintScore Score, string Html);

/// <param name="Score">The pack composed alone as CLAUDE.md (<see cref="ScoreFormat"/>).</param>
public sealed record PackView(Pack Pack, LintScore Score, IReadOnlyList<SectionView> Sections)
{
    public const RulesFormat ScoreFormat = RulesFormat.ClaudeMd;

    public string InitCommand => $"aibysitter init --packs {Pack.Id} --format claude";
}

/// <summary>Lint scores and rendered previews for every pack, computed once.</summary>
public sealed class PackScores(PackCatalog catalog, LintEngine engine)
{
    private readonly Lazy<IReadOnlyList<PackView>> views = new(() => catalog.All.Select(p => View(p, engine)).ToList());

    public IReadOnlyList<PackView> All => views.Value;

    private static PackView View(Pack pack, LintEngine engine) => new(
        pack,
        engine.Score(engine.Lint(PackComposer.Compose([pack], PackView.ScoreFormat), PackView.ScoreFormat)),
        pack.Sections.Select(s => new SectionView(s, engine.Score(engine.Lint(s.Markdown, RulesFormat.Markdown)), MarkdownRenderer.ToHtml(PackComposer.Resolve(string.Join('\n', s.BodyLines), PackComposer.DefaultPath(PackView.ScoreFormat, [pack]))))).ToList());
}
