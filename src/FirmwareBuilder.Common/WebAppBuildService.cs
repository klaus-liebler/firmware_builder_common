namespace FirmwareBuilder.Common;

// vite-Aufruf + Ergebnis-Verifikation ist in beiden Konsumenten (STM32/factory_control_unit,
// ESP32/sensact_firmware) strukturell identisch (per singleFileFirmwareAssetPlugin auf eine
// Brotli-komprimierte Single-File-Asset-Datei bauen), aber Konvention (vite-CLI-Flags,
// Ausgabedateiname) unterscheidet sich je Projekt -- deshalb explizit statt hartkodiert, damit
// keine der beiden Web-Projekte ihre eigene vite.config.ts/Plugin-Konvention aendern muss.
public static class WebAppBuildService
{
    // Nimmt IBuildContext direkt entgegen -- WebRoot ist bereits Teil des Kontexts; viteArgs/
    // expectedOutputFile bleiben explizite Parameter (projekteigene vite-Konvention, s.o.).
    // webDir: eine andere Web-App des Projekts als ctx.WebRoot (z.B. eine zweite, rein statische App).
    public static void Run(IBuildContext ctx, IReadOnlyList<string> viteArgs, string expectedOutputFile, string? webDir = null)
    {
        webDir ??= ctx.WebRoot;
        var viteEntry = FindVite(webDir) ?? throw new InvalidOperationException(
            $"Vite nicht gefunden (node_modules/vite unter {webDir} oder einem Elternverzeichnis) -- zuerst \"npm install\" ausfuehren " +
            "(bei npm-Workspaces im Workspace-Root).");

        ProcessRunner.RunInherit("node", [viteEntry, "build", webDir, .. viteArgs], webDir);

        // Vite's eigenes Plugin (vite-plugin-single-file-firmware-asset.ts) schreibt direkt an
        // diesen Pfad -- verifizieren statt blind vertrauen. Ein frueherer Pfadtiefe-Bug in diesem
        // Plugin schrieb einmal nach web/build/assets/ statt hierher (eine Verzeichnisebene daneben),
        // was diesen Pfad wochenlang veraltet liess, waehrend jede Log-Zeile hier trotzdem Erfolg
        // meldete -- gefunden 2026-08-19 beim Debuggen scheinbar wirkungsloser JS-Aenderungen.
        if (!File.Exists(expectedOutputFile))
        {
            throw new InvalidOperationException(
                $"Web-App-Build hat {expectedOutputFile} nicht erzeugt -- pruefe den Ausgabepfad im " +
                "vite-plugin-single-file-firmware-asset-Plugin bzw. die uebergebenen vite-Argumente.");
        }
        var size = new FileInfo(expectedOutputFile).Length;
        Console.WriteLine($"Web-App gebaut, {expectedOutputFile} geschrieben ({size} Bytes = {size / 1024.0:F2} kiB).");
    }

    // Wie Nodes eigene Modulaufloesung: node_modules im Verzeichnis selbst oder in einem Elternverzeichnis --
    // bei npm-Workspaces liegt vite im Workspace-Root (hochgezogen), nicht unter der einzelnen App.
    private static string? FindVite(string webDir)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(webDir)); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "node_modules", "vite", "bin", "vite.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
