# SieEncode 1.0 – zentrale Schlüsselverwaltung mit Offline-Arbeitsplätzen

**Verbindlich korrigierte Produktarchitektur, 21.11.2026.** Ersetzt das vorherige Online-Krypto-/HSM-/SAM-Zielbild dieser Datei. Die frühere Idee eines zwingenden Serverkontakts vor jeder DESFire-Operation und die Pflicht zu SAM-Lesern passen **nicht** zu den Nutzeranforderungen. Es existiert noch **kein produktiv veröffentlichtes SieEncode-Projekt**. Dies ist eine Entwicklungsgrundlage, keine bereits implementierte Synchronisation oder Sicherheitsfreigabe.

## Nicht verhandelbare fachliche Anforderungen

1. **Keine SAM-Pflicht:** Vorhandene übliche Kartenleser bleiben die Zielhardware. Neue SAM-Geräte werden für 1.0 nicht vorausgesetzt.
2. **Keine dauerhafte Internet-/Serververbindung:** Kodierung, Kartenuntersuchung und notwendige Schlüsselverwaltung funktionieren mit einem zuvor freigegebenen Offline-Bestand. Synchronisation erfolgt bei Gelegenheit; signierte, verschlüsselte Transportpakete über einen vertrauenswürdigen Datenträger sind als Alternative vorzusehen.
3. **Schlüssel dürfen und müssen autorisiert im Klartext angezeigt, kopiert und exportiert werden können**, nicht nur für Kartenschreibvorgänge, sondern z. B. für die Konfiguration von SIPORT. „Schlüssel niemals ausgeben“ ist **kein** zulässiges Produktziel.
4. **Zentrale Autorität für Verwaltung:** Kunden, Schlüsselsätze, Key-Versionen, Projekte, Mitarbeiter, Geräte, Rechte, Historie und Freigaben werden aus einer verantwortlichen zentralen Schlüsselbibliothek verwaltet. Die Zentrale ist *Quelle der Wahrheit*, nicht zwangsläufig während jeder Kartenoperation erreichbar.
5. **Keine falsche sofortige Sperrgarantie:** Eine Person, die einen Schlüssel bereits im Klartext erhalten hat oder einen früher entschlüsselbaren Offline-Vault besitzt, kann diese Kenntnis unabhängig von späterer Sperre behalten. Soft-Expiry, lokale Richtlinien und späterer Import neuer Gruppenrevisionen verhindern das nicht gegen einen bewusst manipulierenden ehemaligen Mitarbeiter.

## Zentrales Datenmodell (fachlich, noch nicht implementiert)

- **Mandant/Kunde:** eindeutige Kundenkennung, Standort-/Systemzuordnung, getrennte Schlüsselräume und Zuständigkeit.
- **Schlüsselobjekt:** stabile `KeyId`, Schlüsseltyp und Algorithmus (z. B. DESFire-AES), Zweck (PICC/App/Leser/SIPORT), Referenz zum verschlüsselten Wert, Version, Status (geplant, aktiv, abgelöst, gesperrt/kompromittiert), Erzeugungs-/Importquelle, Metadaten und Nachfolger. **Keine Klartextwerte in Datenbank-Suchindizes, Logs oder Git.**
- **Schlüsselsatz:** definierte Kombination aus `KeyId` und **Version**, einschließlich kartenseitiger AID-/Key-No.-Bindungen und eventueller Diversifikationsparameter. Gleichnamige Schlüssel sind nicht automatisch identisch.
- **Projekt:** verweist auf ein versioniertes Schlüsselset; die normale Projektdefinition bleibt key-frei. Das Projekt darf keine zentrale, unkontrollierte Kopie sämtlicher Kundenschlüssel erzeugen.
- **Person, Gerät, Rolle, Autorisierung:** Leserecht, Klartextanzeige/Kopieren, Export/SIPORT, Kodieren, Schlüsselwechsel, Vergabe-/Recovery-Administration ausdrücklich separat nach Mandant und Schlüsselset. Idealerweise getrennte Freigabe für hohe Risiken. Keine pauschale „Gruppenmitglied = alle Kundenschlüssel“-Berechtigung.
- **Audit:** lokale und zentrale Änderungshistorie, explizit verantwortete Klartextanzeige/Export/Kodierung/Import/Rotation, beteiligter Benutzer und Gerät, Zeitpunkt, Schlüssel-ID und Version; niemals Geheimwert. Offline-Ereignisse werden später nachgemeldet. **Auf frei kontrollierten Offline-PCs ist vollständige, unveränderbare Protokollierung nicht beweisbar.**

## Zentrale Speicherung und Offline-Kopie

