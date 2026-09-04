namespace FirmwareBuilder.Common;

// Die "vom AppContext.BaseDirectory aufwaerts bis zu einem Repo-Root-Verzeichnis mit CMakeLists.txt
// + einem builder/-Unterverzeichnis"-Logik, zuvor in beiden Projekten (sensact_firmware,
// firmware_factory_control_unit) unabhaengig voneinander dupliziert. AppContext.BaseDirectory zeigt
// zur Laufzeit auf den Build-Output-Ordner (bin/<Config>/<TFM>/...), dessen Tiefe je nach Aufrufart
// (dotnet run/build/publish, ggf. mit RID-Unterordner) variiert -- deshalb robust nach oben suchen
// statt eine feste Anzahl ".."-Sprünge anzunehmen.
public static class BuildContextPaths
{
    public static string FindRootDir(string builderDirName = "builder")
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CMakeLists.txt")) && Directory.Exists(Path.Combine(dir.FullName, builderDirName)))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Konnte Repo-Wurzel nicht finden (gesucht ab {AppContext.BaseDirectory} nach oben, Marker: CMakeLists.txt + {builderDirName}/).");
    }

    // Unterordnernamen, die in beiden Konsumenten (sensact_firmware, firmware_factory_control_unit)
    // identisch sind -- alles darueber hinaus (generated/-Unterstruktur, Board-Archiv-Layout, ...)
    // ist projektspezifisch und bleibt deshalb in der jeweiligen konkreten BuildContext-Klasse.
    public static string BestBinaryBuffersSchemaDir(string rootDir) => Path.Combine(rootDir, "best_binary_buffers_schema");
    public static string WebDir(string rootDir) => Path.Combine(rootDir, "web");
    public static string BuildDir(string rootDir) => Path.Combine(rootDir, "build");
}
