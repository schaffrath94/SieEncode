namespace RFiDGear.Builder.Core;

/// <summary>Host-neutral signed ciphertext protocol. Implementations must
/// authenticate the remote service and the personal device, preserve exact
/// signed bytes, honor cancellation and map CAS conflicts distinctly.
/// A corporate integration must not replace cryptographic authorization
/// with an organizational login or send plaintext catalog material.</summary>
public interface IKeyLibrarySyncTransport : IKeyLibraryPolicyTransitionTransport
{
    Task<CentralKeyLibraryHead> UploadAsync(SignedKeyLibrarySync transfer,
        CancellationToken cancellationToken = default);
    Task<SignedKeyLibrarySync> DownloadAsync(SignedKeyLibraryDownload proof,
        CancellationToken cancellationToken = default);
    Task<CentralKeyLibrarySnapshot> DownloadSnapshotAsync(SignedKeyLibraryDownload proof,
        CancellationToken cancellationToken = default);
    Task<KeyLibraryAuditReceipt> SubmitAuditAsync(SignedKeyLibraryAudit report,
        CancellationToken cancellationToken = default);
    Task<KeyLibraryAuditPage> ReadAuditPageAsync(SignedKeyLibraryAuditQuery query,
        CancellationToken cancellationToken = default);
}
