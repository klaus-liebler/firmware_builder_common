using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record FlashFirmwarePipelineRequest(
    IBoardsDirectoryOptions BoardStorage,
    IStm32ProgrammerOptions Stm32Programmer,
    string RootDir,
    string BoardIdCacheFile,
    string BuildOutputDirectory,
    string Preset,
    string DefaultBoardTypeName,
    string? ExplicitBoardId,
    string FirmwareElfFileName = "firmware_factory_control_unit.elf",
    string McuTypeName = "STM32H563ZI");

public static class FlashFirmwarePipelineService
{
    public static void Run(FlashFirmwarePipelineRequest request)
    {
        var elfPath = Path.Combine(request.BuildOutputDirectory, request.Preset, request.FirmwareElfFileName);
        if (!File.Exists(elfPath))
        {
            throw new InvalidOperationException($"Firmware-Elf nicht gefunden: {elfPath} -- zuerst BuildFirmware fuer Preset \"{request.Preset}\" ausfuehren.");
        }

        Console.WriteLine($"Flashe {elfPath} ueber STM32_Programmer_CLI...");
        Stm32FlashService.FlashElfAndVerify(request.Stm32Programmer, elfPath, request.RootDir);
        Console.WriteLine("Flash erfolgreich verifiziert.");

        var boardId = BoardArchiveContext.RequireBoardId(request.ExplicitBoardId, request.BoardIdCacheFile, "ReadHardwareIds");
        var (chipUid, hostname) = ParseBoardId(boardId);
        var gitInfo = ReadArchivedGitInfo(request.BoardStorage, boardId);
        if (gitInfo is null)
        {
            Console.WriteLine(
                "Warnung: Kein gitstatus.json im Board-Archiv gefunden (ReadGitStatusAndGenerateFiles nicht gelaufen?) -- " +
                "kein board.json/flash_events.jsonl-Eintrag.");
            return;
        }

        var deviceIdentity = ReadArchivedDeviceIdentity(request.BoardStorage, boardId);
        var boardName = deviceIdentity?.BoardName ?? request.DefaultBoardTypeName;

        BoardStateStore.RecordSuccessfulFlash(request.BoardStorage, new RecordFlashParams(
            BoardId: boardId,
            ChipUid: chipUid,
            Hostname: hostname,
            StlinkProbe: Stm32FlashService.TryDetectStlinkProbeSerial(request.Stm32Programmer, request.RootDir),
            BoardTypeName: boardName,
            CmakePreset: request.Preset,
            GitCommitHash: gitInfo.CommitHash,
            GitBranch: gitInfo.Branch,
            GitIsDirty: gitInfo.IsDirty,
            FirmwareVersion: gitInfo.Version,
            McuTypeName: request.McuTypeName));
    }

    private static (string ChipUid, string Hostname) ParseBoardId(string boardId)
    {
        var parts = boardId.Split('_');
        var shortId = parts[0];
        var fullUid = parts.Length > 1 ? parts[1] : boardId;
        return (fullUid.ToUpperInvariant(), $"factory-box-{shortId}");
    }

    private static GitInfo? ReadArchivedGitInfo(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var jsonPath = Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "gitstatus.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<GitInfo>(File.ReadAllText(jsonPath), JsonDefaults.Compact);
    }

    private static DeviceIdentity? ReadArchivedDeviceIdentity(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var jsonPath = Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "device-ids.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceIdentity>(File.ReadAllText(jsonPath), JsonDefaults.Compact);
    }
}
