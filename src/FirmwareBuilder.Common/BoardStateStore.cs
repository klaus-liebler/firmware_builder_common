using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common;

// Ein Board-Record-Schema fuer ALLE Konsumenten (aktuell STM32/firmware_factory_control_unit,
// ESP32/sensact_firmware; perspektivisch auch labathome_firmware, s. Memory
// project_labathome_builder_migration). Epoch-SEKUNDEN + camelCase-JSON-Namen, angelehnt an das
// bisherige STM32-Schema -- sensact ist am 2026-08-27 bewusst darauf umgestellt worden (vorher
// Epoch-Millisekunden/snake_case, historisch aus dem jetzt als deprecated geltenden TS-Paket
// espidf-vite-secure-build-tools uebernommen). Generisch/immer befuellt: die vier Basisfelder.
// Projektspezifisch/optional: jedes Projekt setzt nur seine eigene Teilmenge, der Rest bleibt null
// -- kein erzwungener gemeinsamer Identitaets-/Board-Typ-Begriff, weil sich z.B. STM32s ChipUid
// (string) und sensacts Mac (long) strukturell unterscheiden, und sensact BoardTypeName/-Version
// bewusst NICHT speichert, sondern dynamisch aus node_id ableitet (s. NodeIdBoardTypeCatalog).
public sealed record BoardRecord(
    [property: JsonPropertyName("firstConnectedAtEpoch")] long FirstConnectedAtEpoch,
    [property: JsonPropertyName("lastConnectedAtEpoch")] long LastConnectedAtEpoch,
    // Backing fuer IBuildContext.WebAdminPassword/BoardSettings (s. dort) -- generischer,
    // projektuebergreifender Satz statt eines dedizierten Feldes pro Spezialfall. Das vormalige
    // dedizierte "lastStlinkProbeSerial"-Feld entfaellt ersatzlos; derselbe Wert steht jetzt unter
    // BoardSettings[BoardSettingsKeys.LastDebugProbeId].
    [property: JsonPropertyName("webAdminPassword")] string? WebAdminPassword,
    [property: JsonPropertyName("boardSettings")] Dictionary<string, string> BoardSettings,
    // --- Ab hier optional, projektspezifisch befuellt (s. Klassenkommentar) ---
    [property: JsonPropertyName("chipUid")] string? ChipUid = null,
    [property: JsonPropertyName("hostname")] string? Hostname = null,
    [property: JsonPropertyName("mcuType")] string? McuType = null,
    // SemVer der Hardware-Revision. Bei STM32 wird (noch) an keiner Stelle aktiv aufgeloest/
    // geschrieben -- bleibt beim Flashen einfach erhalten (s. RecordSuccessfulFlash) -- und faellt
    // in IBuildContext.BoardTypeVersion auf BuilderSettings.BoardDefaults.DefaultBoardTypeVersion
    // zurueck, solange kein echter Wert im Board-Archiv gepflegt wurde. Bei sensact bleibt dieses
    // Feld immer null (s. Klassenkommentar).
    [property: JsonPropertyName("boardTypeName")] string? BoardTypeName = null,
    [property: JsonPropertyName("boardTypeVersion")] string? BoardTypeVersion = null,
    // sensact-spezifisch: numerische MAC-Adresse als Board-Identitaet (STM32 nutzt stattdessen
    // ChipUid). Wird auch fuer die Verzeichnisnamens-Bildung (s. BoardPaths, sensact_firmware)
    // gebraucht, nicht nur zur Anzeige.
    [property: JsonPropertyName("mac")] long? Mac = null,
    // sensact-spezifisch: fortlaufende Hardware-Revisionsnummer der physischen Platine -- ANDERES
    // Konzept als BoardTypeVersion (SemVer je Board-TYP), hier eine einfache Zaehlnummer je
    // konkretem Board-Exemplar.
    [property: JsonPropertyName("boardVersion")] long? BoardVersion = null,
    // ESP32-spezifisch (IBuildContextEsp32.FlashEncryptionKeyBurnedAndActivated) -- bei STM32
    // bedeutungslos, bleibt dort immer null.
    [property: JsonPropertyName("flashEncryptionKeyBurnedAndActivated")] bool? FlashEncryptionKeyBurnedAndActivated = null);

public sealed record FlashEvent(
    [property: JsonPropertyName("flashedAtEpoch")] long FlashedAtEpoch,
    [property: JsonPropertyName("cmakePreset")] string CmakePreset,
    [property: JsonPropertyName("gitCommitHash")] string GitCommitHash,
    [property: JsonPropertyName("gitBranch")] string GitBranch,
    [property: JsonPropertyName("gitIsDirty")] bool GitIsDirty,
    [property: JsonPropertyName("firmwareVersion")] string FirmwareVersion);

public sealed record RecordFlashParams(
    string BoardId,
    string ChipUid,
    string Hostname,
    string? StlinkProbe,
    string BoardTypeName,
    string CmakePreset,
    string GitCommitHash,
    string GitBranch,
    bool GitIsDirty,
    string FirmwareVersion,
    string McuTypeName = "STM32H563ZI");

public static class BoardStateStore
{
    public static string? TryGetBoardTypeName(IBoardsDirectoryOptions boardStorage, string boardId) =>
        TryReadBoard(boardStorage, boardId)?.BoardTypeName;

    public static string? TryGetBoardTypeVersion(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var version = TryReadBoard(boardStorage, boardId)?.BoardTypeVersion;
        return string.IsNullOrEmpty(version) ? null : version;
    }

