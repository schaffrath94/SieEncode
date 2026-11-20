# Vorschlag für einen gemeinsamen Bridge-Abnahmetest

Ergänzung zu c3rebro/RFiDGear#232, Stand 17.11.2026. Der letzte Kommentar beschreibt bereits Projektbindung, Kartenpräsenz, Vault-Freigabe und strukturierte Prüfergebnisse. Dieser Vorschlag definiert beobachtbare Ergebnisse für einen Test mit simuliertem Reader und erfundenen Referenzen. Er ist noch nicht gegen eine offizielle Engine ausgeführt.

## Minimale Testanordnung

Zwei synthetische Projektzustände A und B teilen Pfad und Taskanzahl. Sie unterscheiden sich in einer öffentlichen Dateigröße. Zwei simulierte Kartenpräsenz-Ereignisse und ein zählender Testprovider liefern deterministische Eingaben. Der Reader zählt ausgeführte Schreibaufträge. Der Provider zählt freigegebene Schlüsselauflösungen, ohne deren Werte aufzuzeichnen.

| Testfall | Erwartetes Ergebnis |
| --- | --- |
| A freigeben, anschließend im Host auf B ändern, dann Auftrag A starten | Ablehnung vor Kartenausführung. Schreibzähler 0. Keine Schlüsselfreigabe. |
| Karte A scannen und freigeben, vor Start durch Karte B ersetzen | Ablehnung. Schreibzähler 0. Keine Wiederholung ohne neue Freigabe. |
| Eine gültige Referenz aus einem anderen Auftrag anfragen | Keine Schlüsselfreigabe. Auftrag und Ablehnung sind ohne Schlüsselwert zuordenbar. |
| Einen Schreibauftrag abschließen, Karte entfernen, erneut auflegen und ausschließlich Lesetest starten | Schreibzähler bleibt beim Wert nach dem ersten Auftrag. Lesetest erhält eine eigene Laufkennung. |
| Verbindung während der Auflösung verlieren und Client neu verbinden | Kein selbsttätiger Neustart des Schreibauftrags. Client verlangt bei uneindeutigem Ergebnis Prüfung. |
| Lesetest erwartet 32 Byte und beobachtet 16 Byte | Abweichung wird dem richtigen Lauf und Task zugeordnet. Der Client kann sie ohne Logtext erkennen. |

Zum Austausch genügen Host-Commit, Bridge-Version, Testfall, beobachteter Terminalstatus, Taskposition sowie beide Zähler vor und nach dem Versuch. Feldnamen und Transport können sich weiterentwickeln. Interessant für beide Projekte ist, welche Fälle der SampleClient am aktuellen master bereits nachweisen kann. Ein gemeinsamer erster Test wäre der Projektwechsel bei identischem Pfad und identischer Taskanzahl.

## Ausgeführter Kontrolltest am 17.11.2026

Offizieller Quellstand e607c2986cd000c88a6a3101620a963882f738e6, Windows x64, lokales SDK 10.0.404, Release. Der einzelne Test BridgeSampleClientTests.ARunThatExpectsAnotherProject_IsRefused_AndTheHostIsNeverAsked bestand (1/1). Der SampleClient kommuniziert über eine Named Pipe mit dem Testhost. Ein abweichender Projektpfad und eine abweichende Taskanzahl wurden abgelehnt. Der passende Auftrag wurde abgeschlossen. Bei einem Checkout unterhalb des Studio-Verzeichnisses die Studio-spezifische Directory.Build.props vom Upstream-Build ausschließen.

Der getestete StartRunRequest enthält die Projektvorbedingungen ExpectProjectPath und ExpectTaskCount. Ein erwarteter Revisionswert für den geladenen Projektinhalt fehlt im DTO. Die Ablehnung eines Inhaltswechsels bei unverändertem Pfad und gleicher Taskanzahl bleibt daher ein offenes Abnahmekriterium. Der Test nutzte weder den installierten Upstream-Release noch einen Reader.

Der Befund zur Projektbindung wurde in https://github.com/c3rebro/RFiDGear/issues/232#issuecomment-6963270675 übergeben. Auf demselben Commit bestanden zwei weitere hardwarefreie Tests aus BridgeKeyProviderTests (2/2): AClientThatLeavesWhileItIsAsked_CostsTheKey_NotTheWholeTimeout und AKeyThatArrivesAfterTheTimeout_IsNotHandedOut. Sie decken den Verlust des Clients während der Anfrage und die verspätete Antwort nach Timeout ab. Offen bleibt der Gesamtzustand eines laufenden Schreibauftrags nach Verbindungsabbruch und Wiederverbindung.

## Verbindungsverlust und Wiederanlauf

Am selben Upstream-Commit bestanden fünf gezielte Prüfungen aus BridgeSampleClientTests und BridgeRunRecoveryTests (5/5, Named Pipe, synthetischer Host). Nach Verbindungsverlust meldet der SampleClient zunächst Unknown. ResumeAsync mit derselben requestId findet den laufenden Auftrag beim weiterhin aktiven Host wieder. Im Test blieb es bei genau einem Ausführungsauftrag. Bei einem Start-Timeout fragt der Client den Status ab.

Nach simuliertem Hostneustart mit erhaltenem Ausführungsjournal wurde ein zuvor laufender Auftrag als unterbrochen erkannt. Ein zuvor abgeschlossener Auftrag blieb als abgeschlossen bekannt, sein Detailergebnis war verloren. In beiden Fällen wurde er nicht erneut gestartet.

Für eine spätere Studio-Anbindung: Auftragskennung vor dem Start persistent speichern, nach Verbindungsverlust den Status abfragen und unklare oder unterbrochene Läufe zur Prüfung anzeigen. Bei fehlenden Detailergebnissen ist vor Kartenfreigabe ein unabhängiger Readback erforderlich. Die Kombination aus Schlüsselauflösung, Abbruch, Wiederverbindung und tatsächlicher Kartenänderung ist noch nicht im Gesamtablauf geprüft.

