using Aibysitter.Packs;
using Microsoft.Net.Http.Headers;

namespace Aibysitter.Web.Hooks;

/// <summary>The files in the repository's <c>hooks/</c> folder, embedded, served at <c>/hooks/{name}</c> and shown on /Hooks.</summary>
public static class HookFiles
{
    public const string PreCommit = "pre-commit";
    public const string ClaudeCodeSettings = "claude-code-settings.json";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Files =
        new(() => EmbeddedFiles.Read(typeof(HookFiles).Assembly, "hooks/"));

    public static string Get(string name) => Files.Value[name];

    public static string Url(string name) => $"/hooks/{name}";

    public static IEndpointRouteBuilder MapHookFiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/hooks/{name}", (string name, HttpContext http) =>
        {
            if (!Files.Value.TryGetValue(name, out var text))
            {
                return Results.NotFound();
            }

            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=3600";
            return Results.Text(text, name.EndsWith(".json", StringComparison.Ordinal) ? "application/json; charset=utf-8" : "text/plain; charset=utf-8");
        });
        return endpoints;
    }
}
