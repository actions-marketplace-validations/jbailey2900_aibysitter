using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Tests;

internal static partial class LintClient
{
    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string rulesText) => PostAsync(client, rulesText, []);

    /// <param name="extra">Additional form fields; repeated keys allowed (checkbox lists).</param>
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string rulesText, IEnumerable<KeyValuePair<string, string>> extra)
    {
        var form = await client.GetStringAsync("/Lint");
        var token = TokenRegex().Match(form).Groups[1].Value;
        Assert.NotEmpty(token);

        using var content = new FormUrlEncodedContent(new[]
        {
            KeyValuePair.Create("__RequestVerificationToken", token),
            KeyValuePair.Create("RulesText", rulesText),
        }.Concat(extra));

        return await client.PostAsync("/Lint", content);
    }

    /// <summary>Posts the lint-by-URL form; <paramref name="file"/> is the "Lint this instead" choice.</summary>
    public static async Task<HttpResponseMessage> PostUrlAsync(HttpClient client, string repo, string? file = null)
    {
        var form = await client.GetStringAsync("/Lint");
        var token = TokenRegex().Match(form).Groups[1].Value;
        var fields = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("repo", repo) };
        if (file is not null)
        {
            fields.Add(new("file", file));
        }

        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync("/Lint?handler=Url", content);
    }

    public static string Snippet(string html) => html[..Math.Min(html.Length, 2000)];

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}
