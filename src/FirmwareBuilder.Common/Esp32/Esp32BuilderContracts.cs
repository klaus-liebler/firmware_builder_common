namespace FirmwareBuilder.Common.Esp32;

// Analog zu IBuilderAppSettingsStm32 (s. Stm32/Stm32BuilderContracts.cs), fuer Esp32BuildContextBase.
// NpmPackagesDir/WebmanagerBestBinaryBuffersSchemaDir waren bisher nur Teil von IBuildContextEsp32
// und wurden von jedem ESP32-Konsumenten einzeln aus seiner appsettings-Klasse durchgereicht (s.
// sensact_firmware/builder/BuilderOptions.cs) -- jetzt generisch in der Basisklasse gelesen.
public interface IBuilderAppSettingsEsp32 : IBuilderAppSettings
{
    string NpmPackagesDir { get; }
    string WebmanagerBestBinaryBuffersSchemaDir { get; }
}
