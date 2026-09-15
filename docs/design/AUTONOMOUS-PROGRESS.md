# Autonome Weiterarbeit - Stand 2026-09-15

## Auftrag
PDF-Weiterentwicklung und visuelle PDF-Abnahme sind auf ausdruecklichen Wunsch
zurueckgestellt. Bestehende Funktionen bleiben erhalten. Alle anderen Arbeiten
bis zum geprueften internen Release autonom und tokensparsam fortsetzen.
Normale Implementierung, Tests, private Branch-Pushes und isolierte Testpakete
sind autorisiert. Produktive Daten, Vaults und installierte RC15 nicht ersetzen.
Keine unbekannten Karten beschreiben. Fehlende praktische Nachweise offen lassen.

## Verifizierter Stand
- Studio codex/workspace-ui: 61/61 Tests sowie CLI-/WPF-Build erfolgreich;
  tmp/release-readiness-ci.log.
- Runtime codex/upstream-key-context, c959bd4: 193/193 Tests, kein Skip;
  tmp/release-readiness-runtime.log. Neue Runtime unter
  artifacts/runtime-releasecheck-20260914 mit BUILD-IDENTITY.json.
  sourceHadUncommittedChanges=true wegen bestehendem Runtime-Arbeitsstand;
  keine Bereinigung fremder Aenderungen vorgenommen.
- Neuer synthetischer Core-Gesamtablauf: Vault anlegen, mit bestehender Freigabe speichern, Empfaenger entfernen/Recovery-Passphrase wechseln, veralteten Schreibzugriff ablehnen, Sicherung erstellen, Quelldateien entfernen und in anderem Ordner wiederherstellen. Beide zulaessigen Zugaenge, verweigerte alte Zugaenge und Manifest-Hashes geprueft. Alte Vaultkopien bleiben ausdruecklich mit alten Berechtigungen lesbar. 62/62 Regressionstests erfolgreich. Nachweis tmp/vault-lifecycle-regression.log; keine Windows-Mehrbenutzer- oder UI-Abnahme daraus ableiten.
- Vault-Invalid-Handle-Fix, bestehende Empfaenger nicht wieder automatisch
  hinzufuegen, Passwort-/Namensdialogvalidierung bereits implementiert und
  synthetisch im WPF-Smoke geprueft. Preview13 enthaelt nicht alle Folgefixes.
- Erster gemeinsamer Kandidat artifacts/releasecheck-20260914:
  MSI-Vertrag geprueft, administrative Extraktion Exit 0, Studio-/Runtime-EXE
  und DLL jeweils hashgleich zum Build. Das ist keine Installation/Upgrade-Abnahme.
  Paketlauf scheiterte anschliessend an der nur fuer rc-Suffixe geeigneten
  Versionspruefung des Abnahme-Kits.
- Build-Release.ps1 korrigiert: separates ArtifactsPath unter artifacts,
  VERSION im Paket aus PackageVersion, allgemeine Vorabversionssuffixe,
  Pruefsummen nur im erzeugten Abnahme-Kit statt im Quellordner.
  Versionsmuster fuer stabil/rc/releasecheck geprueft (3/3).
- Korrigierter Kandidat: artifacts/releasecheck-20260914-fixed;
  Log tmp/release-readiness-package-fixed.log; Exit 0. Alle drei Paketmanifest-Hashes korrekt; 1024/1024 Dateien im Portable-ZIP und im administrativ extrahierten MSI hashgleich zum Build. VERSION korrekt. Details: RELEASE-READINESS-2026-09-14.md.

## Neuer Startnachweis
Regulaerer Publish ohne Preview-Flag: artifacts/release-startcheck-20260914/Studio,
Version 1.1.0-releasecheck.20260914.2. Runtime unveraendert beigelegt/erkannt.
62/62 CI-Tests und CLI/WPF-Build erfolgreich; Publish, --ui-smoke-test und
--self-test Exit 0. Bericht tmp/release-startcheck-smoke2/SieEncode/ui-smoke/
ui-smoke-result.json dokumentiert den tatsaechlichen isolierten Settingspfad.
Der erste Testversuch zeigte: bisheriges Show/UpdateLayout wartete nicht auf
Loaded; jetzt explizites Warten. Produktionsstart ohne Testargument bleibt
unveraendert. Kein neuer MSI-/ZIP-Build; bestehender fixed-Kandidat enthaelt
diese Teststartkorrektur noch nicht. Details RELEASE-READINESS-2026-09-14.md.

## Neuer Vault-Dialognachweis
Regulaerer Build 1.1.0-releasecheck.20260914.3 unter
artifacts/vault-access-check-20260914/Studio, unveraenderte Runtime beigelegt.
32 UI-Pruefungen und 5 Kodierszenarien bestanden, Prozess Exit 0.
Neuanlage im VaultAccessWindow mit zwei synthetischen Empfaengern positiv
bestaetigt; zurueckgegebene Zertifikate nach OnClosed zum Speichern verwendet.
Gespeicherten Vault per beiden Empfaengern und Recovery geoeffnet, denselben
Vault erneut im Dialog geoeffnet, zweiten Empfaenger per Click entfernt,
Recovery gewechselt, gespeichert und alte Zugaenge am neuen Vault abgewiesen.
Kein Produktfehler in diesem Ablauf gefunden; nur Testabdeckung erweitert.
Log/JSON: tmp/vault-access-smoke/. Keine Windows-Identitaeten erzeugt.
Die Persistierung wird im Test gezielt ueber ProjectKeyVault ausgefuehrt;
der komplette Editor-Speichern-/Entsperren-Clickpfad und native Datei-Dialoge
sind damit noch nicht abgeschlossen. Keine reale Mehrbenutzerabnahme.
Keine erneute unveraenderte Core-CI/Runtime-Pruefung oder MSI-Paketierung.

