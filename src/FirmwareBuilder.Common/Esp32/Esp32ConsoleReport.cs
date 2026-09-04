namespace FirmwareBuilder.Common.Esp32;

// Prozessorspezifischer Aufruf in die Buildercommons (s. BuilderConsoleReport.WriteBoardInfo) --
// ergaenzt den generischen Kern um den Live-Hardware-Vergleich (esptool) und die ESP32-exklusive
// Flash-Encryption-Zeile, als Daten (Label/Value-Paare), nicht als eigene Console.WriteLine-Aufrufe
// -- die Formatierung passiert zentral in BuilderConsoleReport.WriteBoardInfo. extraRows reicht
// Projektspezifisches (z.B. sensacts NodeId/BoardName) unveraendert durch.
public static class Esp32ConsoleReport
{
    public static void WriteBoardInfo(IBuildContextEsp32 ctx, string fileName = "board_info.json", Func<IEnumerable<(string Label, string Value)>>? extraRows = null) =>
        BuilderConsoleReport.WriteBoardInfo(ctx, fileName, () =>
        {
            var rows = new List<(string Label, string Value)>();
            try
            {
                var hw = EspToolService.ReadHardwareIds(ctx.RootDir);
                rows.Add(("Chip type", hw.ChipType + " (live)"));
                rows.Add(("Is current board", hw.Mac == ctx.ChipId.ToEsp32Mac48() ? "yes" : "no"));
            }
            catch (Exception ex)
            {
                rows.Add(("Is current board", $"(unbekannt -- Live-Board-Abfrage nicht moeglich: {ex.Message})"));
            }

            if (extraRows is not null)
            {
                rows.AddRange(extraRows());
            }

            rows.Add(("Encryption active", ctx.FlashEncryptionKeyBurnedAndActivated ? "yes" : "no"));
            return rows;
        });
}
