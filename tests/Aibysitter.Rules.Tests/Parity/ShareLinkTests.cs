using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aibysitter.Web.Linting;
using Xunit.Abstractions;

namespace Aibysitter.Rules.Tests.Parity;

/// <summary>wwwroot/js/share-link.mjs under Node.</summary>
public class ShareLinkTests(ITestOutputHelper output)
{
    private static readonly object Options = new
    {
        maxLength = LintLimits.MaxContentLength,
        formats = RulesFormats.Selectable.Select(f => f.ToString()),
        ruleIds = new LintEngine().Rules.Select(r => r.Id),
    };

    private JsonNode? Run(object[] encode, string[] decode)
    {
        if (NodeRunner.FindNodeOrSkip(output) is not { } node)
        {
            return null;
        }

        return JsonNode.Parse(NodeRunner.Run(node, "share", JsonSerializer.Serialize(new { encode, decode, options = Options })));
    }

    private static object State(string text, string format = "Auto", string[]? disabled = null, int ruleset = RulesetVersion.Current) =>
        new { rulesetVersion = ruleset, format, disabled = disabled ?? [], text };

    private static string Fragment(string json) => Fragment(Encoding.UTF8.GetBytes(json));

    private static string Fragment(byte[] payload)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize))
        {
            deflate.Write(payload);
        }

        return "#s=1." + Base64Url(buffer.ToArray());
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Inflate(string fragment)
    {
        var data = fragment["s=1.".Length..].Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(data.PadRight(data.Length + (4 - data.Length % 4) % 4, '='));
        using var inflate = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var reader = new StreamReader(inflate, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Reason(JsonNode result) => result["ok"]!.GetValue<bool>() ? "ok" : result["reason"]!.GetValue<string>();

    [Fact]
    public void Encode_IsRawDeflateJson_ReadableByDotNet()
    {
        if (Run([State("# Rules\n- Use tabs.", "ClaudeMd", ["R004"])], []) is not { } result)
        {
            return;
        }

        var fragment = result["encoded"]![0]!.GetValue<string>();
        Assert.StartsWith("s=1.", fragment);
        Assert.Matches("^s=1\\.[A-Za-z0-9_-]+$", fragment);
        var json = JsonNode.Parse(Inflate(fragment))!;
        Assert.Equal(1, json["v"]!.GetValue<int>());
        Assert.Equal(RulesetVersion.Current, json["r"]!.GetValue<int>());
        Assert.Equal("ClaudeMd", json["f"]!.GetValue<string>());
        Assert.Equal("R004", json["d"]![0]!.GetValue<string>());
        Assert.Equal("# Rules\n- Use tabs.", json["t"]!.GetValue<string>());
    }

    [Fact]
    public void RoundTrip_KeepsUnicodeCrlfFormatAndRules()
    {
        var text = "# Règles ✓\r\n- Use tabs.\r\n- 日本語 \"quoted\" \\ back\n";
        if (Run([State(text, "CursorMdc", ["R005", "R002", "R005"], 1)], []) is not { } encoded)
        {
            return;
        }

        var fragment = encoded["encoded"]![0]!.GetValue<string>();
        var decoded = Run([], ["#" + fragment, fragment])!["decoded"]!.AsArray();

        foreach (var result in decoded)
        {
            Assert.Equal("ok", Reason(result!));
            var state = result!["state"]!;
            Assert.Equal(text, state["text"]!.GetValue<string>());
            Assert.Equal("CursorMdc", state["format"]!.GetValue<string>());
            Assert.Equal(["R002", "R005"], state["disabled"]!.AsArray().Select(n => n!.GetValue<string>()));
            Assert.Equal(1, state["rulesetVersion"]!.GetValue<int>());
        }
    }

    [Fact]
    public void Decode_RejectsBadInput_WithReason()
    {
        var huge = "{\"v\":1,\"r\":2,\"f\":\"Auto\",\"d\":[],\"t\":\"" + new string('a', 5_000_000) + "\"}";
        var cases = new (string Fragment, string Reason)[]
        {
            ("", "absent"),
            ("#other=1", "absent"),
            ("#s=2." + Base64Url([1, 2, 3]), "version"),
            ("#s=1", "version"),
            ("#s=1.@@@", "corrupt"),
            ("#s=1." + Base64Url(Encoding.UTF8.GetBytes("not deflate at all")), "corrupt"),
            (Fragment("{not json"), "corrupt"),
            (Fragment([0xFF, 0xFE, 0xFD]), "corrupt"),
            (Fragment("[]"), "invalid"),
            (Fragment("{\"v\":1,\"r\":2,\"f\":\"Nope\",\"d\":[],\"t\":\"x\"}"), "invalid"),
            (Fragment("{\"v\":1,\"r\":2,\"f\":\"Auto\",\"d\":[\"R999\"],\"t\":\"x\"}"), "invalid"),
            (Fragment("{\"v\":1,\"r\":2,\"f\":\"Auto\",\"d\":[],\"t\":\"   \"}"), "invalid"),
            (Fragment("{\"v\":1,\"r\":\"2\",\"f\":\"Auto\",\"d\":[],\"t\":\"x\"}"), "invalid"),
            (Fragment("{\"v\":1,\"r\":2,\"f\":\"Auto\",\"d\":[],\"t\":\"" + new string('a', LintLimits.MaxContentLength + 1) + "\"}"), "too-large"),
            (Fragment("{\"v\":1,\"r\":2,\"f\":\"Auto\",\"d\":[],\"t\":\"" + string.Concat(Enumerable.Repeat("a\\n", LintLimits.MaxContentLength / 2)) + "\"}"), "too-large"),
            (Fragment(huge), "too-large"),
            ("#s=1." + new string('A', 400_001), "too-large"),
        };

        if (Run([], cases.Select(c => c.Fragment).ToArray()) is not { } result)
        {
            return;
        }

        var reasons = result["decoded"]!.AsArray().Select(r => Reason(r!)).ToList();
        Assert.Equal(cases.Select(c => c.Reason), reasons);
        Assert.True(Fragment(huge).Length < 20_000, "high-ratio payload should be a short link");
    }

    [Fact]
    public void Decode_AcceptsTextAtTheLimit()
    {
        var text = new string('a', LintLimits.MaxContentLength);
        if (Run([State(text)], []) is not { } encoded)
        {
            return;
        }

        var decoded = Run([], [encoded["encoded"]![0]!.GetValue<string>()])!["decoded"]![0]!;
        Assert.Equal("ok", Reason(decoded));
    }
}
