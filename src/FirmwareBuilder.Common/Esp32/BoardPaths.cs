namespace FirmwareBuilder.Common.Esp32;

// Mac-Formatierung + Board-Verzeichnisname -- genuine ESP32-projektuebergreifende Konvention
// (unveraendert aus dem alten TS-Tooling uebernommen, s. mac_12char/mac_6char/Paths.boardSpecificPath
// in espidf-vite-secure-build-tools, das sowohl von sensact_firmware als auch labathome_firmware
// verwendet wird/wurde -- s. Memory project_labathome_builder_migration). Verzeichnisname-Konvention:
// "<mac6hex>_<macDezimal>_<mac12hex>".
public static class BoardPaths
{
    public static string Mac12Char(long mac) => mac.ToString("x12");

    public static string Mac6Char(long mac) => Mac12Char(mac)[6..];

    public static string BoardDirectoryName(long mac) => $"{Mac6Char(mac)}_{mac}_{Mac12Char(mac)}";

    public static string BoardSpecificPath(string boardsDir, long mac, params string[] subPathParts) =>
        Path.Combine([boardsDir, BoardDirectoryName(mac), .. subPathParts]);
}
