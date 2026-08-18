using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FirmwareBuilder.Common;

public sealed record PemCertificateFiles(string PrivateKeyPath, string CertificatePath);

public sealed record PemCertificateInfo(string Issuer, long IssuedAtEpoch, long ValidUntilEpoch);

public static class DotNetCertificateService
{
    private const string EcP256 = "EC_P256";
    private const string Rsa2048 = "RSA_2048";

    public static PemCertificateFiles EnsureRootCertificateAuthority(ICertificateAuthorityOptions options, string commonName, bool force)
    {
        Directory.CreateDirectory(options.CertsDir);

        if (!force && File.Exists(options.CaKeyPath) && File.Exists(options.CaCertPath))
        {
            return new PemCertificateFiles(options.CaKeyPath, options.CaCertPath);
        }

        using var keyAlgorithm = CreateLeafKeyAlgorithm(options.KeyAlgorithm);
        var request = CreateCertificateRequest(BuildSubject(options.SubjectPrefix, commonName), keyAlgorithm);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

        if (options.IncludeSubjectKeyIdentifier)
        {
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        }

        var notBefore = DateTimeOffset.UtcNow.AddDays(-Math.Abs(options.NotBeforeBackdateDays));
        var notAfter = notBefore.AddDays(ParseCertDays(options.CertDays));
        using var rootCertificate = request.CreateSelfSigned(notBefore, notAfter);

        File.WriteAllText(options.CaKeyPath, ExportPrivateKeyPem(keyAlgorithm));
        File.WriteAllText(options.CaCertPath, rootCertificate.ExportCertificatePem());

        return new PemCertificateFiles(options.CaKeyPath, options.CaCertPath);
    }

