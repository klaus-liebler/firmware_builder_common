namespace FirmwareBuilder.Common.Esp32;

// Analog zu Stm32BoardProvisioningService.ReadHardwareIds: liest die echte Board-Identitaet vom
// angeschlossenen Geraet (hier: esptool), legt/aktualisiert den Board-Record im Archiv (s.
// BoardStateStore.EnsureBoard) und gibt eine einheitliche Konsolen-Ausgabe. Nimmt IBuildContextEsp32
// direkt entgegen (RootDir/BoardsDir sind bereits Teil des Kontexts, unabhaengig davon, dass die
// Board-Identitaet selbst hier erst ermittelt wird). createDefaultRecord/onBoardArchiveReady bleiben
// explizite Parameter, da sie projektspezifisches Verhalten sind, keine Kontext-Werte.
public static class Esp32BoardProvisioningService
{
    public static void PrepareContextWithRealHardware(
        IBuildContextEsp32 ctx,
        // Projektspezifische Default-Werte fuer ein NEU angelegtes Board (mac wird hier
        // hineingereicht, da BoardStateStore.EnsureBoard nur den Zeitstempel kennt).
        Func<long, BoardRecord> createDefaultRecord,
        string fileName = "board_info.json",
        // Hook fuer projektspezifisches Nachverhalten (z.B. sensacts Repo-Root-Cache-Kopie), NACHDEM
        // der Board-Record im Archiv steht.
        Action<string>? onBoardArchiveReady = null)
    {
        var hw = EspToolService.ReadHardwareIds(ctx.RootDir);
        Console.WriteLine("      Chip type: " + hw.ChipType);
        Console.WriteLine("            MAC: " + BoardPaths.Mac6Char(hw.Mac) + " (decimal: " + hw.Mac + ")");
        Console.WriteLine("Flash-Encryption-Key vorhanden: " + (hw.HasFlashEncryptionKey ? "yes" : "no"));

        var boardDir = BoardPaths.BoardSpecificPath(ctx.BoardsDir, hw.Mac);
        var wasKnown = Directory.Exists(boardDir);
        var recordPath = Path.Combine(boardDir, fileName);

        BoardStateStore.EnsureBoard(recordPath, updateLastConnected: true, _ => createDefaultRecord(hw.Mac));
        onBoardArchiveReady?.Invoke(recordPath);

        Console.WriteLine(wasKnown ? "Board bekannt -- Zeitstempel aktualisiert." : "NEUES Board -- Eintrag mit Default-Werten angelegt.");
        Console.WriteLine($"Board-Verzeichnis: {boardDir}");
    }
}
