using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

/// <summary>Two fixed encrypted slots for the last reconciled merge base.
/// Stage before updating the protected cursor; prune only after the cursor
/// commit. A crash may leave two copies, never a growing revision archive.
/// Callers serialize all changes with the existing library workspace lease.
/// No new trust anchor or plaintext storage.</summary>
public static class KeyLibraryMergeReferenceStore
{
    private static string[] Slots(string vaultPath) =>
        [Path.GetFullPath(vaultPath) + ".merge-base-a", Path.GetFullPath(vaultPath) + ".merge-base-b"];

    private static FileRevision Inspect(string path)
    {
        if (!File.Exists(path))
        {
            if (Directory.Exists(path)) throw new InvalidDataException("Referenzspeicher ist kein regulärer Dateipfad.");
            return FileRevision.Missing;
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Verknüpfungen sind im Referenzspeicher nicht zulässig.");
        var bytes = KeyLibraryVaultStore.ReadBytes(path);
        return new(true, KeyLibrarySyncTransport.CiphertextSha256(bytes));
    }

    private static void RequireHead(CentralKeyLibraryHead head)
    {
        if (head.LibraryId == Guid.Empty || head.CentralRevision <= 0 ||
            head.CatalogRevision <= 0 || head.CiphertextSha256 is not { Length: 64 } ||
            !head.CiphertextSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new InvalidDataException("Bestätigte Referenzrevision fehlt.");
    }

    /// <summary>Only the exact reconciled ciphertext is returned. The merge
    /// workflow must still authenticate its signature, policy and content.</summary>
    public static string? Find(string vaultPath, CentralKeyLibraryHead head)
    {
        RequireHead(head);
        return Slots(vaultPath).FirstOrDefault(path =>
        {
            var revision = Inspect(path);
            return revision.Exists && revision.Sha256 == head.CiphertextSha256;
        });
    }

    internal static void Stage(string vaultPath, CentralKeyLibraryCursorStore cursor,
        CentralKeyLibraryHead head, byte[] encrypted)
    {
        RequireHead(head);
        if (KeyLibrarySyncTransport.CiphertextSha256(encrypted) != head.CiphertextSha256)
            throw new CryptographicException("Referenz entspricht nicht dem bestätigten zentralen Ciphertext.");
        var slots = Slots(vaultPath);
        var revisions = slots.Select(Inspect).ToArray();
        if (revisions.Any(r => r.Exists && r.Sha256 == head.CiphertextSha256)) return;
        var previous = cursor.ReadReconciledHead();
        // Keep the slot selected by the currently durable cursor intact.
        // If it is absent, do not pretend another slot is the old base.
        var protectedSlot = Array.FindIndex(revisions, r => r.Exists &&
            r.Sha256 == previous?.CiphertextSha256);
        var target = protectedSlot == 0 ? 1 : 0;
        var written = ConcurrentFileWriter.Write(slots[target], revisions[target], temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(encrypted);
            stream.Flush(true);
        });
        if (written.Sha256 != head.CiphertextSha256 || Inspect(slots[target]) != written)
            throw new ConcurrentFileChangeException(slots[target]);
    }

    internal static void Finish(string vaultPath, CentralKeyLibraryCursorStore cursor,
        CentralKeyLibraryHead head)
    {
        if (cursor.ReadReconciledHead() != head)
            throw new CryptographicException("Referenzbereinigung ohne exakt abgeglichenen Cursor verweigert.");
        var keep = Find(vaultPath, head) ??
            throw new InvalidDataException("Bestätigter Referenzbestand fehlt.");
        foreach (var other in Slots(vaultPath).Where(p => p != keep))
        {
            var revision = Inspect(other);
            if (revision.Exists)
            {
                if (cursor.ReadReconciledHead() != head || Find(vaultPath, head) != keep)
                    throw new ConcurrentFileChangeException(keep);
                ConcurrentFileWriter.Delete(other, revision);
            }
        }
    }

    /// <summary>Backfill a reference before the first offline edit on a
    /// development installation that was reconciled before reference support.
    /// No action if the local bytes already diverged from the cursor.</summary>
    public static void PreserveExistingBase(string vaultPath, CentralKeyLibraryCursorStore cursor,
        CentralKeyLibraryHead head, FileRevision expected)
    {
        if (cursor.ReadReconciledHead() != head || !expected.Exists ||
            expected.Sha256 != head.CiphertextSha256 || !cursor.HasUnchangedLocalBase(expected))
            return;
        var bytes = KeyLibraryVaultStore.ReadBytes(vaultPath);
        if (ConcurrentFileWriter.Capture(vaultPath) != expected)
            throw new ConcurrentFileChangeException(vaultPath);
        Stage(vaultPath, cursor, head, bytes);
        Finish(vaultPath, cursor, head);
    }
}
