using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class KeyLibrarySyncWireTests
{
    public static void Verify()
    {
        var head = new CentralKeyLibraryHead(Guid.NewGuid(), 1,
            new string('A', 64), "keys", new GroupPolicyCheckpoint(1, new string('B', 64)), 1);
        var encoded = KeyLibrarySyncWire.Write(head);
        var limit = KeyLibrarySyncWire.MaximumMetadataBytes;
        if (KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(encoded, limit) != head)
            throw new Exception("Shared head response did not round-trip.");
        var json = Encoding.UTF8.GetString(encoded);
        if (!json.Contains("\"LibraryId\"", StringComparison.Ordinal) ||
            json.Contains("\"libraryId\"", StringComparison.Ordinal))
            throw new Exception("HTTP response naming policy drifted.");
        Reject<JsonException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(
            Encoding.UTF8.GetBytes(json.Replace("\"LibraryId\"", "\"libraryId\"")), limit));
        Reject<JsonException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(
            Encoding.UTF8.GetBytes(json.Insert(1, "\"Unknown\":1,")), limit));
        Reject<InvalidDataException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(
            Encoding.UTF8.GetBytes(json.Insert(1, "\"LibraryId\":null,")), limit));
        Reject<InvalidDataException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(encoded, encoded.Length - 1));
        Reject<InvalidDataException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(ReadOnlyMemory<byte>.Empty, limit));
        Reject<JsonException>(() => KeyLibrarySyncWire.Read<CentralKeyLibraryHead>(
            encoded.AsMemory(0, encoded.Length - 1), limit));
        using var input = new MemoryStream(encoded, writable: false);
        if (KeyLibrarySyncWire.ReadAsync<CentralKeyLibraryHead>(input, encoded.Length)
            .GetAwaiter().GetResult() != head)
            throw new Exception("Bounded streaming rejected exact-size response.");
        input.Position = 0;
        Reject<InvalidDataException>(() => KeyLibrarySyncWire.ReadAsync<CentralKeyLibraryHead>(
            input, encoded.Length - 1).GetAwaiter().GetResult());
        input.Position = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Reject<OperationCanceledException>(() => KeyLibrarySyncWire.ReadAsync<CentralKeyLibraryHead>(
            input, limit, cancelled.Token).GetAwaiter().GetResult());

        // These bytes are deliberately synthetic, not real keys, signatures or vault data.
        var upload = new SignedKeyLibrarySync([1, 2], [3, 4], [5, 6]);
        var uploadCopy = KeyLibrarySyncWire.Read<SignedKeyLibrarySync>(
            KeyLibrarySyncWire.Write(upload), KeyLibrarySyncWire.MaximumEncryptedBytes);
        if (!uploadCopy.Claim.SequenceEqual(upload.Claim) ||
            !uploadCopy.Signature.SequenceEqual(upload.Signature) ||
            !uploadCopy.EncryptedVault.SequenceEqual(upload.EncryptedVault))
            throw new Exception("Transport altered opaque signed bytes.");
        var download = new SignedKeyLibraryDownload([7, 8], [9, 10]);
        var downloadCopy = KeyLibrarySyncWire.Read<SignedKeyLibraryDownload>(
            KeyLibrarySyncWire.Write(download), KeyLibrarySyncWire.MaximumDownloadRequestBytes);
        if (!downloadCopy.Claim.SequenceEqual(download.Claim) ||
            !downloadCopy.Signature.SequenceEqual(download.Signature))
            throw new Exception("Transport altered signed read authorization.");
    }

    private static void Reject<T>(Action operation) where T : Exception
    {
        try { operation(); }
        catch (T) { return; }
        throw new Exception("Malformed sync wire message accepted: " + typeof(T).Name);
    }
}
