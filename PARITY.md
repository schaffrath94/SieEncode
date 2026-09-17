# Funktionsparität zu PowerShell v10.39

## Geltungsbereich des aktuellen Kandidaten

Der Kandidat `1.1.0-releasecheck.20260917` ist softwareseitig mit 37 UI-Prüfungen,
fünf synthetischen Kodierszenarien und einem Dateivergleich von MSI/ZIP geprüft.
Die nachstehende Matrix dokumentiert ältere Funktions- und Abnahmenachweise.
Insbesondere „Erfüllt“ bei realem Bedienlauf, Mehrbenutzerbetrieb oder Hardware
erteilt dem aktuellen Studio-/Runtime-Build keine praktische Freigabe.
Seine offenen Prüfungen sind Installation/Upgrade/Deinstallation in isoliertem
Windows, echter Zweitbenutzer-Vault, Reader/Testkarte einschließlich
ChangeFileSettings und vollständige praktische UI-Parität.
Siehe [aktuelle Kandidatenidentität und Nachweise](docs/design/RELEASE-CANDIDATE-2026-09-17.md).
PDF-Weiterentwicklung und visuelle PDF-Abnahme sind zurückgestellt.

## Historische Funktionsmatrix

Stand: SieEncode 1.1.0-rc15 · 05.09.2026 · interner Abnahmekandidat

Diese Matrix trennt fachliche Funktionsgleichheit von reiner Ähnlichkeit der
Oberfläche. `Erfüllt` verlangt einen automatisierten Nachweis oder einen direkt
prüfbaren C#-Bedienpfad. `Teilweise` bedeutet, dass ein sicherer Kernpfad
vorhanden ist, aber mindestens eine v10.39-Bedienaktion noch fehlt. `Upstream`
kennzeichnet Funktionen, die RFiDGear selbst als verlässliche Schnittstelle
bereitstellen muss.

