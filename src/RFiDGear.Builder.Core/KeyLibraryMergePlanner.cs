using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

public enum KeyLibraryMergeIssueKind
{
    OlderSource, MissingBaseCustomer, MissingBaseVersion, CustomerMetadata,
    DuplicateCustomerName, ImmutableIdentity, SecretValue, KeyMetadata, Status,
    ReusedGenerationValue, MissingCustomer, RevisionExhausted, InvalidCombinedCatalog
}

// No customer names, key metadata text or secrets in a preview. IDs identify
// entries for later protected review; source IDs refer to the verified inventory.
public sealed record KeyLibraryMergeIssue(KeyLibraryMergeIssueKind Kind, Guid? CustomerId,
    Guid? KeyId, int? Version, Guid[] SourceIds);

public sealed record KeyLibraryMergePreview(KeyLibraryDisasterReport Inventory, Guid BaseSourceId,
    KeyLibraryMergeIssue[] Issues, int AddedCustomers, int AddedKeyVersions,
    long? ProposedRevision)
{
    public bool CanPrepareCandidate => Issues.Length == 0 && ProposedRevision.HasValue;

    // Hash only public, key-free preview data and the exact central CAS base.
    // The preview order is intentionally bound too. This is a local approval
    // fingerprint, not a secret hash or independent authority signature.
    public string ApprovalSha256(CentralKeyLibraryHead expectedHead) =>
        Convert.ToHexString(SHA256.HashData(KeyLibrarySyncWire.Write(new
        {
            Format = "SieEncode-Merge-Preview-Approval-v1",
            Head = expectedHead,
            Preview = this
        })));
}

// The only externally returned candidate is an already signed encrypted CAS
// transfer. PreviewSha256 is local evidence, not a signed provenance receipt.
public sealed record KeyLibraryPreparedMerge(KeyLibraryMergePreview Preview,
    string PreviewSha256, CentralKeyLibraryHead ExpectedHead, SignedKeyLibrarySync Transfer);

// The in-memory candidate never leaves the internal domain boundary. A later
// operation must reverify the sources, obtain approval and encrypt/sign it.
internal static class KeyLibraryMergePlanner
{
    internal sealed record Result(KeyLibraryMergePreview Preview, KeyLibraryVaultPayload? Candidate);
    private const int MaximumIssues = 10000;

