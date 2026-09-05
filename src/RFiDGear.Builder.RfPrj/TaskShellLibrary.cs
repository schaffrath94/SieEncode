using System.Xml.Linq;

namespace RFiDGear.Builder.RfPrj;

public sealed class TaskShellLibrary
{
    private readonly Dictionary<string, XElement> shells;
    private TaskShellLibrary(Dictionary<string, XElement> shells) => this.shells = shells;
    public IReadOnlyCollection<string> TaskTypes => shells.Keys;

    public static TaskShellLibrary Load(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory)) throw new DirectoryNotFoundException($"Task-Shell-Ordner fehlt: {fullDirectory}");
        var shells = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(fullDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var task = document.Root?.Element("TaskCollection")?.Element("anyType")
                ?? throw new InvalidDataException($"Task-Shell enthält kein anyType-Element: {path}");
            var taskType = TaskXml.Get(task, "SelectedTaskType");
            var expected = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(taskType, expected, StringComparison.Ordinal)) throw new InvalidDataException($"Task-Shell {expected} enthält Tasktyp {taskType}.");
            if (!shells.TryAdd(taskType, new XElement(task))) throw new InvalidDataException($"Task-Shell ist doppelt: {taskType}");
        }
        if (shells.Count == 0) throw new InvalidDataException("Keine Task-Shells gefunden.");
        return new TaskShellLibrary(shells);
    }

    public XElement Create(string taskType)
        => shells.TryGetValue(taskType, out var task) ? new XElement(task) : throw new KeyNotFoundException($"Task-Shell fehlt: {taskType}");
}
