namespace FirmwareBuilder.Common;

public interface IBoardsDirectoryOptions
{
    string BoardsDir { get; }
}

// Gemeinsame Form der projekteigenen appsettings.json-POCO (sensacts BuilderOptions, STM32s
// BuilderSettings) -- beide binden BoardStorage/Certificates bereits an dieselben geteilten Typen
// (BoardsDirectoryOptions/CertificateAuthorityOptions), nur die umschliessende Klasse (mit ihren
// jeweils zusaetzlichen, projektspezifischen Feldern wie NpmPackagesDir oder Stm32Programmer) ist
// unterschiedlich. Erlaubt AbstractBuildContext, CertsDir/BoardsDir/Certificates einmalig konkret
// zu implementieren, statt sie in jeder Ableitung ein weiteres Mal ident abzuschreiben. Chip-
// spezifische Erweiterungen (IBuilderAppSettingsStm32/IBuilderAppSettingsEsp32) liegen NICHT hier,
// sondern unter Stm32/ bzw. Esp32/, analog zu IBuildContextStm32/IBuildContextEsp32 -- diese Datei
// bleibt chip-neutral.
public interface IBuilderAppSettings
{
    IBoardsDirectoryOptions BoardStorage { get; }
    ICertificateAuthorityOptions Certificates { get; }
}

public interface ICertificateAuthorityOptions
{
    string CertsDir { get; }
    string SubjectPrefix { get; }
    string CertDays { get; }
    string CaCertPath { get; }
    string CaKeyPath { get; }
    string KeyAlgorithm { get; }
    bool IncludeServerAuthEku { get; }
    bool IncludeClientAuthEku { get; }
    bool IncludeSubjectKeyIdentifier { get; }
    bool IncludeAuthorityKeyIdentifier { get; }
    int NotBeforeBackdateDays { get; }
    string? SanIpAddress { get; }
    IReadOnlyList<string> SanDnsEntries { get; }

    // CommonName der Root-CA, falls DotNetCertificateService.EnsureRootCertificateAuthority sie neu
    // anlegen muss (lazy -- existieren CaCertPath/CaKeyPath bereits, wird dieser Wert gar nicht
    // gebraucht). Bis vor kurzem nur auf der konkreten CertificateAuthorityOptions-Klasse (ESP32-
    // Konsumenten lasen sie direkt von dort), jetzt Teil des Interfaces, weil auch STM32s
    // Zertifikat-Provisioning die CA jetzt bei Bedarf selbst anlegt (s. Stm32BoardProvisioningService.
    // GenerateCertificates) statt vorauszusetzen, dass sie schon von Hand existiert.
    string RootCaCommonName { get; }
}
