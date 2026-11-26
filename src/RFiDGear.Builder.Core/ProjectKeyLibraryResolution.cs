using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

/// <summary>
/// Persisted key-free catalog binding. Each project slot points to one exact
/// immutable key generation, never to a mutable name or implicit "latest".
/// </summary>
public sealed record ProjectKeyLibrarySelection(Guid LibraryId, Guid CustomerId,
    ProjectKeyLibraryBinding[] Bindings);

/// <summary>
/// The existing ProjectKeyVaultPayload remains the transient adapter into
/// ProjectKeyVault.Apply and the existing RFiDGear build pipeline. This service
/// does not save any copied raw values into project files or a second Vault.
/// </summary>
public static class ProjectKeyLibraryResolution
{
    public const string TransportPicc = "transport/picc";
    public const string ProductivePicc = "productive/picc";

    public static string TransportApplication(string keyType)
    {
        if (keyType is not ("DF_KEY_AES" or "DF_KEY_DES" or "DF_KEY_3K3DES"))
            throw new InvalidDataException("Unbekannter Transport-Schlüsseltyp.");
        return "transport/application/" + keyType;
    }

    public static string ProductiveApplication(string aid, int keyNumber)
    {
        if (keyNumber is < 0 or > 13)
            throw new InvalidDataException("Ungültige DESFire-Keynummer.");
        return "productive/application/" + ProfileValidator.NormalizeAid(aid) + "/" + keyNumber;
    }

