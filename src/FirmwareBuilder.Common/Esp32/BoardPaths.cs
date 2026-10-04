namespace FirmwareBuilder.Common.Esp32;

// Reine Mac-Hex-Formatierung fuer Konsolen-/Log-Ausgaben (unveraendert aus dem alten TS-Tooling
// uebernommen, s. mac_12char/mac_6char in espidf-vite-secure-build-tools). Die vormals hier
// zusaetzlich lebende Board-Verzeichnisname-Konvention (BoardDirectoryName/BoardSpecificPath,
// "<mac6hex>_<macDezimal>_<mac12hex>") ist entfallen -- BoardUid ist jetzt eine reine Funktion von
// ChipId, gemeinsam mit STM32 ueber ChipId.FormatBoardUid (s. ChipIdEsp32.ToBoardUid).
public static class BoardPaths
{
    public static string Mac12Char(long mac) => mac.ToString("x12");

    public static string Mac6Char(long mac) => Mac12Char(mac)[6..];
}
