using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

internal static class OfflineGroupRecovery
{
    internal static void Validate(OfflineGroupPolicy policy)
    {
        if (policy.RecoveryDevices is null || policy.RecoveryDevices.Length > 100 || policy.Version == 1 && policy.RecoveryDevices.Length != 0)
            throw new InvalidDataException("Ungültiges Recovery-Verzeichnis.");
        if (policy.RecoveryDevices.Length == 0) return;
        // Reuse the same public certificate, fingerprint and size validation as normal devices.
        OfflineGroupDirectory.Validate(policy with { People = [new("recovery", "Recovery", true, policy.RecoveryDevices)] });
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string KeyId(OfflineVaultDevice device)
        {
            using var certificate = OfflineGroupDirectory.ReadPublicCertificate(device.Certificate);
            using var rsa = certificate.GetRSAPublicKey()!;
            return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
        }
        foreach (var device in policy.People.SelectMany(p => p.Devices)) keys.Add(KeyId(device));
        foreach (var grant in policy.Delegations) keys.Add(Convert.ToHexString(SHA256.HashData(grant.SigningPublicKey)));
        foreach (var device in policy.RecoveryDevices)
            if (!keys.Add(KeyId(device))) throw new CryptographicException("Recovery benötigt separate Schlüssel ohne Wiederverwendung als Geräte- oder Delegatenschlüssel.");
    }
}
