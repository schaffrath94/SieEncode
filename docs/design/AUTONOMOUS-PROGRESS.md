# Autonome Weiterarbeit - Stand 2026-09-14

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
