namespace FirmwareBuilder.Common;

// Benannte Konstanten fuer die wohlbekannten IBuildContext.BoardSettings-Schluessel -- ersetzt lose,
// an mehreren Stellen wiederholte Magic-Strings. camelCase, konsistent mit dem uebrigen
// BoardRecord-JSON-Schema (s. BoardStateStore.cs).
public static class BoardSettingsKeys
{
    // Ersetzt den sonst automatisch generierten Hostnamen (s. IBuildContext.Hostname).
    public const string OverrideHostname = "overrideHostname";

    // Ersetzt die sonst automatisch generierte WLAN-AP-SSID.
    public const string OverrideWifiApSsid = "overrideWifiApSsid";

    // Zuletzt verwendete Debug-Probe/Programmer-ID, chip-uebergreifend (ersetzt STM32s vormals
    // dediziertes lastStlinkProbeSerial-Feld).
    public const string LastDebugProbeId = "lastDebugProbeId";
}
