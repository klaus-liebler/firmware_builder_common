namespace FirmwareBuilder.Common.Stm32;

// STM32-seitige Umrechnung zwischen der 96-Bit-Unique-ID und dem chip-neutralen ChipId-
// Speicherformat (s. ChipId-Klassenkommentar). Ersetzt die vormals auf ChipId selbst liegenden
// FromStm32Words/ToStm32Words-Member. FromChipUidHex ergaenzt die bisher in Stm32BuildContext von
// Hand wiederholte "24-Hex-Zeichen -> 3x uint32"-Umrechnung (s. Stm32UidReadResult.Words).
public static class ChipIdStm32
{
    public static ChipId FromStm32Words(uint[] words) => new(words[0], words[1], words[2], 0);

    public static uint[] ToStm32Words(this ChipId id) => [id.Word0, id.Word1, id.Word2];

    public static ChipId FromChipUidHex(string hex24) => FromStm32Words([
        Convert.ToUInt32(hex24[..8], 16),
        Convert.ToUInt32(hex24[8..16], 16),
        Convert.ToUInt32(hex24[16..24], 16),
    ]);

    // Ersetzt die vormals separate Stm32HardwareIdentity.BoardId-Formel ({ShortId}_{ChipUid}) --
    // BoardUid ist jetzt eine reine Funktion von ChipId, dieselbe Formatierungsregel wie bei ESP32
    // (s. ChipId.FormatBoardUid). STM32s 96 Bit sind exakt 3 volle 32-Bit-Woerter, das Verketten
    // aller aktiven Woerter reproduziert deshalb (anders als bei ESP32) byte-identisch den
    // bisherigen ChipUid-Hex-String.
    public static string ToBoardUid(this ChipId id) => ChipId.FormatBoardUid(string.Concat(id.ToStm32Words().Select(w => w.ToString("x8"))));
}
