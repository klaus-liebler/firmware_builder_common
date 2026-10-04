using System.Text.Json;

namespace FirmwareBuilder.Common.Stm32;

// Nimmt IBuildContextStm32 direkt entgegen -- firmwareElfFileName/mcuTypeName bleiben explizite
// Parameter mit projekteigenen Defaults (kein generischer Interface-Wert fuer sowas Spezifisches
// wie den Firmware-Elf-Dateinamen).
public static class FlashFirmwarePipelineService
{
    public static void Run(
        IBuildContextStm32 ctx,
        string firmwareElfFileName = "firmware_factory_control_unit.elf",
        string mcuTypeName = "STM32H563ZI")
    {
        var elfPath = Path.Combine(ctx.BuildDir, ctx.Preset, firmwareElfFileName);
        if (!File.Exists(elfPath))
        {
            throw new InvalidOperationException($"Firmware-Elf nicht gefunden: {elfPath} -- zuerst BuildFirmware fuer Preset \"{ctx.Preset}\" ausfuehren.");
        }

        Console.WriteLine($"Flashe {elfPath} ueber STM32_Programmer_CLI...");
        Stm32FlashService.FlashElfAndVerify(ctx.Stm32Programmer, elfPath, ctx.RootDir);
        Console.WriteLine("Flash erfolgreich verifiziert.");

        var gitInfo = ReadArchivedGitInfo(ctx.BoardArchiveDir);
        if (gitInfo is null)
        {
            Console.WriteLine(
                "Warnung: Kein gitstatus.json im Board-Archiv gefunden (ReadGitStatusAndGenerateFiles nicht gelaufen?) -- " +
                "kein board_info.json/flash_events.jsonl-Eintrag.");
            return;
        }

        var deviceIdentity = ReadDeviceIdentity(ctx.FirmwareGeneratedDir);
        var boardName = deviceIdentity?.BoardName ?? ctx.BoardTypeName;

        BoardStateStore.RecordSuccessfulFlash(ctx.BoardStorage, new RecordFlashParams(
            BoardId: ctx.BoardUid,
            ChipUid: ctx.ChipUid,
            Hostname: ctx.Hostname,
            StlinkProbe: Stm32FlashService.TryDetectStlinkProbeSerial(ctx.Stm32Programmer, ctx.RootDir),
            BoardTypeName: boardName,
            CmakePreset: ctx.Preset,
            GitCommitHash: gitInfo.CommitHash,
            GitBranch: gitInfo.Branch,
            GitIsDirty: gitInfo.IsDirty,
            FirmwareVersion: gitInfo.Version,
            McuTypeName: mcuTypeName));
    }

    private static GitInfo? ReadArchivedGitInfo(string boardArchiveDir)
    {
        var jsonPath = Path.Combine(boardArchiveDir, "generated", "gitstatus.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<GitInfo>(File.ReadAllText(jsonPath), JsonDefaults.Compact);
    }

    // device-ids.json liegt im Projekt (s. Stm32BoardProvisioningService.GenerateDeviceArtifacts),
    // NICHT im Board-Archiv -- der vormalige Lesepfad hier zeigte faelschlich ins Board-Archiv
    // (dort landet device-ids.json nie), wodurch deviceIdentity in der Praxis immer null war und
    // BoardTypeName immer auf den Default zurueckfiel. Jetzt am selben Ort gelesen, an dem
    // GitBuildArtifactsService es auch liest.
    private static DeviceIdentity? ReadDeviceIdentity(string coreGeneratedDir)
    {
        var jsonPath = Path.Combine(coreGeneratedDir, "device-ids.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceIdentity>(File.ReadAllText(jsonPath), JsonDefaults.Compact);
    }
}
