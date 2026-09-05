namespace RFiDGear.Builder.Core;

public sealed record RFiDGearTrustedBuild(
    string Name,
    string ProductVersion,
    string EvidenceSha256,
    string SourceCommit,
    string Evidence);

/// <summary>
/// Exact RFiDGear binaries that completed the Builder software and hardware
/// acceptance. A product version or implementation marker is never sufficient.
/// </summary>
public static class RFiDGearTrustedBuildCatalog
{
    public static IReadOnlyList<RFiDGearTrustedBuild> KeyChangeBuilds { get; } =
    [
        new(
            "RFiDGear Builder Runtime 2.0.10.1",
            "1.0.0+2d0429f2a26985a2d52a83caa493e619dc1c1cfc",
            "0208509E43AE8ADF93583948FC2660CE3D287558E86DA9093CB3CE962C496582",
            "2d0429f2a26985a2d52a83caa493e619dc1c1cfc",
            "177/177 Softwaretests und kombinierter ELATEC/DESFire-Hardwarelauf bestanden: 26/26 Kodiertasks, 6/6 Produktivreads, 6/6 alte Transportreads erwartungsgemäß abgewiesen, Recovery 6/6."),
        new(
            "SieEncode Runtime 1.0.0-rc2 · powered by RFiDGear 2.0.10.1",
            "1.0.0-rc2+rfidgear.2.0.10.1",
            "627A96B8C67916B15BC02E0AC072CFBFD6797996A3096E5FF4E8A336B88E3907",
            "56fd8731a64660226c75fe6cfb415f708e2c2122",
            "Exakter produktiver EvidenceFingerprint aus SieEncode.Runtime.exe und RFiDGear.dll. 177/177 Runtime- und 40/40 Buildertests bestanden. Vollständige ELATEC/DESFire-Hardwareläufe einschließlich RC5: Format 1/1, Mastering 27/27, Kodierung 26/26, Produktivreads 6/6, alte Transportkeys 6/6 abgewiesen, Recovery und finaler Transportread 6/6."),
        new(
            "SieEncode Runtime 1.0.0-rc4 · powered by RFiDGear 2.0.10.3",
            "1.0.0-rc4+rfidgear.2.0.10.3",
            "85590C40D9F3A9E64CE60F0699CBF8B60C413314517745BA38130677000A7669",
            "local-hardware-acceptance-20260827",
            "Exakte RC4-Runtime der sichtbaren RC6-Hardwareabnahme. 177/177 Runtime- und 40/40 Buildertests bestanden. Autorisierte ELATEC/DESFire-Läufe über die sichtbare SieEncode-Oberfläche: Recovery 2/2, Mastering 27/27, Kodierung 26/26, finale Readbacks 3/3 bytegleich, Produktivprobe 6/6 lesbar mit 3/3 Payload-Matches sowie Application-/KeySettings-Abnahme 6/6 autoritativ erfolgreich. Enthält Fresh-Session-Retry und strukturiertes Exception-Logging für ReadAppSettings sowie assemblyneutrale WPF-Ressourcenpfade.")
        ,new(
            "SieEncode Runtime 1.0.0-rc15 · powered by RFiDGear 2.0.10.10",
            "1.0.0-rc15+rfidgear.2.0.10.10",
            "1F4085B22D56D6554BE70C4295B11F42221CDA981E3BC0B0029F92105152F4C9",
            "56fd8731a64660226c75fe6cfb415f708e2c2122+working-tree-20260901",
            "Exakte RC15-Runtime der sichtbaren ELATEC/DESFire-Hardwareabnahme. 183/183 Runtime- und 45/45 Builder-Release-Tests: Bestandsinventar 3 AIDs, additive Ziel-AID F482D5, Mastering 17/18 mit erwartetem Abwesenheitscheck, Transport-Preflight 5/5, Kodierung 7/7, Produktiv-Preflight 5/5, Anwendungsabnahme 2/2, Inventar 4 AIDs bei vollständig erhaltenem Bestand, gezielter DeleteApplication-Rückbau ohne Formatierung und finales Inventar mit den ursprünglichen 3 AIDs. Enthält zustandsbasierte Delete-Nachverifikation für falsche ELATEC-Fehlerrückgaben.")
        ,new(
            "SieEncode Runtime 1.0.0-rc16 · powered by RFiDGear 2.0.10.11",
            "1.0.0-rc16+rfidgear.2.0.10.11",
            "ABB7CF36D36DDEF75EED4E33BD7BCBCEDB98EA3D51F480F1ED8DBEAFEEA05D20",
            "56fd8731a64660226c75fe6cfb415f708e2c2122+working-tree-20260904",
            "Exakte RC16-Runtime der automatisierten ELATEC/DESFire-Hardwareabnahme vom 04.09.2026. Sechs terminal korrelierte Phasen: Recovery 2/2, synthetisches Mastering 10/10, Kodierung 16/16 samt drei frisch authentisierten AES-Application-Keywechseln und bytegleichen Readbacks, Application-/File-Inventur 1/1 mit exakt drei File-Metadatensätzen und gelesenem Mischformatinhalt, Recovery 2/2 sowie abschließende projektlose Standardzustandsanalyse 3/3. 183/183 Runtime- und 48/48 Buildertests. EvidenceFingerprint bindet SieEncode.Runtime.exe und SieEncode.Runtime.dll.")
#if SIEENCODE_ACCEPTANCE
        ,new(
            "SieEncode Runtime 1.0.0-rc17 · ChangeFileSettings-Abnahmekandidat",
            "1.0.0-rc17+rfidgear.2.0.10.12",
            "FCF2340EB6A76D53BBAC1BA3D645D60F0AA3511A9D15467562DFCC94D3378523",
            "56fd8731a64660226c75fe6cfb415f708e2c2122+working-tree-20260905",
            "Nur im internen Abnahmeprogramm freigeschaltet. 183/183 Runtime- und 47/47 Buildertests bestanden. ChangeFileSettings verwendet frische Authentisierung, drei begrenzte Versuche und autoritativen Readback; der reale ELATEC/DESFire-Lauf steht noch aus.")
        ,new(
            "SieEncode Runtime Secret-Provider · interner Abnahmekandidat",
            "1.0.0-rc10+rfidgear.2.0.10.5",
            "780F4948E525F710164F436A9699AB1EB51992C7EA41B5DDAA41947415426016",
            "56fd8731a64660226c75fe6cfb415f708e2c2122+working-tree-20260831",
            "Nur im gesondert kompilierten internen Abnahmeprogramm freigeschaltet. 182/182 Runtime- und 44/44 Buildertests bestanden; reale Hardwarefreigabe steht noch aus. Nicht Bestandteil normaler Builds.")
#endif
    ];
}
