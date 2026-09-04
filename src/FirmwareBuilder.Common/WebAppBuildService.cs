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
    public static void Run(IBuildContext ctx, IReadOnlyList<string> viteArgs, string expectedOutputFile)
    {
        var webDir = ctx.WebRoot;
        var viteEntry = Path.Combine(webDir, "node_modules", "vite", "bin", "vite.js");
        if (!File.Exists(viteEntry))
        {
            throw new InvalidOperationException(
                $"Vite nicht gefunden unter {viteEntry} -- zuerst \"npm install\" im web/-Verzeichnis ausfuehren.");
        }

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
}
