using System.Text.Json;
using Aibysitter.Rules;

namespace Aibysitter.Web.Linting;

/// <summary>POST /api/lint: JSON in, findings and score out. No auth, no CORS, nothing stored.</summary>
public static class LintApi
{
    public const string Path = "/api/lint";
    public const int MaxBodyBytes = 512 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Request(string? Content, string? Format, IReadOnlyList<string?>? Disable);

    public sealed record ApiFinding(string Rule, int Line, string Severity, string Message, string FixHint);

    public sealed record Response(
        int RulesetVersion,
        string DetectedFormat,
        int Score,
        string Grade,
        IReadOnlyList<string> Disabled,
        IReadOnlyList<ApiFinding> Findings,
        IReadOnlyList<ApiFinding> Suppressed);

    public static IEndpointRouteBuilder MapLintApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, HandleAsync);
        return endpoints;
    }

    internal static async Task<IResult> HandleAsync(HttpContext context, LintService lint, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType())
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Content-Type must be application/json.");
        }

        var body = await ReadBodyAsync(context.Request, cancellationToken);
        if (body is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: $"Request body is limited to {MaxBodyBytes / 1024} KB.");
        }

        Request? request;
        try
        {
            request = JsonSerializer.Deserialize<Request>(body, Json);
        }
        catch (JsonException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request body is not valid JSON.");
        }

        return Validate(request, lint, out var format, out var disabled) is { } errors
            ? Results.ValidationProblem(errors)
            : Results.Json(ToResponse(lint.Lint(request!.Content!, format, disabled, "api")), Json);
    }

    /// <summary>Body bytes, or null when over <see cref="MaxBodyBytes"/>.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Dictionary<string, string[]>? Validate(Request? request, LintService lint, out RulesFormat format, out IReadOnlyList<string> disabled)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        format = RulesFormat.Auto;
        disabled = [];

        if (string.IsNullOrWhiteSpace(request?.Content))
        {
            errors["content"] = ["Required."];
        }
        else if (request.Content.Length > LintLimits.MaxContentLength)
        {
            errors["content"] = [$"Limited to {LintLimits.MaxContentLength:N0} characters."];
        }

        if (request?.Format is { } name && !TryParseFormat(name, out format))
        {
            errors["format"] = [$"Unknown format \"{name}\". Use one of: {string.Join(", ", Enum.GetNames<RulesFormat>())}."];
        }

        if (!lint.TryNormalizeDisabled(request?.Disable, out disabled, out var unknown))
        {
            errors["disable"] = [$"Not a lint rule ID: {string.Join(", ", unknown)}."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static bool TryParseFormat(string name, out RulesFormat format) =>
        Enum.TryParse(name, ignoreCase: true, out format) && Enum.IsDefined(format) && !int.TryParse(name, out _);

    private static Response ToResponse(LintOutcome outcome) => new(
        RulesetVersion.Current,
        outcome.Format.ToString(),
        outcome.Score.Value,
        outcome.Score.Grade,
        outcome.Disabled,
        outcome.Findings.Select(ToApi).ToList(),
        outcome.Suppressed.Select(ToApi).ToList());

    private static ApiFinding ToApi(LintRow row) =>
        new(row.Finding.RuleId, row.Finding.Line, row.Severity.ToString(), row.Finding.Message, row.Finding.FixHint);
}
