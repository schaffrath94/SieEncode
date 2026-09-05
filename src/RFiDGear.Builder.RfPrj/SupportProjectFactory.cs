using System.Xml.Linq;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed class SupportProjectFactory(TaskShellLibrary shells)
{
    public RfPrjDocument BuildStandaloneCardRecognition()
    {
        var document = RfPrjDocument.Create();
        var inventory = shells.Create("AppExistCheck");
        TaskXml.Set(inventory, "SelectedTaskType", "PICCApplicationInventory");
        TaskXml.SetMetadata(inventory, 10, "[ANALYSE NUR LESEN] Vollständige PICC-Applikationsinventur");
        document.Append(inventory);
        var index = 10;
        AddPiccProbe(document, ref index, "STANDARD_DES", "DF_KEY_DES", new string('0', 32));
        AddPiccProbe(document, ref index, "STANDARD_AES", "DF_KEY_AES", new string('0', 32));
        return document;
    }

    public RfPrjDocument BuildStandaloneFormat(string keyType, string keyValue)
    {
        if (string.IsNullOrWhiteSpace(keyType)) throw new ArgumentException("PICC-Schlüsseltyp fehlt.", nameof(keyType));
        if (string.IsNullOrWhiteSpace(keyValue)) throw new ArgumentException("PICC-Schlüssel fehlt.", nameof(keyValue));
        var document = RfPrjDocument.Create();
        var format = shells.Create("FormatDesfireCard");
        TaskXml.SetMetadata(format, 10, "[WARTUNG DESTRUKTIV] PICC vollständig formatieren");
        TaskXml.Set(format, "SelectedDesfireMasterKeyEncryptionTypeCurrent", keyType);
        TaskXml.Set(format, "DesfireMasterKeyCurrent", keyValue);
        TaskXml.Set(format, "AppNumberCurrent", "0");
        TaskXml.Set(format, "AppNumberNew", "0");
        TaskXml.Set(format, "AppNumberTarget", "0");
        document.Append(format);
        return document;
    }

    public RfPrjDocument BuildApplicationFileInventory(string aid, string keyType, int keyNumber,
        string keyValue, string outputPath, bool readContent)
    {
        var normalizedAid = ProfileValidator.NormalizeAid(aid);
        if (normalizedAid == "000000") throw new ArgumentException("Für die File-Inventur muss eine Application-AID gewählt werden.", nameof(aid));
        if (keyNumber is < 0 or > 13) throw new ArgumentOutOfRangeException(nameof(keyNumber));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Ausgabepfad fehlt.", nameof(outputPath));
        var document = RfPrjDocument.Create();
        var task = shells.Create("ApplicationKeySettingsChangeover");
        TaskXml.Set(task, "SelectedTaskType", "ApplicationFileInventory");
        TaskXml.SetMetadata(task, 10, $"[ANALYSE NUR LESEN] AID {normalizedAid}: Files und optional lesbare Inhalte inventarisieren");
        TaskXml.Set(task, "AppNumberCurrent", "0x" + normalizedAid);
        TaskXml.Set(task, "AppNumberNew", "0x" + normalizedAid);
        TaskXml.Set(task, "AppNumberTarget", "0x" + normalizedAid);
        TaskXml.Set(task, "SelectedDesfireAppKeyEncryptionTypeCurrent", keyType);
        TaskXml.Set(task, "SelectedDesfireAppKeyNumberCurrent", keyNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        TaskXml.Set(task, "DesfireAppKeyCurrent", keyValue);
        TaskXml.Set(task, "ApplicationInventoryOutputPath", outputPath);
        TaskXml.Set(task, "ApplicationInventoryReadContent", readContent ? "true" : "false");
        document.Append(task);
        return document;
    }

    public RfPrjDocument BuildCardRecognition(ProjectDefinition project, bool includeProductive)
    {
        ArgumentNullException.ThrowIfNull(project);
        var document = RfPrjDocument.Create();
        var index = 10;

        var inventory = shells.Create("AppExistCheck");
        TaskXml.Set(inventory, "SelectedTaskType", "PICCApplicationInventory");
        TaskXml.SetMetadata(inventory, index, "[ERKENNUNG NUR LESEN] Vollständige PICC-Applikationsinventur");
        document.Append(inventory);

        AddPiccProbe(document, ref index, "STANDARD_DES", "DF_KEY_DES", new string('0', 32));
        AddPiccProbe(document, ref index, "STANDARD_AES", "DF_KEY_AES", new string('0', 32));
        AddPiccProbe(document, ref index, "TRANSPORT", project.Transport.PiccKey.Type, project.Transport.PiccKey.Value);
        if (includeProductive && !CardOperationMode.IsExtension(project))
            AddPiccProbe(document, ref index, "PRODUCTIVE", project.Productive.PiccKey.Type, project.Productive.PiccKey.Value);

        return document;
    }

    private void AddPiccProbe(RfPrjDocument document, ref int index,
        string credentialName, string keyType, string keyValue)
    {
        index += 10;
        var probe = shells.Create("AppExistCheck");
        TaskXml.Set(probe, "SelectedTaskType", "PICCAuthCheck");
        TaskXml.SetMetadata(probe, index, $"[ERKENNUNG NUR LESEN] PICC-Schlüsselstatus {credentialName}");
        TaskXml.Set(probe, "AppNumberCurrent", "0");
        TaskXml.Set(probe, "AppNumberNew", "0");
        TaskXml.Set(probe, "AppNumberTarget", "0");
        TaskXml.Set(probe, "SelectedDesfireMasterKeyEncryptionTypeCurrent", keyType);
        TaskXml.Set(probe, "DesfireMasterKeyCurrent", keyValue);
        document.Append(probe);
    }

    public RfPrjDocument BuildApplicationAcceptance(ComposedProfiles profiles, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(project);
        var document = RfPrjDocument.Create();
        var index = 10;
        foreach (var application in CardOperationMode.SelectApplications(profiles, project))
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var keys = GetProductiveKeys(project, application);

            var settings = AcceptanceTask("ReadAppSettings", application, project, keys[0]);
            TaskXml.SetMetadata(settings, index, $"[ABNAHME NUR LESEN] {application.Name}: AID {aid} KeySettings exakt prüfen");
            TaskXml.Set(settings, "IsAppSettingsExactMatch", "true");
            document.Append(settings);
            index += 10;

            var count = AcceptanceTask("CheckAppKeyCount", application, project, keys[0]);
            TaskXml.SetMetadata(count, index, $"[ABNAHME NUR LESEN] {application.Name}: AID {aid} Keyanzahl exakt prüfen");
            document.Append(count);
            index += 10;

            // RFiDGear 2.0.10 exposes implementation markers for CheckFileSettings,
            // but its project enum cannot deserialize that task type. Generating it
            // would make RFiDGear silently execute an empty task loop. File metadata
            // therefore remains explicitly "not verified" until a compatible,
            // execution-tested runtime is available.
        }
        return document;
    }

    private XElement AcceptanceTask(string taskType, ApplicationDefinition application, ProjectDefinition project, string key)
    {
        var task = shells.Create("ApplicationKeySettingsChangeover");
        TaskXml.Set(task, "SelectedTaskType", taskType);
        TaskXml.SetApplication(task, application, project, application.ProductiveKeySettings);
        TaskXml.UseCurrentApplicationOnly(task);
        TaskXml.SetApplicationCredentials(task, application.KeyType, 0, key);
        return task;
    }

    public RfPrjDocument BuildRecovery(ProjectDefinition project, bool productive)
    {
        ArgumentNullException.ThrowIfNull(project);
        var mode = productive ? "Productive" : "Transport";
        var document = RfPrjDocument.Create();
        var format = shells.Create("FormatDesfireCard");
        TaskXml.SetMetadata(format, 10, $"RECOVERY: PICC mit {mode}-PICC-Key formatieren – ALLE Applications/Files werden gelöscht");
        TaskXml.SetPiccCredentials(format, project, productive);
        TaskXml.Set(format, "AppNumberCurrent", "0");
        TaskXml.Set(format, "AppNumberNew", "0");
        TaskXml.Set(format, "AppNumberTarget", "0");
        document.Append(format);

        if (productive)
        {
            var reset = shells.Create("PICCMasterKeyChangeover");
            TaskXml.SetMetadata(reset, 20, "RECOVERY: PICC-MasterKey nach Formatierung auf Transport-PICC zurücksetzen", 10, "NoError");
            TaskXml.Set(reset, "AppNumberCurrent", "0");
            TaskXml.Set(reset, "AppNumberNew", "0");
            TaskXml.Set(reset, "AppNumberTarget", "0");
            TaskXml.Set(reset, "SelectedDesfireMasterKeyEncryptionTypeCurrent", project.Productive.PiccKey.Type);
            TaskXml.Set(reset, "DesfireMasterKeyCurrent", project.Productive.PiccKey.Value);
            TaskXml.Set(reset, "SelectedDesfireMasterKeyEncryptionTypeTarget", project.Transport.PiccKey.Type);
            TaskXml.Set(reset, "DesfireMasterKeyTarget", project.Transport.PiccKey.Value);
            document.Append(reset);
        }
        return document;
    }

    public RfPrjDocument BuildExtensionRollback(ComposedProfiles profiles, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(project);
        if (!CardOperationMode.IsExtension(project))
            throw new InvalidOperationException("Der gezielte Erweiterungs-Rückbau ist nur im Erweiterungsmodus zulässig.");
        var document = RfPrjDocument.Create();
        var index = 10;
        foreach (var application in CardOperationMode.SelectApplications(profiles, project))
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var task = shells.Create("CreateApplication");
            TaskXml.Set(task, "SelectedTaskType", "DeleteApplication");
            TaskXml.SetMetadata(task, index, $"ERWEITERUNG ZURÜCKNEHMEN: ausschließlich Ziel-AID {aid} ({application.Name}) löschen");
            TaskXml.SetPiccCredentials(task, project, productive: false);
            TaskXml.SetApplication(task, application, project, application.ProductiveKeySettings);
            TaskXml.SetApplicationCredentials(task, application.KeyType, 0, GetProductiveKeys(project, application)[0]);
            TaskXml.Set(task, "AppNumberCurrent", "0");
            TaskXml.Set(task, "AppNumberNew", "0x" + aid);
            TaskXml.Set(task, "AppNumberTarget", "0");
            document.Append(task);
            index += 10;
        }
        return document;
    }

    public RfPrjDocument BuildCardPreflight(ComposedProfiles profiles, ProjectDefinition project, string projectDirectory, bool productive)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var mode = productive ? "Productive" : "Transport";
        var document = RfPrjDocument.Create();
        var index = 10;
        foreach (var application in CardOperationMode.SelectApplications(profiles, project))
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var keys = productive ? GetProductiveKeys(project, application) : GetTransportKeys(project, application);
            foreach (var file in application.Files)
            {
                var read = shells.Create("ReadData");
                TaskXml.SetMetadata(read, index, $"[KARTEN-PREFLIGHT NUR LESEN] {application.Name}: AID {aid} File {file.Id} '{file.Name}' mit {mode}keys prüfen");
                TaskXml.SetApplication(read, application, project, productive ? application.ProductiveKeySettings : application.TransportKeySettings);
                TaskXml.SetPiccCredentials(read, project, productive);
                TaskXml.UseCurrentApplicationOnly(read);
                TaskXml.SetApplicationCredentials(read, application.KeyType, 0, keys[0]);
                TaskXml.SetFile(read, file);
                TaskXml.SetReadWriteKeys(read, application, file, keys);
                TaskXml.SetData(read, aid, file.Id, new byte[file.Size]);
                var probePath = Path.Combine(projectDirectory, "card_preflight", mode.ToLowerInvariant(), $"{aid}_File{file.Id:D2}.hex");
                TaskXml.Set(read, "DesfireReadDataFilePath", probePath);
                TaskXml.Set(read, "OverwriteReadDataFileOnRead", "true");
                document.Append(read);
                index += 10;
            }
        }
        return document;
    }

    private static IReadOnlyList<string> GetTransportKeys(ProjectDefinition project, ApplicationDefinition application)
    {
        if (!project.Transport.ApplicationKeys.TryGetValue(application.KeyType, out var key)) throw new InvalidDataException($"Transport Application Key für {application.KeyType} fehlt.");
        return Enumerable.Repeat(key, application.KeyCount).ToArray();
    }

    private static IReadOnlyList<string> GetProductiveKeys(ProjectDefinition project, ApplicationDefinition application)
    {
        var aid = ProfileValidator.NormalizeAid(application.Id);
        if (!project.Productive.ApplicationKeys.TryGetValue(aid, out var keys) || keys.Count != application.KeyCount)
            throw new InvalidDataException($"Produktivschlüssel für AID {aid} fehlen oder besitzen die falsche Anzahl.");
        return keys;
    }
}
