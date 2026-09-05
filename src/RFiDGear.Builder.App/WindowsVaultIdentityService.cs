using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.IO;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed record WindowsVaultIdentity(string DisplayName, string Fingerprint, X509Certificate2 Certificate);

internal static class WindowsVaultIdentityService
{
    private const string FriendlyPrefix = "SieEncode Vault Identity · ";

    public static IReadOnlyList<WindowsVaultIdentity> List()
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates.Cast<X509Certificate2>()
            .Where(certificate => certificate.HasPrivateKey && certificate.FriendlyName.StartsWith(FriendlyPrefix, StringComparison.Ordinal))
            .Select(certificate => new WindowsVaultIdentity(certificate.FriendlyName[FriendlyPrefix.Length..], CertificateVaultService.Fingerprint(certificate), new X509Certificate2(certificate)))
            .OrderBy(identity => identity.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static WindowsVaultIdentity Create(string displayName)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("SieEncode-Identitäten benötigen Windows.");
        var safeName = string.Concat(displayName.Trim().Where(character => !char.IsControl(character) && character is not ',' and not '=' and not '+' and not '"' and not '\\' and not '<' and not '>' and not ';'));
        if (safeName.Length < 3) throw new ArgumentException("Bitte einen eindeutigen Anzeigenamen mit mindestens drei Zeichen angeben.", nameof(displayName));
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest($"CN=SieEncode Vault {safeName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pfx = generated.Export(X509ContentType.Pfx, password);
        try
        {
#pragma warning disable SYSLIB0057
            using var persisted = new X509Certificate2(pfx, password, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
            persisted.FriendlyName = FriendlyPrefix + safeName;
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite); store.Add(persisted);
            var stored = store.Certificates.Find(X509FindType.FindByThumbprint, persisted.Thumbprint, validOnly: false).Cast<X509Certificate2>().FirstOrDefault(certificate => certificate.HasPrivateKey)
                ?? throw new CryptographicException("Die neue SieEncode-Identität wurde nicht mit privatem Schlüssel gespeichert.");
            return new WindowsVaultIdentity(safeName, CertificateVaultService.Fingerprint(stored), new X509Certificate2(stored));
        }
        finally { CryptographicOperations.ZeroMemory(pfx); }
    }

    public static void ExportInvitation(WindowsVaultIdentity identity, string path)
    {
        var invitation = CertificateVaultService.ExportInvitation(identity.Certificate, identity.DisplayName);
        var content = JsonSerializer.Serialize(invitation, ProjectJson.Options);
        var fullPath = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(false)); File.Move(temporary, fullPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static (X509Certificate2 Certificate, string DisplayName) ImportInvitation(string path)
    {
        var invitation = JsonSerializer.Deserialize<VaultRecipientInvitation>(File.ReadAllText(path), ProjectJson.Options)
            ?? throw new InvalidDataException("Empfängereinladung ist leer.");
        return CertificateVaultService.ImportInvitation(invitation);
    }
}
