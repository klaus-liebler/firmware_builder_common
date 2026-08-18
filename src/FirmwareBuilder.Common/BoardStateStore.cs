using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common;

public sealed record BoardRecord(
    [property: JsonPropertyName("chipUid")] string ChipUid,
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("mcuType")] string McuType,
    [property: JsonPropertyName("boardTypeName")] string BoardTypeName,
    [property: JsonPropertyName("firstConnectedAtEpoch")] long FirstConnectedAtEpoch,
    [property: JsonPropertyName("lastConnectedAtEpoch")] long LastConnectedAtEpoch,
    [property: JsonPropertyName("lastStlinkProbeSerial")] string? LastStlinkProbeSerial);

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

    public static void RecordSuccessfulFlash(IBoardsDirectoryOptions boardStorage, RecordFlashParams p)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var existing = TryReadBoard(boardStorage, p.BoardId);

        var board = new BoardRecord(
            ChipUid: p.ChipUid,
            Hostname: p.Hostname,
            McuType: p.McuTypeName,
            BoardTypeName: p.BoardTypeName,
            FirstConnectedAtEpoch: existing?.FirstConnectedAtEpoch ?? now,
            LastConnectedAtEpoch: now,
            LastStlinkProbeSerial: p.StlinkProbe);

        var boardDir = BoardArchiveContext.BoardArchiveDir(boardStorage, p.BoardId);
        Directory.CreateDirectory(boardDir);
        File.WriteAllText(BoardJsonPath(boardStorage, p.BoardId), JsonSerializer.Serialize(board, JsonDefaults.Pretty) + "\n");

        var flashEvent = new FlashEvent(now, p.CmakePreset, p.GitCommitHash, p.GitBranch, p.GitIsDirty, p.FirmwareVersion);
        File.AppendAllText(FlashEventsPath(boardStorage, p.BoardId), JsonSerializer.Serialize(flashEvent, JsonDefaults.Compact) + "\n");

        Console.WriteLine($"{BoardJsonPath(boardStorage, p.BoardId)}: Flash-Ereignis fuer {p.ChipUid} ({p.Hostname}) protokolliert.");
    }

    private static BoardRecord? TryReadBoard(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var path = BoardJsonPath(boardStorage, boardId);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BoardRecord>(File.ReadAllText(path), JsonDefaults.Compact);
    }

    private static string BoardJsonPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardArchiveDir(boardStorage, boardId), "board.json");

    private static string FlashEventsPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardArchiveDir(boardStorage, boardId), "flash_events.jsonl");
}