**Zentrale Schlüsselbibliothek:**
- Server bzw. administrierte Unternehmensinstanz für Autorität, Rechte, Versionierung, Konflikte und Audits. **Kein Internetzwang** für alltägliche Offline-Arbeit; der Server kann im Unternehmensnetz liegen.
- Sensible Werte verschlüsselt, mit getrennt gesichertem Root-/Recovery-Schlüsselmaterial, kleinsten Berechtigungen und geeignetem Backup-/Wiederherstellungsprozess. GitHub/GitLab oder ein normales Quellcode-Repository ist **kein Ablageort für produktive Schlüsselwerte**.
- Die Zentrale verteilt nur die tatsächlich freigegebenen Schlüsselsets an berechtigte Benutzer/Geräte, nicht eine gesamte Schlüsselbibliothek an alle Teammitglieder.

**SieEncode Studio am Mitarbeiter-PC:**
- Lokaler, authentisierter, verschlüsselter **Offline-Vault** als Arbeitsbestand. Enthält nur explizit freigegebene `KeyId + Version` samt notwendigen Metadaten. Schlüssel sind nach berechtigter Entsperrung für Kodierung **und** für gesondert autorisiertes Anzeigen/Exportieren tatsächlich lesbar.
- Schutz durch Benutzer-/Geräteidentität und gesonderte Recovery nach einem dokumentierten Verfahren. Keine klar lesbare Dauerkopie aus einem normalen Projektordner; temporäre Ausgaben (auch RFiDGear) müssen begrenzt, gerechtfertigt und geschützt sein. **Bei lokalem Administratorzugriff und berechtigter Geheimwertanzeige besteht dennoch Extraktionsrisiko**.
- Freigaben können ein von der zentralen Stelle signiertes Gültigkeitsintervall haben. Die aktuelle Anwendung kann nach Ablauf neue, nicht erneuerte Arbeitsvorgänge blockieren. **Ein Zeitstempel auf einer frei kopierbaren Datei ist kein manipulationssicherer kryptografischer Widerruf**, wenn der Benutzer alte Dateien, Clientzustände oder Software kontrolliert.
- Offline-Bearbeitung: Klare Trennung von lokalem Entwurf und zentral bestätigter Revision; keine stillen Überschreibungen. Schlüsseländerungen nur mit definierter Rückmeldung und späterer Konfliktauflösung; freigegebene Kodieroperationen vor Ort bleiben innerhalb des lokalen Bestands möglich.

**Synchronisation bei verfügbarer Verbindung:**
- Anforderung an den zentralen Dienst oder signiertes/verschlüsseltes Datei-Paket (USB/SSD). Transport für klar definierte Empfänger; signierte Autorität, nachvollziehbare Revision und Schutz gegen unbemerkte Veränderung.
- Eingehende Änderungen als **Vorschau**: neu, geändert, entzogen, Konflikt, erforderliche Rotation; Import ist ausdrücklich, atomar und prüfbar. Kein versehentlicher Rollback auf alte Gruppen-/Schlüsselrevisionen im normalen Client.
- Ausgehende Änderungsentwürfe/Audit-Nachmeldungen mit stabiler Ereignis-ID und Konfliktbehandlung. Synchro-Bestätigung nur bei tatsächlicher Annahme durch die Zentrale. Nie „überall verteilt“ behaupten, wenn Offline-Geräte noch nicht aktualisiert sind.

## Bedienoberfläche: neue Schwerpunktaufgabe „Schlüssel verwalten“

**Neben** „Projekte“, „Karten untersuchen“ und „Karten kodieren“ ist die fachliche **Schlüsselverwaltung** ein vollwertiger Anwendungsfall (später in sinnvoller Navigation integrieren, nicht als ungeordneter Button-Wald).

- Kunden-/Standortkontext wählen → Schlüsselsätze, `KeyId`, Version, AID/PICC-Zuordnung und Status sehen. **Werte standardmäßig verdeckt**, Metadaten trotzdem nutzbar.
- „Schlüssel anzeigen“ für berechtigte Benutzer nach bewusster Aktion; „Kopieren“ / „Für SIPORT exportieren“ **gesondert** berechtigen und im lokalen Audit erfassen. Benachrichtigung, dass Ausgaben extern nicht mehr durch Vault-Widerruf kontrolliert werden. Exportformat/Automatisierung erst nach konkreter SIPORT-Spezifikation verifizieren; kein erfundener direkter SIPORT-Import.
- Import von einem vorhandenen System bzw. Schlüssel-Erfassung ausdrücklich, mit Format-, Versions- und Provenienzprüfung; vor dem Speichern Vergleich/Duplikaterkennung, niemals stillschweigende Schlüsselgleichsetzung.
- Schlüssel neu erzeugen, Versionen wechseln oder kompromittierte Werte ersetzen als **eigene explizite Workflows**, mit Prüfung der Karten- und Leser-/SIPORT-Abhängigkeiten. Entfernen aus einer Gruppe bedeutet **keine** automatische Kartenrotation.
- Eine zentrale Schlüsseländerung zeigt Folgen für Projekt, Karten, Leser, SIPORT, Offline-Arbeitsplätze, vorhandene Exporte und getrennte Sicherungen.

## Ehrlicher Berechtigungsentzug

