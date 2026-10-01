using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Tests;

internal static partial class LintClient
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string rulesText)
    {
        var form = await client.GetStringAsync("/Lint");
        var token = TokenRegex().Match(form).Groups[1].Value;
        Assert.NotEmpty(token);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["RulesText"] = rulesText,
        });

        return await client.PostAsync("/Lint", content);
    }

    public static string Snippet(string html) => html[..Math.Min(html.Length, 2000)];

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}