| Bereich | Stand | Nachweis / verbleibender Gap |
|---|---|---|
| Projektbibliothek und übergeordneter Projekteordner | Erfüllt | Rekursive Katalogtests, identischer Pfad in Projektseite und Kodierstation |
| Neue und bestehende Projektdefinition bearbeiten | Erfüllt | Speichern überschreibt atomar; Speichern unter erzeugt bewusst eine neue Definition |
| Legacy-JSON und gebaute Altprojekte migrieren | Erfüllt | Regressionen für beide Quellarten, Profil-/Layoutprüfung und Keyausschluss |
| Profilbindung, Versionierung und Historie | Erfüllt | SHA-256-Bindung, Konfliktschutz, unveränderliche Historie und Wiederherstellung als neue Version |
| Grund- und Zusatzprofile erstellen und bearbeiten | Erfüllt | Mehrere eigene Grundprofile; strukturierter C#-Editor mit Variablen-, Application-, File- und Rezeptschutz; keine fachlichen Profile im Release |
| Kundenprofil duplizieren | Erfüllt ab preview28 | Neue Identität, Version 1.0, keine übernommenen Historienzeitstempel; tiefe Kopie regressionstestet |
| Kodiervorschrift exportieren | Erfüllt | GUI und CLI; Kompatibilitätsfixtures bleiben ausschließlich intern |
| rfPrj als Profilentwurf importieren | Erfüllt | Read-only Import, keine Keys/Schreibdaten, unklare Rezepte bleiben ungelöst |
| Profil-/Projektvergleich | Erfüllt | Keyfreier semantischer TXT-/JSON-Vergleich |
| Keyerzeugung und portabler Vault | Erfüllt | CSPRNG, sichtbare Sitzungsschlüssel, bidirektionale v10.39-Vault-Parität; RC6 öffnet denselben vollständigen Vault in zwei unabhängigen Prozessen, bestätigt Portabilität und identischen Fingerprint und gibt ausschließlich Metadaten aus |
| Projektbuild und sechs rfPrj-Arten | Erfüllt | Mastering, Kodierung, zwei Karten-Preflights und zwei Recoveries semantisch gegen v10.39 geprüft |
| Vertragsartefakte und Payloads | Erfüllt | Sechs Legacy-Verträge semantisch und Payloads bytegenau gegen v10.39 geprüft; zusätzlicher Host-V1.1-Vertrag mit formalen JSON-Schemas, rfPrj-/Taskplan-Fingerprint, geordneten RunId-korrelierten Taskresultaten sowie getrennter Engine-/Consumerbewertung regressionstestet |
| Projektprüfung und Dry-Run | Erfüllt in getrennten Prüfstufen | Strenger Build-Check; schlüsselfreier Betriebscheck für bearbeitbare Definitionen; begrenzter struktureller Check für reine Bauartefakte; synthetische Credential-Simulation und keyfreie Berichte |
| RFiDGear-Start und Capability-Gates | Erfüllt | Host-V1.1 umschließt Kodierung und alle Betriebsaktionen; Binärnachweis für CUSTOMPROJECTFILE, strukturierte Ergebnisse, AUTORUN/AUTOMODE und aufgabenspezifische Capabilities; PR-208-Terminalstatus wird vollständig statt über Einzelmarker erkannt; Keywechsel nur mit Nachverifikation; terminale Engineergebnisse sind unveränderlich; Readerwahl bleibt RFiDGear-eigen |
| Kodierstation und Seriensteuerung | Erfüllt mit exakt freigegebener externer Runtime | Bedien- und Sitzungsfunktionen vollständig; Kern- und sichtbarer UI-Test prüfen Fortlaufend, Liste, Retry/Skip, Ergebnisjournal und sitzungsübergreifenden Duplikatschutz. Der getrennte Fork-Build `RFiDGear Builder Runtime 2.0.10.1` mit PR #205 und #208 bestand 177/177 Softwaretests und den vollständigen 26-Task-Hardwarelauf. Produktversion plus EXE/DLL-Fingerprint sperren jeden abweichenden Build fail-closed. |
| Karten-Preflight und Recovery-Bedienung | Erfüllt | Struktur-/Policyprüfung vor jedem Start; Recovery verlangt zwei Bestätigungen |
| Finale Nutzdatenabnahme | Erfüllt im belegbaren Umfang | Aktuelle Readbacks werden bytegenau geprüft; operative und fehlende Istwerte sind getrennt |
| Application KeySettings und Keyanzahl bitgenau prüfen | Erfüllt im RFiDGear-Vertragsumfang | Kartenstruktur-Abnahme mit `ReadAppSettings` Exact-Match und `CheckAppKeyCount`; Capability-Gate, vollständige Task.Outcome-/Terminal-Korrelation, rfPrj-SHA-256 und keyfreie Berichtsevidenz regressionstestet |
| Filetyp, Größe, Communication und alle Access Rights bitgenau prüfen | Upstream blockiert | Der reale RC5-Lauf beweist: RFiDGear 2.0.10 erkennt `CheckFileSettings` im Projektmanifest nicht und würde sonst 0 Tasks ausführen. RC5 verhindert falsches Grün, erzeugt den inkompatiblen Task nicht und weist diese Metadaten ausdrücklich als ungeprüft aus. |
| Mehrbenutzer- und Parallelbetrieb | Erfüllt | Datei-Revisionen, rechnerübergreifende Locks und exklusive Projekt-Workspace-Lease |
| Hilfe, Roadmap und Entwicklungswerkzeuge in der GUI | Erfüllt ab preview29 | Integrierte Hilfe-/Werkzeugseite, direkte Arbeitswege, lokale Dokumentation und keyfreier lesender Umgebungstest |
| Projektdefinition über GUI löschen | Erfüllt ab preview30 | Nur bearbeitbare Quelldefinition; exakte Pfadvorschau, Bibliotheksgrenze und Revisionsschutz; Vault und Buildartefakte bleiben erhalten |
| Vollständiger automatisierter C#-Bedienlauf | Erfüllt ohne Hardwarezugriff | RC6-Smoke prüft 198 interaktive Hauptfensterflächen, 109 Buttons, 9 Auswahllisten, alle zehn internen Seiten, Profil-Editor, Historie, rfPrj-Import, beide Projekttypen, die Kodierstation sowie sichtbares Umschalten und Fortschalten der Modi Fortlaufend/Liste. Dynamische Projekt-, Key- und Kartendatenfelder besitzen stabile fachliche Automation-IDs. |
| Vollständiger realer C#-Bedienlauf | Erfüllt | Die installierte RC5.6 wurde am 26.08.2026 ausschließlich über sichtbare UI-Aktionen bedient: neues Projekt, CSPRNG-Keys, Speichern, Dry-Run, Build, Projektprüfung, 6/6 Transportread, 26/26 Kodierung, 2/2 Recovery, 27/27 Mastering und finaler 6/6-Transportread. Taskposition, -index, -typ, Entscheidung, Ergebnis, Fehlercode und Zeitstempel werden zusammen mit Terminalstatus, RunId-Korrelation und RunId-Fingerprint keyfrei angezeigt. |
| Windows-Installer und Startmenü | RC8.1-Pilotkandidat | RC6.7/MSI 1.0.22 bestand Runtime-Start, Sitzungsisolation, Hardware- und Produktivabnahme. RC7.1/MSI 1.0.24 ergänzt terminale Laufüberwachung und reguläres automatisches Schließen. RC8.0/MSI 1.0.25 ergänzt die gemeinsame Mehr-Grundprofilbibliothek. RC8.1/MSI 1.0.26 schließt die gemessene Terminal-/Readback-Race-Condition fail-closed. |

## Freigaberegel

Die C#-Generation ersetzt v10.39 erst, wenn alle nicht bewusst entfallenen
Builder-Funktionen `Erfüllt` sind, die verbleibenden Upstream-Grenzen sichtbar
und fail-closed behandelt werden und ein ausdrücklich freigegebener realer
Bedienlauf mit dem Releasekandidaten erfolgreich abgeschlossen wurde.
