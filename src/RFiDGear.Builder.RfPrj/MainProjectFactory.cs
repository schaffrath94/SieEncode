using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed class MainProjectFactory(TaskShellLibrary shells)
{
    public RfPrjDocument BuildMastering(ComposedProfiles profiles, ProjectDefinition project, string projectDirectory)
    {
        var document = RfPrjDocument.Create();
        var next = 10;
        var applications = CardOperationMode.SelectApplications(profiles, project);
        foreach (var application in applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var transportKey = GetTransportKey(project, application);
            var currentKeys = Enumerable.Repeat(transportKey, application.KeyCount).ToArray();
            var referenceFile = application.Files.FirstOrDefault();

            var preflight = shells.Create("AppExistCheck");
            TaskXml.SetMetadata(preflight, next, $"{application.Name}: Vorprüfung – Application {aid} darf noch nicht vorhanden sein");
            SetAppExistCheck(preflight, application, project, transportKey);
            if (referenceFile is not null) { TaskXml.SetFile(preflight, referenceFile); TaskXml.SetReadWriteKeys(preflight, application, referenceFile, currentKeys); }
            document.Append(preflight);
            var preflightIndex = next;
            next += 10;

            var createApplication = shells.Create("CreateApplication");
            TaskXml.SetMetadata(createApplication, next, $"{application.Name}: Application {aid} erstellen", preflightIndex, "IsNotTrue");
            TaskXml.SetApplication(createApplication, application, project, application.TransportKeySettings);
            TaskXml.Set(createApplication, "AppNumberCurrent", "0");
            TaskXml.Set(createApplication, "AppNumberTarget", "0");
            TaskXml.SetApplicationCredentials(createApplication, application.KeyType, 0, transportKey);
            if (referenceFile is not null) { TaskXml.SetFile(createApplication, referenceFile); TaskXml.SetReadWriteKeys(createApplication, application, referenceFile, currentKeys); }
            document.Append(createApplication);
            var previous = next;
            next += 10;

            foreach (var file in application.Files)
            {
                var createFile = shells.Create("CreateFile");
                TaskXml.SetMetadata(createFile, next, $"{application.Name}: File {file.Id} '{file.Name}' erstellen", previous, "NoError");
                TaskXml.SetApplication(createFile, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(createFile);
                TaskXml.SetApplicationCredentials(createFile, application.KeyType, 0, currentKeys[0]);
                TaskXml.SetFile(createFile, file, creationSettings: true);
                TaskXml.SetReadWriteKeys(createFile, application, file.CreationSettings?.Access ?? file.Access, currentKeys);
                TaskXml.SetData(createFile, aid, file.Id, new byte[file.Size]);
                document.Append(createFile);
                previous = next;
                next += 10;
            }

            foreach (var file in application.Files.Where(RequiresZeroFill))
            {
                var zero = new byte[file.Size];
                var initializationName = $"{aid}_File{file.Id:D2}_{file.Size}x00.hex";
                var initializationPath = Path.Combine(projectDirectory, "initialization", initializationName);
                var readbackPath = Path.Combine(projectDirectory, "readback_mastering", initializationName);
                var creationAccess = file.CreationSettings?.Access ?? file.Access;

                var write = shells.Create("WriteData");
                TaskXml.SetMetadata(write, next, $"{application.Name}: File {file.Id} vollständig mit 00 initialisieren", previous, "NoError");
                TaskXml.SetApplication(write, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(write);
                TaskXml.SetFile(write, file, creationSettings: true);
                TaskXml.SetReadWriteKeys(write, application, creationAccess, currentKeys);
                TaskXml.SetData(write, aid, file.Id, zero);
                TaskXml.Set(write, "DesfireDataFilePath", initializationPath);
                TaskXml.Set(write, "RefreshDesfireDataFromFileBeforeWrite", "true");
                document.Append(write);
                var writeIndex = next;
                next += 10;

                var read = shells.Create("ReadData");
                TaskXml.SetMetadata(read, next, $"{application.Name}: Nullinitialisierung File {file.Id} zurücklesen", writeIndex, "NoError");
                TaskXml.SetApplication(read, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(read);
                TaskXml.SetFile(read, file, creationSettings: true);
                TaskXml.SetReadWriteKeys(read, application, creationAccess, currentKeys);
                TaskXml.SetData(read, aid, file.Id, zero);
                TaskXml.Set(read, "DesfireReadDataFilePath", readbackPath);
                TaskXml.Set(read, "OverwriteReadDataFileOnRead", "true");
                document.Append(read);
                previous = next;
                next += 10;
            }

            foreach (var file in application.Files.Where(file => file.CreationSettings is not null))
            {
                var changeKeyNumber = file.CreationSettings!.Access.Change;
                var change = shells.Create("ChangeFileSettings");
                TaskXml.SetMetadata(change, next, $"{application.Name}: File {file.Id} auf Profil-Endzustand härten", previous, "NoError");
                TaskXml.SetApplication(change, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(change);
                TaskXml.SetApplicationCredentials(change, application.KeyType, changeKeyNumber, currentKeys[changeKeyNumber]);
                TaskXml.SetFile(change, file);
                TaskXml.SetReadWriteKeys(change, application, file, currentKeys);
                document.Append(change);
                previous = next;
                next += 10;
            }

            var postflight = shells.Create("AppExistCheck");
            TaskXml.SetMetadata(postflight, next, $"{application.Name}: Nachprüfung – Application {aid} vorhanden", previous, "NoError");
            SetAppExistCheck(postflight, application, project, transportKey);
            if (referenceFile is not null) { TaskXml.SetFile(postflight, referenceFile); TaskXml.SetReadWriteKeys(postflight, application, referenceFile, currentKeys); }
            document.Append(postflight);
            next += 10;
        }
        return document;
    }

    public RfPrjDocument BuildCoding(ComposedProfiles profiles, ProjectDefinition project, string projectDirectory)
    {
        var document = RfPrjDocument.Create();
        var next = 10;
        int? previous = null;
        var applications = CardOperationMode.SelectApplications(profiles, project);
        foreach (var application in applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var transportKey = GetTransportKey(project, application);
            var currentKeys = Enumerable.Repeat(transportKey, application.KeyCount).ToArray();
            var referenceFile = application.Files.FirstOrDefault();
            var check = shells.Create("AppExistCheck");
            TaskXml.SetMetadata(check, next, $"{application.Name}: Preflight – Transport-Application {aid} vorhanden", previous, previous is null ? "Empty" : "NoError");
            SetAppExistCheck(check, application, project, transportKey);
            if (referenceFile is not null) { TaskXml.SetFile(check, referenceFile); TaskXml.SetReadWriteKeys(check, application, referenceFile, currentKeys); }
            document.Append(check);
            previous = next;
            next += 10;
        }

        foreach (var application in applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var transportKey = GetTransportKey(project, application);
            var currentKeys = Enumerable.Repeat(transportKey, application.KeyCount).ToArray();
            var productiveKeys = GetProductiveKeys(project, application).ToArray();
            var referenceFile = application.Files.FirstOrDefault();
            foreach (var file in application.Files.Where(file => file.Coding is not null))
            {
                var zero = new byte[file.Size];
                var payloadPath = Path.Combine(projectDirectory, "payloads", file.Coding!.Output);
                var readbackPath = Path.Combine(projectDirectory, "readback", file.Coding.Output);
                var write = shells.Create("WriteData");
                TaskXml.SetMetadata(write, next, $"{application.Name}: projektspezifische Daten in File {file.Id} schreiben", previous, "NoError");
                TaskXml.SetApplication(write, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(write);
                TaskXml.SetFile(write, file);
                TaskXml.SetReadWriteKeys(write, application, file, currentKeys);
                TaskXml.SetData(write, aid, file.Id, zero);
                TaskXml.Set(write, "DesfireDataFilePath", payloadPath);
                TaskXml.Set(write, "RefreshDesfireDataFromFileBeforeWrite", "true");
                document.Append(write);
                var writeIndex = next;
                next += 10;

                var read = shells.Create("ReadData");
                TaskXml.SetMetadata(read, next, $"{application.Name}: projektspezifische Daten aus File {file.Id} zurücklesen", writeIndex, "NoError");
                TaskXml.SetApplication(read, application, project, application.TransportKeySettings);
                TaskXml.UseCurrentApplicationOnly(read);
                TaskXml.SetFile(read, file);
                TaskXml.SetReadWriteKeys(read, application, file, currentKeys);
                TaskXml.SetData(read, aid, file.Id, zero);
                TaskXml.Set(read, "DesfireReadDataFilePath", readbackPath);
                TaskXml.Set(read, "OverwriteReadDataFileOnRead", "true");
                document.Append(read);
                previous = next;
                next += 10;
            }

            foreach (var targetKeyNumber in Enumerable.Range(1, application.KeyCount - 1).Append(0))
            {
                var change = shells.Create("ApplicationKeyChangeover");
                TaskXml.SetMetadata(change, next, $"{application.Name}: Key {targetKeyNumber} vom Transport- auf Produktivschlüssel ändern", previous, "NoError");
                TaskXml.SetApplication(change, application, project, application.TransportKeySettings);
                TaskXml.Set(change, "SelectedDesfireAppKeyNumberCurrent", targetKeyNumber);
                var oldTarget = currentKeys[targetKeyNumber];
                if (application.ChangeKeyMode == "ChangeKeyUsingMK" && targetKeyNumber != 0)
                {
                    TaskXml.Set(change, "DesfireAppKeyCurrent", currentKeys[0]);
                    TaskXml.Set(change, "DesfireAppKeyCurrentOld", oldTarget);
                }
                else
                {
                    TaskXml.Set(change, "DesfireAppKeyCurrent", oldTarget);
                    TaskXml.Set(change, "DesfireAppKeyCurrentOld", oldTarget);
                }
                TaskXml.Set(change, "SelectedDesfireAppKeyEncryptionTypeTarget", application.KeyType);
                TaskXml.Set(change, "DesfireAppKeyTarget", productiveKeys[targetKeyNumber]);
                if (referenceFile is not null) { TaskXml.SetFile(change, referenceFile); TaskXml.SetReadWriteKeys(change, application, referenceFile, currentKeys); }
                document.Append(change);
                currentKeys[targetKeyNumber] = productiveKeys[targetKeyNumber];
                previous = next;
                next += 10;
            }

            var settings = shells.Create("ApplicationKeySettingsChangeover");
            TaskXml.SetMetadata(settings, next, $"{application.Name}: produktive Application KeySettings {application.ProductiveKeySettings} setzen", previous, "NoError");
            TaskXml.SetApplication(settings, application, project, application.ProductiveKeySettings);
            TaskXml.SetApplicationCredentials(settings, application.KeyType, 0, currentKeys[0]);
            if (referenceFile is not null) { TaskXml.SetFile(settings, referenceFile); TaskXml.SetReadWriteKeys(settings, application, referenceFile, currentKeys); }
            document.Append(settings);
            previous = next;
            next += 10;
        }

        if (!CardOperationMode.IsExtension(project))
        {
            var piccChange = shells.Create("PICCMasterKeyChangeover");
            TaskXml.SetMetadata(piccChange, next, "PICC: Transport-MasterKey auf produktiven PICC-MasterKey ändern", previous, "NoError");
            SetPICCNumbersToZero(piccChange);
            TaskXml.Set(piccChange, "SelectedDesfireMasterKeyEncryptionTypeCurrent", project.Transport.PiccKey.Type);
            TaskXml.Set(piccChange, "DesfireMasterKeyCurrent", project.Transport.PiccKey.Value);
            TaskXml.Set(piccChange, "SelectedDesfireMasterKeyEncryptionTypeTarget", project.Productive.PiccKey.Type);
            TaskXml.Set(piccChange, "DesfireMasterKeyTarget", project.Productive.PiccKey.Value);
            document.Append(piccChange);
            previous = next;
            next += 10;

            var piccSettings = shells.Create("PICCMasterKeySettingsChangeover");
            TaskXml.SetMetadata(piccSettings, next, "PICC: produktive MasterKeySettings setzen", previous, "NoError");
            SetPICCNumbersToZero(piccSettings);
            TaskXml.Set(piccSettings, "SelectedDesfireMasterKeyEncryptionTypeCurrent", project.Productive.PiccKey.Type);
            TaskXml.Set(piccSettings, "DesfireMasterKeyCurrent", project.Productive.PiccKey.Value);
            TaskXml.Set(piccSettings, "SelectedDesfireMasterKeyEncryptionTypeTarget", project.Productive.PiccKey.Type);
            TaskXml.Set(piccSettings, "DesfireMasterKeyTarget", project.Productive.PiccKey.Value);
            TaskXml.SetKeySettings(piccSettings, project.Productive.PiccKeySettings, "ChangeKeyUsingMK");
            document.Append(piccSettings);
            previous = next;
            next += 10;
        }

        foreach (var application in applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            var productiveKeys = GetProductiveKeys(project, application);
            foreach (var file in application.Files.Where(file => file.Coding is not null))
            {
                var verify = shells.Create("ReadData");
                TaskXml.SetMetadata(verify, next, $"{application.Name}: Endprüfung – File {file.Id} mit Produktivkey zurücklesen", previous, "NoError");
                TaskXml.SetApplication(verify, application, project, application.ProductiveKeySettings);
                TaskXml.UseCurrentApplicationOnly(verify);
                TaskXml.SetFile(verify, file);
                TaskXml.SetReadWriteKeys(verify, application, file, productiveKeys);
                TaskXml.SetData(verify, aid, file.Id, new byte[file.Size]);
                TaskXml.Set(verify, "DesfireReadDataFilePath", Path.Combine(projectDirectory, "verification", file.Coding!.Output));
                TaskXml.Set(verify, "OverwriteReadDataFileOnRead", "true");
                document.Append(verify);
                previous = next;
                next += 10;
            }
        }
        return document;
    }

    private static void SetAppExistCheck(System.Xml.Linq.XElement task, ApplicationDefinition application, ProjectDefinition project, string transportKey)
    {
        TaskXml.SetApplication(task, application, project, application.TransportKeySettings);
        TaskXml.Set(task, "AppNumberCurrent", "0");
        TaskXml.Set(task, "AppNumberTarget", "0");
        TaskXml.SetApplicationCredentials(task, application.KeyType, 0, transportKey);
    }

    private static void SetPICCNumbersToZero(System.Xml.Linq.XElement task)
    {
        TaskXml.Set(task, "AppNumberCurrent", "0");
        TaskXml.Set(task, "AppNumberNew", "0");
        TaskXml.Set(task, "AppNumberTarget", "0");
    }

    private static bool RequiresZeroFill(FileDefinition file)
        => file.MasteringInitialization?.Type switch { "none" => false, "zeroFill" or null => file.Type is "StdDataFile" or "BackupFile", var value => throw new InvalidDataException($"Unbekannte Mastering-Initialisierung: {value}") };

    private static string GetTransportKey(ProjectDefinition project, ApplicationDefinition application)
        => project.Transport.ApplicationKeys.TryGetValue(application.KeyType, out var key) ? key : throw new InvalidDataException($"Transport Application Key für {application.KeyType} fehlt.");

    private static IReadOnlyList<string> GetProductiveKeys(ProjectDefinition project, ApplicationDefinition application)
    {
        var aid = ProfileValidator.NormalizeAid(application.Id);
        return project.Productive.ApplicationKeys.TryGetValue(aid, out var keys) && keys.Count == application.KeyCount ? keys : throw new InvalidDataException($"Produktivschlüssel für AID {aid} fehlen oder besitzen die falsche Anzahl.");
    }
}
