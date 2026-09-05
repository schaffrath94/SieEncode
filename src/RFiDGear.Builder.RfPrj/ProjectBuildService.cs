using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public sealed record ProjectBuildResult(
    string ProjectDirectory,
    ProjectPreflightReport Preflight,
    IReadOnlyDictionary<string, string> SessionSecrets);

public sealed class ProjectBuildService
{
    public ProjectBuildResult Build(
        ProjectDefinition runtimeProject,
        ProfileLibrary library,
        TaskShellLibrary shells,
        string sourceConfigPath,
        string projectsDirectory,
        string builderVersion)
    {
        ArgumentNullException.ThrowIfNull(runtimeProject);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(shells);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConfigPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(builderVersion);

        ProfileBindingService.EnsureBuildCompatible(runtimeProject, library);

        var requiresProductiveSecrets = runtimeProject.Project.Build.Contains("Coding", StringComparer.OrdinalIgnoreCase);
        var validation = ProjectContractValidator.Validate(runtimeProject, library, requiresProductiveSecrets).ToList();
        if (validation.Count > 0)
            throw new InvalidDataException("Projekt kann nicht gebaut werden:\n" + string.Join("\n", validation.Select(issue => $"{issue.Code}: {issue.Message}")));

        var composition = library.Compose(runtimeProject.Profiles.Base, runtimeProject.Profiles.Customers);
        var outputDirectory = ProjectPaths.ResolveBuiltProject(runtimeProject, projectsDirectory);
        Directory.CreateDirectory(outputDirectory);
        using var workspaceLease = WorkspaceLease.Acquire(outputDirectory, "Projektbuild");
        Directory.CreateDirectory(Path.Combine(outputDirectory, "payloads"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "verification"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "readback"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "readback_mastering"));
        MasteringInitializationService.Prepare(composition, outputDirectory);

        var name = runtimeProject.Project.Name;
        var extension = CardOperationMode.IsExtension(runtimeProject);
        var main = new MainProjectFactory(shells);
        var support = new SupportProjectFactory(shells);
        var sessionSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        SaveSecretFree(name + "_CardRecognition_ReadOnly.rfPrj",
            support.BuildCardRecognition(runtimeProject, requiresProductiveSecrets));
        if (runtimeProject.Project.Build.Contains("Mastering", StringComparer.OrdinalIgnoreCase))
            SaveSecretFree(name + "_Mastering.rfPrj", main.BuildMastering(composition, runtimeProject, outputDirectory));
        else
            DeleteBuilderArtifact(outputDirectory, name + "_Mastering.rfPrj");

        SaveSecretFree(name + "_CardPreflight_Transport.rfPrj", support.BuildCardPreflight(composition, runtimeProject, outputDirectory, productive: false));
        if (!extension)
            SaveSecretFree(name + "_Recovery_Format_TransportPICC.rfPrj", support.BuildRecovery(runtimeProject, productive: false));
        else
        {
            DeleteBuilderArtifact(outputDirectory, name + "_Recovery_Format_TransportPICC.rfPrj");
            SaveSecretFree(name + "_Erweiterung_Rueckbau.rfPrj", support.BuildExtensionRollback(composition, runtimeProject));
        }
        if (!extension) DeleteBuilderArtifact(outputDirectory, name + "_Erweiterung_Rueckbau.rfPrj");

        if (requiresProductiveSecrets)
        {
            SaveSecretFree(name + "_Kodierung.rfPrj", main.BuildCoding(composition, runtimeProject, outputDirectory));
            SaveSecretFree(name + "_CardPreflight_Productive.rfPrj", support.BuildCardPreflight(composition, runtimeProject, outputDirectory, productive: true));
            SaveSecretFree(name + "_Acceptance_Application_Productive.rfPrj", support.BuildApplicationAcceptance(composition, runtimeProject));
            if (!extension)
                SaveSecretFree(name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj", support.BuildRecovery(runtimeProject, productive: true));
            else
                DeleteBuilderArtifact(outputDirectory, name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj");
        }
        else
        {
            DeleteBuilderArtifact(outputDirectory, name + "_Kodierung.rfPrj");
            DeleteBuilderArtifact(outputDirectory, name + "_CardPreflight_Productive.rfPrj");
            DeleteBuilderArtifact(outputDirectory, name + "_Acceptance_Application_Productive.rfPrj");
            DeleteBuilderArtifact(outputDirectory, name + "_Recovery_Format_ProductivePICC_ToTransport.rfPrj");
        }

        var context = new ContractArtifactContext(runtimeProject, composition, Path.GetFullPath(sourceConfigPath), builderVersion, DateTime.Now);
        ContractArtifactFactory.SaveAll(outputDirectory, ContractArtifactFactory.Build(context));
        if (!requiresProductiveSecrets) DeleteBuilderArtifact(outputDirectory, "coding.schema.json");

        var preflight = ProjectPreflight.Inspect(runtimeProject, library, sourceConfigPath, outputDirectory);
        if (preflight.Issues.Count > 0)
            throw new InvalidDataException("Build wurde erzeugt, hat den abschließenden Preflight aber nicht bestanden:\n" +
                string.Join("\n", preflight.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        return new ProjectBuildResult(outputDirectory, preflight, sessionSecrets);

        void SaveSecretFree(string artifactName, RfPrjDocument document)
        {
            var protectedProject = RfPrjSecretProtection.Protect(document, artifactName);
            foreach (var secret in protectedProject.Secrets)
                if (!sessionSecrets.TryAdd(secret.Key, secret.Value))
                    throw new InvalidDataException($"Doppelte Sitzungsreferenz: {secret.Key}");
            RfPrjArchive.Save(Path.Combine(outputDirectory, artifactName), protectedProject.Project);
        }
    }

    private static void DeleteBuilderArtifact(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (File.Exists(path)) File.Delete(path);
    }
}
