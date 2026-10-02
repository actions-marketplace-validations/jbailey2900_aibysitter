using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aibysitter.Web.Data;

/// <summary>For dotnet-ef only (migrations, bundle). The bundle is given its connection string with --connection at run time.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AibysitterDbContext>
{
    public AibysitterDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AibysitterDbContext>()
            .UseSqlServer("Server=.;Database=Aibysitter;Integrated Security=true;TrustServerCertificate=true")
            .Options);
}