## Editor-Vault-v2-Nachweis vom 15.09.
Der vorbereitete Test ist abgeschlossen: 33 UI-Checks und 5 Kodierszenarien,
Exit 0; Bericht tmp/editor-vault-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Regulaerer Build 1.1.0-releasecheck.20260914.4 unter
artifacts/editor-vault-check-20260914/Studio (dieser Lauf ohne beigelegte Runtime).
Speichern und Entsperren ueber die tatsaechlichen Editor-Buttons und Passwort-
dialoge bestaetigt, einschliesslich automatisch erzeugter unabhaengiger Sicherung.
Nach erneutem Laden der keyfreien Projektdefinition bleiben Keys gesperrt;
erst Entsperren stellt den urspruenglichen Schluesselsatz und Verifikationsstatus her.
Keine Produktkorrektur notwendig, nur neue Testabdeckung. Keine erneute Core-CI
oder Runtime-Pruefung bei unveraenderter Implementierung. Kein neues MSI/ZIP.
Weiter offen: v3-Editorpfad (Windows-Identitaet/Recovery/Empfaengerverwaltung),
native Datei-Dialoge sowie Installation/Upgrade und reale Mehrbenutzer/Hardware.
Nicht den erfolgreich abgeschlossenen v2-Clickpfad erneut inventarisieren.

## Aktuell: v3-Editorablauf abgeschlossen
Stand 15.09.: regulaerer Build 1.1.0-releasecheck.20260915.1 unter
artifacts/editor-v3-check-20260915/Studio, ohne beigelegte Runtime in diesem Lauf.
34 UI-Checks, 5 Kodierszenarien und 62/62 Core-Regressionstests bestanden;
CLI/WPF-Build erfolgreich. UI-Prozess Exit 0, stderr leer.
Editor verwaltet Identitaetsquelle jetzt ueber Delegate mit unveraendertem
Windows-Standard. Synthetischer Test prueft per Editor-Click: v2 zu v3 freigeben,
mit Identitaet entsperren, Schluessel aendern (Verifikation erlischt), mit Identitaet
speichern (Recovery bleibt nutzbar) und ohne Identitaet per Recovery entsperren.
Nachweise tmp/editor-v3-ci.log und tmp/editor-v3-smoke/SieEncode/ui-smoke/ui-smoke-result.json.
Kein echter Windows-Mehrbenutzernachweis. Kein neuer MSI-/ZIP-Build.
Naechster gezielter Abschnitt: nach ausschliesslichem Recovery-Entsperren ohne
Identitaet den Speichern-/Freigabe-Anwenderablauf pruefen; v3-Save nutzt derzeit
weiterhin Zertifikatszugriff. Nicht den bereits bestandenen Identitaetspfad wiederholen.
Danach native Import/Export-Dateidialoge und isolierte Installer-/Upgrade-Umgebung.

## Naechste Arbeiten / Freigabekriterien
1. Korrigierter MSI-/Portable-/Abnahme-Kit-Lauf ist abgeschlossen; Archive,
   Versionsangaben und Hashmanifeste sind geprueft. Keine unveraenderten Gesamtlaeufe.
2. Produktionsbuild mit isolierten Testdaten starten und Import/Speichern/
   Export sowie Vault-Neuanlage, Wiederoeffnen, Sicherung/Wiederherstellung,
   Empfaengeraenderung durchgaengig pruefen. Regulärer Produktionsbuild hat jetzt
   den isolierten UI-Smoke bestanden (31 Checks, 5 Kodierszenarien). Teststart
   nutzt expliziten SettingsStore mit vorab gespeicherten Testpfaden; wartet
   auf Loaded. Keine APPDATA-Umleitung als Isolation vorausgesetzt. Native
   Dateidialoge und vollstaendige Vault-UI-Kette bleiben gesondert offen.
3. Installieren/Upgrade/Deinstallieren in echter isolierter Windows-Umgebung.
   MajorUpgrade nutzt gleiche UpgradeCode wie RC15: auf diesem produktiven Host
   keinen /i-Test mit bloss geaendertem INSTALLFOLDER durchfuehren.
4. Zweiter Windows-Benutzer/Rechner fuer Mehrbenutzer-Vault und Wiederherstellung.
   Reader/Testtransponder fuer ChangeFileSettings und kompletten Bedienlauf.
   Geeignete Umgebung bisher nicht bestaetigt. Die neue Runtime-Binaeridentitaet
   besitzt keine uebertragbare Hardwarefreigabe; Hashschutz nicht aufweichen.
5. RC15-Funktionsparitaet als Anwenderablauf abschliessen. Der vorhandene
   XAML-Vertragscheck ist kein vollstaendiger Bediennachweis. Physische DPI-
   und native Dateidialogtests bleiben praktische Abnahme.
6. Erst nach belegten Kriterien finales internes Release benennen. PDF bewusst
   separat; Code-Signing und externer Pilot weiter zurueckgestellt.

## Arbeitsweise
Nur kanonische Repositories verwenden, RC15/Historie unberuehrt lassen.
Keine privaten Profile, Schluessel, Zertifikate oder reale Kartenevidenz committen.
Pro Abschnitt sinnvolle Arbeit fertigstellen, Nachweise aktualisieren und sichern.
Keine neuen Aufgaben, keine doppelten Automationen, keine Routine-Benachrichtigungen.
Automation sieencode-ui-und-pdf-fertigstellen wurde am 14.09. auf
SieEncode Release selbststaendig absichern umgestellt; stuendlicher Heartbeat.
