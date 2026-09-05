using System.IO.Compression;
using System.Text;
using System.Xml;

namespace RFiDGear.Builder.RfPrj;

public static class RfPrjArchive
{
    private const string DatabaseEntry = "taskdatabase.xml";

    public static RfPrjDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.Where(entry => string.Equals(entry.FullName.Replace('\\', '/'), DatabaseEntry, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length != 1) throw new InvalidDataException($"rfPrj muss genau eine {DatabaseEntry} enthalten; gefunden: {entries.Length}.");
        using var stream = entries[0].Open();
        return RfPrjDocument.Parse(stream);
    }

    public static void Save(string path, RfPrjDocument project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(DatabaseEntry, CompressionLevel.Optimal);
                using var stream = entry.Open();
                var settings = new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace,
                    CloseOutput = false
                };
                using var writer = XmlWriter.Create(stream, settings);
                project.Xml.Save(writer);
            }
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
