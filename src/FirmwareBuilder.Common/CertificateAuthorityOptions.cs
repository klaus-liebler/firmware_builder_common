namespace FirmwareBuilder.Common;

// Zentralisierte, konkrete Implementierung von ICertificateAuthorityOptions -- vorher in beiden
// Projekten (sensact_firmware, firmware_factory_control_unit) unabhaengig als projekt-lokale
// Klasse dupliziert (dort teils per Adapter, teils direkt implementiert).
public sealed class CertificateAuthorityOptions : ICertificateAuthorityOptions
{
    public string CertsDir { get; set; } = "";
    public string SubjectPrefix { get; set; } = "";
    public string CertDays { get; set; } = "3000";
    public string KeyAlgorithm { get; set; } = "RSA_2048";
    public bool IncludeServerAuthEku { get; set; } = true;
    public bool IncludeClientAuthEku { get; set; } = true;
    public bool IncludeSubjectKeyIdentifier { get; set; } = false;
    public bool IncludeAuthorityKeyIdentifier { get; set; } = true;
    public int NotBeforeBackdateDays { get; set; } = 1;
    public string? SanIpAddress { get; set; }
    public List<string> SanDnsEntries { get; set; } = ["{hostname}", "{hostname}.local", "{hostname}.fritz.box"];
    public string RootCaCommonName { get; set; } = "root-ca";

    public string CaCertPath => Path.Combine(CertsDir, "rootCA.pem.crt");
    public string CaKeyPath => Path.Combine(CertsDir, "rootCA.pem.key");

    IReadOnlyList<string> ICertificateAuthorityOptions.SanDnsEntries => SanDnsEntries;
}
