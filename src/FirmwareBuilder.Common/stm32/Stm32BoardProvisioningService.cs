using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record ReadHardwareIdsRequest(
    IBoardsDirectoryOptions BoardStorage,
    IStm32ProgrammerOptions Stm32Programmer,
    string BuildDir,
    string BoardIdCacheFile,
    string RootDir,
    string DefaultBoardTypeName,
    Func<string, string?>? ResolveBoardTypeNameByBoardId = null);

public sealed record GenerateCertificatesRequest(
    IBoardsDirectoryOptions BoardStorage,
    ICertificateAuthorityOptions Certificates,
    string BoardIdCacheFile,
    string RootDir,
    string? ExplicitBoardId,
    bool Force);

public sealed record GenerateDeviceArtifactsRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string? ExplicitBoardId);

public static class Stm32BoardProvisioningService
{
    private static string HardwareIdentitySnapshotPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "hardware-identity.json");

    private static string CertificateInfoPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "certificate-info.json");

    public static HardwareIdsResult ReadHardwareIds(ReadHardwareIdsRequest request)
    {
        Console.WriteLine("Lese Chip-ID vom angeschlossenen Board...");

        var uid = Stm32HardwareIdentityService.ReadUniqueId(request.Stm32Programmer, request.RootDir);
        var hw = Stm32HardwareIdentityService.BuildIdentity(uid);
        var boardName = request.ResolveBoardTypeNameByBoardId?.Invoke(hw.BoardId) ?? request.DefaultBoardTypeName;

        Console.WriteLine(
            $"Chip-ID: {hw.ChipUid} -> Hostname: {hw.Hostname}, Board-Type: {boardName}, USB-NCM-MAC: {string.Join(":", hw.UsbNcmMac.Select(Stm32HardwareIdentityService.ToHex2))}, " +
            $"Ethernet-MAC: {string.Join(":", hw.EthMac.Select(Stm32HardwareIdentityService.ToHex2))}");

        var boardDir = BoardArchiveContext.BoardArchiveDir(request.BoardStorage, hw.BoardId);
        var generatedDir = BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, hw.BoardId);
        Directory.CreateDirectory(boardDir);
        Directory.CreateDirectory(generatedDir);

        var snapshot = new HardwareIdentitySnapshot(
            Hostname: hw.Hostname,
            ChipUid: hw.ChipUid,
            ChipUidWords: uid.Words,
            UsbNcmMacBytes: hw.UsbNcmMac,
            EthMacBytes: hw.EthMac,
            BoardName: boardName);

        File.WriteAllText(HardwareIdentitySnapshotPath(request.BoardStorage, hw.BoardId), JsonSerializer.Serialize(snapshot, JsonDefaults.Pretty) + "\n");
        BoardArchiveContext.WriteCachedBoardId(request.BuildDir, request.BoardIdCacheFile, hw.BoardId);

        Console.WriteLine($"Geschrieben: {HardwareIdentitySnapshotPath(request.BoardStorage, hw.BoardId)}");
        return new HardwareIdsResult(hw.BoardId, hw.Hostname, hw.ChipUid);
    }

    public static void GenerateCertificates(GenerateCertificatesRequest request)
    {
        var boardId = BoardArchiveContext.RequireBoardId(request.ExplicitBoardId, request.BoardIdCacheFile, "ReadHardwareIds");
        var snapshot = ReadHardwareSnapshot(request.BoardStorage, boardId);
        var boardDir = BoardArchiveContext.BoardArchiveDir(request.BoardStorage, boardId);
        var generatedDir = BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId);
        Directory.CreateDirectory(boardDir);
        Directory.CreateDirectory(generatedDir);
        var dnsHostnames = ResolveSanDnsEntries(request.Certificates.SanDnsEntries, snapshot.Hostname);

        // RSA-2048 leaf: used by the OPC UA server (SecurityPolicy#Basic256Sha256 mandates RSA,
        // see the OPC UA security project notes) -- this is "the" device certificate/key file
        // name pair (device_certificate.der/device_key.der), kept as-is for backward compat.
        // applicationUri MUST match service_structs.cpp's APPLICATION_URI constant exactly (Part 6
        // requires the URI SAN to equal the server's actual ApplicationDescription.applicationUri).
        var certFiles = DotNetCertificateService.EnsureBoardCertificate(
            request.Certificates,
            snapshot.Hostname,
            boardDir,
            request.Force,
            ipAddress: request.Certificates.SanIpAddress,
            dnsHostnames: dnsHostnames,
            applicationUri: "urn:factory-control-unit:opcua-native");

        // EC_P256 leaf: used by the HTTPS webserver -- kept as a SEPARATE certificate from the RSA
        // one above (same CA, same identity/SANs, different key algorithm). Reusing the RSA leaf
        // for HTTPS too made every page load noticeably sluggish and caused WebSocket connection
        // failures on this no-PKA board (STM32H563): ECDSA-P256 is dramatically cheaper than
        // RSA-2048's software modexp for the many short-lived parallel connections a browser opens
        // per page, whereas OPC UA only ever pays the RSA cost once per long-lived SecureChannel.
        // Found via live testing 2026-08-19.
        var ecCertFiles = DotNetCertificateService.EnsureBoardCertificate(
            request.Certificates,
            snapshot.Hostname,
            boardDir,
            request.Force,
            ipAddress: request.Certificates.SanIpAddress,
            dnsHostnames: dnsHostnames,
            outputFileBaseName: $"{snapshot.Hostname}-ec",
            keyAlgorithmOverride: "EC_P256");

        var certDer = DotNetCertificateService.ConvertPemToDer(certFiles.CertificatePath, "cert");
        var keyDer = DotNetCertificateService.ConvertPemToDer(certFiles.PrivateKeyPath, "key");
        var ecCertDer = DotNetCertificateService.ConvertPemToDer(ecCertFiles.CertificatePath, "cert");
        var ecKeyDer = DotNetCertificateService.ConvertPemToDer(ecCertFiles.PrivateKeyPath, "key");
        var certInfo = DotNetCertificateService.ReadCertificateInfo(ecCertFiles.CertificatePath);
        // The root CA's own certificate (public, safe to embed/distribute) -- sent alongside the
        // RSA leaf certificate wherever the OPC UA server presents its identity (SenderCertificate/
        // serverCertificate), so a client can build a complete trust chain without a separate
        // out-of-band CA fetch. Found necessary via live UAExpert testing 2026-08-19
        // (BadCertificateChainIncomplete when only the leaf was sent).
        var caCertDer = DotNetCertificateService.ConvertPemToDer(request.Certificates.CaCertPath, "cert");

        File.WriteAllBytes(Path.Combine(generatedDir, "device_certificate.der"), certDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "device_key.der"), keyDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "device_certificate_ec.der"), ecCertDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "device_key_ec.der"), ecKeyDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "root_ca.der"), caCertDer);
        File.WriteAllText(
            CertificateInfoPath(request.BoardStorage, boardId),
            JsonSerializer.Serialize(certInfo, JsonDefaults.Pretty) + "\n");

        Console.WriteLine($"Zertifikate aktualisiert: {generatedDir}");
    }

    public static void GenerateDeviceArtifacts(GenerateDeviceArtifactsRequest request)
    {
        var boardId = BoardArchiveContext.RequireBoardId(request.ExplicitBoardId, request.BoardIdCacheFile, "ReadHardwareIds");
        var snapshot = ReadHardwareSnapshot(request.BoardStorage, boardId);
        var certInfo = ReadCertificateInfo(request.BoardStorage, boardId);
        var generatedDir = BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId);
        Directory.CreateDirectory(generatedDir);

        var deviceIds = new DeviceIdentity(
            snapshot.Hostname,
            snapshot.ChipUid,
            string.Join(":", snapshot.UsbNcmMacBytes.Select(Stm32HardwareIdentityService.ToHex2)),
            string.Join(":", snapshot.EthMacBytes.Select(Stm32HardwareIdentityService.ToHex2)),
            snapshot.BoardName,
            certInfo);

        File.WriteAllText(Path.Combine(generatedDir, "device_ids.hh"), RenderDeviceIdsHh(
            snapshot.Hostname,
            snapshot.ChipUidWords,
            snapshot.UsbNcmMacBytes,
            snapshot.EthMacBytes));
        File.WriteAllText(Path.Combine(generatedDir, "device-ids.json"), JsonSerializer.Serialize(deviceIds, JsonDefaults.Pretty) + "\n");
        File.WriteAllText(Path.Combine(generatedDir, "board-variant.json"),
            JsonSerializer.Serialize(new BoardVariant(snapshot.BoardName), JsonDefaults.Pretty) + "\n");

        Console.WriteLine($"Geschrieben: {generatedDir} (device_ids.hh, device-ids.json, board-variant.json)");
    }

    private static HardwareIdentitySnapshot ReadHardwareSnapshot(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var path = HardwareIdentitySnapshotPath(boardStorage, boardId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{path} fehlt -- zuerst ReadHardwareIds ausfuehren.");
        }

        return JsonSerializer.Deserialize<HardwareIdentitySnapshot>(File.ReadAllText(path), JsonDefaults.Compact)
            ?? throw new InvalidOperationException($"{path} konnte nicht gelesen werden.");
    }

    private static IReadOnlyList<string> ResolveSanDnsEntries(IReadOnlyList<string> configuredEntries, string hostname)
    {
        return configuredEntries
            .Select(entry => (entry ?? string.Empty).Trim())
            .Where(entry => entry.Length > 0)
            .Select(entry => entry.Replace("{hostname}", hostname, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CertificateInfo ReadCertificateInfo(IBoardsDirectoryOptions boardStorage, string boardId)
    {
        var path = CertificateInfoPath(boardStorage, boardId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{path} fehlt -- zuerst GenerateCertificatesLazy/GenerateCertificatesForced ausfuehren.");
        }

        var info = JsonSerializer.Deserialize<PemCertificateInfo>(File.ReadAllText(path), JsonDefaults.Compact)
            ?? throw new InvalidOperationException($"{path} konnte nicht gelesen werden.");
        return new CertificateInfo(info.Issuer, info.IssuedAtEpoch, info.ValidUntilEpoch);
    }

    private static string RenderDeviceIdsHh(string hostname, uint[] words, byte[] netMac, byte[] ethMac)
    {
        var (w0, w1, w2) = (words[0], words[1], words[2]);
        var serialString = string.Concat(w0.ToString("X8"), w1.ToString("X8"), w2.ToString("X8"));
        var netMacString = string.Concat(netMac.Select(Stm32HardwareIdentityService.ToHex2));
        var netMacInit = string.Join(", ", netMac.Select(b => $"0x{Stm32HardwareIdentityService.ToHex2(b)}"));
        var ethMacInit = string.Join(", ", ethMac.Select(b => $"0x{Stm32HardwareIdentityService.ToHex2(b)}"));

        return $$"""
            #pragma once
            // Generiert von FirmwareBuilder.Common/Stm32BoardProvisioningService.cs -- nicht von Hand editieren.
            #ifdef __cplusplus
            #include <cstdint>
            #define DEVICE_IDS_CONST constexpr
            #else
            #include <stdint.h>
            #define DEVICE_IDS_CONST static const
            #endif
            DEVICE_IDS_CONST char DEVICE_HOSTNAME[] = "{{hostname}}";
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W0 = 0x{{w0.ToString("X8")}};
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W1 = 0x{{w1.ToString("X8")}};
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W2 = 0x{{w2.ToString("X8")}};
            DEVICE_IDS_CONST char DEVICE_USB_SERIAL_STRING[] = "{{serialString}}";
            DEVICE_IDS_CONST uint8_t DEVICE_USB_NCM_MAC[6] = {{{netMacInit}}};
            DEVICE_IDS_CONST char DEVICE_USB_NCM_MAC_STRING[] = "{{netMacString}}";
            DEVICE_IDS_CONST uint8_t DEVICE_ETH_MAC[6] = {{{ethMacInit}}};
            #undef DEVICE_IDS_CONST

            """;
    }
}
