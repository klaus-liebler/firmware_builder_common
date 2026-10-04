namespace FirmwareBuilder.Common.Esp32;

// ESP32-seitige Umrechnung zwischen der 48-Bit-MAC-Adresse und dem chip-neutralen ChipId-
// Speicherformat (s. ChipId-Klassenkommentar). Ersetzt die vormals auf ChipId selbst liegenden
// FromEsp32Mac48/ToEsp32Mac48-Member.
public static class ChipIdEsp32
{
    public static ChipId FromEsp32Mac48(long mac48) => new(
        Word0: unchecked((uint)mac48),
        Word1: unchecked((uint)(mac48 >> 32)),
        Word2: 0,
        Word3: 0);

    public static long ToEsp32Mac48(this ChipId id) => (long)id.Word0 | ((long)id.Word1 << 32);

    // Ersetzt BoardPaths.BoardDirectoryName(mac) -- BoardUid ist jetzt eine reine Funktion von
    // ChipId statt einer zweiten, unabhaengig aus der rohen MAC berechneten Darstellung. "x12"
    // reproduziert die traditionelle 12-stellige MAC-Hex-Darstellung (48 Bit), NICHT einfach
    // Word0/Word1 einzeln verkettet -- s. ChipId.FormatBoardUid-Kommentar.
    public static string ToBoardUid(this ChipId id) => ChipId.FormatBoardUid(id.ToEsp32Mac48().ToString("x12"));
}
