using Microsoft.Extensions.Configuration;

namespace FirmwareBuilder.Common;

public static class BuilderAppSettings
{
    public static T LoadFromAppBase<T>(string fileName = "appsettings.json") where T : class
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(fileName, optional: false)
            .Build();
        return config.Get<T>()
            ?? throw new InvalidOperationException($"{fileName} konnte nicht gebunden werden.");
    }
}
