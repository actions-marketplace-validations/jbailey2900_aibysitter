namespace Aibysitter.Web.Seo;

/// <summary>Page titles for rule and check pages, phrased as the question a developer would search. Drafts; site copy, not rule text.</summary>
public static class RuleSearchTitles
{
    public static readonly IReadOnlyDictionary<string, string> ById = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["R001"] = "Should CLAUDE.md explain why each rule exists?",
        ["R002"] = "Why does my AI agent ignore instructions like \"handle errors properly\"?",
        ["R003"] = "What happens when CLAUDE.md says both always and never?",
        ["R004"] = "How long should CLAUDE.md be?",
        ["R005"] = "Do duplicate lines in AGENTS.md matter?",
        ["R006"] = "How do I find paths and scripts in CLAUDE.md that no longer exist?",
        ["R007"] = "Why does my AI agent skip instructions that say \"try to\" or \"if possible\"?",
        ["R008"] = "Does IMPORTANT in CLAUDE.md make an AI agent follow the rule?",
        ["R009"] = "Is it safe to put API keys in CLAUDE.md?",
        ["R010"] = "Should a rules file start with \"You are an expert developer\"?",
        ["R011"] = "Do empty headings in a rules file affect an AI agent?",
        ["R012"] = "What does an unclosed code fence do to a rules file?",
        ["R013"] = "Should agent instructions be paragraphs or bullet points?",
        ["R014"] = "Why does \"see the docs\" in CLAUDE.md not work?",
        ["R015"] = "What frontmatter does a Cursor .mdc rule need?",
        ["R016"] = "Why is my Cursor rule not applied?",
        ["P001"] = "How do I catch placeholder values an AI agent left in a pull request?",
        ["P002"] = "How do I catch TODO stubs in AI-written code?",
        ["P003"] = "How do I catch AI-written tests that assert nothing?",
        ["P004"] = "How do I stop an AI agent from changing files outside its task?",
        ["P005"] = "How do I catch secrets an AI agent committed?",
        ["P006"] = "How do I catch an AI agent skipping failing tests?",
        ["P007"] = "How do I catch an AI agent suppressing compiler warnings?",
        ["P008"] = "How do I catch an AI agent deleting tests?",
        ["P009"] = "How do I catch empty catch blocks in AI-written code?",
        ["P010"] = "How do I see when an AI agent adds a package dependency?",
        ["P011"] = "How do I catch an AI agent editing CI configuration?",
        ["P012"] = "How do I catch debug statements left in AI-written code?",
        ["P013"] = "How do I catch build output and .env files in an AI agent's pull request?",
        ["P014"] = "How do I lint CLAUDE.md changes in a pull request?",
    };

    /// <summary>"{question} ({id})", or "{id} {name}" when no question is defined.</summary>
    public static string For(string id, string name) => ById.TryGetValue(id, out var question) ? $"{question} ({id})" : $"{id} {name}";
}
