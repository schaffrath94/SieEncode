namespace RFiDGear.Builder.Runtime;

public sealed record CodingStationQuickStartRequest(
    string ExecutablePath,
    CodingSessionDefinition Definition,
    string PreflightStatus,
    bool ArtifactOnly);

public sealed record CodingStationQuickStartResult(
    RFiDGearLaunchResult Launch,
    string SafetyState,
    PayloadInvalidationResult Invalidation);

public sealed class CodingStationQuickStartService
{
    private readonly IRFiDGearLauncher launcher;
    private readonly PayloadPreparationStateService payloadState;

    public CodingStationQuickStartService(IRFiDGearLauncher? launcher = null, PayloadPreparationStateService? payloadState = null)
    {
        this.launcher = launcher ?? new RFiDGearLauncher();
        this.payloadState = payloadState ?? new PayloadPreparationStateService();
    }

    public async Task<CodingStationQuickStartResult> StartAsync(
        CodingStationQuickStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArtifactOnly)
            throw new InvalidOperationException("Der Ein-Schritt-Start ist für reine Bauartefakte gesperrt, weil kein vollständiger Stale-/Profil-Preflight möglich ist. Das Projekt kann weiterhin bewusst in die Kodierstation geladen werden.");
        if (!string.Equals(request.PreflightStatus, "OK", StringComparison.Ordinal))
            throw new InvalidOperationException("Kodiersession wird nicht gestartet, weil der vollständige Projekt-Preflight Fehler enthält.");
        var invalidation = payloadState.Invalidate(request.Definition,
            "Quick-Start geöffnet; neue Kartendaten wurden noch nicht mit Enter bereitgestellt.");
        var result = await launcher.LaunchAsync(new RFiDGearLaunchRequest(
            request.ExecutablePath, request.Definition.CodingProjectPath, Autorun: false, AutoMode: false), cancellationToken).ConfigureAwait(false);
        if (result.Autorun || result.AutoMode)
            throw new InvalidOperationException("Sicherheitsvertrag verletzt: Quick-Start darf keinen automatischen Kartenlauf aktivieren.");
        return new CodingStationQuickStartResult(result,
            $"RFiDGear wurde nur mit dem konkreten Kodierprojekt geöffnet. {invalidation.RemovedPayloads} alte Payloaddatei(en) gesperrt/entfernt; noch keine neue Payload bereitgestellt und kein Kartenlauf gestartet.", invalidation);
    }
}
