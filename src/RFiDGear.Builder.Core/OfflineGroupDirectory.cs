using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public static class OfflineGroupDirectory
{
    internal static HashSet<string> Validate(OfflineGroupPolicy policy)
    {
        if (policy.People is null || policy.People.Length > 10000 || policy.Version == 1 && policy.People.Length != 0)
            throw new InvalidDataException("Ungültiges Personenverzeichnis oder falsche Richtlinienversion.");
        var people = new HashSet<string>(StringComparer.Ordinal);
        var devices = new HashSet<string>(StringComparer.Ordinal);
        var deviceKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var person in policy.People)
        {
            if (person is null || !Id(person.Id) || !people.Add(person.Id) || !Name(person.Name) || person.Devices is null || person.Devices.Length > 100)
                throw new InvalidDataException("Person ist ungültig oder doppelt vorhanden.");
            foreach (var device in person.Devices)
            {
                if (device is null || !Name(device.Name) || device.Fingerprint is not { Length: 64 } ||
                    !device.Fingerprint.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') || !devices.Add(device.Fingerprint) ||
                    string.IsNullOrWhiteSpace(device.Certificate) || device.Certificate.Length > 32768)
                    throw new InvalidDataException("Gerät ist ungültig oder mehreren Personen zugeordnet.");
                using var certificate = ReadPublicCertificate(device.Certificate);
                using var rsa = certificate.GetRSAPublicKey();
                if (certificate.HasPrivateKey || rsa is null || rsa.KeySize < 3072 || CertificateVaultService.Fingerprint(certificate) != device.Fingerprint)
                    throw new CryptographicException("Gerätezertifikat passt nicht zum öffentlichen Fingerabdruck.");
                // A different certificate does not make a reused private key a separate device.
                // Include disabled entries: reissuing their certificate must not undo a device revocation.
                if (!deviceKeys.Add(Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))))
                    throw new CryptographicException("Jeder Gerätezugang benötigt einen eigenen Schlüssel. Ein vorhandener Schlüssel darf nicht mit einem anderen Zertifikat erneut zugeordnet werden.");
                // Reading an old signed policy must remain possible after a certificate expires.
                // Current validity is enforced when resolving recipients for a new vault.
            }
        }
        return people;
    }

    /// <summary>Requires signature verification at the call site; validates structure again. No implicit all-groups fallback.</summary>
    public static VaultRecipientInvitation[] ResolveRecipients(OfflineGroupPolicy policy, string groupId, DateTimeOffset now)
    {
        if (policy.Version != 2) throw new InvalidDataException("Personenbasierte Richtlinie erforderlich.");
        Validate(policy);
        if (policy.Groups is null) throw new InvalidDataException("Gruppen fehlen.");
        var matches = policy.Groups.Where(g => g is not null && g.Id == groupId).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Gruppe fehlt oder ist nicht eindeutig.");
        var group = matches[0];
        if (group.Members is null || group.Members.Distinct(StringComparer.Ordinal).Count() != group.Members.Length)
            throw new InvalidDataException("Ungültige Gruppenmitgliedschaft.");
        var result = new List<VaultRecipientInvitation>();
        foreach (var personId in group.Members)
        {
            var person = policy.People.SingleOrDefault(p => p.Id == personId) ?? throw new InvalidDataException("Unbekannte Person in Gruppe.");
            if (!person.Enabled) continue;
            foreach (var device in person.Devices.Where(d => d.Enabled))
            {
                using var certificate = ReadPublicCertificate(device.Certificate);
                if (now.UtcDateTime < certificate.NotBefore.ToUniversalTime() || now.UtcDateTime >= certificate.NotAfter.ToUniversalTime())
                    throw new CryptographicException("Aktiver Gerätezugang ist noch nicht gültig oder abgelaufen: " + device.Name);
                result.Add(new VaultRecipientInvitation { DisplayName = person.Name + " · " + device.Name, Fingerprint = device.Fingerprint, Certificate = device.Certificate });
            }
        }
        if (result.Count == 0) throw new InvalidDataException("Gruppe hat keinen aktiven Gerätezugang.");
        return result.ToArray();
    }

    private static bool Id(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    internal static X509Certificate2 ReadPublicCertificate(string value)
    {
        var bytes = Convert.FromBase64String(value);
        if (X509Certificate2.GetCertContentType(bytes) != X509ContentType.Cert)
            throw new CryptographicException("Nur öffentliche Einzelzertifikate sind zulässig, keine Schlüsselarchive.");
        var certificate = new X509Certificate2(bytes);
        if (!certificate.RawData.SequenceEqual(bytes)) { certificate.Dispose(); throw new CryptographicException("Zertifikat muss im eindeutigen DER-Format vorliegen."); }
        return certificate;
    }
    private static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl);
}
