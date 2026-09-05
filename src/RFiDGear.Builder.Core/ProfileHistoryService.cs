using System.Text.Json;
using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public sealed record ProfileHistoryEntry(
    string ProfileId,
    string Version,
    DateTimeOffset? ModifiedAt,
    string Digest,
    string Path,
    bool IsCurrent,
    bool IntegrityValid,
    string Issue);

public sealed record ProfileRestoreResult(string ProfilePath, string RestoredFromPath, string Version, string PreviousVersion);

public sealed class ProfileHistoryService
{
    private readonly ProfileStore store;

    public ProfileHistoryService(ProfileStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<ProfileHistoryEntry> List(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        var current = store.Load().Get(profileId);
        if (!string.Equals(current.Profile.Kind, "customer", StringComparison.Ordinal))
            throw new InvalidDataException("Nur Kundenprofile besitzen eine wiederherstellbare Historie.");

        var entries = new List<ProfileHistoryEntry>
        {
            new(profileId, current.Profile.Version, current.Profile.ModifiedAt,
                ProfileIdentityService.ComputeDigest(current), CurrentPath(profileId), true, true, "")
        };
        entries.AddRange(store.ListHistory(profileId).Select(path => InspectSnapshot(profileId, path)));
        return entries.OrderByDescending(entry => entry.IsCurrent)
            .ThenByDescending(entry => entry.ModifiedAt)
            .ThenByDescending(entry => entry.Version, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ProfileDefinition Load(ProfileHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IntegrityValid) throw new InvalidDataException("Historienstand ist beschädigt und kann nicht verwendet werden: " + entry.Issue);
        if (entry.IsCurrent) return new ProfileEditingService().Clone(store.Load().Get(entry.ProfileId));
        return LoadAndValidateSnapshot(entry.ProfileId, entry.Path, verifyDigest: true);
    }

    public ProfileDiffReport CompareWithCurrent(ProfileHistoryEntry entry, DateTime? now = null)
    {
        var historical = Load(entry);
        var current = store.Load().Get(entry.ProfileId);
        return new ProfileDiffService().Compare(
            new ComposedProfiles([historical], historical.Applications, historical.Variables),
            new ComposedProfiles([current], current.Applications, current.Variables),
            $"Historie {historical.Profile.Version}", $"Aktuell {current.Profile.Version}", now);
    }

    public ProfileRestoreResult RestoreAsNewVersion(ProfileHistoryEntry entry, string newVersion)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsCurrent) throw new InvalidDataException("Der aktuelle Profilstand muss nicht wiederhergestellt werden.");
        newVersion = (newVersion ?? "").Trim();
        if (!Regex.IsMatch(newVersion, "^[A-Za-z0-9][A-Za-z0-9._+-]*$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Neue Profilversion darf nur Buchstaben, Ziffern sowie . _ + - enthalten.");

        var versions = List(entry.ProfileId).Select(item => item.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (versions.Contains(newVersion))
            throw new InvalidDataException($"Profilversion '{newVersion}' ist bereits aktuell oder in der Historie vorhanden. Bitte eine neue eindeutige Version vergeben.");

        var current = store.Load().Get(entry.ProfileId);
        var restored = Load(entry);
        restored.Profile.Version = newVersion;
        var path = store.SaveCustomer(restored);
        return new ProfileRestoreResult(path, entry.Path, newVersion, current.Profile.Version);
    }

    private ProfileHistoryEntry InspectSnapshot(string profileId, string path)
    {
        try
        {
            var profile = LoadAndValidateSnapshot(profileId, path, verifyDigest: true);
            return new(profileId, profile.Profile.Version, profile.Profile.ModifiedAt,
                ProfileIdentityService.ComputeDigest(profile), Path.GetFullPath(path), false, true, "");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new(profileId, "?", null, "", Path.GetFullPath(path), false, false, exception.Message);
        }
    }

    private ProfileDefinition LoadAndValidateSnapshot(string profileId, string path, bool verifyDigest)
    {
        var fullPath = Path.GetFullPath(path);
        var expectedDirectory = Path.GetFullPath(Path.Combine(store.HistoryDirectory, profileId));
        var prefix = expectedDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Historienpfad liegt nicht im geschützten Profil-Historienordner.");
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Historienstand wurde nicht gefunden.", fullPath);

        using var stream = File.OpenRead(fullPath);
        var profile = JsonSerializer.Deserialize<ProfileDefinition>(stream, ProfileEditingService.SerializationOptions)
            ?? throw new InvalidDataException("Historienstand ist leer.");
        if (!string.Equals(profile.Profile.Id, profileId, StringComparison.Ordinal) || !string.Equals(profile.Profile.Kind, "customer", StringComparison.Ordinal))
            throw new InvalidDataException("Historienstand gehört nicht zum gewählten Kundenprofil.");
        var issues = ProfileValidator.Validate(profile);
        if (issues.Count > 0) throw new InvalidDataException("Historienstand ist fachlich ungültig: " + string.Join("; ", issues.Select(issue => issue.Message)));

        if (verifyDigest)
        {
            var match = Regex.Match(Path.GetFileName(fullPath), "_([0-9A-Fa-f]{12})\\.json$");
            if (!match.Success || !ProfileIdentityService.ComputeDigest(profile).StartsWith(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Historienstand besteht die SHA-256-Integritätsprüfung nicht.");
        }
        return profile;
    }

    private string CurrentPath(string profileId)
    {
        var userPath = Path.Combine(store.UserDirectory, profileId + ".json");
        return File.Exists(userPath) ? userPath : "Gebündelter Profilstand";
    }
}
