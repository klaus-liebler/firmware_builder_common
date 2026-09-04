namespace FirmwareBuilder.Common;

// Gemeinsame Chip-Identitaets-Speicherung fuer ESP32 (48-bit MAC-Adresse) und STM32 (96-bit
// Unique-ID, s. Stm32HardwareIdentityService.ReadUniqueId) -- 4x uint32 (128 Bit) sind fuer beide
// gross genug, ohne dass jedes Projekt seine eigene long/string-Kodierung erfindet, die fuers
// jeweils andere nicht passt. ESP32 belegt Word0/Word1 (48 Bit reichen in die unteren 48 Bit von
// zwei Woertern), Word2/Word3 bleiben 0. STM32 belegt Word0..Word2 (96 Bit, exakt
// Stm32UidReadResult.Words), Word3 bleibt 0. Ersetzt IBuildContextEsp32.BoardMac (vormals separat
// als long gespeichert).
public readonly record struct ChipId(uint Word0, uint Word1, uint Word2, uint Word3)
{
    public static ChipId FromEsp32Mac48(long mac48) => new(
        Word0: unchecked((uint)mac48),
        Word1: unchecked((uint)(mac48 >> 32)),
        Word2: 0,
        Word3: 0);

    public long ToEsp32Mac48() => (long)Word0 | ((long)Word1 << 32);

    public static ChipId FromStm32Words(uint[] words) => new(words[0], words[1], words[2], 0);

    public uint[] ToStm32Words() => [Word0, Word1, Word2];
}
