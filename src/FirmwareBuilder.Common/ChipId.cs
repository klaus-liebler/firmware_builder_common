namespace FirmwareBuilder.Common;

// Gemeinsame Chip-Identitaets-Speicherung fuer ESP32 (48-bit MAC-Adresse) und STM32 (96-bit
// Unique-ID, s. Stm32HardwareIdentityService.ReadUniqueId) -- 4x uint32 (128 Bit) sind fuer beide
// gross genug, ohne dass jedes Projekt seine eigene long/string-Kodierung erfindet, die fuers
// jeweils andere nicht passt. ESP32 belegt Word0/Word1 (48 Bit reichen in die unteren 48 Bit von
// zwei Woertern), Word2/Word3 bleiben 0. STM32 belegt Word0..Word2 (96 Bit, exakt
// Stm32UidReadResult.Words), Word3 bleibt 0. Die chip-spezifische Umrechnung von/nach diesem
// neutralen Speicherformat lebt NICHT hier, sondern hinter den jeweiligen Chip-Toolchains: s.
// Esp32.ChipIdEsp32 (FromEsp32Mac48/ToEsp32Mac48/ToBoardUid) bzw. ChipIdStm32
// (FromStm32Words/ToStm32Words/FromChipUidHex/ToBoardUid) -- diese Datei selbst bleibt chip-neutral.
public readonly record struct ChipId(uint Word0, uint Word1, uint Word2, uint Word3)
{
    // Kanonischer Board-Archiv-/Cache-Schluessel: {letzte 6 Hexstellen}_{voller Hex}. Gemeinsame
    // Formatierungsregel fuer BEIDE Chips (s. ChipIdEsp32.ToBoardUid/ChipIdStm32.ToBoardUid) --
    // nimmt bewusst einen bereits chip-spezifisch formatierten Hex-String entgegen statt selbst
    // Woerter zu verketten: ESP32s 48 Bit fuellen keine ganzen 32-Bit-Woerter, ein generisches
    // Wort-Verketten wuerde also fuehrende Nullen einstreuen und die gewohnte 12-stellige
    // MAC-Hex-Darstellung verfaelschen. STM32s 96 Bit (3 volle Woerter) haben dieses Problem nicht,
    // durchlaufen zur Konsistenz aber dieselbe Formatierungsregel.
    public static string FormatBoardUid(string hexLowercase) => $"{hexLowercase[^6..]}_{hexLowercase}";
}
