using System.Text.Json;

namespace FirmwareBuilder.Common;

// Konsolen-Reporting, das in ALLEN Konsumenten (aktuell ESP32/sensact_firmware, STM32/
// firmware_factory_control_unit) identisch aussehen soll. Die jeweiligen "Info"-[BuildStep]-Methoden
// bestehen dadurch nur noch aus einem Aufruf der prozessorspezifischen Variante
// (Esp32ConsoleReport.WriteBoardInfo/Stm32ConsoleReport.WriteBoardInfo), die ihrerseits hierher
// delegiert. Aufrufer LIEFERN NUR DATEN (Label/Value-Paare) -- die eigentliche Ausgabe (Formatierung,
// Ausrichtung) passiert an genau einer Stelle (WriteRows), statt ueber verstreute Console.WriteLine-
// Aufrufe mit von Hand nachgezaehlter Leerzeichen-Polsterung je Aufrufer.
public static class BuilderConsoleReport
{
    public static void WriteGitStatus(GitInfo info) => WriteRows([
        ("Commit Hash", info.CommitHash),
        ("Branch", info.Branch),
        ("Tag", info.Tag),
        ("Commit Date", DateTimeOffset.FromUnixTimeSeconds(info.CommitDateEpoch).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
        ("Commit Author", info.CommitAuthor),
        ("Commit Message", info.CommitMessage),
        ("Is Dirty?", info.IsDirty ? "yes" : "no"),
        ("Version", info.Version),
    ]);

    // Generischer Kern der Board-Info-Ausgabe. fileName ist der Board-Record-Dateiname im
    // Board-Archiv -- projektuebergreifend "board_info.json" (s. BoardRecord-Klassenkommentar in
    // BoardStateStore.cs: gleiches JSON-Schema in beiden Projekten). extraRows liefert zusaetzliche
    // Zeilen (z.B. Esp32ConsoleReport haengt den Live-Hardware-Vergleich + Encryption-Status an,
    // sensacts Program.cs darueber hinaus NodeId) -- wird NUR aufgerufen, wenn die Board-Identitaet
    // ueberhaupt aufloesbar ist, und seine Zeilen werden mit allen anderen zusammen in EINEM
    // Ausrichtungsdurchlauf gedruckt.
    public static void WriteBoardInfo(IBuildContext ctx, string fileName, Func<IEnumerable<(string Label, string Value)>>? extraRows = null)
    {
        var rows = new List<(string Label, string Value)>();

        string boardUid;
        try
        {
            boardUid = ctx.BoardUid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Kein Board bekannt: {ex.Message}");
            return;
        }

        rows.Add(("Board UID", boardUid));
        rows.Add(("Board Type Name", ctx.BoardTypeName));
        rows.Add(("Board Type Version", ctx.BoardTypeVersion));
        rows.Add(("Hostname", ctx.Hostname));

        if (extraRows is not null)
        {
            rows.AddRange(extraRows());
        }

        var recordPath = Path.Combine(ctx.BoardArchiveDir, fileName);
        if (!File.Exists(recordPath))
        {
            WriteRows(rows);
            Console.WriteLine($"Kein Board-Archiv-Eintrag unter {recordPath} gefunden.");
            return;
        }
        var record = JsonSerializer.Deserialize<BoardRecord>(File.ReadAllText(recordPath), JsonDefaults.Compact)
            ?? throw new InvalidOperationException($"{recordPath} konnte nicht gelesen werden.");

        if (record.BoardVersion is { } boardVersion)
        {
            rows.Add(("Board Version", boardVersion.ToString()));
        }
        rows.Add(("Board Settings", FormatBoardSettings(ctx.BoardSettings)));
        rows.Add(("First connected", FormatEpochSeconds(record.FirstConnectedAtEpoch)));
        rows.Add(("Last connected", FormatEpochSeconds(record.LastConnectedAtEpoch)));

        WriteRows(rows);
    }

    // Rechtsbuendig an der laengsten Label DIESER Ausgabe ausgerichtet -- automatisch konsistent,
    // egal wie viele/welche Zeilen ein Aufrufer beisteuert.
    private static void WriteRows(IReadOnlyList<(string Label, string Value)> rows)
    {
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Label.Length);
        foreach (var (label, value) in rows)
        {
            Console.WriteLine(label.PadLeft(width) + ": " + value);
        }
    }

    private static string FormatBoardSettings(IReadOnlyDictionary<string, string> settings) =>
        settings.Count == 0 ? "(none)" : string.Join(", ", settings.Select(kv => $"{kv.Key}={kv.Value}"));

    private static string FormatEpochSeconds(long epochSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