- Zentrale Verwaltung markiert Benutzer/Identität/Schlüsselsatz-Exportrechte als entzogen. Weitere zentral ausgegebene Freigaben und neue Offline-Pakete werden verweigert; bereits synchronisierte, kooperative Clients übernehmen den Entzug beim nächsten gesicherten Abgleich.
- Der **aktuelle** Vault wird gegebenenfalls für neue Empfänger neu verschlüsselt und ein aktueller Schlüsselstand neu verteilt. Der vorher berechtigte Benutzer kann dennoch aufbewahrte Offline-Dateien entschlüsseln oder bereits im Klartext erhaltene Werte behalten.
- Kompromittierte tatsächliche DESFire-Kartenschlüssel werden nach abgestimmtem, verifiziertem Plan auf betroffenen Karten und in Lesern/SIPORT gewechselt; gegebenenfalls mehrere Schlüsselversionen während der Übergangsphase und dokumentierte Vollständigkeit. **Nur so kann früheres Schlüsselwissen technisch entwertet werden.**
- UI-Status differenziert: `Zentrale Freigabe entzogen`, `An Arbeitsplätze verteilt`, `Aktueller Vault neu verschlüsselt`, `Kartenschlüssel rotiert und an Karten/Lesern verifiziert`. Keiner dieser Status wird aus einer anderen Aktion automatisch abgeleitet.

## Umsetzungsschnitt zur vorhandenen Codebasis

Das vorhandene `ProjectKeyVault`/`ProjectGroupVault` und die persönliche Zertifikats- und Gruppenlogik sind eine technische Grundlage. `ProjectSecretPipeServer` kann in der derzeitigen Entwicklungsengine **vollständige Schlüsselwerte** an RFiDGear liefern; damit ist das Ziel `Schlüssel unter keinen Umständen lokal sichtbar` ohnehin weder implementiert noch fachlich gewollt. Die temporären Projektstände und ihre Löschung sind eine Risiko-Reduktion, keine Extraktionssperre.

**Vorgeschlagene Reihenfolge:**
1. Gemeinsames, projektunabhängiges `KeyRecord`/`KeySet`-Modell samt Version/Referenzen/Berechtigungen und klarer Trennung von Geheimwert und Metadaten konzipieren.
2. Persönlichen/gruppenbasierten verschlüsselten lokalen Offline-Bestand auf diesem Modell aufbauen und die vorhandenen Projekt-Vault-Funktionen gezielt weiterverwenden, nicht parallel eine zweite kryptografische Implementation anfangen.
3. Eigene Schlüsselverwaltungsansicht für Anzeigen/Kopieren/Import/Export/Versionierung gestalten, mit echten Rechteprüfungen und lokalen Auditereignissen – alles erst synthetisch testen.
4. Zentrale Masterbibliothek und signierten/verschlüsselten Paketabgleich mit Offline-Konfliktprüfung integrieren, ohne einen ständig erreichbaren Dienst vorauszusetzen.
5. Schutz der Schlüssel im normalen RFiDGear-/Kodierablauf, Testkarten, sichere Reader-/SIPORT-Exports und Wiederherstellung als gebündelte 1.0-Abnahme verifizieren.

**Kein** verpflichtender Umbau auf Online-`IKeyOperationProvider`, HSM oder SAM-Leser als Voraussetzung für das erste Release. HSM/SAM bleiben optionale, später unabhängig zu bewertende Unternehmens-/Hardwaremaßnahmen, wenn ein anderer Bedrohungsrahmen dies erfordert.

## Noch zu entscheiden / ohne falsche Versprechen

- Wo die zentrale Verwaltung betrieben wird und wer sie administriert (Firmen-LAN, kundenspezifische Instanz, sichere Datenträgerübergabe); wie viele Mitarbeiter und Geräte gleichzeitig beteiligt sind.
- Ob Mitarbeiter **nur ausgewählte Schlüsselsets** oder kundenspezifische komplette Bestände offline mitnehmen dürfen; welche Rollen Klartextzugriff/SIPORT-Export bekommen.
- Welche Offline-Intervalle und Sicherheitsmaßnahmen im Feld sinnvoll sind. Ein Ablaufdatum ist ein **Bedien- und Betriebs-Gate**, kein beweisbarer sofortiger Widerruf.
- Schlüsselimport/-export zu SIPORT zunächst anhand seines konkreten Formats und der tatsächlich geforderten Karte-/Leser-Schlüsselverwendung prüfen.
- Zentrale Benutzer-/Geräteidentitäten, unabhängiges Recovery, gelöschte/verlorene Endgeräte, verschlüsselte Backups, Verfügbarkeit und Wiederanlauf.

**Referenz zur fundamentalen Entzugsgrenze:** `docs/design/OFFLINE-ENTZUG-GRENZEN-1.0.md`. Änderungen aus früheren Gesprächen, die ein reines Online-KMS und nie lesbare Schlüssel als Standard behaupten, sind durch die drei verbindlichen Anforderungen oben überholt.
