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

public sealed record HardwareIdentitySnapshot(
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("chipUid")] string ChipUid,
    [property: JsonPropertyName("chipUidWords")] uint[] ChipUidWords,
    [property: JsonPropertyName("usbNcmMacBytes")] byte[] UsbNcmMacBytes,
    [property: JsonPropertyName("ethMacBytes")] byte[] EthMacBytes,
    [property: JsonPropertyName("boardName")] string BoardName);
