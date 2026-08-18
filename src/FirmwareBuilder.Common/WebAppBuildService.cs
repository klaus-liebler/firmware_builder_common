namespace FirmwareBuilder.Common;

public static class WebAppBuildService
{
    public static void Run(string rootDir, string webDir, string assetsDir)
    {
        var viteEntry = Path.Combine(rootDir, "node_modules", "vite", "bin", "vite.js");
        if (!File.Exists(viteEntry))
        {
            throw new InvalidOperationException(
                $"Vite nicht gefunden unter {viteEntry} -- zuerst \"npm install\" im Repo-Root ausfuehren.");
        }

        var viteConfig = Path.Combine(webDir, "vite.config.ts");
        ProcessRunner.RunInherit("node", [viteEntry, "build", webDir, "--config", viteConfig], webDir);
        Console.WriteLine($"Web-App gebaut, {Path.Combine(assetsDir, "index.html.br")} geschrieben.");
    }
}