## Kombinierter Test mit ausstehender Vault-Anfrage

ResumeAfterPendingVaultDisconnect bestand in 911 ms am Upstream-Stand e607c2986cd000c88a6a3101620a963882f738e6 mit lokalem Testzusatz. Der synthetische Ausführungshost fordert während des Auftrags einen Schlüssel über die Bridge an. Erst nachdem der Client die Anfrage erhalten hat, trennt der Test die Pipe. Der Provider liefert ProviderUnavailable und keinen Schlüssel. Der Testhost modelliert diesen Fehler als fehlgeschlagenen Auftrag. Der getrennte Client sieht Unknown. Ein neuer Client erhält bei ResumeAsync zur ursprünglichen requestId den Terminalstatus failure. Der Ausführungszähler bleibt bei eins. Es wird keine Karte angesprochen.

Reproduktion im sauberen Checkout des genannten Commits mit SDK 10.0.404: beiliegenden Patch mit git apply anwenden und dotnet test RFiDGear.Tests/RFiDGear.Tests.csproj -c Release --filter FullyQualifiedName~ResumeAfterPendingVaultDisconnect ausführen. Der Patch ergänzt ausschließlich die Tests. Die Fehlerzuordnung und der physische Kartenzustand in einem realen DESFire-Task bleiben separat abzunehmen.

## DESFire-Task bei ausbleibendem Schlüssel

Stand e607c2986cd000c88a6a3101620a963882f738e6. Drei bestehende Upstream-Testmethoden mit aufzeichnendem Reader-Doppel ausgeführt. Zehn DESFire-Fälle bestanden. Bei ReadData ergaben sieben Auflösungsfehler (NotFound, NotAuthorized, ProviderUnavailable, Timeout, Cancelled, InvalidKey, Unsupported) jeweils KeyUnavailable und null Reader-Aufrufe. Bei ProviderUnavailable bestätigte der TaskExecutionService zusätzlich IsSuccess=false, NormalizedErrorCode=KeyUnavailable und ExecutionOutcome.Failure. Bei ApplicationKeyChangeover und PICCMasterKeyChangeover scheiterte die Beschaffung des neuen Schlüssels, obwohl ein anderer Schlüssel bereits angefragt wurde. Auch hier null Reader-Aufrufe, einschließlich Authentifizierung.

Der Methodenfilter erfasste zusätzlich acht gleichnamige Classic-Fälle. Gesamt 18/18. Der lokale Bridge-Testzusatz aus dem vorherigen Lauf war im Testassembly enthalten und wurde nicht erneut ausgeführt. Diese Ergebnisse beziehen sich auf den jeweiligen Task vor dessen erstem Kartenbefehl. Bereits ausgeführte frühere Tasks eines Auftrags sind separat zu bewerten. Hardware und der externe Studio-Adapter bleiben eigenständige Abnahmen.

## Soll/Ist-Ergebnis und öffentlicher Installer, 18.11.2026

Im Release-Testbuild von f58785f (mit dem lokalen Updater-Patch) bestanden zwei bisher offene Prüfungen aus CheckFileSettingsTests mit simuliertem Reader. TheTaskReportsItsComparisonToTheLogAsPlainNumbers_AndForgetsItOnRequest prüft am ViewModel unter anderem Matches, die Istgröße 16 und das erste Zugriffsbyte. TheComparisonOfOneRun_DoesNotSurviveIntoTheNext bestätigt, dass nach einem Authentifizierungsfehler der vorherige Vergleich gelöscht ist. Beide lassen sich über den Methodennamen mit dotnet test --filter auswählen.

Im Bridge-Vertrag dieses Commits enthält TaskOutcomeDto Position, Projektindex, Typ, Erfolg, Fehlercode und Überspringgrund. Ein Feld für die verglichenen Größen fehlt. Offen bleibt die Übermittlung eines abweichenden Größenpaars (Soll 32, Ist 16) an Studio mit Lauf- und Taskzuordnung.

Der öffentliche Release v2.0.10.0 verweist laut GitHub-API auf 1045f70ac64d0a049dfcd82f8b61b627faa22ea6. Sein vollständig gelisteter Git-Baum enthält keinen Pfad mit RFiDGear.Bridge. GitHub nennt für RFiDGearBundleSetup.exe 285441360 Byte und SHA-256 5584e013e5eacde915b27124518979721bab8e56e25c01a76084ec787cbc9471. Dies ist ein Metadatenabgleich. Der Installerstart und die Abnahme einer offiziell ausgelieferten Bridge stehen aus.

## Automatik bei Kartenpräsenz, 18.11.2026

Vier AutoModeControllerTests im vorhandenen Release-Testbuild f58785f mit lokalem Updater-Patch bestanden (4/4). Eine beim Start bereits aufliegende Karte löst einen Lauf aus. Bei unveränderter Karte über fünf Abfragen bleibt es bei einem Lauf. Nach erkanntem Entfernen und erneutem Auflegen derselben Karte erfolgt ein zweiter Lauf mit neuer RunId. Eine andere UID löst auch ohne dazwischen beobachteten Leerzustand einen Lauf aus. Alle vier Fälle verwenden simulierte Präsenzereignisse.

Für die Stationsintegration folgt daraus: Vor Aktivierung der Automatik muss der beabsichtigte Auftrag feststehen, auch wenn die Karte bereits aufliegt. Der Wechsel vom Schreibauftrag zum reinen Lesetest samt Zählung real ausgelöster Schreibbefehle bleibt gesondert zu prüfen.
