using System.Text.Json;

namespace FirmwareBuilder.Common.Stm32;

// STM32-only in der Praxis (sensact_firmware ruft dies nie auf -- generiert seine eigenen
// TS/C++-Build-Infos anders), deshalb hier unter Stm32/ statt im Wurzelverzeichnis. Nimmt
// IBuildContextStm32 direkt entgegen (RootDir/FirmwareGeneratedDir/WebGeneratedDir/BoardArchiveDir
// sind bereits Teil des Kontexts) -- defaultBoardTypeName/FirmwareVersion* bleiben explizite
// Parameter, weil sie NICHT aus ctx ableitbar sind, solange (Warnfall) noch kein Board-Kontext
// bekannt ist.
public static class GitBuildArtifactsService
{
    public static void Generate(IBuildContextStm32 ctx, string defaultBoardTypeName, int firmwareVersionMajor, int firmwareVersionMinor, int firmwareVersionPatch)
    {
        var info = GitInfoReader.ReadGitInfo(ctx.RootDir);
        // device-ids.json liegt im Projekt (s. Stm32BoardProvisioningService.GenerateDeviceArtifacts).
        var deviceIdentity = ReadDeviceIdentity(ctx.FirmwareGeneratedDir);
        var boardName = deviceIdentity?.BoardName ?? defaultBoardTypeName;

        var hhContent = RenderGitConstantsHh(info);
        var firmwareConstantsContent = RenderFirmwareConstantsHh(boardName, firmwareVersionMajor, firmwareVersionMinor, firmwareVersionPatch);
        var tsContent = RenderBuildInfoTs(info, boardName, firmwareVersionMajor, firmwareVersionMinor, firmwareVersionPatch, deviceIdentity);

        // gitconstants.hh/firmware_constants.hh/build-info.ts sind reine Ableitung aus Git-Status +
        // device-ids.json -- gehoeren ins Projekt (s. Projektgedaechtnis "generierte Dateien nur im
        // Projekt"), unabhaengig davon, ob ueberhaupt ein Board-Kontext bekannt ist.
        Directory.CreateDirectory(ctx.FirmwareGeneratedDir);
        Directory.CreateDirectory(ctx.WebGeneratedDir);
        File.WriteAllText(Path.Combine(ctx.FirmwareGeneratedDir, "gitconstants.hh"), hhContent);
        File.WriteAllText(Path.Combine(ctx.FirmwareGeneratedDir, "firmware_constants.hh"), firmwareConstantsContent);
        File.WriteAllText(Path.Combine(ctx.WebGeneratedDir, "build-info.ts"), tsContent);

        if (deviceIdentity is null)
        {
            Console.WriteLine(
                "Warnung: Kein device-ids.json im Projekt gefunden (GenerateDeviceArtifacts nicht gelaufen?) -- " +
                "Hostname/Chip-UID/MAC in build-info.ts bleiben leer.");
        }

        // gitstatus.json bleibt im Board-Archiv (EXCEPTION, kein reines Build-Derivat): wird von
        // FlashFirmwarePipelineService.RecordSuccessfulFlash spaeter gelesen, um Flash-Ereignisse in
        // flash_events.jsonl mit dem Git-Stand zum Flash-Zeitpunkt zu korrelieren -- persistenter
        // Audit-Trail, kein Build-Output.
        string? boardId;
        try
        {
            boardId = ctx.BoardUid;
        }
        catch (InvalidOperationException)
        {
            boardId = null;
        }

        if (boardId is not null)
        {
            var archiveGeneratedDir = Path.Combine(ctx.BoardArchiveDir, "generated");
            Directory.CreateDirectory(archiveGeneratedDir);
            File.WriteAllText(Path.Combine(archiveGeneratedDir, "gitstatus.json"), JsonSerializer.Serialize(info, JsonDefaults.Pretty) + "\n");
        }
        else
        {
            Console.WriteLine("Warnung: Kein Board-Kontext bekannt -- gitstatus.json wird nicht geschrieben (kein Flash-Event-Abgleich moeglich).");
        }

        Console.WriteLine($"Git-Status ({info.CommitHash}, {info.Branch}, dirty={(info.IsDirty ? "true" : "false")}) -> {ctx.FirmwareGeneratedDir} / {ctx.WebGeneratedDir}.");
    }