    internal static Result Build(KeyLibraryDisasterReport inventory, Guid baseId,
        IReadOnlyDictionary<Guid, KeyLibraryVaultPayload> sources)
    {
        var baseline = sources[baseId];
        var ordered = sources.OrderBy(s => s.Key).ToArray();
        var issues = new List<KeyLibraryMergeIssue>();
        void Issue(KeyLibraryMergeIssueKind kind, Guid? customer, Guid? key, int? version,
            IEnumerable<Guid> ids)
        {
            if (issues.Count >= MaximumIssues)
                throw new InvalidDataException("Zu viele Konflikte für eine begrenzte Zusammenführungsvorschau.");
            issues.Add(new(kind, customer, key, version, ids.Distinct().OrderBy(id => id).ToArray()));
        }

        // Missing history is not a deletion request. Even a genuine signed
        // branch cannot authorize silent loss of any baseline ID or version.
        foreach (var (id, payload) in ordered)
        {
            KeyLibraryVault.Validate(payload);
            var customers = payload.Catalog.Customers.Select(c => c.CustomerId).ToHashSet();
            var keys = KeyLibraryContracts.Validate(payload.Catalog);
            if (payload.Catalog.Revision < baseline.Catalog.Revision)
                Issue(KeyLibraryMergeIssueKind.OlderSource, null, null, null, [baseId, id]);
            foreach (var customer in baseline.Catalog.Customers)
                if (!customers.Contains(customer.CustomerId))
                    Issue(KeyLibraryMergeIssueKind.MissingBaseCustomer, customer.CustomerId, null, null, [baseId, id]);
            foreach (var key in baseline.Catalog.Keys)
                if (!keys.ContainsKey(new(key.KeyId, key.Version)))
                    Issue(KeyLibraryMergeIssueKind.MissingBaseVersion, key.CustomerId, key.KeyId, key.Version, [baseId, id]);
        }

        var customerGroups = ordered.SelectMany(s => s.Value.Catalog.Customers
            .Select(c => (Source: s.Key, Customer: c))).GroupBy(x => x.Customer.CustomerId).ToArray();
        var customersCombined = new List<KeyLibraryCustomer>();
        foreach (var group in customerGroups)
        {
            var first = group.First().Customer;
            if (group.Any(x => x.Customer != first))
                Issue(KeyLibraryMergeIssueKind.CustomerMetadata, group.Key, null, null, group.Select(x => x.Source));
            customersCombined.Add(first);
        }
        foreach (var group in customersCombined.GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
            foreach (var customer in group)
                Issue(KeyLibraryMergeIssueKind.DuplicateCustomerName, customer.CustomerId, null, null,
                    customerGroups.Single(g => g.Key == customer.CustomerId).Select(x => x.Source));

        var keyGroups = ordered.SelectMany(s =>
        {
            var values = s.Value.Secrets.ToDictionary(k => k.Key);
            return s.Value.Catalog.Keys.Select(k =>
                (Source: s.Key, Entry: k, Secret: values[new(k.KeyId, k.Version)]));
        }).GroupBy(x => new KeyLibraryAddress(x.Entry.KeyId, x.Entry.Version)).ToArray();
        var entries = new List<KeyLibraryEntry>();
        var secrets = new List<KeyLibrarySecret>();
        foreach (var group in keyGroups)
        {
            var first = group.First();
            var ids = group.Select(x => x.Source);
            var key = first.Entry;
            if (group.Any(x => x.Entry.CustomerId != key.CustomerId || x.Entry.KeyType != key.KeyType))
                Issue(KeyLibraryMergeIssueKind.ImmutableIdentity, null, key.KeyId, key.Version, ids);
            if (group.Any(x => !string.Equals(x.Secret.HexValue, first.Secret.HexValue, StringComparison.OrdinalIgnoreCase)))
                Issue(KeyLibraryMergeIssueKind.SecretValue, null, key.KeyId, key.Version, ids);
            if (group.Any(x => (x.Entry.Name, x.Entry.Purpose, x.Entry.HostSystem) != (key.Name, key.Purpose, key.HostSystem)))
                Issue(KeyLibraryMergeIssueKind.KeyMetadata, null, key.KeyId, key.Version, ids);
            if (group.Any(x => x.Entry.Status != key.Status))
                Issue(KeyLibraryMergeIssueKind.Status, null, key.KeyId, key.Version, ids);
            entries.Add(key);
            secrets.Add(first.Secret);
        }

        // Also check across distinct generations: each individual branch may
        // be valid while its union changes the customer/type of a stable ID.
        var knownCustomers = customersCombined.Select(c => c.CustomerId).ToHashSet();
        var sourceIdsByKey = keyGroups.GroupBy(g => g.Key.KeyId).ToDictionary(g => g.Key,
            g => g.SelectMany(v => v.Select(x => x.Source)).Distinct().ToArray());
        foreach (var group in entries.GroupBy(k => k.KeyId))
        {
            var first = group.First();
            if (group.Any(k => k.CustomerId != first.CustomerId || k.KeyType != first.KeyType))
                Issue(KeyLibraryMergeIssueKind.ImmutableIdentity, null, group.Key, null, sourceIdsByKey[group.Key]);
            if (group.Any(k => !knownCustomers.Contains(k.CustomerId)))
                Issue(KeyLibraryMergeIssueKind.MissingCustomer, null, group.Key, null, sourceIdsByKey[group.Key]);
        }
        foreach (var group in secrets.GroupBy(s => s.Key.KeyId))
            if (group.Select(s => s.HexValue).Distinct(StringComparer.OrdinalIgnoreCase).Count() != group.Count())
                Issue(KeyLibraryMergeIssueKind.ReusedGenerationValue, null, group.Key, null, sourceIdsByKey[group.Key]);

        var baseCustomers = baseline.Catalog.Customers.Select(c => c.CustomerId).ToHashSet();
        var baseKeys = KeyLibraryContracts.Validate(baseline.Catalog);
        var addedCustomers = customersCombined.Count(c => !baseCustomers.Contains(c.CustomerId));
        var addedVersions = entries.Count(k => !baseKeys.ContainsKey(new(k.KeyId, k.Version)));
        var maxRevision = ordered.Max(s => s.Value.Catalog.Revision);
        if (maxRevision == long.MaxValue && (addedCustomers > 0 || addedVersions > 0))
            Issue(KeyLibraryMergeIssueKind.RevisionExhausted, null, null, null, sources.Keys);

        KeyLibraryVaultPayload? candidate = null;
        if (issues.Count == 0 && (addedCustomers > 0 || addedVersions > 0))
        {
            var proposed = new KeyLibraryVaultPayload(new(baseline.Catalog.LibraryId,
                checked(maxRevision + 1), entries.OrderBy(k => k.KeyId).ThenBy(k => k.Version).ToArray())
                { Customers = customersCombined.OrderBy(c => c.CustomerId).ToArray() },
                secrets.OrderBy(s => s.Key.KeyId).ThenBy(s => s.Key.Version).ToArray());
            try
            {
                KeyLibraryVault.Validate(proposed);
                foreach (var source in ordered)
                    KeyLibraryVaultStore.ValidateImmutableHistory(source.Value, proposed);
                candidate = proposed;
            }
            catch (InvalidDataException)
            {
                Issue(KeyLibraryMergeIssueKind.InvalidCombinedCatalog, null, null, null, sources.Keys);
            }
        }
        return new(new(inventory, baseId, issues.ToArray(), addedCustomers, addedVersions,
            candidate?.Catalog.Revision), candidate);
    }
}
