using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record ContractArtifactContext(
    ProjectDefinition Project,
    ComposedProfiles Profiles,
    string SourceConfigPath,
    string BuilderVersion,
    DateTime GeneratedAt);

public static class ContractArtifactFactory
{
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyDictionary<string, JsonNode> Build(ContractArtifactContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var coding = context.Project.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        var artifacts = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase)
        {
            ["recovery.plan.json"] = BuildRecoveryPlan(context, coding),
            ["card-preflight.plan.json"] = BuildCardPreflightPlan(context, coding),
            ["card-recognition.plan.json"] = BuildCardRecognitionPlan(context, coding),
            ["project.definition.json"] = BuildProjectDefinition(context),
            ["builder.manifest.json"] = BuildManifest(context, coding),
            ["acceptance.plan.json"] = BuildAcceptancePlan(context, coding),
            ["rfidgear.host-contract.json"] = BuildHostContract(context, coding)
        };
        if (coding) artifacts["coding.schema.json"] = BuildCodingSchema(context);
        return artifacts;
    }

    public static void SaveAll(string outputDirectory, IReadOnlyDictionary<string, JsonNode> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        foreach (var (name, document) in artifacts)
        {
            if (Path.GetFileName(name) != name) throw new InvalidDataException($"Ungültiger Artefaktname: {name}");
            AtomicJson.Write(Path.Combine(root, name), document);
        }
    }

    private static JsonObject BuildRecoveryPlan(ContractArtifactContext context, bool coding)
    {
        var extension = CardOperationMode.IsExtension(context.Project);
        var plan = new JsonObject
        {
        ["schemaVersion"] = "1.0",
        ["projectName"] = context.Project.Project.Name,
        ["generatedAt"] = Timestamp(context),
        ["destructive"] = !extension,
        ["transportProject"] = extension ? null : context.Project.Project.Name + "_Recovery_Format_TransportPICC.rfPrj",
        ["productiveProject"] = !extension && coding ? context.Project.Project.Name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj" : null,
        ["productiveResetsPiccToTransport"] = !extension && coding,
        ["resultAfterTransport"] = extension ? null : "Applications/Files gelöscht; PICC-MasterKey bleibt Transport.",
        ["resultAfterProductive"] = !extension && coding ? "Applications/Files gelöscht; PICC-MasterKey wird anschließend auf Transport zurückgesetzt." : null,
        ["warning"] = extension
            ? "Formatieren ist gesperrt. Der gezielte Rückbau löscht ausschließlich die neuen Ziel-AIDs des Zusatzprofils."
            : "Recovery ist destruktiv und darf nur bewusst nach einem fehlgeschlagenen/zu verwerfenden Kartenlauf verwendet werden."
        };
        if (extension) plan["extensionRollbackProject"] = context.Project.Project.Name + "_Erweiterung_Rueckbau.rfPrj";
        return plan;
    }

    private static JsonObject BuildCardRecognitionPlan(ContractArtifactContext context, bool coding)
    {
        var extension = CardOperationMode.IsExtension(context.Project);
        var plan = new JsonObject
        {
        ["schemaVersion"] = "1.0",
        ["projectName"] = context.Project.Project.Name,
        ["generatedAt"] = Timestamp(context),
        ["readOnly"] = true,
        ["project"] = context.Project.Project.Name + "_CardRecognition_ReadOnly.rfPrj",
        ["inventory"] = "complete-aid-list",
        ["piccCredentialProbes"] = coding
            ? new JsonArray("STANDARD_DES", "STANDARD_AES", "TRANSPORT", "PRODUCTIVE")
            : new JsonArray("STANDARD_DES", "STANDARD_AES", "TRANSPORT"),
        ["classificationRule"] = "Empty requires a successful inventory with exactly zero applications. Authentication failures alone never classify a card as empty.",
        ["limitations"] = new JsonArray(
            "PICC-KeySettings werden nur gelesen und niemals verändert.",
            "Ein unbekannter PICC-Key bleibt UNKNOWN; es findet keine Schlüsselsuche statt.")
        };
        if (extension)
        {
            var extensionAids = CardOperationMode.SelectApplications(context.Profiles, context.Project)
                .Select(application => ProfileValidator.NormalizeAid(application.Id)).ToArray();
            plan["cardMode"] = CardOperationMode.ExtendExisting;
            plan["extensionTargetAids"] = new JsonArray(extensionAids.Select(value => JsonValue.Create(value)).ToArray());
        }
        return plan;
    }

    private static JsonObject BuildCardPreflightPlan(ContractArtifactContext context, bool coding)
    {
        var files = new JsonArray();
        foreach (var application in CardOperationMode.SelectApplications(context.Profiles, context.Project))
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            foreach (var file in application.Files)
            {
                files.Add(new JsonObject
                {
                    ["aid"] = aid,
                    ["applicationName"] = application.Name,
                    ["fileId"] = file.Id,
                    ["name"] = file.Name,
                    ["type"] = file.Type,
                    ["size"] = file.Size,
                    ["communication"] = file.Communication,
                    ["accessText"] = AccessText(file.Access),
                    ["readKeyNo"] = file.Access.Read,
                    ["codingPayload"] = file.Coding is null ? null : Path.Combine("payloads", file.Coding.Output),
                    ["transportOutput"] = Path.Combine("card_preflight\\\\transport", $"{aid}_File{file.Id:D2}.hex"),
                    ["productiveOutput"] = Path.Combine("card_preflight\\\\productive", $"{aid}_File{file.Id:D2}.hex")
                });
            }
        }
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["projectName"] = context.Project.Project.Name,
            ["generatedAt"] = Timestamp(context),
            ["readOnly"] = true,
            ["transportProject"] = context.Project.Project.Name + "_CardPreflight_Transport.rfPrj",
            ["productiveProject"] = coding ? context.Project.Project.Name + "_CardPreflight_Productive.rfPrj" : null,
            ["transportDirectory"] = "card_preflight\\\\transport",
            ["productiveDirectory"] = "card_preflight\\\\productive",
            ["productiveProbeAvailable"] = coding,
            ["credentialSetsEquivalent"] = coding && CredentialSetsEquivalent(context),
            ["files"] = files,
            ["limitations"] = new JsonArray(
                "Phase 1 prüft erwartete Files ausschließlich über ReadData.",
                "Keine sichere UID-/vollständige AID-/FileSettings-Inventur.",
                "Kein lesbares File bedeutet nicht automatisch, dass der Transponder leer ist.")
        };
    }

    private static JsonObject BuildProjectDefinition(ContractArtifactContext context)
    {
        var applications = new JsonArray();
        foreach (var application in context.Profiles.Applications)
            applications.Add(JsonSerializer.SerializeToNode(application, ProfileJsonOptions));
        return new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = context.Project.Project.Name, ["generatedAt"] = Timestamp(context) },
            ["profiles"] = ProfileSummary(context),
            ["applications"] = applications
        };
    }

    private static JsonObject BuildManifest(ContractArtifactContext context, bool coding)
    {
        var usesChangeFileSettings = CardOperationMode.SelectApplications(context.Profiles, context.Project).SelectMany(application => application.Files).Any(file => file.CreationSettings is not null);
        var features = new JsonObject
        {
            ["changeFileSettings"] = usesChangeFileSettings,
            ["finalVerification"] = coding,
            ["payloadOnly"] = coding,
            ["acceptancePlan"] = true,
            ["cardPreflightReadOnly"] = true,
            ["cardRecognitionReadOnly"] = true,
            ["masteringFullInitialization"] = !CardOperationMode.IsExtension(context.Project),
            ["recoveryFormat"] = !CardOperationMode.IsExtension(context.Project)
        };
        if (CardOperationMode.IsExtension(context.Project)) features["extensionExistingCard"] = true;
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["builderVersion"] = context.BuilderVersion,
            ["projectName"] = context.Project.Project.Name,
            ["generatedAt"] = Timestamp(context),
            ["sourceConfigPath"] = Path.GetFullPath(context.SourceConfigPath),
            ["configSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.SourceConfigPath))),
            ["buildModes"] = new JsonArray(context.Project.Project.Build.Select(value => JsonValue.Create(value)).ToArray()),
            ["rfPrjManifestVersion"] = "2.0.6",
            ["recommendedRFiDGearVersion"] = "2.0.10.0",
            ["requiredRFiDGearVersion"] = usesChangeFileSettings ? "2.0.6.0" : null,
            ["features"] = features,
            ["profiles"] = ProfileSummary(context)
        };
    }

    private static JsonObject BuildAcceptancePlan(ContractArtifactContext context, bool coding)
    {
        var applications = new JsonArray();
        foreach (var application in CardOperationMode.SelectApplications(context.Profiles, context.Project))
        {
            var files = new JsonArray();
            foreach (var file in application.Files)
            {
                files.Add(new JsonObject
                {
                    ["id"] = file.Id, ["name"] = file.Name, ["type"] = file.Type, ["size"] = file.Size,
                    ["communication"] = file.Communication,
                    ["access"] = AccessNode(file.Access), ["accessText"] = AccessText(file.Access),
                    ["codingOutput"] = file.Coding?.Output
                });
            }
            applications.Add(new JsonObject
            {
                ["aid"] = ProfileValidator.NormalizeAid(application.Id), ["name"] = application.Name,
                ["keyType"] = application.KeyType, ["keyCount"] = application.KeyCount,
                ["keySettings"] = application.ProductiveKeySettings, ["changeKeyMode"] = application.ChangeKeyMode,
                ["files"] = files
            });
        }
        return new JsonObject
        {
            ["schemaVersion"] = "1.0", ["projectName"] = context.Project.Project.Name, ["generatedAt"] = Timestamp(context),
            ["profiles"] = ProfileSummary(context), ["applications"] = applications,
            ["verificationDirectory"] = coding ? "verification" : null,
            ["applicationAcceptanceProject"] = coding ? context.Project.Project.Name + "_Acceptance_Application_Productive.rfPrj" : null,
            ["applicationAcceptanceEvidence"] = coding ? "acceptance/application-settings.result.json" : null,
            ["fileSettingsCheckMode"] = coding ? "not verified: RFiDGear 2.0.10 cannot deserialize CheckFileSettings projects" : null,
            ["note"] = "Schlüsselwerte sind absichtlich nicht Bestandteil dieses Prüfplans."
        };
    }

    private static JsonObject BuildHostContract(ContractArtifactContext context, bool coding)
    {
        var name = context.Project.Project.Name;
        var extension = CardOperationMode.IsExtension(context.Project);
        var operations = new JsonArray(
            new JsonObject { ["kind"] = "mastering", ["project"] = name + "_Mastering.rfPrj", ["modes"] = new JsonArray("open", "autorun") },
            new JsonObject { ["kind"] = "card-preflight-transport", ["project"] = name + "_CardPreflight_Transport.rfPrj", ["modes"] = new JsonArray("autorun") },
            new JsonObject { ["kind"] = "card-preflight-productive", ["project"] = coding ? name + "_CardPreflight_Productive.rfPrj" : null, ["modes"] = coding ? new JsonArray("autorun") : new JsonArray() },
            new JsonObject { ["kind"] = "application-acceptance-productive", ["project"] = coding ? name + "_Acceptance_Application_Productive.rfPrj" : null, ["modes"] = coding ? new JsonArray("autorun") : new JsonArray() },
            new JsonObject { ["kind"] = "coding", ["project"] = coding ? name + "_Kodierung.rfPrj" : null, ["modes"] = coding ? new JsonArray("open", "automode") : new JsonArray() },
            new JsonObject { ["kind"] = "recovery-transport", ["project"] = extension ? null : name + "_Recovery_Format_TransportPICC.rfPrj", ["modes"] = extension ? new JsonArray() : new JsonArray("autorun") },
            new JsonObject { ["kind"] = "recovery-productive", ["project"] = !extension && coding ? name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj" : null, ["modes"] = !extension && coding ? new JsonArray("autorun") : new JsonArray() });
        if (extension)
            operations.Add(new JsonObject { ["kind"] = "extension-rollback", ["project"] = name + "_Erweiterung_Rueckbau.rfPrj", ["modes"] = new JsonArray("autorun") });
        var approvedKeyChangeBuilds = new JsonArray();
        foreach (var build in RFiDGearTrustedBuildCatalog.KeyChangeBuilds)
        {
            approvedKeyChangeBuilds.Add(new JsonObject
            {
                ["name"] = build.Name,
                ["productVersion"] = build.ProductVersion,
                ["evidenceSha256"] = build.EvidenceSha256,
                ["sourceCommit"] = build.SourceCommit
            });
        }
        return new JsonObject
        {
            ["schemaVersion"] = "1.1",
            ["contractId"] = "de.rfidgear.host.v1",
            ["projectName"] = name,
            ["generatedAt"] = Timestamp(context),
            ["adapterStatus"] = "builder-process-adapter",
            ["ownership"] = new JsonObject
            {
                ["builder"] = new JsonArray("profiles", "project-definition", "key-vault", "payloads", "workflow", "acceptance"),
                ["rfidgear"] = new JsonArray("reader-provider", "card-polling", "task-engine", "card-commands", "task-results")
            },
            ["transport"] = new JsonObject
            {
                ["current"] = "CUSTOMPROJECTFILE plus structured-log and project-local readbacks",
                ["target"] = "IRFiDGearHostV1",
                ["requestDirectory"] = "integration/host-v1/runs",
                ["resultDirectory"] = "integration/host-v1/runs"
            },
            ["operations"] = operations,
            ["correlation"] = new JsonObject
            {
                ["requestId"] = "GUID",
                ["engineRunId"] = "privacy-safe SHA-256 of structured RFiDGear RunId",
                ["projectIdentity"] = "rfPrj SHA-256",
                ["taskIdentity"] = "TaskPosition, CurrentTaskIndex, type and key-free task-plan SHA-256",
                ["terminalEvents"] = new JsonArray("TaskLoop.Success", "TaskLoop.Failure"),
                ["completionEvidence"] = new JsonArray("ordered-structured-task-outcomes", "single-run-id", "terminal-event", "consumer-acceptance")
            },
            ["resultSemantics"] = new JsonObject
            {
                ["stateOwner"] = "RFiDGear engine terminal result",
                ["acceptanceStateOwner"] = "Builder consumer verification",
                ["accepted"] = "state=successful AND acceptanceState=successful",
                ["terminalEngineResultImmutable"] = true
            },
            ["safety"] = new JsonObject
            {
                ["keyChangeRequiresPostAuthentication"] = true,
                ["knownBlocker"] = "c3rebro/RFiDGear#178",
                ["unknownCapabilityPolicy"] = "fail-closed",
                ["keyChangeReleasePolicy"] = "implementation markers plus exact hardware-approved EXE/DLL evidence fingerprint from the Builder trust catalog",
                ["approvedKeyChangeBuilds"] = approvedKeyChangeBuilds
            },
            ["privacy"] = new JsonObject
            {
                ["requestContainsKeys"] = false,
                ["resultContainsKeys"] = false,
                ["requestContainsCardData"] = false,
                ["resultContainsCardData"] = false,
                ["rfPrjMayContainKeys"] = true
            }
        };
    }

    private static JsonObject BuildCodingSchema(ContractArtifactContext context)
    {
        var variables = new JsonArray();
        foreach (var variable in CardOperationMode.SelectVariables(context.Profiles, context.Project))
        {
            var node = new JsonObject
            {
                ["id"] = variable.Id, ["label"] = variable.Label, ["scope"] = variable.Scope,
                ["type"] = variable.Type, ["required"] = variable.Required
            };
            AddIf(node, "length", variable.Length); AddIf(node, "allowedLengths", variable.AllowedLengths);
            AddIf(node, "minLength", variable.MinLength); AddIf(node, "maxLength", variable.MaxLength); AddIf(node, "default", variable.Default);
            if (variable.Scope == "project") node["value"] = context.Project.Values.GetValueOrDefault(variable.Id, "");
            variables.Add(node);
        }
        var payloads = new JsonArray();
        foreach (var application in CardOperationMode.SelectApplications(context.Profiles, context.Project))
        foreach (var file in application.Files.Where(file => file.Coding is not null))
        {
            payloads.Add(new JsonObject
            {
                ["aid"] = ProfileValidator.NormalizeAid(application.Id), ["appName"] = application.Name,
                ["fileId"] = file.Id, ["fileName"] = file.Name, ["size"] = file.Size,
                ["output"] = file.Coding!.Output,
                ["recipe"] = JsonSerializer.SerializeToNode(file.Coding.Recipe, ProfileJsonOptions)
            });
        }
        return new JsonObject
        {
            ["schemaVersion"] = "1.2", ["projectName"] = context.Project.Project.Name,
            ["keySetIdentity"] = KeySetIdentityService.Compute(ProjectKeyVault.FromProject(context.Project)),
            ["codingProject"] = context.Project.Project.Name + "_Kodierung.rfPrj", ["profiles"] = ProfileSummary(context),
            ["variables"] = variables, ["payloads"] = payloads, ["finalVerificationDirectory"] = "verification"
        };
    }

    private static JsonArray ProfileSummary(ContractArtifactContext context)
    {
        var result = new JsonArray();
        foreach (var profile in context.Profiles.Profiles)
            result.Add(new JsonObject { ["id"] = profile.Profile.Id, ["code"] = profile.Profile.Code, ["version"] = profile.Profile.Version, ["kind"] = profile.Profile.Kind });
        return result;
    }

    private static bool CredentialSetsEquivalent(ContractArtifactContext context)
    {
        foreach (var application in CardOperationMode.SelectApplications(context.Profiles, context.Project))
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            if (!context.Project.Productive.ApplicationKeys.TryGetValue(aid, out var productive)) return false;
            if (!context.Project.Transport.ApplicationKeys.TryGetValue(application.KeyType, out var transport)) return false;
            foreach (var file in application.Files)
                if (file.Access.Read >= productive.Count || !string.Equals(productive[file.Access.Read], transport, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static JsonObject AccessNode(AccessDefinition access) => new()
    {
        ["read"] = access.Read, ["write"] = access.Write, ["readWrite"] = access.ReadWrite, ["change"] = access.Change
    };

    private static string AccessText(AccessDefinition access) => $"R{access.Read}/W{access.Write}/RW{access.ReadWrite}/C{access.Change}";
    private static string Timestamp(ContractArtifactContext context) => context.GeneratedAt.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
    private static void AddIf<T>(JsonObject target, string name, T? value) where T : struct { if (value.HasValue) target[name] = JsonValue.Create(value.Value); }
    private static void AddIf(JsonObject target, string name, string? value) { if (value is not null) target[name] = value; }
    private static void AddIf(JsonObject target, string name, List<int>? value) { if (value is not null) target[name] = new JsonArray(value.Select(item => JsonValue.Create(item)).ToArray()); }
}

internal static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write(string path, JsonNode document)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, document, Options);
            stream.Flush(true);
            stream.Close();
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
