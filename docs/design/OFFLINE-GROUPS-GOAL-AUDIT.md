# Auftragsabgleich Offline-Gruppen – 29.10.2026

Gesamtziel **nicht abgeschlossen**. Am 24.10.2026 hat der Benutzer den Aufbau
einer neuen separaten VirtualBox-Testumgebung autorisiert. Die neue VM ist
nicht mehr fuer verlaessliche laengere Abnahmen einsatzbereit: Windows-Systemlog
1074 belegt automatische Abschaltungen wegen abgelaufener Evaluierung.
Der Benutzer bestaetigte am 29.10., dass derzeit keine gueltig lizenzierte
Ersatz-Testumgebung verfuegbar ist. Bestehende VM und Testdaten erhalten;
keine Neustartschleife oder Lizenzumgehung. Reale MSI-Installation 1.2.1 und Upgrade auf den Kandidaten,
installierte DLL-Hashes sowie Self-Test und UI-Smoke beider Versionen bestanden
(Administrator). Siehe [Testlab-Stand](OFFLINE-GROUPS-TESTLAB.md) und
[Ergebnis](offline-groups-installed-suite.json). Vorhandene fremde VMs
bleiben unverändert. Keine neue Automation und kein weiterer Paketbau.

## Bewerteter Stand

Testkandidat `1.3.0-offline-groups.1` aus sauberer Studio-Quelle
`2be1be0857c1933e5bc91c95d3296f84052ce6b7`. Danach ausschließlich Dokumentation.
Die [Kandidatennachweise](OFFLINE-GROUPS-CANDIDATE-1.3.0-1.md) enthalten exakte
Hashwerte und Grenzen. CI: `tmp/group-device-import-ci.log`, 72/72 Tests;
publizierter Kandidat: `tmp/offline-groups-candidate-smoke/SieEncode/ui-smoke/ui-smoke-result.json`,
60 Checks, Version geprüft. Diese Belege sind lokale Softwareprüfungen.

## Anforderungen und Nachweise

| Anforderung aus dem Auftrag | Konkrete Umsetzung und vorhandene Evidenz | Bewertung / verbleibender Nachweis |
| --- | --- | --- |
| Überlappende zentral verwaltete Gruppen | `OfflineGroupAdministrationStore`, `OfflineGroupDirectory`; `OfflineGroupMatrixWorkflowTests` entschlüsselt alle 24 Kombinationen der acht Personen und drei gewünschten Gruppen mit unabhängigen Schlüsseln. | Matrix synthetisch belegt; zwei reale Windows-Benutzer mit gemeinsamem lokalem Ordner und zwei Gruppen per Produkt-API bestanden. |
| Explizite Vertrauensregistrierung | `OfflineGroupTrustStore.Enroll`, Registrierungsdialog; Kandidaten-Smoke prüft fehlende Bestätigung, falschen Fingerabdruck und Überschreibschutz. | Lokale Dialogfunktionen und DPAPI belegt; unabhängiger Fingerabdruckabgleich im echten Anwenderlauf offen. |
| Persistenter Versionsstand und Offlinebetrieb | Signierte Richtlinien, gebundene lokale DPAPI-Datei, Rücksprung-/Konfliktprüfung; `OfflineGroupTrustTests`, `VerifyOfflineTransition`. | Persistenter Trust und alte lokale Kopie in neuem Prozess unter B bestanden; keine getrennte Maschine oder unerreichbare Netzwerkablage getestet. |
| Getrennte Verwaltungsrechte | Separater Signierspeicher; Schlüsseltrennung zu Geräten, Delegation und Recovery; Hauptverwalter ist nicht automatisch Vaultempfänger. | Core-/Windows-Integration belegt. Vollständiger nativer Rollenablauf offen. |
| Gruppenbegrenzte Delegation | `OfflineGroupDelegationService`, `OfflineGroupDelegateStore`; Signatur, Umfang, veraltete Anträge, Replay, Entzug und Verwaltungsnachfolge geprüft. | Zusaetzlich reale Windows-Benutzer A/B: persistente Delegatenschluessel, Scopebegrenzung, Genehmigung, Replay und Entzug bestanden; siehe offline-groups-vm-authority-results.json. |
| Personen-/Geräteverwaltung | Mehrere Geräte pro Person, Aktivierung/Entzug; unabhängige RSA-Schlüssel pro Gerät. Wiederverwendung bereits beim Einladungsimport abgewiesen, Entwurf unverändert. | Coretests und Kandidaten-Verwaltungsdialogtest bestanden. |
| Authentisierte Projektzuordnung | `GroupVaultService` bindet Bibliothek, Projekt-ID, Gruppe, Richtlinienstand und Empfänger kryptografisch; `ProjectGroupVault`, Erstellungs-/Kopierassistent. | Zusaetzlich unter zwei Windows-Benutzern: volle Projektdateien, neue Kopie-ID, unveraenderte Originaldateien, erhaltene Schluesselnutzdaten und abgewiesene ID-Manipulation per Produkt-API belegt (offline-groups-vm-project-results.json). |
| Kontrollierter Empfängerabgleich | Authentisierte Vorschau, Bestätigung im Editor, frischer Inhaltsschlüssel, verschlüsselte Altsicherung; `GroupVaultTests.VerifyReconciliation` und Matrixworkflow. | Stale-Vorschau, später Entzug, beschädigte Sicherung, konkurrierender Vault geprüft. Rotation mit Altsicherung in lokalem gemeinsamen VM-Ordner bestanden; Cloud-Konflikte offen. |
| Separater Recoveryzugang | Eigene Identität und Sicherung, zentrale Freigabe, bibliotheksweite Recovery, ausdrücklicher Editorweg; keine normale Buildfreigabe. | Echte Windows-Schluesselinstallation durch Produkt-API und Entschluesselung in separatem Prozess bestanden. Auch nach echtem VM-Neustart am 26.10. per Produkt-API bestanden; native Dialogabnahme offen. |
| Verständliche integrierte Einrichtung | Einstellungen, getrennte Rollen, interne Anleitung, leere Dialogzustände, private Sicherung vor öffentlicher Nachfolgeranfrage. | Teile nativ angesehen, Dialogfunktionen getestet; vollständiger nativer Bedienlauf einschließlich Datei-/Bestätigungsdialogen offen. |
| Sicherheits-/Integrationsprüfungen | CI 72/72; Kandidat 60 UI-Checks; echte Kindprozessabbrüche an Aktivierungsgrenzen und Neustart; MSI-Extraktion mit 1.034 Hashvergleichen. | Reale MSI-Installation/Upgrade und installierte Selbst-/UI-Tests in LTSC bestanden. Acht Produkt-API-Phasen unter zwei normalen Windows-Benutzern bestanden. Keine Stromausfall-, native Gesamt- oder neue Hardwarefreigabe. |
| Private Entwicklung und Erhalt bestehender Daten | Privater Branch `codex/offline-vault-groups`; Tests mit synthetischen Daten. Bisherige 1.2.1-MSI/Studio-/Runtime-Dateien vor/nach Paketbau hashgleich. | Keine produktiven Vaults/Identitäten migriert oder gelöscht, vorhandene Installation nicht ersetzt. |
| Ehrliche Offline-Widerrufsgrenzen | Schnellstart, Dialoge und Matrixworkflow: alte Kopien und offline gebliebene Truststände bleiben nutzbar; spätere Entzüge erst nach Import bekannt. | Dokumentiert und synthetisch demonstriert; keine Behauptung sofortiger globaler Sperrung. |