    public static PemCertificateFiles EnsureBoardCertificate(
        ICertificateAuthorityOptions options,
        string commonName,
        string boardDir,
        bool force,
        string? ipAddress,
        IReadOnlyList<string> dnsHostnames,
        string? outputFileBaseName = null)
    {
        var fileBaseName = string.IsNullOrWhiteSpace(outputFileBaseName) ? commonName : outputFileBaseName;
        var keyPath = Path.Combine(boardDir, $"{fileBaseName}.pem.key");
        var certPath = Path.Combine(boardDir, $"{fileBaseName}.pem.crt");

        if (!force && File.Exists(keyPath) && File.Exists(certPath))
        {
            return new PemCertificateFiles(keyPath, certPath);
        }

        if (!File.Exists(options.CaCertPath) || !File.Exists(options.CaKeyPath))
        {
            throw new InvalidOperationException(
                $"CA-Dateien fehlen ({options.CaCertPath}, {options.CaKeyPath}). Vorher Root-CA erzeugen.");
        }

        Directory.CreateDirectory(boardDir);

        using var leafKey = CreateLeafKeyAlgorithm(options.KeyAlgorithm);
        var request = CreateCertificateRequest(BuildSubject(options.SubjectPrefix, commonName), leafKey);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment,
            false));

        var enhancedKeyUsages = new OidCollection();
        if (options.IncludeServerAuthEku)
        {
            enhancedKeyUsages.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        }
        if (options.IncludeClientAuthEku)
        {
            enhancedKeyUsages.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        }
        if (enhancedKeyUsages.Count > 0)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsages, false));
        }

        if (options.IncludeSubjectKeyIdentifier)
        {
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        }

        var sanBuilder = new SubjectAlternativeNameBuilder();
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(ipAddress));
        }
        foreach (var dns in dnsHostnames)
        {
            if (!string.IsNullOrWhiteSpace(dns))
            {
                sanBuilder.AddDnsName(dns);
            }
        }
        request.CertificateExtensions.Add(sanBuilder.Build());

        using var caCertificate = X509Certificate2.CreateFromPemFile(options.CaCertPath, options.CaKeyPath);
        if (options.IncludeAuthorityKeyIdentifier)
        {
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                caCertificate,
                includeKeyIdentifier: false,
                includeIssuerAndSerial: true));
        }

        var notBefore = DateTimeOffset.UtcNow.AddDays(-Math.Abs(options.NotBeforeBackdateDays));
        var notAfter = notBefore.AddDays(ParseCertDays(options.CertDays));
        using var signedLeaf = request.Create(caCertificate, notBefore, notAfter, RandomPositiveSerialNumber(20));
        using var leafWithPrivateKey = CopyWithPrivateKey(signedLeaf, leafKey);

        File.WriteAllText(keyPath, ExportPrivateKeyPem(leafKey));
        File.WriteAllText(certPath, leafWithPrivateKey.ExportCertificatePem());

        return new PemCertificateFiles(keyPath, certPath);
    }

    public static byte[] ConvertPemToDer(string pemPath, string kind)
    {
        return kind switch
        {
            "cert" => X509Certificate2.CreateFromPemFile(pemPath).Export(X509ContentType.Cert),
            "key" => ReadPrivateKeyPemAsDer(pemPath),
            _ => throw new ArgumentException($"Unbekannter PEM-Typ \"{kind}\". Erlaubt: cert|key."),
        };
    }

    public static PemCertificateInfo ReadCertificateInfo(string certificatePath)
    {
        using var certificate = X509Certificate2.CreateFromPemFile(certificatePath);
        return new PemCertificateInfo(
            certificate.Issuer,
            new DateTimeOffset(certificate.NotBefore, TimeSpan.Zero).ToUnixTimeSeconds(),
            new DateTimeOffset(certificate.NotAfter, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    private static byte[] ReadPrivateKeyPemAsDer(string privateKeyPath)
    {
        var pem = File.ReadAllText(privateKeyPath);

        try
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return ec.ExportPkcs8PrivateKey();
        }
        catch (CryptographicException)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa.ExportPkcs8PrivateKey();
        }
    }

    private static string ExportPrivateKeyPem(AsymmetricAlgorithm keyAlgorithm)
    {
        return keyAlgorithm switch
        {
            ECDsa ec => ec.ExportPkcs8PrivateKeyPem(),
            RSA rsa => rsa.ExportPkcs8PrivateKeyPem(),
            _ => throw new NotSupportedException($"Nicht unterstuetzter Schluesseltyp: {keyAlgorithm.GetType().Name}"),
        };
    }

    private static CertificateRequest CreateCertificateRequest(X500DistinguishedName subject, AsymmetricAlgorithm keyAlgorithm)
    {
        return keyAlgorithm switch
        {
            ECDsa ec => new CertificateRequest(subject, ec, HashAlgorithmName.SHA256),
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            _ => throw new NotSupportedException($"Nicht unterstuetzter Schluesseltyp: {keyAlgorithm.GetType().Name}")
        };
    }

    private static X509Certificate2 CopyWithPrivateKey(X509Certificate2 certificate, AsymmetricAlgorithm keyAlgorithm)
    {
        return keyAlgorithm switch
        {
            ECDsa ec => certificate.CopyWithPrivateKey(ec),
            RSA rsa => certificate.CopyWithPrivateKey(rsa),
            _ => throw new NotSupportedException($"Nicht unterstuetzter Schluesseltyp: {keyAlgorithm.GetType().Name}")
        };
    }

    private static AsymmetricAlgorithm CreateLeafKeyAlgorithm(string keyAlgorithm)
    {
        return keyAlgorithm.ToUpperInvariant() switch
        {
            EcP256 => ECDsa.Create(ECCurve.NamedCurves.nistP256),
            Rsa2048 => RSA.Create(2048),
            _ => throw new ArgumentException($"Unbekannter KeyAlgorithm \"{keyAlgorithm}\". Erlaubt: {EcP256}, {Rsa2048}.")
        };
    }

    private static int ParseCertDays(string certDays)
    {
        if (!int.TryParse(certDays, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"CertDays muss eine positive ganze Zahl sein, war: \"{certDays}\".");
        }
        return parsed;
    }

    private static byte[] RandomPositiveSerialNumber(int numberOfBytes)
    {
        var bytes = RandomNumberGenerator.GetBytes(numberOfBytes);
        bytes[^1] &= 0x7F;
        return bytes;
    }

    private static X500DistinguishedName BuildSubject(string subjectPrefix, string commonName)
    {
        var relativeDistinguishedNames = subjectPrefix
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse()
            .ToArray();

        var suffix = relativeDistinguishedNames.Length == 0 ? "" : ", " + string.Join(", ", relativeDistinguishedNames);
        return new X500DistinguishedName($"CN={commonName}{suffix}");
    }
}
