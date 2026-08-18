namespace FirmwareBuilder.Common;

public interface IBoardsDirectoryOptions
{
    string BoardsDir { get; }
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
