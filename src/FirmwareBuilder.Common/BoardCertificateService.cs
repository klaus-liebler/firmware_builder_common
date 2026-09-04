namespace FirmwareBuilder.Common;

// Generisches Board-Zertifikat-Provisioning: lazy-Check, Root-CA sicherstellen, SAN-DNS-Eintraege
// mit {hostname} substituieren, Board-Zertifikat erzeugen. Weder projekt- noch chip-spezifisch --
// nimmt die Basis-IBuildContext entgegen (Certificates/BoardArchiveDir sind bereits Teil des
// Kontexts); rootCaCommonName/hostname/outputFileBaseName bleiben explizite Parameter, weil sie
// NICHT Teil des generischen IBuildContext-Vertrags sind (RootCaCommonName ist konkrete-Klasse-only,
// s. CertificateAuthorityOptions; Hostname muss der Aufrufer projektspezifisch aufloesen, z.B.
// sensact ueber ISensactContext.NodeId).
public static class BoardCertificateService
{
    public static void EnsureBoardCertificate(IBuildContext ctx, string rootCaCommonName, string hostname, string outputFileBaseName, bool force = false)
    {
        var certDir = Path.Combine(ctx.BoardArchiveDir, "certificates");
        var keyPath = Path.Combine(certDir, $"{outputFileBaseName}.pem.key");
        var crtPath = Path.Combine(certDir, $"{outputFileBaseName}.pem.crt");
        if (!force && File.Exists(keyPath) && File.Exists(crtPath))
        {
            Console.WriteLine($"Board-Zertifikat existiert bereits ({crtPath}) -- nichts zu tun (lazy).");
            return;
        }

        DotNetCertificateService.EnsureRootCertificateAuthority(ctx.Certificates, rootCaCommonName, force: false);

        var dnsEntries = ctx.Certificates.SanDnsEntries
            .Select(e => (e ?? string.Empty).Trim())
            .Where(e => e.Length > 0)
            .Select(e => e.Replace("{hostname}", hostname, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Directory.CreateDirectory(certDir);
        DotNetCertificateService.EnsureBoardCertificate(
            ctx.Certificates,
            commonName: hostname,
            boardDir: certDir,
            force: force,
            ipAddress: ctx.Certificates.SanIpAddress,
            dnsHostnames: dnsEntries,
            outputFileBaseName: outputFileBaseName);
        Console.WriteLine($"Neues Board-Zertifikat erzeugt -> {crtPath}");
    }
}
