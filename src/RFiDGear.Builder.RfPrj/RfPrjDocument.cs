using System.Globalization;
using System.Xml.Linq;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed class RfPrjDocument
{
    private readonly XDocument document;

    private RfPrjDocument(XDocument document)
    {
        this.document = document;
        if (document.Root?.Name.LocalName != "ChipTaskHandlerModel") throw new InvalidDataException("taskdatabase.xml besitzt kein ChipTaskHandlerModel-Wurzelelement.");
        if (TaskCollection is null) throw new InvalidDataException("taskdatabase.xml besitzt keine TaskCollection.");
    }

    public string ManifestVersion => document.Root?.Element("ManifestVersion")?.Value ?? "";
    public IReadOnlyList<XElement> Tasks => TaskCollection.Elements("anyType").ToArray();
    internal XDocument Xml => document;
    private XElement TaskCollection => document.Root!.Element("TaskCollection")!;

    public static RfPrjDocument Create(string manifestVersion = "2.0.6")
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        return new RfPrjDocument(new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("ChipTaskHandlerModel",
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", xsd),
                new XElement("ManifestVersion", manifestVersion),
                new XElement("TaskCollection"))));
    }

    public static RfPrjDocument Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new RfPrjDocument(XDocument.Load(stream, LoadOptions.PreserveWhitespace));
    }

    public XElement Append(XElement task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.Name.LocalName != "anyType") throw new InvalidDataException("Task muss ein anyType-XML-Element sein.");
        var clone = new XElement(task);
        TaskCollection.Add(clone);
        return clone;
    }

    public IReadOnlyList<RfPrjTaskSummary> Summarize()
        => Tasks.Select((task, position) => new RfPrjTaskSummary(
            position,
            TaskXml.Get(task, "CurrentTaskIndex"),
            TaskXml.Get(task, "SelectedTaskType"),
            TaskXml.Get(task, "SelectedTaskDescription"),
            TaskXml.Get(task, "SelectedExecuteConditionTaskIndex"),
            TaskXml.Get(task, "SelectedExecuteConditionErrorLevel"),
            TaskXml.Get(task, "AppNumberCurrent"),
            TaskXml.Get(task, "FileNumberCurrent"),
            TaskXml.Get(task, "DesfireDataFilePath"),
            TaskXml.Get(task, "DesfireReadDataFilePath"),
            TaskXml.Get(task, "FileSizeCurrent"))).ToArray();
}

public sealed record RfPrjTaskSummary(
    int Position,
    string Index,
    string Type,
    string Description,
    string ConditionIndex,
    string ConditionLevel,
    string Aid,
    string FileId,
    string InputPath = "",
    string OutputPath = "",
    string FileSize = "");

public static class TaskXml
{
    public static string Get(XElement task, string name) => task.Element(name)?.Value ?? "";