    /// <summary>
    /// Define the exact set of slots from the current immutable profile composition.
    /// Never infer key requirements by looking at a customer's existing raw values.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RequiredSlots(ProjectDefinition project,
        ProfileLibrary profiles)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profiles);

        var composition = profiles.Compose(project.Profiles.Base, project.Profiles.Customers);
        var applications = CardOperationMode.SelectApplications(composition, project);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TransportPicc] = RequireType(project.Transport.PiccKey?.Type)
        };
        if (!CardOperationMode.IsExtension(project))
            expected.Add(ProductivePicc, RequireType(project.Productive.PiccKey?.Type));

        foreach (var type in applications.Select(item => item.KeyType).Distinct(StringComparer.Ordinal))
            expected.Add(TransportApplication(type), RequireType(type));
        foreach (var app in applications)
        {
            if (app.KeyCount is < 1 or > 14)
                throw new InvalidDataException("Ungültige Anzahl produktiver Anwendungsschlüssel.");
            for (var i = 0; i < app.KeyCount; i++)
            {
                var slot = ProductiveApplication(app.Id, i);
                if (!expected.TryAdd(slot, RequireType(app.KeyType)))
                    throw new InvalidDataException("Mehrfach belegter Anwendungsschlüsselplatz.");
            }
        }
        return expected;
    }

    /// <summary>
    /// Bind a draft explicitly, stripping all prior raw values in favor of
    /// version-pinned references. Caller presents the approved mapping from the
    /// one shared key library, not a collection of new project secrets.
    /// </summary>
    public static ProjectDefinition Bind(ProjectDefinition draft, ProfileLibrary profiles,
        KeyLibrarySnapshot catalog, Guid customerId, IEnumerable<ProjectKeyLibraryBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(bindings);
        if (draft.Project.GroupVault is not null)
            throw new InvalidOperationException("Projekt darf nicht gleichzeitig einen unabhängigen Gruppen-Vault und eine zentrale Schlüsselbibliothek führen.");

        var assigned = bindings.ToArray();
        ValidateBindings(draft, profiles, catalog, customerId, assigned);
        var output = ProjectJson.WithoutAllSecrets(draft);
        output.Project.KeyLibrary = new ProjectKeyLibrarySelection(catalog.LibraryId, customerId, assigned);
        return output;
    }

    /// <summary>
    /// Unlocking/authorization occurs before this call. This is an ephemeral
    /// hydration for the existing project builder; project JSON stays key-free.
    /// </summary>
    public static ProjectDefinition ResolveForBuild(ProjectDefinition project, ProfileLibrary profiles,
        KeyLibraryVaultPayload unlockedLibrary)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(unlockedLibrary);
        var binding = project.Project.KeyLibrary ??
            throw new InvalidOperationException("Projekt hat keine freigegebene Schlüsselbibliotheksbindung.");
        if (project.Project.GroupVault is not null)
            throw new InvalidOperationException("Dauerhafte Projekt- und Bibliotheks-Vaults dürfen nicht parallel verwendet werden.");
        EnsureNoEmbeddedSecrets(project);
        KeyLibraryVault.Validate(unlockedLibrary);
        if (binding.LibraryId != unlockedLibrary.Catalog.LibraryId)
            throw new CryptographicException("Projekt gehört nicht zur entsperrten Schlüsselbibliothek.");

        var required = ValidateBindings(project, profiles, unlockedLibrary.Catalog,
            binding.CustomerId, binding.Bindings);
        var lookup = binding.Bindings.ToDictionary(item => item.Slot, item => item, StringComparer.Ordinal);

        var hydrated = ProjectJson.WithoutAllSecrets(project);
        hydrated.Transport.PiccKey = Resolve(TransportPicc);
        hydrated.Transport.ApplicationKeys = required.Where(x => x.Key.StartsWith("transport/application/", StringComparison.Ordinal))
            .ToDictionary(x => x.Value, x => Resolve(x.Key).Value, StringComparer.Ordinal);
        if (required.ContainsKey(ProductivePicc))
            hydrated.Productive.PiccKey = Resolve(ProductivePicc);
        hydrated.Productive.ApplicationKeys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var applicationSlot in required.Keys.Where(x => x.StartsWith("productive/application/", StringComparison.Ordinal)))
        {
            var parts = applicationSlot.Split('/');
            var aid = parts[2];
            var index = int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            if (!hydrated.Productive.ApplicationKeys.TryGetValue(aid, out var values))
            {
                values = [];
                hydrated.Productive.ApplicationKeys.Add(aid, values);
            }
            if (values.Count != index)
                throw new InvalidDataException("Lückenhafter oder unsortierter produktiver Keyset-Slot.");
            values.Add(Resolve(applicationSlot).Value);
        }
        hydrated.Project.KeyLibrary = null;
        hydrated.TransientKeyLibraryResolution = true;
        return hydrated;

        KeyReference Resolve(string slot) =>
            KeyLibraryKeyResolver.ResolveActive(unlockedLibrary,
                lookup[slot].CustomerId ?? binding.CustomerId, lookup[slot].Key, required[slot]);
    }

    public static ProjectKeyVaultPayload ResolveTransientPayload(ProjectDefinition keyFreeProject,
        ProfileLibrary profiles, KeyLibraryVaultPayload unlockedLibrary)
        => ProjectKeyVault.FromProject(ResolveForBuild(keyFreeProject, profiles, unlockedLibrary));

    private static IReadOnlyDictionary<string, string> ValidateBindings(ProjectDefinition project,
        ProfileLibrary profiles, KeyLibrarySnapshot catalog, Guid customerId, ProjectKeyLibraryBinding[] bindings)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (customerId == Guid.Empty || bindings is null)
            throw new InvalidDataException("Eindeutiger Kunde und vollständige Projekt-Schlüsselreferenzen erforderlich.");
        var required = RequiredSlots(project, profiles);
        var index = KeyLibraryContracts.Validate(catalog);
        if (bindings.Length != required.Count)
            throw new InvalidDataException("Projekt muss alle erforderlichen Schlüsselplätze genau einmal belegen.");
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding is null || binding.Slot is null || !required.TryGetValue(binding.Slot, out var expectedType) ||
                !used.Add(binding.Slot) || binding.Key is null ||
                !index.TryGetValue(binding.Key, out var entry))
                throw new InvalidDataException("Unbekannter, mehrfacher oder fehlender Bibliotheks-Schlüsselplatz.");
            if (binding.CustomerId == Guid.Empty)
                throw new InvalidDataException("Explizite Kundenkennung des Schlüsselplatzes darf nicht leer sein.");
            var slotCustomer = binding.CustomerId ?? customerId;
            if (entry.CustomerId != slotCustomer)
                throw new CryptographicException("Schlüsselplatz verweist auf einen anderen als den ausdrücklich zugeordneten Kunden.");
            if (!string.Equals(entry.KeyType, expectedType, StringComparison.Ordinal))
                throw new InvalidDataException("Schlüsseltyp stimmt nicht mit dem kartenseitigen Schlüsselplatz überein.");
            if (entry.Status != KeyLibraryContracts.Active)
                throw new InvalidOperationException("Dieser Schlüsselplatz verwendet eine abgelöste oder kompromittierte Version.");
        }
        return required;
    }

    /// <summary>
    /// Persisted project definitions with central bindings must never contain
    /// even one raw key. Used by ProjectJson.Load and both Save overloads.
    /// </summary>
    public static void EnsureSafeStoredProject(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var binding = project.Project.KeyLibrary;
        if (binding is null) return;
        if (project.Project.GroupVault is not null || binding.LibraryId == Guid.Empty ||
            binding.CustomerId == Guid.Empty || binding.Bindings is null || binding.Bindings.Length == 0)
            throw new InvalidDataException("Ungültige doppelte Vault-Bindung oder leere Bibliotheksreferenzen.");
        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in binding.Bindings)
        {
            if (item is null || item.Slot is not { Length: > 0 and <= 160 } ||
                item.Slot.Any(char.IsWhiteSpace) || !slots.Add(item.Slot) ||
                item.Key is null || item.Key.KeyId == Guid.Empty || item.Key.Version < 1 ||
                item.CustomerId == Guid.Empty)
                throw new InvalidDataException("Ungültiger oder doppelter Schlüsselplatz in gespeicherter Projektdefinition.");
        }
        EnsureNoEmbeddedSecrets(project);
    }

    private static void EnsureNoEmbeddedSecrets(ProjectDefinition project)
    {
        if (!string.IsNullOrWhiteSpace(project.Transport.PiccKey?.Value) ||
            project.Transport.ApplicationKeys.Values.Any(x => !string.IsNullOrWhiteSpace(x)) ||
            !string.IsNullOrWhiteSpace(project.Productive.PiccKey?.Value) ||
            project.Productive.ApplicationKeys.Values.SelectMany(v => v).Any(x => !string.IsNullOrWhiteSpace(x)))
            throw new InvalidDataException("Bibliotheksgebundene Projektdatei darf keine Klartextschlüssel enthalten.");
    }

    private static string RequireType(string? value) =>
        value is "DF_KEY_AES" or "DF_KEY_DES" or "DF_KEY_3K3DES"
            ? value
            : throw new InvalidDataException("Unbekannter kartenseitiger Schlüsseltyp im Projekt.");
}