## Nächster Abnahmenachweis

### Native Nachweise seit 26.10.2026

Siehe [Bedienprotokoll](OFFLINE-GROUPS-NATIVE-SETUP-ACCEPTANCE.md), Originalbilder
und [unabhaengige Exportpruefung](offline-groups-native-export-result.json).

| Bedienablauf | Aktueller Nachweis | Verbleibend |
| --- | --- | --- |
| Persoenliche Identitaet und Einladung | Nativ angelegt, nach Neustart erhalten; oeffentlicher Export ohne privaten Schluessel separat validiert. | Vollstaendiger neuer Arbeitsplatzablauf mit Reader. |
| Verwaltung registrieren | Nativ unter labadmin registriert; neuer App-Prozess erkennt denselben Stand. | Unabhaengiger menschlicher Fingerprintkanal. |
| Bestehende Hauptverwaltung | Unter echtem testerB-Profil geladen; neue leere Gruppe nativ signiert als Revision 9; oeffentlicher Export separat validiert. | Native Erstverwaltung, private Sicherung/Restore, Personen-/Geraeteaenderungen. Testfixtures wurden vorbereitend kopiert. |
| Neueren Stand uebernehmen | Export Revision 9 gueltig; labadmin noch Revision 8. | Native Vorschau, Bestaetigung und Wiederlesen auf labadmin. |
| Projekt-/Recovery-/Delegationsablauf | Produkt-API-Nachweise unter getrennten Windows-Benutzern vorhanden. | Durchgehende native Dialogablaeufe inklusive Datei-/Bestaetigungsdialogen. |
| Installerlebenszyklus | Installation und Upgrade bestanden. | Deinstallation mit Erhalt der Testidentitaeten/-daten. |
| Kollegenanleitung | HTML-Entwurf mit Rollenwegen, Gruppenmatrix, Ablaufgrafik und fuenf Originalansichten vorhanden. | Visuelle Gesamtkontrolle und Bilder der verbleibenden geprueften Bedienablaeufe; keine finale Releaseanleitung. |

Das Lizenzproblem entwertet abgeschlossene Nachweise nicht, blockiert aber die
planbare Fortsetzung der restlichen VM-Abnahme. Softwaretests werden nicht als
Ersatz fuer diese Bediennachweise gewertet. PDF-Weiterentwicklung bleibt zurueckgestellt.

Die eingerichteten normalen Windows-Testbenutzer testerA/testerB fuer den
SieEncode-Gesamtablauf nach [Abnahmeanleitung](../OFFLINE-GRUPPEN-ABNAHME.md)
verwenden. Die acht Produkt-API-Phasen einschliesslich echter Recoveryinstallation
sind [belegt](offline-groups-vm-vault-results.json). Delegation und autorisierte Verwaltungsnachfolge unter A/B per Produkt-API bestanden (offline-groups-vm-authority-results.json). Native Bedienung und Deinstallation bleiben offen.
Vorhandene Unternehmens-VMs nicht verwenden.
Hostinstallation, Windows-Benutzer, Sicherheitskonfiguration und Zertifikatsspeicher
nicht als Ersatz fuer die vorhandene separate Testumgebung veraendern.

Ein neuer unveränderter CI-Lauf oder ein weiterer MSI-Bau schließt diese Lücke
nicht. Bis zum Abschluss der verbleibenden Abnahmeschritte bleibt die
Gesamtfreigabe offen. Verlorener/kompromittierter Verwaltungsschlüssel hat keinen
automatischen Ersatzanker; die normale autorisierte Nachfolge ist davon getrennt.
