using System.Text.Json;
using System.Text.RegularExpressions;

namespace RFiDGear.Builder.Core;

public sealed class ProfileEditingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ProfileDefinition CreateCustomer() => new()
    {
        Profile = new ProfileMetadata
        {
            Id = "Neues-Kundenprofil",
            Code = "NEU",
            Name = "Neues Kundenprofil",
            Kind = "customer",
            Version = "1.0"
        }
    };

    public ProfileDefinition CreateBase() => new()
    {
        Profile = new ProfileMetadata
        {
            Id = "Neues-Grundprofil",
            Code = "BASIS",
            Name = "Neues Grundprofil",
            Kind = "base",
            Version = "1.0"
        }
    };

    public ProfileDefinition Clone(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Deserialize<ProfileDefinition>(JsonSerializer.Serialize(profile, JsonOptions), JsonOptions)!;
    }

    public ProfileDefinition CreateCloneDraft(ProfileDefinition source, IEnumerable<string> existingProfileIds)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(existingProfileIds);
        if (!string.Equals(source.Profile.Kind, "customer", StringComparison.Ordinal))
            throw new InvalidDataException("Nur Kundenprofile können dupliziert werden.");

        var existing = existingProfileIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var root = source.Profile.Id + "-Kopie";
        var id = root;
        for (var suffix = 2; existing.Contains(id); suffix++) id = root + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var clone = Clone(source);
        clone.Profile.Id = id;
        clone.Profile.Code = source.Profile.Code + "-KOPIE";
        clone.Profile.Name = source.Profile.Name + " (Kopie)";
        clone.Profile.Kind = "customer";
        clone.Profile.Version = "1.0";
        clone.Profile.CreatedAt = null;
        clone.Profile.ModifiedAt = null;
        clone.Profile.PreviousVersion = null;
        return clone;
    }

    public IReadOnlyList<string> FindVariableReferences(ProfileDefinition profile, string variableId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Applications.SelectMany(application => application.Files.SelectMany(file =>
                (file.Coding?.Recipe ?? []).Where(step => (step.Op is "asciiDigits" or "bcdDigits" or "hexBytes" or "asciiText" or "utf8Text" or "uintBigEndian" or "uintLittleEndian") && string.Equals(step.Source, variableId, StringComparison.Ordinal))
                    .Select(_ => $"AID {application.Id}, Datei {file.Id} ({file.Name})")))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    public void RenameVariable(ProfileDefinition profile, string oldId, string newId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        newId = newId.Trim();
        if (!Regex.IsMatch(newId, "^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Variablen-ID muss mit einem Buchstaben beginnen und darf nur Buchstaben, Ziffern und _ enthalten.");
        if (profile.Variables.Any(variable => !string.Equals(variable.Id, oldId, StringComparison.Ordinal) && string.Equals(variable.Id, newId, StringComparison.Ordinal)))
            throw new InvalidDataException($"Variable '{newId}' ist bereits vorhanden.");
        var variable = profile.Variables.SingleOrDefault(candidate => string.Equals(candidate.Id, oldId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Variable '{oldId}' wurde nicht gefunden.");
        variable.Id = newId;
        foreach (var step in profile.Applications.SelectMany(application => application.Files).SelectMany(file => file.Coding?.Recipe ?? []))
            if ((step.Op is "asciiDigits" or "bcdDigits" or "hexBytes" or "asciiText" or "utf8Text" or "uintBigEndian" or "uintLittleEndian") && string.Equals(step.Source, oldId, StringComparison.Ordinal)) step.Source = newId;
    }

    public void DeleteVariable(ProfileDefinition profile, string variableId)
    {
        var references = FindVariableReferences(profile, variableId);
        if (references.Count > 0)
            throw new InvalidDataException($"Variable '{variableId}' wird noch verwendet: {string.Join("; ", references)}");
        var removed = profile.Variables.RemoveAll(variable => string.Equals(variable.Id, variableId, StringComparison.Ordinal));
        if (removed == 0) throw new KeyNotFoundException($"Variable '{variableId}' wurde nicht gefunden.");
    }

    public void ChangeKeyCount(ApplicationDefinition application, int keyCount)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (keyCount is < 1 or > 14) throw new InvalidDataException("Keyanzahl muss zwischen 1 und 14 liegen.");
        var used = application.Files.SelectMany(file => AccessValues(file.Access).Concat(file.CreationSettings is null ? [] : AccessValues(file.CreationSettings.Access))).DefaultIfEmpty(-1).Max();
        if (used >= keyCount) throw new InvalidDataException($"Key{used} wird noch in Dateirechten verwendet. Die Keyanzahl kann nicht auf {keyCount} reduziert werden.");
        application.KeyCount = keyCount;
    }

    public static string KeySettingsFromFlags(bool allowChangeMasterKey, bool allowListApplications, bool allowCreateDelete, bool allowConfigurationChange)
    {
        var value = (allowChangeMasterKey ? 1 : 0) | (allowListApplications ? 2 : 0) | (allowCreateDelete ? 4 : 0) | (allowConfigurationChange ? 8 : 0);
        return value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static (bool ChangeMasterKey, bool ListApplications, bool CreateDelete, bool ConfigurationChange) KeySettingsFlags(string value)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed is < 0 or > 15)
            throw new InvalidDataException("KeySettings müssen zwischen 00 und 0F liegen.");
        return ((parsed & 1) != 0, (parsed & 2) != 0, (parsed & 4) != 0, (parsed & 8) != 0);
    }

    public IReadOnlyList<ValidationIssue> Validate(ProfileDefinition profile) => ProfileValidator.Validate(profile);

    internal static JsonSerializerOptions SerializationOptions => JsonOptions;

    private static IEnumerable<int> AccessValues(AccessDefinition access)
    {
        yield return access.Read;
        yield return access.Write;
        yield return access.ReadWrite;
        yield return access.Change;
    }
}

public sealed class ProfileStore
{
    private readonly string bundledDirectory;
    private readonly string userDirectory;
    private readonly string baseDirectory;
    private readonly Func<DateTimeOffset> utcNow;

    public ProfileStore(string bundledDirectory, string userDirectory, Func<DateTimeOffset>? utcNow = null, string? baseDirectory = null)
    {
        this.bundledDirectory = Path.GetFullPath(bundledDirectory);
        this.userDirectory = Path.GetFullPath(userDirectory);
        this.baseDirectory = Path.GetFullPath(baseDirectory ?? Path.Combine(Directory.GetParent(this.userDirectory)?.FullName ?? this.userDirectory, "base"));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public string UserDirectory => userDirectory;
    public string BaseDirectory => baseDirectory;
    public string HistoryDirectory => Path.Combine(Directory.GetParent(userDirectory)?.FullName ?? userDirectory, "history");
    public string GetCustomerPath(string profileId) => Path.Combine(userDirectory, profileId + ".json");
    public string GetBasePath(string profileId) => Path.Combine(baseDirectory, profileId + ".json");

    public ProfileLibrary Load()
    {
        var bundled = ProfileLibrary.Load(bundledDirectory).Profiles.ToDictionary(profile => profile.Profile.Id, new ProfileEditingService().Clone, StringComparer.Ordinal);
        if (Directory.Exists(baseDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(baseDirectory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
            {
                var profile = LoadProfile(path);
                if (!string.Equals(profile.Profile.Kind, "base", StringComparison.Ordinal))
                    throw new InvalidDataException($"Im Grundprofilordner sind ausschließlich Grundprofile erlaubt: {path}");
                if (bundled.TryGetValue(profile.Profile.Id, out var original) && string.Equals(original.Profile.Kind, "customer", StringComparison.Ordinal))
                    throw new InvalidDataException($"Das Grundprofil '{profile.Profile.Id}' kollidiert mit einem gebündelten Kundenprofil.");
                bundled[profile.Profile.Id] = profile;
            }
        }
        if (Directory.Exists(userDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(userDirectory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
            {
                var profile = LoadProfile(path);
                if (!string.Equals(profile.Profile.Kind, "customer", StringComparison.Ordinal))
                    throw new InvalidDataException($"Im Benutzerprofilordner sind ausschließlich Kundenprofile erlaubt: {path}");
                if (bundled.TryGetValue(profile.Profile.Id, out var original) && string.Equals(original.Profile.Kind, "base", StringComparison.Ordinal))
                    throw new InvalidDataException($"Das Masterprofil '{profile.Profile.Id}' darf nicht überschrieben werden.");
                bundled[profile.Profile.Id] = profile;
            }
        }
        var library = ProfileLibrary.FromProfiles(bundled.Values);
        var issues = library.Validate().Where(issue => issue.Code != "BASE_PROFILE_COUNT").ToArray();
        if (issues.Length > 0) throw new InvalidDataException("Profilbibliothek ist ungültig: " + string.Join("; ", issues.Select(issue => issue.Message)));
        return library;
    }

    public string SaveBase(ProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!string.Equals(profile.Profile.Kind, "base", StringComparison.Ordinal))
            throw new InvalidDataException("Nur Profile vom Typ 'base' dürfen als Grundprofil importiert werden.");
        var issues = ProfileValidator.Validate(profile);
        if (issues.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        if (!Regex.IsMatch(profile.Profile.Id, "^[A-Za-z0-9][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Profil-ID enthält unzulässige Zeichen.");
        if (File.Exists(GetCustomerPath(profile.Profile.Id)))
            throw new InvalidDataException($"Die Profil-ID '{profile.Profile.Id}' wird bereits von einem Kundenprofil verwendet.");

        Directory.CreateDirectory(baseDirectory);
        var path = GetBasePath(profile.Profile.Id);
        var expectedRevision = ConcurrentFileWriter.Capture(path);
        _ = ConcurrentFileWriter.Write(path, expectedRevision, temporary =>
        {
            var previous = File.Exists(path) ? LoadProfile(path) : Load().Profiles.SingleOrDefault(candidate =>
                string.Equals(candidate.Profile.Id, profile.Profile.Id, StringComparison.Ordinal));
            if (previous is not null)
            {
                if (!string.Equals(previous.Profile.Kind, "base", StringComparison.Ordinal))
                    throw new InvalidDataException($"Die Profil-ID '{profile.Profile.Id}' gehört nicht zu einem Grundprofil.");
                var previousDigest = ProfileIdentityService.ComputeDigest(previous);
                var currentDigest = ProfileIdentityService.ComputeDigest(profile);
                if (string.Equals(previousDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
                {
                    profile.Profile.CreatedAt = previous.Profile.CreatedAt;
                    profile.Profile.ModifiedAt = previous.Profile.ModifiedAt;
                    profile.Profile.PreviousVersion = previous.Profile.PreviousVersion;
                }
                else
                {
                    if (string.Equals(previous.Profile.Version, profile.Profile.Version, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Das Grundprofil wurde geändert, verwendet aber weiterhin Version '{profile.Profile.Version}'. Bitte eine neue eindeutige Version vergeben.");
                    EnsureVersionNotArchived(profile.Profile.Id, profile.Profile.Version);
                    Archive(previous, previousDigest);
                    profile.Profile.PreviousVersion = previous.Profile.Version;
                    profile.Profile.CreatedAt = previous.Profile.CreatedAt ?? utcNow();
                    profile.Profile.ModifiedAt = utcNow();
                }
            }
            else
            {
                profile.Profile.CreatedAt ??= utcNow();
                profile.Profile.ModifiedAt = utcNow();
            }
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, profile, ProfileEditingService.SerializationOptions);
            stream.Flush(true);
        });
        _ = Load();
        return path;
    }

    public string SaveCustomer(ProfileDefinition profile) => SaveCustomer(profile, null);

    public string SaveCustomer(ProfileDefinition profile, FileRevision? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!string.Equals(profile.Profile.Kind, "customer", StringComparison.Ordinal)) throw new InvalidDataException("Nur Kundenprofile dürfen im dauerhaften Benutzerprofilordner gespeichert werden.");
        var issues = ProfileValidator.Validate(profile);
        if (issues.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        if (!Regex.IsMatch(profile.Profile.Id, "^[A-Za-z0-9][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Profil-ID enthält unzulässige Zeichen.");

        Directory.CreateDirectory(userDirectory);
        var path = GetCustomerPath(profile.Profile.Id);
        expectedRevision ??= ConcurrentFileWriter.Capture(path);
        _ = ConcurrentFileWriter.Write(path, expectedRevision, temporary =>
        {
            var previous = LoadCustomer(path);
            if (previous is null)
            {
                try
                {
                    previous = Load().Profiles.SingleOrDefault(candidate =>
                        string.Equals(candidate.Profile.Id, profile.Profile.Id, StringComparison.Ordinal) &&
                        string.Equals(candidate.Profile.Kind, "customer", StringComparison.Ordinal));
                }
                catch (KeyNotFoundException) { }
            }
            if (previous is null)
            {
                var now = utcNow();
                profile.Profile.CreatedAt ??= now;
                profile.Profile.ModifiedAt = now;
            }
            else
            {
                var previousDigest = ProfileIdentityService.ComputeDigest(previous);
                var currentDigest = ProfileIdentityService.ComputeDigest(profile);
                if (!string.Equals(previousDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(previous.Profile.Version, profile.Profile.Version, StringComparison.Ordinal))
                        throw new InvalidDataException($"Profilinhalt wurde geändert, aber die Version ist weiterhin '{profile.Profile.Version}'. Bitte vor dem Speichern eine neue Profilversion vergeben.");
                    EnsureVersionNotArchived(profile.Profile.Id, profile.Profile.Version);
                    var now = utcNow();
                    Archive(previous, previousDigest);
                    profile.Profile.PreviousVersion = previous.Profile.Version;
                    profile.Profile.CreatedAt = previous.Profile.CreatedAt ?? now;
                    profile.Profile.ModifiedAt = now;
                }
                else
                {
                    var fallback = previous.Profile.CreatedAt is null || previous.Profile.ModifiedAt is null ? utcNow() : default;
                    profile.Profile.CreatedAt = previous.Profile.CreatedAt ?? profile.Profile.CreatedAt ?? fallback;
                    profile.Profile.ModifiedAt = previous.Profile.ModifiedAt ?? profile.Profile.ModifiedAt ?? fallback;
                    profile.Profile.PreviousVersion = previous.Profile.PreviousVersion;
                }
            }
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, profile, ProfileEditingService.SerializationOptions);
            stream.Flush(true);
        });
        _ = Load();
        return path;
    }

    private static ProfileDefinition? LoadCustomer(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProfileDefinition>(stream, ProfileEditingService.SerializationOptions)
            ?? throw new InvalidDataException($"Profil ist leer: {path}");
    }

    private static ProfileDefinition LoadProfile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProfileDefinition>(stream, ProfileEditingService.SerializationOptions)
            ?? throw new InvalidDataException($"Profil ist leer: {path}");
    }

    public IReadOnlyList<string> ListHistory(string profileId)
    {
        var directory = Path.Combine(HistoryDirectory, profileId);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    private void Archive(ProfileDefinition profile, string digest)
    {
        var safeVersion = Regex.Replace(profile.Profile.Version, "[^A-Za-z0-9_.-]", "_");
        var directory = Path.Combine(HistoryDirectory, profile.Profile.Id);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{safeVersion}_{digest[..12]}.json");
        if (!File.Exists(path)) SaveAtomic(path, profile, overwrite: false);
    }

    private void EnsureVersionNotArchived(string profileId, string version)
    {
        foreach (var path in ListHistory(profileId))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var archived = JsonSerializer.Deserialize<ProfileDefinition>(stream, ProfileEditingService.SerializationOptions)
                    ?? throw new InvalidDataException($"Historienstand ist leer: {path}");
                if (string.Equals(archived.Profile.Version, version, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Profilversion '{version}' ist bereits in der Historie vorhanden. Jede fachliche Profilversion muss eindeutig sein.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Profilhistorie ist beschädigt und muss vor dem Speichern geprüft werden: {path}", exception);
            }
        }
    }

    private static void SaveAtomic(string path, ProfileDefinition profile, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, profile, ProfileEditingService.SerializationOptions);
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
