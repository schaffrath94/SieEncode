using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>Public transport only. Reading an invitation does not establish trust or grant authority.</summary>
public static class GroupDelegationTransport
{
    private sealed record InvitationEnvelope(string Format, int Version, GroupDelegateInvitation Invitation);
    private sealed record ChangeEnvelope(string Format, int Version, SignedGroupChange Change);
    public const int MaximumBytes = 2 * 1024 * 1024;
    private const string InvitationFormat = "SieEncode-Group-Delegate-Invitation";
    private const string ChangeFormat = "SieEncode-Group-Change-Package";
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };

    public static string InvitationFingerprint(GroupDelegateInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        if (invitation.LibraryId == Guid.Empty || invitation.PersonId is not { Length: > 0 and <= 128 } ||
            !invitation.PersonId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new InvalidDataException("Ungültige Verwaltungsidentität.");
        using var rsa = OfflineGroupDelegationService.OpenPublicKey(invitation.SigningPublicKey);
        return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    }

    public static byte[] WriteInvitation(GroupDelegateInvitation invitation)
    {
        InvitationFingerprint(invitation);
        return Write(new InvitationEnvelope(InvitationFormat, 1, invitation));
    }

    public static GroupDelegateInvitation ReadInvitation(byte[] bytes)
    {
        var envelope = Read<InvitationEnvelope>(bytes);
        if (envelope.Format != InvitationFormat || envelope.Version != 1 || envelope.Invitation is null)
            throw new InvalidDataException("Ungültige Verwaltungseinladung.");
        InvitationFingerprint(envelope.Invitation);
        return envelope.Invitation;
    }

    public static byte[] WriteChange(SignedGroupChange change)
    {
        ValidateChange(change);
        return Write(new ChangeEnvelope(ChangeFormat, 1, change));
    }

    // Signature/scope verification must follow against the receiver's current enrolled state.
    public static SignedGroupChange ReadChange(byte[] bytes)
    {
        var envelope = Read<ChangeEnvelope>(bytes);
        if (envelope.Format != ChangeFormat || envelope.Version != 1)
            throw new InvalidDataException("Ungültige Gruppenänderungsdatei.");
        ValidateChange(envelope.Change);
        return envelope.Change;
    }

    public static byte[] ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Austauschdatei ist leer oder zu groß.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        return bytes;
    }

    private static void ValidateChange(SignedGroupChange change)
    {
        if (change is null || change.Payload is null || change.Payload.Length is 0 or > 1048576 ||
            change.Signature is null || change.Signature.Length is < 384 or > 2048)
            throw new InvalidDataException("Ungültiger signierter Änderungsdatensatz.");
    }

    private static T Read<T>(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Austauschdatei ist leer oder zu groß.");
        var snapshot = bytes.ToArray();
        using (var document = JsonDocument.Parse(snapshot, new JsonDocumentOptions { MaxDepth = 8 }))
            OfflineGroupPolicyService.RejectDuplicateProperties(document.RootElement);
        return JsonSerializer.Deserialize<T>(snapshot, Options) ?? throw new InvalidDataException("Austauschdatei fehlt.");
    }

    private static byte[] Write<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Austauschdatei ist zu groß.");
        return bytes;
    }
}
