using System.Security.Cryptography;

namespace RFiDGear.Builder.Core;

public sealed record FileRevision(bool Exists, string Sha256)
{
    public static FileRevision Missing { get; } = new(false, "");
}

public sealed class ConcurrentFileChangeException(string path)
    : IOException($"Die Datei wurde seit dem Öffnen von einem anderen Benutzer oder Programm geändert: {path}. Die fremde Fassung wurde nicht überschrieben. Bitte neu laden oder bewusst unter einem anderen Namen speichern.")
{
    public string ConflictingPath { get; } = path;
}

public static class ConcurrentFileWriter
{
    public static FileRevision Capture(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) return FileRevision.Missing;
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(true, Convert.ToHexString(SHA256.HashData(stream)));
    }

    public static FileRevision Write(string path, FileRevision expected, Action<string> writeTemporary)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(writeTemporary);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var lockPath = fullPath + ".rfidgear-builder.lock";
        using var writeLock = Acquire(lockPath);
        if (Capture(fullPath) != expected) throw new ConcurrentFileChangeException(fullPath);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            writeTemporary(temporary);
            if (Capture(fullPath) != expected) throw new ConcurrentFileChangeException(fullPath);
            File.Move(temporary, fullPath, true);
            return Capture(fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Delete(string path, FileRevision expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var fullPath = Path.GetFullPath(path);
        var lockPath = fullPath + ".rfidgear-builder.lock";
        using var writeLock = Acquire(lockPath);
        if (Capture(fullPath) != expected) throw new ConcurrentFileChangeException(fullPath);
        if (!expected.Exists) throw new FileNotFoundException("Die zu löschende Datei ist nicht mehr vorhanden.", fullPath);
        File.Delete(fullPath);
    }

    private static FileStream Acquire(string lockPath)
    {
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException($"Die Datei wird gerade von einem anderen Builder gespeichert: {Path.GetFileNameWithoutExtension(lockPath)}. Bitte den Speichervorgang erneut versuchen.", exception);
        }
    }
}
