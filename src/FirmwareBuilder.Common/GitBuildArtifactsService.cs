using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record GitBuildArtifactsRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string RootDir,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    string DefaultBoardTypeName,
    int FirmwareVersionMajor,
    int FirmwareVersionMinor,
    int FirmwareVersionPatch,
    string? ExplicitBoardId);

public static class GitBuildArtifactsService
{
    public static void Generate(GitBuildArtifactsRequest request)
    {
        var info = GitInfoReader.ReadGitInfo(request.RootDir);
        var boardId = BoardArchiveContext.ResolveBoardId(request.ExplicitBoardId, request.BoardIdCacheFile);
        var deviceIdentity = boardId is not null ? ReadDeviceIdentity(request.BoardStorage, boardId) : null;
        var boardName = deviceIdentity?.BoardName ?? request.DefaultBoardTypeName;

        var hhContent = RenderGitConstantsHh(info);
        var firmwareConstantsContent = RenderFirmwareConstantsHh(request, boardName);
        var tsContent = RenderBuildInfoTs(info, request, boardName, deviceIdentity);
        var jsonContent = JsonSerializer.Serialize(info, JsonDefaults.Pretty) + "\n";

        if (boardId is not null)
        {
            var dir = BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "gitconstants.hh"), hhContent);
            File.WriteAllText(Path.Combine(dir, "firmware_constants.hh"), firmwareConstantsContent);
            File.WriteAllText(Path.Combine(dir, "build-info.ts"), tsContent);
            File.WriteAllText(Path.Combine(dir, "gitstatus.json"), jsonContent);
            Console.WriteLine($"Git-Status ({info.CommitHash}, {info.Branch}, dirty={(info.IsDirty ? "true" : "false")}) -> Board-Archiv ({boardId}).");
            if (deviceIdentity is null)
            {
                Console.WriteLine("Warnung: Kein device-ids.json im Board-Archiv gefunden -- Hostname/Chip-UID/MAC in build-info.ts bleiben leer.");
            }

            return;
        }

        Console.WriteLine(
            "Warnung: Kein Board-Kontext bekannt -- schreibe Git-Status direkt nach Core/generated bzw. web/generated (kein Archiv-Eintrag).");
        Directory.CreateDirectory(request.CoreGeneratedDir);
        Directory.CreateDirectory(request.WebGeneratedDir);
        File.WriteAllText(Path.Combine(request.CoreGeneratedDir, "gitconstants.hh"), hhContent);
        File.WriteAllText(Path.Combine(request.CoreGeneratedDir, "firmware_constants.hh"), firmwareConstantsContent);
        File.WriteAllText(Path.Combine(request.WebGeneratedDir, "build-info.ts"), tsContent);
    }

    private static DeviceIdentity? ReadDeviceIdentity(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var jsonPath = Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "device-ids.json");
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

    private static string RenderFirmwareConstantsHh(GitBuildArtifactsRequest request, string boardName)
    {
        return $$"""
            #pragma once
            // GENERIERT von FirmwareBuilder.Common/GitBuildArtifactsService.cs -- nicht von Hand editieren.
            #include <cstdint>
            #include <string_view>

            constexpr std::string_view BOARD_NAME = "{{EscapeCppString(boardName)}}";
            constexpr uint16_t FW_VERSION_MAJOR = {{request.FirmwareVersionMajor}};
            constexpr uint16_t FW_VERSION_MINOR = {{request.FirmwareVersionMinor}};
            constexpr uint16_t FW_VERSION_PATCH = {{request.FirmwareVersionPatch}};

            """;
    }

    private static string TsStringLiteral(string s) => JsonSerializer.Serialize(s, JsonDefaults.Compact);

    private static string RenderBuildInfoTs(GitInfo info, GitBuildArtifactsRequest request, string boardName, DeviceIdentity? deviceIdentity)
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
            export const FW_VERSION_MAJOR = {{request.FirmwareVersionMajor}};
            export const FW_VERSION_MINOR = {{request.FirmwareVersionMinor}};
            export const FW_VERSION_PATCH = {{request.FirmwareVersionPatch}};

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