    public static void Set(XElement task, string name, object? value)
    {
        var element = task.Element(name);
        if (element is null) { element = new XElement(name); task.Add(element); }
        element.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    public static void Remove(XElement task, string name) => task.Element(name)?.Remove();

    public static void SetCondition(XElement task, int? taskIndex, string errorLevel)
    {
        if (taskIndex is null)
        {
            Remove(task, "SelectedExecuteConditionTaskIndex");
            Set(task, "SelectedExecuteConditionErrorLevel", "Empty");
        }
        else
        {
            Set(task, "SelectedExecuteConditionTaskIndex", taskIndex.Value);
            Set(task, "SelectedExecuteConditionErrorLevel", errorLevel);
        }
    }

    public static void SetMetadata(XElement task, int index, string description, int? conditionIndex = null, string conditionLevel = "Empty")
    {
        Set(task, "CurrentTaskIndex", index);
        Set(task, "SelectedTaskDescription", description);
        SetCondition(task, conditionIndex, conditionLevel);
    }

    public static void SetKeySettings(XElement task, string settings, string changeMode)
    {
        if (settings.Length != 2 || !byte.TryParse(settings, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags) || flags > 0x0F)
            throw new InvalidDataException($"Ungültige KeySettings: {settings}");
        Set(task, "SelectedDesfireAppKeySettingsCreateNewApp", changeMode);
        Set(task, "IsAllowChangeMKChecked", BooleanText((flags & 0x01) != 0));
        Set(task, "IsAllowListingWithoutMKChecked", BooleanText((flags & 0x02) != 0));
        Set(task, "IsAllowCreateDelWithoutMKChecked", BooleanText((flags & 0x04) != 0));
        Set(task, "IsAllowConfigChangableChecked", BooleanText((flags & 0x08) != 0));
    }

    public static void SetPiccCredentials(XElement task, ProjectDefinition project, bool productive)
    {
        var key = productive ? project.Productive.PiccKey : project.Transport.PiccKey;
        Set(task, "SelectedDesfireMasterKeyEncryptionTypeCurrent", key.Type);
        Set(task, "DesfireMasterKeyCurrent", key.Value);
        Set(task, "SelectedDesfireMasterKeyEncryptionTypeTarget", key.Type);
        Set(task, "DesfireMasterKeyTarget", key.Value);
    }

    public static void SetApplication(XElement task, ApplicationDefinition application, ProjectDefinition project, string keySettings)
    {
        var aid = "0x" + ProfileValidator.NormalizeAid(application.Id);
        Set(task, "AppNumberNew", aid);
        Set(task, "AppNumberCurrent", aid);
        Set(task, "AppNumberTarget", aid);
        Set(task, "SelectedDesfireAppMaxNumberOfKeys", application.KeyCount);
        Set(task, "SelectedDesfireAppKeyEncryptionTypeCreateNewApp", application.KeyType);
        Set(task, "SelectedDesfireAppKeyEncryptionTypeCurrent", application.KeyType);
        Set(task, "SelectedDesfireAppKeyEncryptionTypeTarget", application.KeyType);
        SetKeySettings(task, keySettings, application.ChangeKeyMode);
        SetPiccCredentials(task, project, productive: false);
    }

    public static void SetApplicationCredentials(XElement task, string keyType, int keyNumber, string keyValue)
    {
        Set(task, "SelectedDesfireAppKeyEncryptionTypeCurrent", keyType);
        Set(task, "SelectedDesfireAppKeyNumberCurrent", keyNumber);
        Set(task, "DesfireAppKeyCurrent", keyValue);
        Set(task, "DesfireAppKeyCurrentOld", keyValue);
        Set(task, "DesfireAppKeyTarget", keyValue);
    }

    public static void SetReadWriteKeys(XElement task, ApplicationDefinition application, FileDefinition file, IReadOnlyList<string> currentKeys)
        => SetReadWriteKeys(task, application, file.Access, currentKeys);

    public static void SetReadWriteKeys(XElement task, ApplicationDefinition application, AccessDefinition access, IReadOnlyList<string> currentKeys)
    {
        if (access.Read >= currentKeys.Count || access.Write >= currentKeys.Count) throw new InvalidDataException("Read-/Write-Keynummer liegt außerhalb der bereitgestellten Schlüssel.");
        Set(task, "SelectedDesfireReadKeyNumber", access.Read);
        Set(task, "SelectedDesfireWriteKeyNumber", access.Write);
        Set(task, "SelectedDesfireReadKeyEncryptionType", application.KeyType);
        Set(task, "SelectedDesfireWriteKeyEncryptionType", application.KeyType);
        Set(task, "DesfireReadKeyCurrent", currentKeys[access.Read]);
        Set(task, "DesfireWriteKeyCurrent", currentKeys[access.Write]);
    }

    public static void UseCurrentApplicationOnly(XElement task)
    {
        var current = Get(task, "AppNumberCurrent");
        if (!string.IsNullOrEmpty(current)) Set(task, "AppNumberNew", current);
    }

    public static void SetFile(XElement task, FileDefinition file, bool creationSettings = false)
    {
        var communication = creationSettings && file.CreationSettings is not null ? file.CreationSettings.Communication : file.Communication;
        var access = creationSettings && file.CreationSettings is not null ? file.CreationSettings.Access : file.Access;
        Set(task, "FileNumberCurrent", file.Id);
        Set(task, "FileSizeCurrent", file.Size);
        Set(task, "SelectedDesfireFileType", file.Type);
        Set(task, "SelectedDesfireFileCryptoMode", communication);
        Set(task, "SelectedDesfireFileAccessRightRead", Access(access.Read));
        Set(task, "SelectedDesfireFileAccessRightWrite", Access(access.Write));
        Set(task, "SelectedDesfireFileAccessRightReadWrite", Access(access.ReadWrite));
        Set(task, "SelectedDesfireFileAccessRightChange", Access(access.Change));
    }

    public static void SetData(XElement task, string aid, int fileId, ReadOnlySpan<byte> bytes)
    {
        foreach (var rootName in new[] { "ChildNodeViewModelFromChip", "ChildNodeViewModelTemp" })
        {
            var root = task.Element(rootName);
            var grandChild = root?.Element("Children")?.Element("RFiDChipGrandChildLayerViewModel");
            if (root is null || grandChild is null) continue;
            Set(grandChild, "DataAsHexString", Convert.ToHexString(bytes));
            var desfireFile = grandChild.Element("DesfireFile");
            if (desfireFile is not null)
            {
                Set(desfireFile, "Data", Convert.ToBase64String(bytes));
                Set(desfireFile, "FileID", fileId);
            }
            Set(grandChild, "GrandChildNodeHeader", $"File No.: [{fileId:D3}]");
            Set(grandChild, "SelectedDataLengthInBytes", bytes.Length);
            Set(root, "ChildNodeHeader", $"AppID: 0x{aid}");
        }
    }

    private static string Access(int keyNumber)
    {
        if (keyNumber is < 0 or > 13) throw new InvalidDataException($"Access-Key {keyNumber} wird nicht unterstützt.");
        return $"AR_KEY{keyNumber}";
    }

    private static string BooleanText(bool value) => value ? "true" : "false";
}
