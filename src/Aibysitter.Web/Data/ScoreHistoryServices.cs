using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aibysitter.Web.Data;

public static class ScoreHistoryServices
{
    /// <summary>EF store and retention when <c>ConnectionStrings:Aibysitter</c> is set; otherwise <see cref="NullScoreHistory"/>. Never creates or migrates the database.</summary>
    public static IServiceCollection AddAibysitterScoreHistory(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AibysitterDbContext.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IScoreHistory, NullScoreHistory>();
            return services;
        }

        services.AddDbContextFactory<AibysitterDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));
        services.AddSingleton<IScoreHistory, EfScoreHistory>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<ScoreHistoryRetention>();
        return services;
    }
}