    // Generischer "lies bestehenden Eintrag oder lege einen mit Default-Werten an, optional
    // last-connected auffrischen"-Ablauf -- ersetzte das vormals in SensactBuildContext.EnsureBoard
    // duplizierte Gegenstueck zu RecordSuccessfulFlash (dort: Flash-Ereignis, hier: reines
    // Verbinden/Auswaehlen eines Boards, ohne Flash-Historie). Arbeitet bewusst auf einem EXPLIZITEN
    // Pfad statt boardStorage+boardId, weil sich Verzeichnisnamens-Konventionen je Projekt
    // unterscheiden (STM32: boardId direkt als Unterordner, s. BoardArchiveContext.BoardArchiveDir;
    // sensact: "{mac6hex}_{macDezimal}_{mac12hex}", s. BoardPaths.BoardSpecificPath).
    public static BoardRecord EnsureBoard(string boardJsonPath, bool updateLastConnected, Func<long, BoardRecord> createDefault)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(boardJsonPath)!);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!File.Exists(boardJsonPath))
        {
            Console.WriteLine($"Keine {Path.GetFileName(boardJsonPath)} unter {boardJsonPath} -- lege neuen Eintrag mit Default-Werten an.");
            var board = createDefault(now);
            File.WriteAllText(boardJsonPath, JsonSerializer.Serialize(board, JsonDefaults.Pretty) + "\n");
            return board;
        }

        var existing = JsonSerializer.Deserialize<BoardRecord>(File.ReadAllText(boardJsonPath), JsonDefaults.Compact)
            ?? throw new InvalidOperationException($"{boardJsonPath} konnte nicht gelesen werden.");
        if (!updateLastConnected)
        {
            return existing;
        }

        var touched = existing with { LastConnectedAtEpoch = now };
        File.WriteAllText(boardJsonPath, JsonSerializer.Serialize(touched, JsonDefaults.Pretty) + "\n");
        return touched;
    }

    public static void RecordSuccessfulFlash(IBoardsDirectoryOptions boardStorage, RecordFlashParams p)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var existing = TryReadBoard(boardStorage, p.BoardId);

        var boardSettings = existing?.BoardSettings is { } s
            ? new Dictionary<string, string>(s)
            : [];
        if (p.StlinkProbe is not null)
        {
            boardSettings[BoardSettingsKeys.LastDebugProbeId] = p.StlinkProbe;
        }

        var board = new BoardRecord(
            ChipUid: p.ChipUid,
            Hostname: p.Hostname,
            McuType: p.McuTypeName,
            BoardTypeName: p.BoardTypeName,
            BoardTypeVersion: existing?.BoardTypeVersion,
            FirstConnectedAtEpoch: existing?.FirstConnectedAtEpoch ?? now,
            LastConnectedAtEpoch: now,
            WebAdminPassword: existing?.WebAdminPassword,
            BoardSettings: boardSettings);

        var boardDir = BoardArchiveContext.BoardArchiveDir(boardStorage, p.BoardId);
        Directory.CreateDirectory(boardDir);
        File.WriteAllText(BoardJsonPath(boardStorage, p.BoardId), JsonSerializer.Serialize(board, JsonDefaults.Pretty) + "\n");

        var flashEvent = new FlashEvent(now, p.CmakePreset, p.GitCommitHash, p.GitBranch, p.GitIsDirty, p.FirmwareVersion);
        File.AppendAllText(FlashEventsPath(boardStorage, p.BoardId), JsonSerializer.Serialize(flashEvent, JsonDefaults.Compact) + "\n");

        Console.WriteLine($"{BoardJsonPath(boardStorage, p.BoardId)}: Flash-Ereignis fuer {p.ChipUid} ({p.Hostname}) protokolliert.");
    }

    // Oeffentlich, damit IBuildContextStm32/IBuildContextEsp32-Implementierungen (Stm32BuildContext,
    // SensactBuildContext) BoardTypeName/WebAdminPassword/BoardSettings/Hostname direkt aus
    // demselben Board-Record lesen koennen, ohne die Logik zu duplizieren. fileName bleibt
    // parametrisiert (falls ein zukuenftiger Konsument einen abweichenden Namen braucht), ist aber
    // projektuebergreifend auf "board_info.json" vereinheitlicht (vormals STM32: "board.json").
    public static BoardRecord? TryReadBoard(IBoardsDirectoryOptions boardStorage, string boardId, string fileName = "board_info.json")
    {
        var path = BoardJsonPath(boardStorage, boardId, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BoardRecord>(File.ReadAllText(path), JsonDefaults.Compact);
    }

    // Oeffentlich, damit GenerateDeviceArtifacts board.json (kompakt genug, um ohne eigenen
    // Projektions-Typ auszukommen -- s. Kommentar dort) unveraendert in die Generated-Staging-
    // Struktur kopieren kann, von wo aus CopyGeneratedFilesToBuildDirectory es wie jede andere
    // generierte Datei ins Projektverzeichnis uebernimmt.
    public static string BoardJsonPath(IBoardsDirectoryOptions boardStorage, string boardId, string fileName = "board_info.json") =>
        Path.Combine(BoardArchiveContext.BoardArchiveDir(boardStorage, boardId), fileName);

    private static string FlashEventsPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardArchiveDir(boardStorage, boardId), "flash_events.jsonl");
}
