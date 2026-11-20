using System.Reflection;

namespace RFiDGear.Builder.App;

internal static class AppIdentity
{
    public static string DisplayVersion { get; } = ResolveVersion()
#if SIEENCODE_UI_PREVIEW
        + " · UI-TEST · keine Hardwarefreigabe"
#endif
        ;

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppIdentity).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational)) return informational.Split('+', 2)[0];
        var product = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        return string.IsNullOrWhiteSpace(product) ? "Version unbekannt" : product.Split('+', 2)[0];
    }
}
