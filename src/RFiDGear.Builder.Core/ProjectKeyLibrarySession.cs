using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>Key-free evidence of the exact locally authorized session basis.
/// Not proof of fresh online authorization or a hardware acceptance.</summary>
public sealed record ProjectKeyLibrarySessionBasis(Guid LibraryId, string GroupId,
    GroupPolicyCheckpoint Policy, FileRevision VaultFile, long CatalogRevision,
    string PersonId, string DeviceFingerprint, Guid[] Customers);

public static class ProjectKeyLibrarySession
{
    public static (ProjectDefinition Hydrated, ProjectKeyLibrarySessionBasis Basis) Resolve(
        ProjectDefinition project, ProfileLibrary profiles, string path,
        OfflineGroupTrustStore trust, string group, X509Certificate2 identity, DateTimeOffset now)
    {
        ProjectKeyLibraryResolution.EnsureSafeStoredProject(project);
        var binding = project.Project.KeyLibrary ??
            throw new InvalidOperationException("Projekt besitzt keine Bibliotheksbindung.");
        var policy = trust.LoadOffline();
        if (policy.Policy.LibraryId != binding.LibraryId)
            throw new CryptographicException("Projekt gehört zu einer anderen Bibliothek.");
        var actor = KeyLibraryDisclosure.RequireMember(policy, group, identity, now);
        var customers = binding.Bindings.Select(b => b.CustomerId ?? binding.CustomerId)
            .Append(binding.CustomerId).Distinct().OrderBy(id => id).ToArray();
        RequireCustomers(policy.Policy, actor.PersonId, customers);
        var file = ConcurrentFileWriter.Capture(path);
        if (!file.Exists) throw new FileNotFoundException("Freigegebene lokale Schlüsselbibliothek fehlt.");
        var bytes = KeyLibraryVaultStore.ReadBytes(path);
        if (!policy.Policy.CustomerScopesEnabled &&
            GroupVaultService.Read(bytes).Header.Policy != policy.Checkpoint)
            throw new CryptographicException("Bibliotheks-Vault und bestätigter Gruppenstand stimmen nicht überein.");
        var unlocked = KeyLibraryVaultStore.OpenEncrypted(bytes, trust, group, [identity]);
        var hydrated = ProjectKeyLibraryResolution.ResolveForBuild(project, profiles, unlocked);
        var basis = new ProjectKeyLibrarySessionBasis(binding.LibraryId, group, policy.Checkpoint,
            file, unlocked.Catalog.Revision, actor.PersonId, actor.DeviceFingerprint, customers);
        RequireCurrent(basis, path, trust, identity, now);
        return (hydrated, basis);
    }

    public static void RequireCurrent(ProjectKeyLibrarySessionBasis basis, string path,
        OfflineGroupTrustStore trust, X509Certificate2 identity, DateTimeOffset now)
    {
        var policy = trust.LoadOffline();
        if (policy.Policy.LibraryId != basis.LibraryId || policy.Checkpoint != basis.Policy ||
            ConcurrentFileWriter.Capture(path) != basis.VaultFile)
            throw new InvalidOperationException("Bibliotheksstand wurde geändert. Projekt erneut für eine lokale Sitzung öffnen.");
        var actor = KeyLibraryDisclosure.RequireMember(policy, basis.GroupId, identity, now);
        if (actor.PersonId != basis.PersonId || actor.DeviceFingerprint != basis.DeviceFingerprint)
            throw new CryptographicException("Persönlicher Sitzungszugang wurde geändert.");
        RequireCustomers(policy.Policy, actor.PersonId, basis.Customers);
    }

    private static void RequireCustomers(OfflineGroupPolicy policy, string personId, Guid[] customers)
    {
        if (!policy.CustomerScopesEnabled) return;
        var person = policy.People.Single(p => p.Id == personId);
        if (customers.Any(id => !OfflineGroupDirectory.CanDecryptCustomer(policy, person, id)))
            throw new CryptographicException("Nicht alle benötigten Kundenbereiche sind für dieselbe Person freigegeben.");
    }
}
