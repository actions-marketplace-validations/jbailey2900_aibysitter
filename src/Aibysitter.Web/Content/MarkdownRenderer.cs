using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Aibysitter.Web.Content;

/// <summary>
/// Markdown to HTML for notes and gallery entries. Raw HTML is escaped, YAML frontmatter is dropped, and the output
/// carries no inline styles or scripts, so it renders under the site's CSP.
/// </summary>
public static class MarkdownRenderer
{
    private const int MaxHeadingLevel = 6;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    /// <param name="headingOffset">Levels added to every heading, so embedded content sits under the page's own headings.</param>
    public static string ToHtml(string markdown, int headingOffset = 0)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            heading.Level = Math.Min(MaxHeadingLevel, heading.Level + headingOffset);
        }

        // Column alignment renders as an inline style attribute, which the CSP blocks.
        foreach (var column in document.Descendants<Table>().SelectMany(t => t.ColumnDefinitions))
        {
            column.Alignment = null;
        }

        return document.ToHtml(Pipeline);
    }
}
