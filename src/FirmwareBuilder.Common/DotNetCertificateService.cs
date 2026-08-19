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
        string? outputFileBaseName = null,
        string? keyAlgorithmOverride = null,
        string? applicationUri = null)
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

        // keyAlgorithmOverride lets one board have multiple leaf certs on different algorithms
        // signed by the same CA (mixed-algorithm chains are valid X.509, see the Create() call
        // below) -- e.g. this project's OPC UA server needs RSA (SecurityPolicy#Basic256Sha256
        // mandates it) while the HTTPS webserver is far better off with EC_P256 on hardware
        // without a PKA (RSA-2048's software modexp is fine for one handshake per long-lived OPC
        // UA channel, but far too slow for a browser's many short-lived parallel HTTPS
        // connections per page load -- found via live testing 2026-08-19).
        using var leafKey = CreateLeafKeyAlgorithm(keyAlgorithmOverride ?? options.KeyAlgorithm);
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
        // OPC UA application-instance certificates (Part 6) are expected to carry the server's
        // ApplicationUri as a URI SAN entry -- without it, strict clients (UAExpert, asyncua) flag
        // BadCertificateUriInvalid, even though it doesn't block a connection (confirmed via
        // asyncua's own cert-generation helper doing exactly this, 2026-08-19). Only the OPC UA
        // leaf cert passes this; the HTTPS leaf has no ApplicationUri concept.
        if (!string.IsNullOrWhiteSpace(applicationUri))
        {
            sanBuilder.AddUri(new Uri(applicationUri));
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
        // request.Create(X509Certificate2 issuerCertificate, ...) -- the convenience overload --
        // throws "the issuer certificate public key algorithm (X) does not match the value for
        // this certificate request (Y), use the X509SignatureGenerator overload" whenever the CA
        // and leaf use different key algorithms (confirmed 2026-08-19: this project's shared CA
        // is RSA-2048, its leaf certificates are EC_P256 by design -- deployed and working, e.g.
        // the current factory-box-363836 device certificate is exactly this combination). Mixed
        // RSA-issuer/EC-leaf chains are valid X.509; .NET's convenience overload just can't build
        // one -- use the X509SignatureGenerator overload it points to instead.
        using var caPrivateKey = GetPrivateKey(caCertificate);
        var caSignatureGenerator = CreateSignatureGenerator(caPrivateKey);
        using var signedLeaf = request.Create(caCertificate.SubjectName, caSignatureGenerator, notBefore, notAfter, RandomPositiveSerialNumber(20));
        using var leafWithPrivateKey = CopyWithPrivateKey(signedLeaf, leafKey);

        File.WriteAllText(keyPath, ExportPrivateKeyPem(leafKey));
        File.WriteAllText(certPath, leafWithPrivateKey.ExportCertificatePem());

        return new PemCertificateFiles(keyPath, certPath);
    }

    public static byte[] ConvertPemToDer(string pemPath, string kind)
    {
        return kind switch
        {
            // X509Certificate2.CreateFromPemFile(pemPath) (single-arg, or with keyPemFilePath:
            // null) throws "the key contents do not contain a PEM ... or the key does not match
            // the certificate" on this project's EC leaf certificates -- confirmed via an
            // isolated repro (2026-08-19) that the file itself is a perfectly well-formed,
            // cert-only PEM (no key section at all); this .NET version's PEM-to-X509Certificate2
            // loading path apparently still attempts EC key extraction and fails. A certificate
            // PEM is just base64(DER) with markers though -- no cryptographic/key-matching
            // operation is needed for a pure format conversion, so skip X509Certificate2's PEM
            // loader entirely here.
            "cert" => DecodePemToDer(File.ReadAllText(pemPath)),
            "key" => ReadPrivateKeyPemAsDer(pemPath),
            _ => throw new ArgumentException($"Unbekannter PEM-Typ \"{kind}\". Erlaubt: cert|key."),
        };
    }

    // See ConvertPemToDer's "cert" case above for why this doesn't use
    // X509Certificate2.CreateFromPemFile either.
    private static byte[] DecodePemToDer(string pemText)
    {
        var fields = PemEncoding.Find(pemText);
        var der = new byte[fields.DecodedDataLength];
        Convert.TryFromBase64Chars(pemText.AsSpan(fields.Base64Data), der, out _);
        return der;
    }

    public static PemCertificateInfo ReadCertificateInfo(string certificatePath)
    {
        using var certificate = X509CertificateLoader.LoadCertificate(DecodePemToDer(File.ReadAllText(certificatePath)));
        // X509Certificate2.NotBefore/NotAfter return DateTime in the LOCAL system timezone
        // (documented .NET behavior), not UTC -- pairing them with TimeSpan.Zero as if they were
        // already UTC throws "The UTC Offset of the local dateTime parameter does not match the
        // offset argument" on any machine whose local offset isn't zero (confirmed 2026-08-19,
        // Germany/UTC+2). ToUniversalTime() converts correctly based on the DateTime's own Kind.
        return new PemCertificateInfo(
            certificate.Issuer,
            new DateTimeOffset(certificate.NotBefore.ToUniversalTime()).ToUnixTimeSeconds(),
            new DateTimeOffset(certificate.NotAfter.ToUniversalTime()).ToUnixTimeSeconds());
    }

    // NX_SECURE_X509_KEY_TYPE_EC_DER (firmware_factory_control_unit's net_setup.cpp) expects the
    // traditional SEC1/RFC 5915 "ECPrivateKey" DER structure -- confirmed 2026-08-19 by tracing
    // NetX Secure's own parser (nx_secure_x509_ec_private_key_parse.c): it reads a SEQUENCE whose
    // first element must be INTEGER(1) (the RFC 5915 version field) directly followed by an OCTET
    // STRING (the key). PKCS#8 (ExportPkcs8PrivateKey(), the previous code here) wraps the key in
    // its OWN outer SEQUENCE with version INTEGER(0) and an AlgorithmIdentifier before the actual
    // key bytes -- NetX Secure's parser reads that outer 0 where it expects 1 and rejects the
    // whole key with NX_SECURE_PKCS1_INVALID_PRIVATE_KEY (confirmed via an isolated repro
    // comparing both exports' raw DER bytes). ExportECPrivateKey()/ExportRSAPrivateKey() export
    // the traditional (PKCS#1-family) format directly instead -- NetX Secure's RSA parser is
    // likewise literally named nx_secure_x509_pkcs1_rsa_private_key_parse.c, i.e. also PKCS#1,
    // not PKCS#8, even though this project only actually uses the EC path today.
    private static byte[] ReadPrivateKeyPemAsDer(string privateKeyPath)
    {
        var pem = File.ReadAllText(privateKeyPath);

        try
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return ec.ExportECPrivateKey();
        }
        catch (CryptographicException)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa.ExportRSAPrivateKey();
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

    // The issuer's key type doesn't have to match the leaf's -- a mixed-algorithm chain (this
    // project's RSA-2048 CA signing EC_P256 leaves) is valid X.509 and is what's actually
    // deployed. request.Create(X509Certificate2, ...) can't build one (throws pointing at this
    // exact overload); GetPrivateKey()/CreateSignatureGenerator() together are the fix.
    private static AsymmetricAlgorithm GetPrivateKey(X509Certificate2 certificate)
    {
        return (AsymmetricAlgorithm?)certificate.GetRSAPrivateKey() ?? certificate.GetECDsaPrivateKey()
            ?? throw new NotSupportedException($"CA-Zertifikat \"{certificate.Subject}\" hat keinen unterstuetzten privaten Schluessel (RSA/ECDsa) oder keinen privaten Schluessel ueberhaupt.");
    }

    private static X509SignatureGenerator CreateSignatureGenerator(AsymmetricAlgorithm issuerPrivateKey)
    {
        return issuerPrivateKey switch
        {
            RSA rsa => X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1),
            ECDsa ec => X509SignatureGenerator.CreateForECDsa(ec),
            _ => throw new NotSupportedException($"Nicht unterstuetzter CA-Schluesseltyp: {issuerPrivateKey.GetType().Name}"),
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
