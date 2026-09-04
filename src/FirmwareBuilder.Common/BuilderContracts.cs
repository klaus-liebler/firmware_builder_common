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
// zu implementieren, statt sie in jeder Ableitung ein weiteres Mal ident abzuschreiben.
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
}

public interface IStm32ProgrammerOptions
{
    string ResolveStm32ProgrammerCli();
}
