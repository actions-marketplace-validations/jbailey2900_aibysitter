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

    public static string Snippet(string html) => html[..Math.Min(html.Length, 2000)];

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}
