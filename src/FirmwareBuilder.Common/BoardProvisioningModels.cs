using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common;

public sealed record CertificateInfo(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("issuedAtEpoch")] long IssuedAtEpoch,
    [property: JsonPropertyName("validUntilEpoch")] long ValidUntilEpoch);

public sealed record DeviceIdentity(
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("chipUid")] string ChipUid,
    [property: JsonPropertyName("usbNcmMac")] string UsbNcmMac,
    [property: JsonPropertyName("ethMac")] string EthMac,
    [property: JsonPropertyName("boardName")] string BoardName,
    [property: JsonPropertyName("certificate")] CertificateInfo Certificate);

public sealed record HardwareIdsResult(string BoardId, string Hostname, string ChipUid);

// Written alongside device_ids.hh/device-ids.json (GenerateDeviceArtifacts) and copied into
// Core/generated/ like them -- lets a project's own CMakeLists.txt read the already-resolved
// board type at CMake-configure time (e.g. to auto-select a pin-mapping variant) instead of
// requiring a human to pick the matching preset/compile-define by hand. Deliberately just the
// resolved boardTypeName string, not a project-specific boolean like "isNucleo": this library is
// shared across projects, so which boardTypeName values mean what stays entirely in each
// project's own CMakeLists.txt.
public sealed record BoardVariant(
    [property: JsonPropertyName("boardTypeName")] string BoardTypeName);

public sealed record HardwareIdentitySnapshot(
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("chipUid")] string ChipUid,
    [property: JsonPropertyName("chipUidWords")] uint[] ChipUidWords,
    [property: JsonPropertyName("usbNcmMacBytes")] byte[] UsbNcmMacBytes,
    [property: JsonPropertyName("ethMacBytes")] byte[] EthMacBytes,
    [property: JsonPropertyName("boardName")] string BoardName);
