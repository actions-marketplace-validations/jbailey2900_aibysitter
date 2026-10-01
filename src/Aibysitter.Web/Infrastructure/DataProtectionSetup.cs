using Microsoft.AspNetCore.DataProtection;

namespace Aibysitter.Web.Infrastructure;

public static class DataProtectionSetup
{
    public const string ApplicationName = "Aibysitter";
    public const string KeysPathKey = "DataProtection:KeysPath";

    /// <summary>
    /// With DataProtection:KeysPath set (IIS: DataProtection__KeysPath), keys persist in that folder across
    /// recycles, encrypted with machine-scope DPAPI on Windows. Without it, framework defaults apply.
    /// </summary>
    public static IServiceCollection AddAibysitterDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName(ApplicationName);

        var keysPath = configuration[KeysPathKey];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            if (OperatingSystem.IsWindows())
            {
                builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
            }
        }

        return services;
    }
}
