namespace RFiDGear.Builder.Core;

public sealed class WorkspaceLease : IDisposable
{
    public const string FileName = ".rfidgear-builder.workspace.lock";
    private FileStream? stream;

    private WorkspaceLease(string directory, FileStream stream)
    {
        Directory = directory;
        LockPath = Path.Combine(directory, FileName);
        this.stream = stream;
    }

    public string Directory { get; }
    public string LockPath { get; }
    public bool IsHeld => stream is not null;

    public static WorkspaceLease Acquire(string directory, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(root);
        var path = Path.Combine(root, FileName);
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new(root, stream);
        }
        catch (IOException exception)
        {
            throw new IOException($"Projektordner ist bereits durch einen anderen Builder belegt. '{purpose}' wurde nicht gestartet: {root}. Beenden Sie dort zuerst Build oder Kodiersitzung und versuchen Sie es erneut.", exception);
        }
    }

    public void Dispose()
    {
        var held = Interlocked.Exchange(ref stream, null);
        held?.Dispose();
    }
}