    private static DeviceIdentity? ReadDeviceIdentity(string coreGeneratedDir)
    {
        var jsonPath = Path.Combine(coreGeneratedDir, "device-ids.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceIdentity>(File.ReadAllText(jsonPath), JsonDefaults.Compact);
    }

    private static string EscapeCppString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string RenderGitConstantsHh(GitInfo info)
    {
        return $$"""
            #pragma once

            /**
             * @file gitconstants.hh
             * @brief Auto-generated Git information constants
             * Generated at epoch: {{info.BuildTimestampEpoch}}
             */

            #include <cstdint>
            #include <string_view>

            namespace git {

            /// Git commit short hash
            constexpr std::string_view COMMIT_HASH = "{{EscapeCppString(info.CommitHash)}}";

            /// Git branch name
            constexpr std::string_view BRANCH = "{{EscapeCppString(info.Branch)}}";

            /// Git tag or commit hash
            constexpr std::string_view TAG = "{{EscapeCppString(info.Tag)}}";

            /// Last commit date and time, Unix-Epoch-Sekunden (Formatierung/Zeitzone am Anzeigeort)
            constexpr int64_t COMMIT_DATE_EPOCH = {{info.CommitDateEpoch}};

            /// Last commit author
            constexpr std::string_view COMMIT_AUTHOR = "{{EscapeCppString(info.CommitAuthor)}}";

            /// Last commit message
            constexpr std::string_view COMMIT_MESSAGE = "{{EscapeCppString(info.CommitMessage)}}";

            /// Is the working directory dirty (has uncommitted changes)?
            constexpr bool IS_DIRTY = {{(info.IsDirty ? "true" : "false")}};

            /// Build timestamp, Unix-Epoch-Sekunden (Formatierung/Zeitzone am Anzeigeort)
            constexpr int64_t BUILD_TIMESTAMP_EPOCH = {{info.BuildTimestampEpoch}};

            /// Full version string
            constexpr std::string_view VERSION = "{{EscapeCppString(info.Version)}}";

            } // namespace git

            """;
    }

    private static string RenderFirmwareConstantsHh(string boardName, int major, int minor, int patch)
    {
        return $$"""
            #pragma once
            // GENERIERT von FirmwareBuilder.Common/GitBuildArtifactsService.cs -- nicht von Hand editieren.
            #include <cstdint>
            #include <string_view>

            constexpr std::string_view BOARD_NAME = "{{EscapeCppString(boardName)}}";
            constexpr uint16_t FW_VERSION_MAJOR = {{major}};
            constexpr uint16_t FW_VERSION_MINOR = {{minor}};
            constexpr uint16_t FW_VERSION_PATCH = {{patch}};

            """;
    }

    private static string TsStringLiteral(string s) => JsonSerializer.Serialize(s, JsonDefaults.Compact);

    private static string RenderBuildInfoTs(GitInfo info, string boardName, int major, int minor, int patch, DeviceIdentity? deviceIdentity)
    {
        return $$"""
            // GENERIERT von FirmwareBuilder.Common/GitBuildArtifactsService.cs -- nicht von Hand editieren.

            export const GIT_COMMIT_HASH = {{TsStringLiteral(info.CommitHash)}};
            export const GIT_BRANCH = {{TsStringLiteral(info.Branch)}};
            export const GIT_TAG = {{TsStringLiteral(info.Tag)}};
            export const GIT_IS_DIRTY = {{(info.IsDirty ? "true" : "false")}};
            export const GIT_COMMIT_MESSAGE = {{TsStringLiteral(info.CommitMessage)}};
            export const GIT_COMMIT_DATE_EPOCH = {{info.CommitDateEpoch}};
            export const BUILD_TIMESTAMP_EPOCH = {{info.BuildTimestampEpoch}};

            export const BOARD_NAME = {{TsStringLiteral(boardName)}};
            export const FW_VERSION_MAJOR = {{major}};
            export const FW_VERSION_MINOR = {{minor}};
            export const FW_VERSION_PATCH = {{patch}};

            export const DEVICE_HOSTNAME = {{TsStringLiteral(deviceIdentity?.Hostname ?? "")}};
            export const DEVICE_CHIP_UID = {{TsStringLiteral(deviceIdentity?.ChipUid ?? "")}};
            export const DEVICE_ETH_MAC = {{TsStringLiteral(deviceIdentity?.EthMac ?? "")}};
            export const DEVICE_USB_NCM_MAC = {{TsStringLiteral(deviceIdentity?.UsbNcmMac ?? "")}};

            export const DEVICE_CERT_ISSUER = {{TsStringLiteral(deviceIdentity?.Certificate.Issuer ?? "")}};
            export const DEVICE_CERT_ISSUED_AT_EPOCH = {{deviceIdentity?.Certificate.IssuedAtEpoch ?? 0}};
            export const DEVICE_CERT_VALID_UNTIL_EPOCH = {{deviceIdentity?.Certificate.ValidUntilEpoch ?? 0}};

            """;
    }
}
