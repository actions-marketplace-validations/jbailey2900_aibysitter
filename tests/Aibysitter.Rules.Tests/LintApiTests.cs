using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aibysitter.Web.Linting;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class LintApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client() =>
        factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Lint:PermitLimit", "100000")).CreateClient();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<(HttpResponseMessage Response, JsonElement Body)> Post(object body)
    {
        var response = await Client().PostAsJsonAsync(LintApi.Path, body);
        var text = await response.Content.ReadAsStringAsync();
        return (response, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement);
    }

    [Fact]
    public async Task Sample_ReturnsSameFindingsAndScoreAsEngine()
    {
        var engine = new LintEngine();
        var expected = engine.Analyze(SampleRules.Text);
        var score = engine.Score(expected.Findings);

        var (response, body) = await Post(new { content = SampleRules.Text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(RulesetVersion.Current, body.GetProperty("rulesetVersion").GetInt32());
        Assert.Equal("Markdown", body.GetProperty("detectedFormat").GetString());
        Assert.Equal(score.Value, body.GetProperty("score").GetInt32());
        Assert.Equal(score.Grade, body.GetProperty("grade").GetString());
        Assert.Empty(body.GetProperty("disabled").EnumerateArray());
        Assert.Equal(
            expected.Findings.Select(f => $"{f.RuleId}:{f.Line}:{f.Message}:{f.FixHint}"),
            body.GetProperty("findings").EnumerateArray().Select(f => $"{f.GetProperty("rule")}:{f.GetProperty("line")}:{f.GetProperty("message")}:{f.GetProperty("fixHint")}"));
        Assert.Equal("Info", body.GetProperty("findings")[0].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Disable_RemovesRule_EchoesIds_AndRaisesScore()
    {
        var (_, all) = await Post(new { content = SampleRules.Text });
        var (response, body) = await Post(new { content = SampleRules.Text, disable = new[] { "r001", "R001", "R003" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "R001", "R003" }, body.GetProperty("disabled").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain(body.GetProperty("findings").EnumerateArray(), f => f.GetProperty("rule").GetString() is "R001" or "R003");
        Assert.True(body.GetProperty("score").GetInt32() > all.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Format_IsCaseInsensitive_AndSuppressedAreReturned()
    {
        var (response, body) = await Post(new { content = "---\nalwaysApply: false\n---\n<!-- aibysitter-disable R016 -->\n# Rules\n- Use tabs.", format = "cursormdc" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CursorMdc", body.GetProperty("detectedFormat").GetString());
        Assert.Equal("R016", body.GetProperty("suppressed")[0].GetProperty("rule").GetString());
    }

    [Theory]
    [InlineData("{}", "content")]
    [InlineData("{\"content\": \"   \"}", "content")]
    [InlineData("{\"content\": \"x\", \"format\": \"Word\"}", "format")]
    [InlineData("{\"content\": \"x\", \"format\": \"3\"}", "format")]
    [InlineData("{\"content\": \"x\", \"disable\": [\"R006\"]}", "disable")]
    [InlineData("{\"content\": \"x\", \"disable\": [\"P001\"]}", "disable")]
    public async Task InvalidField_Returns400ValidationProblem(string json, string field)
    {
        var response = await Client().PostAsync(LintApi.Path, Json(json));
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ContentOverLimit_Returns400()
    {
        var (response, body) = await Post(new { content = new string('x', LintLimits.MaxContentLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("content", out _));
    }

    [Fact]
    public async Task MalformedJson_Returns400Problem()
    {
        var response = await Client().PostAsync(LintApi.Path, Json("{\"content\": "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BodyOverCap_Returns413()
    {
        var json = "{\"content\": \"" + new string('x', LintApi.MaxBodyBytes) + "\"}";

        var response = await Client().PostAsync(LintApi.Path, Json(json));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task NonJsonContentType_Returns415()
    {
        var response = await Client().PostAsync(LintApi.Path, new StringContent("content=x", Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("OPTIONS")]
    public async Task CrossOriginRequest_GetsNoCorsHeaders(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), LintApi.Path) { Content = Json("{\"content\": \"x\"}") };
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await Client().SendAsync(request);

        Assert.DoesNotContain(response.Headers, h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApiPage_DocumentedResponse_MatchesRealResponse()
    {
        var client = Client();
        var html = await client.GetStringAsync("/API");
        var request = JsonBody(html, "api-request");
        var documented = JsonBody(html, "api-response");

        var actual = await client.PostAsync(LintApi.Path, Json(request));
        var real = await actual.Content.ReadAsStringAsync();

        Assert.True(
            System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(documented), System.Text.Json.Nodes.JsonNode.Parse(real)),
            $"Documented: {documented}\nReal: {real}");
        Assert.Contains("lint requests in 60 seconds", html);
    }

    /// <summary>The JSON body of the HTTP example in the pre block with the given id.</summary>
    private static string JsonBody(string html, string id)
    {
        var start = html.IndexOf($"<pre id=\"{id}\"><code>", StringComparison.Ordinal);
        var block = System.Net.WebUtility.HtmlDecode(html[start..html.IndexOf("</code></pre>", start, StringComparison.Ordinal)]);
        return block[block.IndexOf("\n{", StringComparison.Ordinal)..];
    }
}
