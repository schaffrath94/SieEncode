using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>Must authenticate the local state and bind it to the intended OS user. No plaintext fallback.</summary>
public interface ILocalGroupTrustProtection
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] protectedState);
}

/// <summary>Local-only enrollment and last accepted policy. Never place this file in the shared library.</summary>
public sealed class OfflineGroupTrustStore
{
    private sealed record State(int Version, Guid LibraryId, byte[] AdministratorPublicKey, SignedGroupPolicy SignedPolicy);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 20 };
    private readonly string path;
    private readonly Guid libraryId;
    private readonly ILocalGroupTrustProtection protection;
    private const int MaximumStateBytes = 3 * 1024 * 1024;

    public OfflineGroupTrustStore(string localPath, Guid expectedLibraryId, ILocalGroupTrustProtection protection)
    {
        if (expectedLibraryId == Guid.Empty) throw new ArgumentException("Bibliotheks-ID fehlt.", nameof(expectedLibraryId));
        path = Path.GetFullPath(localPath);
        libraryId = expectedLibraryId;
        this.protection = protection ?? throw new ArgumentNullException(nameof(protection));
    }

    public static string AdministratorFingerprint(byte[] subjectPublicKeyInfo)
    {
        using var administrator = OpenAdministrator(subjectPublicKeyInfo);
        return Convert.ToHexString(SHA256.HashData(administrator.ExportSubjectPublicKeyInfo()));
    }

    // The UI must obtain this fingerprint over an independently confirmed channel.
    public VerifiedGroupPolicy Enroll(byte[] administratorPublicKey, string confirmedFingerprint, SignedGroupPolicy initialPolicy)
    {
        var key = administratorPublicKey.ToArray();
        if (!string.Equals(AdministratorFingerprint(key), confirmedFingerprint, StringComparison.Ordinal))
            throw new CryptographicException("Verwaltungsschlüssel stimmt nicht mit dem bestätigten Fingerabdruck überein.");
        var signed = Snapshot(initialPolicy);
        using var administrator = OpenAdministrator(key);
        var verified = OfflineGroupPolicyService.Verify(signed, administrator, libraryId);
        Save(new(1, libraryId, key, signed), FileRevision.Missing);
        return verified;
    }

    public VerifiedGroupPolicy LoadOffline()
    {
        var (state, _) = Load();
        using var administrator = OpenAdministrator(state.AdministratorPublicKey);
        return OfflineGroupPolicyService.Verify(state.SignedPolicy, administrator, libraryId);
    }

    public VerifiedGroupPolicy AcceptUpdate(SignedGroupPolicy candidate)
    {
        var (state, revision) = Load();
        using var administrator = OpenAdministrator(state.AdministratorPublicKey);
        var current = OfflineGroupPolicyService.Verify(state.SignedPolicy, administrator, libraryId);
        var signed = Snapshot(candidate);
        var next = OfflineGroupPolicyService.Verify(signed, administrator, libraryId, current.Checkpoint);
        Save(state with { SignedPolicy = signed }, revision);
        return next;
    }

    private (State State, FileRevision Revision) Load()
    {
        // Hash precisely the read snapshot; a concurrent update must not be overwritten.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumStateBytes) throw new InvalidDataException("Lokaler Vertrauensstand ist ungültig.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var revision = new FileRevision(true, Convert.ToHexString(SHA256.HashData(bytes)));
        var plaintext = protection.Unprotect(bytes);
        try
        {
            if (plaintext.Length > MaximumStateBytes) throw new InvalidDataException("Lokaler Vertrauensstand ist zu groß.");
            var state = JsonSerializer.Deserialize<State>(plaintext, Options) ?? throw new InvalidDataException("Lokaler Vertrauensstand fehlt.");
            if (state.Version != 1 || state.LibraryId != libraryId || state.AdministratorPublicKey is null || state.SignedPolicy is null)
                throw new InvalidDataException("Lokaler Vertrauensstand gehört nicht zu dieser Bibliothek.");
            return (state, revision);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private void Save(State state, FileRevision expected)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        try
        {
            var bytes = protection.Protect(plaintext);
            if (bytes.Length is 0 or > MaximumStateBytes) throw new InvalidDataException("Geschützter Vertrauensstand ist ungültig.");
            ConcurrentFileWriter.Write(path, expected, temporary =>
            {
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); stream.Flush(true);
            });
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static SignedGroupPolicy Snapshot(SignedGroupPolicy signed)
    {
        ArgumentNullException.ThrowIfNull(signed);
        if (signed.Payload is null || signed.Payload.Length is 0 or > 1024 * 1024 || signed.Signature is null || signed.Signature.Length > 2048)
            throw new InvalidDataException("Ungültige Gruppenfreigabe.");
        return new(signed.Payload.ToArray(), signed.Signature.ToArray());
    }

    private static RSA OpenAdministrator(byte[] key)
    {
        if (key is null || key.Length is 0 or > 4096) throw new CryptographicException("Ungültiger Verwaltungsschlüssel.");
        var rsa = RSA.Create();
        try
        {
            rsa.ImportSubjectPublicKeyInfo(key, out var read);
            if (read != key.Length || rsa.KeySize < 3072) throw new CryptographicException("Ungültiger Verwaltungsschlüssel.");
            return rsa;
        }
        catch { rsa.Dispose(); throw; }
    }
}
