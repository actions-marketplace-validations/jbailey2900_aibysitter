using Aibysitter.Rules;
using Aibysitter.Web.GitHub;
using Aibysitter.Web.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddRazorPages();
    builder.Services.AddHealthChecks();
    builder.Services.AddSingleton(_ => new LintEngine());
    builder.Services.AddAibysitterHardening(builder.Configuration);
    builder.Services.AddAibysitterDataProtection(builder.Configuration);
    builder.Services.AddAibysitterGitHubApp(builder.Configuration);

    var app = builder.Build();

    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseRouting();
    app.UseAuthorization();

    app.MapStaticAssets();
    app.MapRazorPages().WithStaticAssets();
    app.MapHealthChecks("/health");
    app.MapGitHubWebhook();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
