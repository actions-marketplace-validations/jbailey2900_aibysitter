using Aibysitter.Rules.PullRequests;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aibysitter.Web.GitHub;

public static class GitHubServiceCollectionExtensions
{
    public static IServiceCollection AddAibysitterGitHubApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IGitHubGateway, OctokitGitHubGateway>();
        services.AddSingleton<ReviewQueue>();
        services.AddSingleton(_ => new PullRequestReviewer());
        services.AddSingleton<ReviewProcessor>();
        services.AddHostedService<ReviewWorker>();
        return services;
    }
}
