using System.Text.Json;

namespace FirmwareBuilder.Common.Stm32;

public static class Stm32BoardProvisioningService
{
    private static string HardwareIdentitySnapshotPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "hardware-identity.json");

    private static string CertificateInfoPath(IBoardsDirectoryOptions boardStorage, string boardId) =>
        Path.Combine(BoardArchiveContext.BoardGeneratedDir(boardStorage, boardId), "certificate-info.json");

    // defaultBoardTypeName/resolveBoardTypeNameByBoardId bleiben explizite Parameter (nicht Teil von
    // ctx) -- an dieser Stelle ist die Board-Identitaet noch nicht bekannt (das ist ja der Zweck
    // dieser Methode), ctx.BoardTypeName waere hier noch gar nicht aufloesbar.
    public static HardwareIdsResult ReadHardwareIds(IBuildContextStm32 ctx, string defaultBoardTypeName, Func<string, string?>? resolveBoardTypeNameByBoardId = null)
    {
        Console.WriteLine("Lese Chip-ID vom angeschlossenen Board...");

        var uid = Stm32HardwareIdentityService.ReadUniqueId(ctx.Stm32Programmer, ctx.RootDir);
        var hw = Stm32HardwareIdentityService.BuildIdentity(uid);
        var boardName = resolveBoardTypeNameByBoardId?.Invoke(hw.BoardId) ?? defaultBoardTypeName;

        Console.WriteLine(
            $"Chip-ID: {hw.ChipUid} -> Hostname: {hw.Hostname}, Board-Type: {boardName}, USB-NCM-MAC: {string.Join(":", hw.UsbNcmMac.Select(Stm32HardwareIdentityService.ToHex2))}, " +
            $"Ethernet-MAC: {string.Join(":", hw.EthMac.Select(Stm32HardwareIdentityService.ToHex2))}");

        var boardDir = BoardArchiveContext.BoardArchiveDir(ctx.BoardStorage, hw.BoardId);
        var generatedDir = BoardArchiveContext.BoardGeneratedDir(ctx.BoardStorage, hw.BoardId);
        Directory.CreateDirectory(boardDir);
        Directory.CreateDirectory(generatedDir);

        var snapshot = new HardwareIdentitySnapshot(
            Hostname: hw.Hostname,
            ChipUid: hw.ChipUid,
            ChipUidWords: uid.Words,
            UsbNcmMacBytes: hw.UsbNcmMac,
            EthMacBytes: hw.EthMac,
            BoardName: boardName);

        File.WriteAllText(HardwareIdentitySnapshotPath(ctx.BoardStorage, hw.BoardId), JsonSerializer.Serialize(snapshot, JsonDefaults.Pretty) + "\n");
        BoardArchiveContext.WriteCachedBoardId(ctx.BuildDir, ctx.BoardIdCacheFile, hw.BoardId);

        Console.WriteLine($"Geschrieben: {HardwareIdentitySnapshotPath(ctx.BoardStorage, hw.BoardId)}");
        return new HardwareIdsResult(hw.BoardId, hw.Hostname, hw.ChipUid);
    }

    public static void GenerateCertificates(IBuildContextStm32 ctx, bool force)
    {
        var boardId = ctx.BoardUid;
        var snapshot = ReadHardwareSnapshot(ctx.BoardStorage, boardId);
        var boardDir = BoardArchiveContext.BoardArchiveDir(ctx.BoardStorage, boardId);
        var generatedDir = BoardArchiveContext.BoardGeneratedDir(ctx.BoardStorage, boardId);
        Directory.CreateDirectory(boardDir);
        Directory.CreateDirectory(generatedDir);

        // Lazy wie EnsureBoardCertificate selbst (force: false) -- auf dieser Maschine bereits
        // vorhanden, deshalb ein No-Op; auf einer frischen Maschine ohne CA-Dateien war dieser
        // Aufruf bisher der fehlende Schritt (GenerateCertificates warf "CA-Dateien fehlen -- vorher
        // Root-CA erzeugen", ohne dass irgendein Schritt das je selbst getan haette -- s.
        // BoardCertificateService, das ESP32-Pendant, das das schon immer tat).
        DotNetCertificateService.EnsureRootCertificateAuthority(ctx.Certificates, ctx.Certificates.RootCaCommonName, force: false);

        var dnsHostnames = DotNetCertificateService.ResolveSanDnsEntries(ctx.Certificates.SanDnsEntries, snapshot.Hostname);

        // Single RSA-2048 leaf, shared by both the OPC UA server (SecurityPolicy#Basic256Sha256
        // mandates RSA, see the OPC UA security project notes) and the HTTPS webserver. A second,
        // EC_P256 leaf was generated here for a while specifically for HTTPS, on the theory that
        // ECDSA-P256 is cheap enough to fix the sluggish-page-load/WebSocket-failure symptoms seen
        // on this no-PKA board (STM32H563) -- that theory was wrong: the actual cause was RSA
        // running in an unoptimized Debug build (no compiler-level speedup, still no hardware PKA),
        // not RSA-vs-EC per se; Release builds handle the shared RSA cert fine on both servers.
        // Reverted back to one shared cert. applicationUri MUST match service_structs.cpp's
        // APPLICATION_URI constant exactly (Part 6 requires the URI SAN to equal the server's
        // actual ApplicationDescription.applicationUri).
        var certFiles = DotNetCertificateService.EnsureBoardCertificate(
            ctx.Certificates,
            snapshot.Hostname,
            boardDir,
            force,
            ipAddress: ctx.Certificates.SanIpAddress,
            dnsHostnames: dnsHostnames,
            applicationUri: "urn:factory-control-unit:opcua-native");

        var certDer = DotNetCertificateService.ConvertPemToDer(certFiles.CertificatePath, "cert");
        var keyDer = DotNetCertificateService.ConvertPemToDer(certFiles.PrivateKeyPath, "key");
        var certInfo = DotNetCertificateService.ReadCertificateInfo(certFiles.CertificatePath);
        // The root CA's own certificate (public, safe to embed/distribute) -- sent alongside the
        // RSA leaf certificate wherever the OPC UA server presents its identity (SenderCertificate/
        // serverCertificate), so a client can build a complete trust chain without a separate
        // out-of-band CA fetch. Found necessary via live UAExpert testing 2026-08-19
        // (BadCertificateChainIncomplete when only the leaf was sent).
        var caCertDer = DotNetCertificateService.ConvertPemToDer(ctx.Certificates.CaCertPath, "cert");

        File.WriteAllBytes(Path.Combine(generatedDir, "device_certificate.der"), certDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "device_key.der"), keyDer);
        File.WriteAllBytes(Path.Combine(generatedDir, "root_ca.der"), caCertDer);
        File.WriteAllText(
            CertificateInfoPath(ctx.BoardStorage, boardId),
            JsonSerializer.Serialize(certInfo, JsonDefaults.Pretty) + "\n");

        Console.WriteLine($"Zertifikate aktualisiert: {generatedDir}");
    }

    public static void GenerateDeviceArtifacts(IBuildContextStm32 ctx)
    {
        var boardId = ctx.BoardUid;
        var snapshot = ReadHardwareSnapshot(ctx.BoardStorage, boardId);
        var certInfo = ReadCertificateInfo(ctx.BoardStorage, boardId);
        Directory.CreateDirectory(ctx.FirmwareGeneratedDir);

        var deviceIds = new DeviceIdentity(
            snapshot.Hostname,
            snapshot.ChipUid,
            string.Join(":", snapshot.UsbNcmMacBytes.Select(Stm32HardwareIdentityService.ToHex2)),
            string.Join(":", snapshot.EthMacBytes.Select(Stm32HardwareIdentityService.ToHex2)),
            snapshot.BoardName,
            certInfo);

        // hh und JSON werden aus DENSELBEN snapshot/certInfo-Objekten gerendert (keine zwei separat
        // konstruierten Sichten mehr) und tragen dadurch denselben Feldumfang -- device_ids.hh
        // bekam vorher weder BoardName noch Zertifikatsinfo, obwohl beides bereits fuer die JSON-
        // Seite vorlag.
        File.WriteAllText(Path.Combine(ctx.FirmwareGeneratedDir, "device_ids.hh"), RenderDeviceIdsHh(snapshot, certInfo));
        File.WriteAllText(Path.Combine(ctx.FirmwareGeneratedDir, "device-ids.json"), JsonSerializer.Serialize(deviceIds, JsonDefaults.Pretty) + "\n");

        // board_info.json (Board-Archiv-Root) unveraendert ins Projekt spiegeln -- einzige Ausnahme
        // hier: persistenter Zustand, keine Ableitung, keine eigene schmalere Projektionsdatei mehr
        // (frueher board-variant.json/BoardVariant). CMakeLists.txt liest boardTypeName direkt aus
        // dieser Kopie. Existiert board_info.json noch nicht (Board noch nie erfolgreich geflasht),
        // wird uebersprungen.
        var boardJsonPath = BoardStateStore.BoardJsonPath(ctx.BoardStorage, boardId);
        if (File.Exists(boardJsonPath))
        {
            File.Copy(boardJsonPath, Path.Combine(ctx.FirmwareGeneratedDir, "board_info.json"), overwrite: true);
        }

        Console.WriteLine($"Geschrieben: {ctx.FirmwareGeneratedDir} (device_ids.hh, device-ids.json, board_info.json)");
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

    private static string RenderDeviceIdsHh(HardwareIdentitySnapshot snapshot, CertificateInfo cert)
    {
        var (w0, w1, w2) = (snapshot.ChipUidWords[0], snapshot.ChipUidWords[1], snapshot.ChipUidWords[2]);
        var serialString = string.Concat(w0.ToString("X8"), w1.ToString("X8"), w2.ToString("X8"));
        var netMac = snapshot.UsbNcmMacBytes;
        var ethMac = snapshot.EthMacBytes;
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
            DEVICE_IDS_CONST char DEVICE_HOSTNAME[] = "{{snapshot.Hostname}}";
            DEVICE_IDS_CONST char DEVICE_BOARD_NAME[] = "{{snapshot.BoardName}}";
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W0 = 0x{{w0.ToString("X8")}};
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W1 = 0x{{w1.ToString("X8")}};
            DEVICE_IDS_CONST uint32_t DEVICE_CHIP_UID_W2 = 0x{{w2.ToString("X8")}};
            DEVICE_IDS_CONST char DEVICE_USB_SERIAL_STRING[] = "{{serialString}}";
            DEVICE_IDS_CONST uint8_t DEVICE_USB_NCM_MAC[6] = {{{netMacInit}}};
            DEVICE_IDS_CONST char DEVICE_USB_NCM_MAC_STRING[] = "{{netMacString}}";
            DEVICE_IDS_CONST uint8_t DEVICE_ETH_MAC[6] = {{{ethMacInit}}};
            DEVICE_IDS_CONST char DEVICE_CERT_ISSUER[] = "{{cert.Issuer}}";
            DEVICE_IDS_CONST int64_t DEVICE_CERT_ISSUED_AT_EPOCH = {{cert.IssuedAtEpoch}};
            DEVICE_IDS_CONST int64_t DEVICE_CERT_VALID_UNTIL_EPOCH = {{cert.ValidUntilEpoch}};
            #undef DEVICE_IDS_CONST

            """;
    }
}
