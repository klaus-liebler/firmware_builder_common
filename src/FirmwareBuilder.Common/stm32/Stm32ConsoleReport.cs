namespace FirmwareBuilder.Common;

// Prozessorspezifischer Aufruf in die Buildercommons (s. BuilderConsoleReport.WriteBoardInfo) --
// ergaenzt den generischen Kern um den Live-Hardware-Vergleich (STM32_Programmer_CLI), als Daten
// (Label/Value-Paare), nicht als eigene Console.WriteLine-Aufrufe -- die Formatierung passiert
// zentral in BuilderConsoleReport.WriteBoardInfo. Kein FlashEncryption-Pendant (das ist
// ESP32-exklusiv, s. Esp32ConsoleReport). extraRows reicht Projektspezifisches unveraendert durch.
public static class Stm32ConsoleReport
{
    public static void WriteBoardInfo(IBuildContextStm32 ctx, string fileName = "board_info.json", Func<IEnumerable<(string Label, string Value)>>? extraRows = null) =>
        BuilderConsoleReport.WriteBoardInfo(ctx, fileName, () =>
        {
            var rows = new List<(string Label, string Value)>();
            try
            {
                var uid = Stm32HardwareIdentityService.ReadUniqueId(ctx.Stm32Programmer, ctx.RootDir);
                var identity = Stm32HardwareIdentityService.BuildIdentity(uid);
                rows.Add(("Chip UID (live)", identity.ChipUid));
                rows.Add(("Is current board", string.Equals(identity.ChipUid, ctx.ChipUid, StringComparison.OrdinalIgnoreCase) ? "yes" : "no"));
            }
            catch (Exception ex)
            {
                rows.Add(("Is current board", $"(unbekannt -- Live-Board-Abfrage nicht moeglich: {ex.Message})"));
            }

            if (extraRows is not null)
            {
                rows.AddRange(extraRows());
            }

            return rows;
        });
}
