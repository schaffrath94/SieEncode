# Vault-Abnahmetest für SieEncode 1.0 (später gemeinsam ausführen)

**Plan, kein bereits durchgeführter Test.** Für Version 1.0 setzen wir bei **null veröffentlichten SieEncode-Projekten, null alten Projekt-Vaults und keiner Migrationspflicht** an. Software-Testkandidaten aus der Entwicklung sind **keine** früheren Produktiv-Releases. Es werden nur erfundene Testidentitäten, Gruppen und Karten verwendet.

## A · Eigenen Zugang auf zwei Windows-Benutzern einrichten

1. Für den Sammeltest zwei getrennte Windows-Benutzer verwenden: **Hauptverwaltung** und **Kollege**. Zu Testbeginn dürfen auch **keine** lokalen Testidentitäten oder Gruppenbibliotheken vorliegen; eine bereits ausdrücklich angelegte Testidentität wird weiterverwendet.
2. Beide öffnen `Einstellungen → 1 · Mein persönlicher Vault-Zugang`. Wenn kein Zugang vorhanden ist: **einmalig** anlegen. Wenn einer vorhanden ist: denselben verwenden. Bei mehreren gleichnamigen Zugängen muss die Auswahl anhand des **Fingerabdrucks** explizit bleiben.
3. **Kollege:** Öffentliche Einladung exportieren und der Hauptverwaltung geben. Den Fingerabdruck unabhängig, etwa telefonisch, bestätigen. Kein privater Schlüssel darf die eigene Windows-Identität verlassen. Die Einladung allein erteilt **keine** Gruppenrechte.

Erwartung: Bei persönlicher Einrichtung keine alte Team-Empfängerliste, keine Altprojekt-Migration und kein automatisches Speichern/Erzeugen weiterer Identitäten.

## B · Erste Gruppenverwaltung und Berechtigungen

1. **Hauptverwaltung:** `Einstellungen → 2 · Teamzugriff und Offline-Gruppen → Ich verwalte Personen und Gruppen → Personen und Gruppen verwalten` öffnen. Für den **ersten neuen Testverbund** ausdrücklich `Nur bei Neugründung → Neue unabhängige Hauptverwaltung erstellen` verwenden. Privaten Signierschlüssel schützen. Bei späterem Öffnen **keine zweite Verwaltung** erzeugen.
2. Einladung des Kollegen importieren, Fingerabdruck unabhängig bestätigen, Person/Gerät und Gruppe `Test-Lokal` freigeben. Optional zweite Gruppe `Test-Andere` zur Negativprobe anlegen und dort **nicht** freigeben.
3. **Separate Recovery-Identität** auf dafür vorgesehenem Testarbeitsplatz erstellen, privat geschützt sichern, öffentliche Recovery-Einladung zentral freigeben. Erst dann ist die Gruppenprojekt-Vault-Erstellung vollständig vorbereitet.
4. Änderung in der Hauptverwaltung **signiert speichern** und den **öffentlichen Gruppenstand exportieren**.
5. **Kollege:** `Ersten Gruppenstand registrieren` oder nach späterer Änderung `Aktualisierten Gruppenstand importieren` verwenden. Verwaltungsfingerabdruck unabhängig bestätigen. Eine sichtbare Registrierungsdatei ist noch kein Beweis für Zugriff auf ein Projekt.

Erwartung: Zugriff nur auf tatsächlich freigegebene Gruppe; keine versteckten Synchronisationen, keine automatischen Rechte und keine privaten Schlüssel in öffentlichen Dateien.

## C · Neues Gruppenprojekt und Zugang prüfen

1. Mit einem für `Test-Lokal` berechtigten Benutzer **Neues Projekt** öffnen, Profil und Testwerte ausfüllen und Kartenschlüssel vorbereiten.
2. `Gruppen-Vault für dieses Projekt einrichten` wählen. Der Dialog muss ohne Registrierung, ohne Gruppenmitglieder oder ohne aktiven Recovery-Zugang den **konkreten fehlenden Schritt** erklären und Erstellung verweigern.
3. Richtige Bibliothek und `Test-Lokal` auswählen, neuen eindeutigen Projektnamen eingeben, ausdrücklich bestätigen. Erwartung: neue Projekt-ID, verschlüsselter Projekt-Vault, authentisiertes Wiederöffnen; **kein** unverschlüsseltes Schlüsselmaterial in der Projektdefinition.
4. **Kollege:** Freigegebenes neues Gruppenprojekt mit lokal importiertem Gruppenstand und eigenem Windows-Schlüssel öffnen. `Test-Andere` sowie unberechtigte Windows-Benutzer müssen zuverlässig abgewiesen werden. Bei einer Ablehnung soll die Oberfläche Hauptverwaltung/Freigabe/Import/Vault-Abgleich erklären, **nicht** zum Erzeugen neuer Kartenschlüssel auffordern.

## D · Änderung und Entzug

1. Hauptverwaltung entzieht dem Testkollegen die Gruppe, signiert und exportiert den geänderten Stand. Der Kollege importiert die neue Revision. Am Testprojekt den **Empfängerabgleich** durch berechtigte Person ausdrücklich ausführen.
2. Der aktuelle Vault muss den entzogenen Zugang ablehnen. Frühere **absichtlich separat erzeugte Offline-Kopien** ggf. weiter zugänglich: eine dokumentierte technische Grenze, **keine** Fernsperre. Für produktive Karten ersetzt das keine Schlüsselrotation.
3. Zusätzlich separaten Recovery-Zugang, legitime Freigabewiederherstellung und gegebenenfalls ausdrücklich erteilte **Verwaltungsvollmacht** prüfen. Gruppenmitgliedschaft allein darf keine signierten Verwaltungsänderungen erlauben.

## E · Abschluss des einzigen gebündelten Meilensteintests

Zusammen mit Projekten, Kodierstation und Kartenuntersuchung Windows-WPF-Build, UI-Smoketests, echte Zwei-Benutzer-Verifikation und freigegebene Testkarten prüfen; danach MSI-/Upgrade-, Datenerhaltungs- und Sicherheitstest. Kein Mini-Release je UI-Commit.

**Rückmeldungen:** Status, Bedienfehler und unkritische Screenshots – **keine** Fingerabdrücke, Schlüssel, Passphrasen, privaten Sicherungen oder Kundendaten öffentlich teilen. Nur ein tatsächlich gemeinsam abgenommener Kandidat darf später als erstes Release 1.0 veröffentlicht werden.
