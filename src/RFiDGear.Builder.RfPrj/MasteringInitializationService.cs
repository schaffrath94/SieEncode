using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.RfPrj;

public static class MasteringInitializationService
{
    public static void Prepare(ComposedProfiles profiles, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var directory = Path.Combine(Path.GetFullPath(outputDirectory), "initialization");
        Directory.CreateDirectory(directory);
        foreach (var application in profiles.Applications)
        {
            var aid = ProfileValidator.NormalizeAid(application.Id);
            foreach (var file in application.Files.Where(RequiresZeroFill))
            {
                var name = $"{aid}_File{file.Id:D2}_{file.Size}x00.hex";
                var path = Path.Combine(directory, name);
                var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporary, PayloadEngine.ToHexFileText(new byte[file.Size]), new System.Text.UTF8Encoding(false));
                    File.Move(temporary, path, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
    }

    private static bool RequiresZeroFill(FileDefinition file)
        => file.MasteringInitialization?.Type switch
        {
            "none" => false,
            "zeroFill" or null => file.Type is "StdDataFile" or "BackupFile",
            var value => throw new InvalidDataException($"Unbekannte Mastering-Initialisierung: {value}")
        };
}
