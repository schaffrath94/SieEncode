using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>Protected preparation only. Never register this file as an active administration.
/// Public requests require the original enrolled checkpoint; a portable backup alone grants no trust.</summary>
public sealed class OfflineGroupSuccessorStore(string localPath, Guid libraryId, ILocalGroupTrustProtection protection)
{
    private const string Format = "SieEncode-Group-Successor-Preparation";
    private const int MaximumBytes = 8 * 1024 * 1024;
    private sealed record State(string Format, int Version, Guid LibraryId, byte[] PrivateKey,
        GroupEnrollmentPackage Current, AdministratorTransitionRequest Request);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 24 };
    private readonly string path = Path.GetFullPath(localPath);

    public AdministratorTransitionRequest Create(OfflineGroupTrustStore trust, GroupPolicyCheckpoint expected)
    {
        var current = trust.ExportPublic();
        if (current.LibraryId != libraryId || libraryId == Guid.Empty) throw new CryptographicException("Falsche Bibliothek für Nachfolgervorbereitung.");
        if (GroupEnrollmentPackages.Validate(current).Checkpoint != expected) throw new ConcurrentFileChangeException(path);
        using var key = RSA.Create(3072);
        var request = GroupAdministratorTransition.Prepare(current, key);
        var privateKey = key.ExportPkcs8PrivateKey();
        try
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(new State(Format, 1, libraryId, privateKey, current, request), Options);
            try
            {
                if (plaintext.Length > MaximumBytes) throw new InvalidDataException("Nachfolgervorbereitung ist zu groß.");
                var bytes = protection.Protect(plaintext);
                if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Geschützte Vorbereitung ist ungültig.");
                ConcurrentFileWriter.Write(path, FileRevision.Missing, temporary =>
                {
                    RequireCurrent(current, trust);
                    using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.Write(bytes); stream.Flush(true);
                    RequireCurrent(current, trust);
                });
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
        return ExportRequest(trust);
    }

    public AdministratorTransitionRequest ExportRequest(OfflineGroupTrustStore trust)
    {
        var state = Load();
        try
        {
            using var key = Validate(state);
            RequireCurrent(state.Current, trust);
            return GroupAdministratorTransition.ReadRequest(GroupAdministratorTransition.WriteRequest(state.Request));
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    // Recovery of preparation remains possible even after trust has advanced. Restoration into an
    // active administration is separately gated by OfflineGroupAdministrationStore and enrolled trust.
    public byte[] ExportPrivateBackup(string passphrase)
    {
        var state = Load();
        try
        {
            using var key = Validate(state);
            return GroupPrivateKeyBackups.Protect(key, new(libraryId, GroupPrivateKeyBackups.Administrator), passphrase);
        }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    internal (RSA Key, GroupEnrollmentPackage Previous) OpenForActivation(Guid expectedLibrary)
    {
        if (expectedLibrary != libraryId) throw new CryptographicException("Falsche Bibliothek für Aktivierung.");
        var state = Load();
        try { return (Validate(state), state.Current); }
        finally { CryptographicOperations.ZeroMemory(state.PrivateKey); }
    }

    private static void RequireCurrent(GroupEnrollmentPackage prepared, OfflineGroupTrustStore trust)
    {
        var current = trust.ExportPublic();
        if (current.LibraryId != prepared.LibraryId || !current.AdministratorPublicKey.SequenceEqual(prepared.AdministratorPublicKey) ||
            GroupEnrollmentPackages.Validate(current).Checkpoint != GroupEnrollmentPackages.Validate(prepared).Checkpoint)
            throw new CryptographicException("Gruppenstand seit Vorbereitung geändert. Vorhandenen Schlüssel nicht überschreiben; Übergabe neu abstimmen.");
    }

    private RSA Validate(State state)
    {
        var next = GroupAdministratorTransition.InspectRequest(state.Request, state.Current);
        var key = RSA.Create();
        try
        {
            key.ImportPkcs8PrivateKey(state.PrivateKey, out var read);
            if (read != state.PrivateKey.Length || key.KeySize < 3072 || !key.ExportSubjectPublicKeyInfo().SequenceEqual(next.AdministratorPublicKey))
                throw new CryptographicException("Vorbereiteter privater Schlüssel passt nicht zur Anforderung.");
            return key;
        }
        catch { key.Dispose(); throw; }
    }

    private State Load()
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Ungültige Nachfolgervorbereitung.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var plaintext = protection.Unprotect(bytes);
        State? state = null;
        try
        {
            if (plaintext.Length is 0 or > MaximumBytes) throw new InvalidDataException("Ungültige Nachfolgervorbereitung.");
            using (var json = JsonDocument.Parse(plaintext, new JsonDocumentOptions { MaxDepth = 24 })) OfflineGroupPolicyService.RejectDuplicateProperties(json.RootElement);
            state = JsonSerializer.Deserialize<State>(plaintext, Options);
            if (state is null || state.Format != Format || state.Version != 1 || libraryId == Guid.Empty || state.LibraryId != libraryId ||
                state.PrivateKey is null || state.PrivateKey.Length is 0 or > 16384 || state.Current is null || state.Current.LibraryId != libraryId || state.Request is null)
                throw new InvalidDataException("Nachfolgervorbereitung gehört nicht zu dieser Bibliothek.");
            return state;
        }
        catch
        {
            if (state?.PrivateKey is not null) CryptographicOperations.ZeroMemory(state.PrivateKey);
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}
