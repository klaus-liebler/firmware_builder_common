namespace FirmwareBuilder.Common;

public static class WebAppBuildService
{
    public static void Run(string rootDir, string webDir, string assetsDir)
    {
        var viteEntry = Path.Combine(webDir, "node_modules", "vite", "bin", "vite.js");
        if (!File.Exists(viteEntry))
        {
            throw new InvalidOperationException(
                $"Vite nicht gefunden unter {viteEntry} -- zuerst \"npm install\" im web/-Verzeichnis ausfuehren.");
        }

        var viteConfig = Path.Combine(webDir, "vite.config.ts");
        ProcessRunner.RunInherit("node", [viteEntry, "build", webDir, "--config", viteConfig], webDir);

        var outFile = Path.Combine(assetsDir, "index.html.br");
        // Vite's own plugin (web/build-tools/vite-plugin-single-file-firmware-asset.ts) writes
        // directly to this path -- verify it actually landed here instead of trusting silently.
        // A prior path-depth bug in that plugin wrote to web/build/assets/ instead (one directory
        // level off), which left THIS path stale for weeks while every log line here kept
        // claiming success -- confirmed 2026-08-19 while debugging seemingly-inert JS changes.
        if (!File.Exists(outFile))
        {
            throw new InvalidOperationException(
                $"Web-App-Build hat {outFile} nicht erzeugt -- pruefe den Ausgabepfad in " +
                "web/build-tools/vite-plugin-single-file-firmware-asset.ts.");
        }
        Console.WriteLine($"Web-App gebaut, {outFile} geschrieben ({new FileInfo(outFile).Length} Bytes).");
    }
}
