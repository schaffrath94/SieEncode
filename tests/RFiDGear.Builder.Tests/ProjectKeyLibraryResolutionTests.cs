using System.Security.Cryptography;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class ProjectKeyLibraryResolutionTests
{
    public static void Verify()
    {
        var customer = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var profiles = ProfileLibrary.FromProfiles(
        [
            new ProfileDefinition
            {
                Profile = new ProfileMetadata { Id = "root", Kind = "base", Name = "Synthetic root" },
                Applications =
                [
                    new ApplicationDefinition { Id = "A1B2C3", Name = "Access", KeyType = "DF_KEY_AES", KeyCount = 2 },
                    new ApplicationDefinition { Id = "D4E5F6", Name = "Service", KeyType = "DF_KEY_3K3DES", KeyCount = 1 }
                ]
            }
        ]);
        var project = new ProjectDefinition
        {
            Project = new ProjectSection { Id = Guid.NewGuid(), Name = "Synthetic cards",
                Build = ["Mastering", "Coding"] },
            Profiles = new ProfileSelection { Base = "root" },
            Transport = new TransportConfiguration
            {
                KeySetId = "standard",
                PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('B', 32) },
                ApplicationKeys = new Dictionary<string, string>
                {
                    ["DF_KEY_AES"] = new string('B', 32),
                    ["DF_KEY_3K3DES"] = new string('B', 48)
                }
            },
            Productive = new ProductiveConfiguration
            {
                PiccKey = new KeyReference { Type = "DF_KEY_AES", Value = new string('B', 32) },
                ApplicationKeys = new Dictionary<string, List<string>>
                {
                    ["A1B2C3"] = [new string('B', 32), new string('B', 32)],
                    ["D4E5F6"] = [new string('B', 48)]
                }
            }
        };
        var required = ProjectKeyLibraryResolution.RequiredSlots(project, profiles);
        if (required.Count != 7 ||
            required[ProjectKeyLibraryResolution.TransportPicc] != "DF_KEY_AES" ||
            required[ProjectKeyLibraryResolution.ProductiveApplication("a1b2c3", 1)] != "DF_KEY_AES")
            throw new Exception("Profile did not produce all required exact key slots.");

        var entries = new List<KeyLibraryEntry>();
        var secrets = new List<KeyLibrarySecret>();
        var links = new List<ProjectKeyLibraryBinding>();
        var keyCounter = 0;
        foreach (var (slot, type) in required)
        {
            var keyId = Guid.NewGuid();
            var addr = new KeyLibraryAddress(keyId, 1);
            var hex = new string("0123456789ABCDEF"[keyCounter++], type == "DF_KEY_3K3DES" ? 48 : 32);
            entries.Add(new KeyLibraryEntry(customer, keyId, 1, slot, type, slot, "SIPORT", KeyLibraryContracts.Active));
            secrets.Add(new KeyLibrarySecret(addr, hex));
            links.Add(new ProjectKeyLibraryBinding(slot, addr));
        }

        var catalog = new KeyLibrarySnapshot(libraryId, 1, entries.ToArray());
        var vault = new KeyLibraryVaultPayload(catalog, secrets.ToArray());
        KeyLibraryVault.Validate(vault);
        var bound = ProjectKeyLibraryResolution.Bind(project, profiles, catalog, customer, links);
        if (bound.Project.KeyLibrary?.LibraryId != libraryId || bound.Project.KeyLibrary.CustomerId != customer ||
            !string.IsNullOrEmpty(bound.Transport.PiccKey.Value) ||
            !string.IsNullOrEmpty(bound.Productive.PiccKey.Value) ||
            bound.Transport.ApplicationKeys.Values.Any(x => !string.IsNullOrEmpty(x)) ||
            bound.Productive.ApplicationKeys.Values.SelectMany(x => x).Any(x => !string.IsNullOrEmpty(x)))
            throw new Exception("Project library binding must preserve only key-free references.");
        if (project.Project.KeyLibrary is not null || project.Transport.PiccKey.Value != new string('B', 32))
            throw new Exception("Binding unexpectedly changed the source project draft.");

        var encoded = JsonSerializer.Serialize(bound, ProjectJson.Options);
        if (encoded.Contains(new string('B', 32), StringComparison.Ordinal) ||
            !encoded.Contains(nameof(ProjectSection.KeyLibrary), StringComparison.Ordinal))
            throw new Exception("Serialized project contains a raw key or lost its library bindings.");
        var projectFile = Path.Combine(Path.GetTempPath(), "SieEncode-key-bind-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ProjectJson.Save(projectFile, bound);
            var original = File.ReadAllBytes(projectFile);
            var revision = ConcurrentFileWriter.Capture(projectFile);
            Reject<InvalidDataException>(() => ProjectJson.Save(projectFile, CloneWithRawSecret()));
            Reject<InvalidDataException>(() => ProjectJson.Save(projectFile, CloneWithRawSecret(), revision));
            if (!File.ReadAllBytes(projectFile).SequenceEqual(original))
                throw new Exception("A rejected project write damaged the existing key-free document.");
            File.WriteAllText(projectFile, JsonSerializer.Serialize(CloneWithRawSecret(), ProjectJson.Options));
            Reject<InvalidDataException>(() => ProjectJson.Load(projectFile));
        }
        finally { if (File.Exists(projectFile)) File.Delete(projectFile); }

        var reloaded = JsonSerializer.Deserialize<ProjectDefinition>(encoded, ProjectJson.Options)!;
        if (reloaded.Project.KeyLibrary?.Bindings.Length != 7 ||
            reloaded.Project.KeyLibrary.Bindings[0].Key.Version != 1)
            throw new Exception("Exact library references did not survive project JSON.");

        var hydrated = ProjectKeyLibraryResolution.ResolveForBuild(reloaded, profiles, vault);
        if (hydrated.Transport.PiccKey.Value != secrets[0].HexValue ||
            hydrated.Productive.PiccKey.Value != secrets[1].HexValue ||
            hydrated.Productive.ApplicationKeys["A1B2C3"][1] !=
            secrets.Single(s => s.Key == links.Single(l => l.Slot == "productive/application/A1B2C3/1").Key).HexValue)
            throw new Exception("The correct key version was not mapped to existing project fields.");

        var payload = ProjectKeyLibraryResolution.ResolveTransientPayload(reloaded, profiles, vault);
        var rebuilt = ProjectKeyVault.Apply(reloaded, payload);
        if (rebuilt.Productive.PiccKey.Value != hydrated.Productive.PiccKey.Value ||
            rebuilt.Transport.ApplicationKeys["DF_KEY_3K3DES"] != hydrated.Transport.ApplicationKeys["DF_KEY_3K3DES"] ||
            rebuilt.Productive.ApplicationKeys["D4E5F6"][0] != hydrated.Productive.ApplicationKeys["D4E5F6"][0])
            throw new Exception("The existing ProjectKeyVault adapter lost a catalog key slot.");

        var truncated = links.Take(links.Count - 1).ToArray();
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, catalog, customer, truncated));
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, catalog, customer,
            links.Select((link, index) => index == 1 ? link with { Slot = links[0].Slot } : link).ToArray()));
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, catalog, customer,
            links.Select((link, index) => index == 1 ? link with { Key = new KeyLibraryAddress(Guid.NewGuid(), 1) } : link).ToArray()));
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, catalog, customer,
            links.Select((link, index) => index == 1 ? link with { Key = links.Last().Key } : link).ToArray()));

        var crossCustomer = catalog with { Keys = entries.Select((key, i) =>
            i == 1 ? key with { CustomerId = otherCustomer } : key).ToArray() };
        Reject<CryptographicException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, crossCustomer, customer, links));
        var retired = catalog with { Keys = entries.Select((key, i) =>
            i == 1 ? key with { Status = KeyLibraryContracts.Retired } : key).ToArray() };
        Reject<InvalidOperationException>(() => ProjectKeyLibraryResolution.Bind(project, profiles, retired, customer, links));
        Reject<CryptographicException>(() => ProjectKeyLibraryResolution.ResolveForBuild(
            reloaded, profiles, vault with { Catalog = catalog with { LibraryId = Guid.NewGuid() } }));
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.ResolveForBuild(
            reloaded, profiles, vault with { Catalog = catalog with { Keys = entries.Skip(1).ToArray() },
                Secrets = secrets.Skip(1).ToArray() }));
        Reject<InvalidDataException>(() => ProjectKeyLibraryResolution.ResolveForBuild(
            CloneWithRawSecret(), profiles, vault));
        Reject<InvalidOperationException>(() => ProjectKeyLibraryResolution.ResolveForBuild(project, profiles, vault));

        var both = ProjectJson.WithoutAllSecrets(project);
        both.Project.GroupVault = new ProjectGroupVaultBinding(libraryId, "keys");
        Reject<InvalidOperationException>(() => ProjectKeyLibraryResolution.Bind(both, profiles, catalog, customer, links));

        ProjectDefinition CloneWithRawSecret()
        {
            var copy = JsonSerializer.Deserialize<ProjectDefinition>(encoded, ProjectJson.Options)!;
            copy.Productive.PiccKey.Value = new string('B', 32);
            return copy;
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Invalid project-library binding was accepted: " + typeof(T).Name);
    }
}
