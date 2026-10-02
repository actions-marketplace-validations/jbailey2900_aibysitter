using System.Net;
using System.Text;

namespace Aibysitter.Web.Linting;

public enum FetchStatus
{
    Found,
    NotFound,
    TooLarge,
    TimedOut,
    Unreachable,
}

/// <param name="FileName">The file linted, when <see cref="Status"/> is Found or TooLarge.</param>
/// <param name="OtherFiles">Other supported files present on the default branch, in fixed order.</param>
public sealed record FetchResult(FetchStatus Status, string? FileName, string? Content, IReadOnlyList<string> OtherFiles);

/// <summary>
/// Reads a rules file from raw.githubusercontent.com, default branch only. Every supported name is probed in parallel;
/// the preferred name wins when present, otherwise the first in <see cref="FileNames"/> order. Redirects are followed
/// only within raw.githubusercontent.com.
/// </summary>
public sealed class RawGitHubFetcher(HttpClient http)
{
    public const string Host = "raw.githubusercontent.com";
    public const int MaxBytes = 100 * 1024;
    public const int MaxRedirects = 3;

    public static readonly IReadOnlyList<string> FileNames =
        ["CLAUDE.md", "AGENTS.md", ".github/copilot-instructions.md", "GEMINI.md", ".cursorrules", ".windsurfrules"];

    private enum ProbeState
    {
        Missing,
        Found,
        TooLarge,
        TimedOut,
        Failed,
    }

    private sealed record Probe(string FileName, ProbeState State, string? Content);

    /// <summary>Total time allowed for all probes.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public static Uri RawUrl(RepoRef repo, string fileName) => new($"https://{Host}/{repo.Owner}/{repo.Repo}/HEAD/{fileName}");

    public async Task<FetchResult> FetchAsync(RepoRef repo, string? preferred, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Timeout);
        var probes = await Task.WhenAll(FileNames.Select(name => ProbeAsync(RawUrl(repo, name), name, budget.Token)));
        cancellationToken.ThrowIfCancellationRequested();
        return Choose(probes, preferred);
    }

    private static FetchResult Choose(Probe[] probes, string? preferred)
    {
        var present = probes.Where(p => p.State is ProbeState.Found or ProbeState.TooLarge).ToList();
        var chosen = present.FirstOrDefault(p => p.FileName == preferred) ?? present.FirstOrDefault();
        if (chosen is null)
        {
            return new FetchResult(EmptyStatus(probes), null, null, []);
        }

        var others = present.Where(p => p != chosen).Select(p => p.FileName).ToList();
        return chosen.State == ProbeState.Found
            ? new FetchResult(FetchStatus.Found, chosen.FileName, chosen.Content, others)
            : new FetchResult(FetchStatus.TooLarge, chosen.FileName, null, others);
    }

    private static FetchStatus EmptyStatus(Probe[] probes) =>
        probes.Any(p => p.State == ProbeState.TimedOut) ? FetchStatus.TimedOut
        : probes.Any(p => p.State == ProbeState.Failed) ? FetchStatus.Unreachable
        : FetchStatus.NotFound;

    private async Task<Probe> ProbeAsync(Uri url, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (IsRedirect(response.StatusCode))
                {
                    if (NextHop(url, response.Headers.Location) is not { } next)
                    {
                        return new Probe(fileName, ProbeState.Missing, null);
                    }

                    url = next;
                    continue;
                }

                return await ReadAsync(response, fileName, cancellationToken);
            }

            return new Probe(fileName, ProbeState.Missing, null);
        }
        catch (OperationCanceledException)
        {
            return new Probe(fileName, ProbeState.TimedOut, null);
        }
        catch (HttpRequestException)
        {
            return new Probe(fileName, ProbeState.Failed, null);
        }
    }

    private static async Task<Probe> ReadAsync(HttpResponseMessage response, string fileName, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new Probe(fileName, ProbeState.Missing, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new Probe(fileName, ProbeState.Failed, null);
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return new Probe(fileName, ProbeState.TooLarge, null);
        }

        var bytes = await ReadCappedAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken);
        if (bytes is null)
        {
            return new Probe(fileName, ProbeState.TooLarge, null);
        }

        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes).TrimStart('﻿');
        return text.Length > LintLimits.MaxContentLength
            ? new Probe(fileName, ProbeState.TooLarge, null)
            : new Probe(fileName, ProbeState.Found, text);
    }

    /// <summary>The body, or null when it exceeds <see cref="MaxBytes"/>.</summary>
    private static async Task<byte[]?> ReadCappedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// <summary>The redirect target when it stays on https://raw.githubusercontent.com; otherwise null.</summary>
    private static Uri? NextHop(Uri current, Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        var next = location.IsAbsoluteUri ? location : new Uri(current, location);
        return next.Scheme == Uri.UriSchemeHttps && next.Host == Host && next.IsDefaultPort && next.UserInfo.Length == 0 ? next : null;
    }
}
