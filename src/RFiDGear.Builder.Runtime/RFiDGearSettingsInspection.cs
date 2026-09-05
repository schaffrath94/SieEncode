using System.Xml.Linq;

namespace RFiDGear.Builder.Runtime;

public sealed record RFiDGearReaderSettingsInfo(
    string SettingsPath,
    bool Exists,
    string Provider,
    string Port,
    string ReaderName,
    bool IsConfigured,
    string? Error);

public static class RFiDGearSettingsInspection
{
    public static string GetDefaultSettingsPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear", "settings.xml");

    public static RFiDGearReaderSettingsInfo Inspect(string? settingsPath = null)
    {
        var path = Path.GetFullPath(string.IsNullOrWhiteSpace(settingsPath) ? GetDefaultSettingsPath() : settingsPath);
        if (!File.Exists(path))
            return new RFiDGearReaderSettingsInfo(path, false, "", "", "", false, "RFiDGear settings.xml wurde nicht gefunden.");

        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            var provider = Read(document, "DefaultReaderProvider");
            var port = Read(document, "LastUsedComPort");
            var readerName = Read(document, "DefaultReaderName");
            var configured = !string.IsNullOrWhiteSpace(provider) && !string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase);
            return new RFiDGearReaderSettingsInfo(path, true, provider, port, readerName, configured,
                configured ? null : "In RFiDGear ist kein Reader-Provider ausgewählt.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return new RFiDGearReaderSettingsInfo(path, true, "", "", "", false, exception.Message);
        }
    }

    private static string Read(XDocument document, string name)
        => document.Descendants(name).FirstOrDefault()?.Value.Trim() ?? "";
}
