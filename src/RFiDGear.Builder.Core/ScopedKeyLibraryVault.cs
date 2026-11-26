using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

public sealed record CustomerKeyLibraryBlock(Guid CustomerId, GroupVaultDocument Vault);
public sealed record ScopedKeyLibraryBundle(string Format, int Version, Guid LibraryId,
    string GroupId, GroupPolicyCheckpoint Policy, long CatalogRevision, CustomerKeyLibraryBlock[] Customers);
public sealed record SignedScopedKeyLibraryBundle(byte[] Payload, byte[] Signature);

/// <summary>One signed transportable library, independent authenticated
/// ciphertext per customer. No full-library content key, no parallel database.
/// Integration with the existing file store and central CAS is a separate gate.</summary>
public static class ScopedKeyLibraryVault
{
    public const string Format = "SieEncode-Scoped-Key-Library-v1";
    public const int MaximumBytes = 16 * 1024 * 1024;
    private const int MaximumCustomers = 10000;

    public static SignedScopedKeyLibraryBundle Protect(KeyLibraryVaultPayload full,
        GroupEnrollmentPackage enrollment, RSA authority, string groupId, DateTimeOffset now)
    {
        var verified = GroupEnrollmentPackages.Validate(enrollment);
        var policy = verified.Policy;
        KeyLibraryVault.Validate(full);
        if (!policy.CustomerScopesEnabled || full.Catalog.LibraryId != policy.LibraryId ||
            full.Catalog.Customers.Length > MaximumCustomers ||
            !authority.ExportSubjectPublicKeyInfo().SequenceEqual(enrollment.AdministratorPublicKey))
            throw new CryptographicException("Vollständiger Katalog und passende Kundenbereichsautorität erforderlich.");
        var group = RequireGroup(policy, groupId);
        // An explicitly all-customer personal member and separate recovery
        // must remain available, including for future customers.
        if (!policy.People.Any(p => p.Enabled && p.AllKeyLibraryCustomers &&
            group.Members.Contains(p.Id, StringComparer.Ordinal) && p.Devices.Any(d => d.Enabled)) ||
            !policy.RecoveryDevices.Any(d => d.Enabled))
            throw new InvalidOperationException("Mindestens ein vollständiger persönlicher Zugang und Recovery erforderlich.");
        var customerIds = full.Catalog.Customers.Select(c => c.CustomerId).ToHashSet();
        if (full.Catalog.Keys.Any(k => !customerIds.Contains(k.CustomerId)))
            throw new InvalidDataException("Schlüssel ohne registrierten Kunden.");
        var entries = full.Catalog.Keys.ToLookup(k => k.CustomerId);
        var secrets = full.Secrets.ToDictionary(s => s.Key);
        var blocks = new List<CustomerKeyLibraryBlock>();
        foreach (var customer in full.Catalog.Customers.OrderBy(c => c.CustomerId))
        {
            var keys = entries[customer.CustomerId].ToArray();
            var payload = new KeyLibraryVaultPayload(new(policy.LibraryId, full.Catalog.Revision, keys)
                { Customers = [customer] }, keys.Select(k => secrets[new(k.KeyId, k.Version)]).ToArray());
            var document = GroupVaultService.Protect(System.Text.Encoding.UTF8.GetString(KeyLibrarySyncWire.Write(payload)), verified,
                customer.CustomerId, groupId, now, GroupVaultPurpose.CustomerKeyLibrary);
            blocks.Add(new(customer.CustomerId, document));
        }
        var bundle = new ScopedKeyLibraryBundle(Format, 1, policy.LibraryId, groupId,
            verified.Checkpoint, full.Catalog.Revision, blocks.ToArray());
        ValidateBundle(bundle, verified, groupId);
        var bytes = KeyLibrarySyncWire.Write(bundle);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Kundenbibliothek ist zu groß.");
        return new(bytes, authority.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    /// <summary>Verify the entire signed bundle using the already pinned
    /// enrollment. This does not decrypt any customer data.</summary>
    public static ScopedKeyLibraryBundle Verify(SignedScopedKeyLibraryBundle signed,
        GroupEnrollmentPackage enrollment, string groupId)
    {
        ArgumentNullException.ThrowIfNull(signed);
        var policy = GroupEnrollmentPackages.Validate(enrollment);
        using var authority = OfflineGroupDelegationService.OpenPublicKey(enrollment.AdministratorPublicKey);
        if (signed.Payload is not { Length: > 0 and <= MaximumBytes } ||
            signed.Signature is null || signed.Signature.Length != authority.KeySize / 8)
            throw new CryptographicException("Ungültige signierte Kundenbibliothek.");
        var bytes = signed.Payload.ToArray();
        if (!authority.VerifyData(bytes, signed.Signature.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Signatur der Kundenbibliothek ungültig.");
        var bundle = KeyLibrarySyncWire.Read<ScopedKeyLibraryBundle>(bytes, MaximumBytes);
        ValidateBundle(bundle, policy, groupId);
        return bundle;
    }

    /// <summary>Open only authorized blocks using a single personal device.
    /// The result is a projection, never the authoritative editable catalog.
    /// requireFull is mandatory for future administrative mutation paths.</summary>
    public static KeyLibraryVaultPayload Open(SignedScopedKeyLibraryBundle signed,
        OfflineGroupTrustStore trust, string groupId, X509Certificate2 identity,
        DateTimeOffset now, bool requireFull = false, bool recovery = false)
    {
        var enrolled = trust.ExportPublic();
        var verified = GroupEnrollmentPackages.Validate(enrolled);
        var bundle = Verify(signed, enrolled, groupId);
        var policy = verified.Policy;
        var fingerprint = CertificateVaultService.Fingerprint(identity);
        if (!identity.HasPrivateKey || now.UtcDateTime < identity.NotBefore.ToUniversalTime() ||
            now.UtcDateTime >= identity.NotAfter.ToUniversalTime())
            throw new CryptographicException("Aktiver privater Zugang erforderlich.");
        var encoded = Convert.ToBase64String(identity.RawData);
        bool Matches(OfflineVaultDevice d) => d.Enabled && d.Fingerprint == fingerprint && d.Certificate == encoded;
        OfflineVaultPerson? person = null;
        if (recovery)
        {
            if (!policy.RecoveryDevices.Any(Matches)) throw new CryptographicException("Separater Recovery-Zugang fehlt.");
        }
        else
        {
            var group = RequireGroup(policy, groupId);
            person = policy.People.SingleOrDefault(p => p.Enabled && group.Members.Contains(p.Id, StringComparer.Ordinal) &&
                p.Devices.Any(Matches)) ?? throw new CryptographicException("Aktiver persönlicher Gruppenzugang fehlt.");
            if (requireFull && !person.AllKeyLibraryCustomers)
                throw new CryptographicException("Vollständige Kundenfreigabe erforderlich; Teilbestand ist nicht editierbar.");
        }
        var customers = new List<KeyLibraryCustomer>();
        var keys = new List<KeyLibraryEntry>();
        var secrets = new List<KeyLibrarySecret>();
        foreach (var block in bundle.Customers)
        {
            if (!recovery && !OfflineGroupDirectory.CanDecryptCustomer(policy, person!, block.CustomerId)) continue;
            var json = GroupVaultService.Unprotect(block.Vault, verified, block.CustomerId, groupId,
                [identity], recovery, GroupVaultPurpose.CustomerKeyLibrary);
            var payload = KeyLibrarySyncWire.Read<KeyLibraryVaultPayload>(
                System.Text.Encoding.UTF8.GetBytes(json), MaximumBytes);
            KeyLibraryVault.Validate(payload);
            if (payload.Catalog.LibraryId != bundle.LibraryId || payload.Catalog.Revision != bundle.CatalogRevision ||
                payload.Catalog.Customers.Length != 1 || payload.Catalog.Customers[0].CustomerId != block.CustomerId ||
                payload.Catalog.Keys.Any(k => k.CustomerId != block.CustomerId))
                throw new CryptographicException("Kundenblock passt nicht zur signierten Bibliothek und Revision.");
            customers.Add(payload.Catalog.Customers[0]);
            keys.AddRange(payload.Catalog.Keys); secrets.AddRange(payload.Secrets);
        }
        if (trust.LoadOffline().Checkpoint != verified.Checkpoint)
            throw new CryptographicException("Kundenrichtlinie wurde während der Entschlüsselung geändert.");
        var result = new KeyLibraryVaultPayload(new(bundle.LibraryId, bundle.CatalogRevision, keys.ToArray())
            { Customers = customers.ToArray() }, secrets.ToArray());
        KeyLibraryVault.Validate(result);
        return result;
    }

    private static OfflineVaultGroup RequireGroup(OfflineGroupPolicy policy, string groupId) =>
        policy.Groups.SingleOrDefault(g => g.Id == groupId) ??
        throw new CryptographicException("Bibliotheksgruppe fehlt.");

    private static void ValidateBundle(ScopedKeyLibraryBundle bundle, VerifiedGroupPolicy verified, string groupId)
    {
        var policy = verified.Policy;
        var group = RequireGroup(policy, groupId);
        if (!policy.CustomerScopesEnabled || bundle.Format != Format || bundle.Version != 1 ||
            bundle.LibraryId != policy.LibraryId || bundle.GroupId != groupId ||
            bundle.Policy != verified.Checkpoint || bundle.CatalogRevision < 1 ||
            bundle.Customers is null || bundle.Customers.Length > MaximumCustomers)
            throw new CryptographicException("Kundenbibliothek passt nicht zur bestätigten Richtlinie.");
        var ids = new HashSet<Guid>();
        var generations = new HashSet<Guid>();
        var recovery = policy.RecoveryDevices.Where(d => d.Enabled).Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
        foreach (var block in bundle.Customers)
        {
            if (block is null || block.CustomerId == Guid.Empty || !ids.Add(block.CustomerId))
                throw new InvalidDataException("Kundenblock fehlt oder ist doppelt.");
            // Existing serializer verifies structure, recipient uniqueness and sizes.
            _ = GroupVaultService.Write(block.Vault);
            var h = block.Vault.Header;
            var allowed = policy.People.Where(p => p.Enabled && group.Members.Contains(p.Id, StringComparer.Ordinal) &&
                OfflineGroupDirectory.CanDecryptCustomer(policy, p, block.CustomerId))
                .SelectMany(p => p.Devices).Where(d => d.Enabled).Select(d => d.Fingerprint).ToHashSet(StringComparer.Ordinal);
            if (h.Format != GroupVaultService.CustomerKeyLibraryFormat || h.LibraryId != bundle.LibraryId ||
                h.ProjectId != block.CustomerId || h.GroupId != groupId || h.Policy != verified.Checkpoint ||
                !generations.Add(h.KeyGeneration) || allowed.Count == 0 || recovery.Count == 0 ||
                !allowed.SetEquals(h.Recipients.Where(r => r.Role == "member").Select(r => r.Fingerprint)) ||
                !recovery.SetEquals(h.Recipients.Where(r => r.Role == "recovery").Select(r => r.Fingerprint)))
                throw new CryptographicException("Kundenblock oder Empfängerkreis stimmt nicht mit der signierten Freigabe überein.");
        }
    }
}
