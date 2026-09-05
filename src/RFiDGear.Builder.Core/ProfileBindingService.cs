using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RFiDGear.Builder.Core;

public enum ProfileBindingState
{
    Match,
    Missing,
    VersionMismatch,
    DigestMismatch
}

public sealed record ProfileBindingStatus(
    string ProfileId,
    string CurrentVersion,
    string? BoundVersion,
    string CurrentDigest,
    string? BoundDigest,
    ProfileBindingState State)
{
    public bool IsMatch => State == ProfileBindingState.Match;
}

public static class ProfileIdentityService
{
    public static string ComputeDigest(ProfileDefinition profile) => Compute(profile, includeVersion: true);

    internal static string ComputeContentDigest(ProfileDefinition profile) => Compute(profile, includeVersion: false);

    private static string Compute(ProfileDefinition profile, bool includeVersion)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var clone = new ProfileEditingService().Clone(profile);
        clone.Profile.CreatedAt = null;
        clone.Profile.ModifiedAt = null;
        clone.Profile.PreviousVersion = null;
        if (!includeVersion) clone.Profile.Version = "";
        var json = JsonSerializer.Serialize(clone, ProfileEditingService.SerializationOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

public static class ProfileBindingService
{
    public static IReadOnlyList<ProfileBindingStatus> Evaluate(ProjectDefinition project, ProfileLibrary library)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(library);
        var selected = SelectedIds(project);
        return selected.Select(id =>
        {
            var current = library.Get(id);
            var digest = ProfileIdentityService.ComputeDigest(current);
            if (project.Profiles.Bindings is null || !project.Profiles.Bindings.TryGetValue(id, out var binding))
                return new ProfileBindingStatus(id, current.Profile.Version, null, digest, null, ProfileBindingState.Missing);
            var state = !string.Equals(binding.Version, current.Profile.Version, StringComparison.Ordinal)
                ? ProfileBindingState.VersionMismatch
                : !string.Equals(binding.Digest, digest, StringComparison.OrdinalIgnoreCase)
                    ? ProfileBindingState.DigestMismatch
                    : ProfileBindingState.Match;
            return new ProfileBindingStatus(id, current.Profile.Version, binding.Version, digest, binding.Digest, state);
        }).ToArray();
    }

    public static void BindCurrent(ProjectDefinition project, ProfileLibrary library)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(library);
        project.Profiles.Bindings = SelectedIds(project).ToDictionary(
            id => id,
            id =>
            {
                var profile = library.Get(id);
                return new ProfileBinding { Version = profile.Profile.Version, Digest = ProfileIdentityService.ComputeDigest(profile) };
            }, StringComparer.Ordinal);
    }

    public static void PruneToSelection(ProjectDefinition project)
    {
        if (project.Profiles.Bindings is null) return;
        var selected = SelectedIds(project).ToHashSet(StringComparer.Ordinal);
        foreach (var id in project.Profiles.Bindings.Keys.Where(id => !selected.Contains(id)).ToArray())
            project.Profiles.Bindings.Remove(id);
        if (project.Profiles.Bindings.Count == 0) project.Profiles.Bindings = null;
    }

    public static void EnsureBuildCompatible(ProjectDefinition project, ProfileLibrary library)
    {
        var statuses = Evaluate(project, library);
        var failures = statuses.Where(status => !status.IsMatch).ToArray();
        if (failures.Length == 0) return;
        throw new InvalidDataException("Projektprofile sind nicht reproduzierbar gebunden:\n" + string.Join("\n", failures.Select(DescribeFailure)) +
            "\nDie aktuell installierten Profilstände müssen im Projekteditor bewusst übernommen werden.");
    }

    public static string DescribeFailure(ProfileBindingStatus status) => status.State switch
    {
        ProfileBindingState.Missing => $"{status.ProfileId}: noch keine Profilbindung (installiert {status.CurrentVersion})",
        ProfileBindingState.VersionMismatch => $"{status.ProfileId}: gebunden {status.BoundVersion}, installiert {status.CurrentVersion}",
        ProfileBindingState.DigestMismatch => $"{status.ProfileId}: Version {status.CurrentVersion} stimmt, Inhalt wurde aber verändert",
        _ => $"{status.ProfileId}: OK · {status.CurrentVersion} · {status.CurrentDigest[..12]}"
    };

    private static IReadOnlyList<string> SelectedIds(ProjectDefinition project)
        => new[] { project.Profiles.Base }.Concat(project.Profiles.Customers).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
}
