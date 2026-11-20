# SieEncode – gezielter Kollegentest

Stand 07.11.2026 · **1.3.0-offline-groups.4** · interner Testkandidat.
Buttontexte gegen den Quellstand des Installers abgeglichen. Keine abgeschlossene
Hardwarefreigabe. Nur vereinbarte Testprojekte verwenden.

## START – Installation und Vorbereitung

Im Paket liegt **SieEncode_1.3.0-offline-groups.4_internal_x64.msi**.
Das ZIP ist nur die Versandhülle; es enthält kein portables Programm.

1. Gesamtes ZIP in einen lokalen Ordner entpacken.
2. MSI doppelklicken und den Windows-Installationsdialog abschließen. Benötigte
   Administratorrechte ggf. über eure IT bereitstellen. Der interne Installer
   ist unsigniert; keine Unternehmensschutzrichtlinien umgehen. Ein Update ersetzt
   die vorhandene SieEncode-Installation. Keine Identitäten oder Vaults löschen.
3. SieEncode über das Startmenü öffnen. Version **1.3.0-offline-groups.4** prüfen.
4. Neue Arbeitsplätze nach **02-KURZSTART.html** einrichten. Bereits eingerichtete
   Arbeitsplätze behalten Identität und Registrierung. Die folgenden Testschritte
   sind kein Auftrag, die vollständige Ersteinrichtung erneut zu testen.
5. Vor Beginn füllt der Testverantwortliche die Angaben unten aus. Bei fehlenden
   Angaben den betreffenden Test als **nicht durchgeführt** zurückmelden.

| Angabe | Vom Verantwortlichen einzutragen |
| --- | --- |
| A: Hauptverwaltung, Windows-Benutzer/Rechner | |
| B: normales Mitglied / später begrenzter Verwalter | |
| Gemeinsamer Test-Projektordner | |
| Gemeinsamer Profilordner | |
| Bibliotheks-ID und unabhängiger Kontakt zum Fingerabdruckabgleich | |
| Testperson B / Gerätefingerabdruck B | |
| Gruppe Lokal: A und B; Gruppe Lokal 2: nur A | |
| Je ein synthetisches Testprojekt für Lokal und Lokal 2 | |
| Separater Recovery-Verantwortlicher, Sicherung geprüft | |

**Bestehende VM:** Für die bisherigen Bedienprojekte SieEncode unter **testerB**
starten. Projekt **Neues Projekt Gruppe Kopie** liegt unter
`C:\Users\testerB\Documents\SieEncode-Projekte`. Unter labadmin fehlt dieser
persönliche Zugang. VM-Beispieldaten nicht als produktive Einrichtung verteilen.

**Nicht wiederholen:** automatische Funktionsprüfung, Installationszyklen,
Normal-/Recovery-Wechsel, kryptografische Kopierprüfung und Offline-Test sind
bereits bestanden. Hier geht es um noch nicht bestätigte Bedienwege.

## T1 – Auswahl nach Projektkopie (ein normaler Tester, ca. 3 Minuten)

1. **Projekte** → Bereich **Bibliothek**: vereinbartes Gruppen-Testprojekt anklicken.
2. **Bearbeiten**, dann **Vault entsperren** anklicken. Normalen Zugang verwenden,
   nicht **Mit Recovery-Schlüssel öffnen**. Keine Schlüsselwerte verändern.
3. Falls der Expertenbereich fehlt: den Ansichtsumschalter oben rechts anklicken
   (zunächst **Standardansicht**). **Weitere Editoraktionen · Experten** aufklappen.
4. **Speichern unter** anklicken. Im Dialog **Gruppenprojekt vollständig kopieren**
   einen noch nicht vorhandenen Namen eingeben, z. B. `Kollegentest-Kopie-01`.
   **OK** anklicken.
5. Prüfen: Editor und Projektanzeige oben nennen die neue Kopie. Zu **Projekte**
   wechseln: Die neue Kopie ist ausgewählt, das Original weiterhin vorhanden.

**Bestanden:** Alle drei Anzeigen beziehen sich auf die Kopie, Original vorhanden.
Ein erneuter vollständiger Neustart-/Recovery-Test ist hier nicht erforderlich.
**Feedback:** T1, ursprünglicher/neuer Testname, welche Anzeige ggf. abweicht.

## Wiederkehrender Schritt U – neuen Gruppenstand übernehmen

Diese Klickfolge wird in T2–T4 jeweils bei B benötigt:

1. A: im Fenster **Offline-Gruppen verwalten · Entwicklung** auf
   **Gespeicherten Stand öffentlich exportieren** klicken. Im Dateidialog einen
   neuen eindeutigen Namen verwenden und **Speichern** klicken. Nur diese
   öffentliche Datei an B geben; Revision notieren.
2. B: **Einstellungen** → **Registrierte Gruppen aktualisieren**. Die neue
   `.group-enrollment.json` auswählen und **Öffnen** klicken.
3. Im Fenster **Registrierte Gruppen aktualisieren** Bibliotheks-ID, bisherige
   und neue Revision sowie geänderte Personen/Gruppen prüfen.
4. Checkbox **Den geprüften neuen Stand für diesen Windows-Benutzer übernehmen.**
   aktivieren, dann **Geprüften Stand übernehmen** klicken.

**Erwartung:** Neuer Stand übernommen. „Dieser Gruppenstand ist bereits registriert.“
bedeutet, dass keine neuere Datei ausgewählt wurde; nicht die Registrierung löschen.

## T2 – Mitglied entfernen und wieder aufnehmen (A und B, ca. 10 Minuten)

Voraussetzung: oben vereinbarte Testgruppen/Projekte. Nur die Testperson verändern.

1. B: Lokal-Testprojekt unter **Projekte** auswählen → **Bearbeiten** →
   **Vault entsperren**. Zugriff muss möglich sein. Projekt schließen über
   **Projekt schließen**. Keine noch entsperrte Sitzung für die spätere Sperrprüfung verwenden.
2. A: **Einstellungen** → **Offline-Gruppen verwalten**. Richtige Verwaltung
   auswählen, in der Gruppentabelle **Lokal**, in deren Mitgliederliste **B**
   markieren. **Mitglied aus Gruppe entfernen** → nach Kontrolle **Ja**.
3. **Gruppen signiert speichern** anklicken. Neue Revision notieren.
   Fenster mit **Schließen** schließen.
4. A: Lokal-Testprojekt → **Bearbeiten** → **Vault entsperren** →
   **Gruppenempfänger abgleichen**. Vorschau muss den entzogenen Zugang zeigen.
   Im Dialog **Gruppenempfänger abgleichen** erst nach Kontrolle **Ja** klicken.
   Meldung über Aktualisierung/verschlüsselte Altsicherung notieren.
5. Neuen Stand mit Schritt **U** an B übertragen.
6. B: Lokal-Testprojekt erneut öffnen → **Vault entsperren**. Erwartung:
   aktueller Vault wird abgewiesen. Alte, getrennte Kopien sind kein Sperrnachweis.
7. A: **Offline-Gruppen verwalten**. Unter **Personen und Geräte** B auswählen,
   in der Gruppentabelle Lokal auswählen, **Ausgewählte Person zur Gruppe hinzufügen**,
   dann **Gruppen signiert speichern**. Fenster schließen.
8. A: wie Schritt 4 erneut abgleichen. Danach Schritt **U** wiederholen.
   B öffnet das Projekt erneut und klickt **Vault entsperren**: Zugriff wieder möglich.

**Bestanden:** Entzug und Wiederaufnahme über die Dialoge funktionieren;
keine fremde Gruppe/Person wurde geändert. **Feedback:** T2, Revisionen, Ergebnis
von Schritt 6 und 8, genaue Fehlermeldung bei Abweichungen.

## T3 – Zweites Gerät derselben Person (nur wenn verfügbar, ca. 10 Minuten)

Voraussetzung: B hat ein zweites Testgerät oder getrenntes Windows-Profil B2 mit
eigener Identität. Ohne B2: **nicht durchgeführt**, keinen Ersatznachweis erfinden.

1. B2: **Einstellungen** → **Vault-Ersteinrichtung starten** → eigene Identität
   auswählen/erstellen → **Öffentliche Einladung exportieren**. Nur die öffentliche
   Datei und unabhängig bestätigten Fingerabdruck an A geben.
2. A: **Offline-Gruppen verwalten** → **Personen und Geräte** → vorhandene Person B
   auswählen → **Geräteeinladung zur Person hinzufügen**. Einladung auswählen,
   **Öffnen**, Fingerabdruck unabhängig prüfen und Bestätigung mit **Ja** beantworten.
3. **Gruppen signiert speichern**, Projekt wie T2/4 abgleichen. B/B2 übernehmen
   aktuellen Stand; B2 registriert eine erstmals verwendete Bibliothek nach Kurzstart.
   Beide müssen dasselbe freigegebene Testprojekt normal entsperren können.
4. A: Person B auswählen. In der unteren Gerätetabelle nur beim Gerät B2 die
   Checkbox in Spalte **Aktiv** deaktivieren. Nicht die obere Personen-Checkbox
   ändern. Andere Zelle anklicken, dann **Gruppen signiert speichern**.
5. Vault erneut abgleichen, neuen Stand an B/B2 geben. Beide öffnen das Projekt
   frisch: B bleibt berechtigt, B2 wird abgewiesen.
6. Zum Aufräumen B2 wieder aktivieren, signiert speichern, abgleichen und verteilen,
   sofern mit dem Testverantwortlichen so vereinbart.

**Feedback:** T3, Gerätebezeichnungen, Revisionen und beide Zugriffsergebnisse.
Keine privaten Einladungen oder Schlüssel an den Feedbackbogen anhängen.

## T4 – Begrenzte Verwaltungsvollmacht (A und B, ca. 10 Minuten)

Voraussetzung: B wieder normales Mitglied von Lokal; A ist Hauptverwaltung.

1. B: **Einstellungen** → **Mein Gruppenverwaltungsplatz**. Bibliothek auswählen →
   **Bibliothek laden**. Eigene Person auswählen → **Person und Arbeitsplatz laden**.
   Falls erstmals gefragt: separaten Verwaltungsschlüssel nach Kontrolle mit **Ja**
   erzeugen. **Öffentliche Verwaltungseinladung exportieren**, Datei speichern.
   Öffentliche Datei und unabhängig abgeglichenen Verwaltungsfingerabdruck an A geben.
2. A: **Offline-Gruppen verwalten**; nur Lokal in der Gruppentabelle markieren.
   **Verwaltungseinladung für ausgewählte Gruppen freigeben** anklicken, Einladung
   öffnen. Im Dialog **Verwaltungsvollmacht bestätigen** den unabhängig erhaltenen
   Verwaltungsfingerabdruck eingeben → **OK**. Nicht den Gerätefingerabdruck verwenden.
3. Schritt **U** durchführen. B öffnet seinen Verwaltungsplatz erneut wie Schritt 1.
   Nur Lokal darf bearbeitbar sein; Lokal 2 darf keine Bearbeitungsvollmacht erhalten.
4. B ändert den Namen der freigegebenen Gruppe testweise in `Lokal Test` und klickt
   **Änderung signieren und exportieren**. Öffentliche `.group-change.json` speichern
   und an A geben. Noch ist kein zentraler Gruppenstand geändert.
5. A: **Signierte Gruppenänderung prüfen und übernehmen** anklicken, Datei öffnen,
   angezeigte Änderung kontrollieren und mit **Ja** bestätigen. Dieselbe Datei
   nochmals einreichen: sie muss ohne erneute Änderung abgewiesen werden.
6. A: zuerst den genehmigten Stand exportieren; B übernimmt ihn mit Schritt U.
   Dann die zugehörige Vollmacht auswählen → **Ausgewählte Vollmacht entziehen** →
   nach Kontrolle **Ja**. Erneut den neuen Stand exportieren; B übernimmt ihn.
7. B lädt seinen Verwaltungsplatz neu: keine weitere gültige Änderung mit der
   entzogenen Vollmacht möglich. Die persönliche Geräteidentität bleibt vorhanden.

**Feedback:** T4, nur Lokal bearbeitbar ja/nein, Übernahme, erneutes Einreichen,
Entzug; Revisionen und genaue Fehlermeldungen. Aufräumen: A kann den Gruppennamen
wieder auf Lokal setzen, **Gruppen signiert speichern** und neuen Stand verteilen.

## T5 – Gemeldeter Stationsfehler mit geeignetem Reader (Verantwortlicher)

Nur mit bekannter Testkarte, passendem Projektzugang und vereinbartem Reader.
Ohne diese Voraussetzungen **nicht durchgeführt**. Keine unbekannte Karte verwenden.

1. **Einstellungen** → **Reader-Einstellungen in RFiDGear öffnen**. Dort passenden
   Anbieter/Port einstellen, speichern, RFiDGear schließen. Dessen Dialoge hängen
   vom Anbieter ab; diese Anleitung erfindet dafür keine Buttonbeschriftungen.
2. In SieEncode **Leserkonfiguration neu prüfen**. Dies allein ist kein Hardwaretest.
3. **Projekte** → vereinbartes Passphrasen-Testprojekt auswählen → **Projekt wählen**.
   Bei **Vault für Kodierstation entsperren** die bisherige Projektpassphrase lokal
   eingeben → **OK**. Keine Schlüsselwerte für das Feedback notieren.
4. In der Station **Projekt prüfen**. Befunde wörtlich festhalten. Fehlende Builddateien
   bei einem bloßen Entwurf nicht als falschen Kartenschlüssel interpretieren.
5. Nur bei bereitstehendem Projekt und geeigneter Testkarte **Transponder erkennen**
   einmal anklicken. Erwartung: kein Fehler wegen unaufgelöster `secret://`-Referenz.
   Ergebnis und ggf. Reader-/Authentisierungsfehler notieren. Keine Formatierung,
   Vorbereitung oder Kodierung im Rahmen dieses Tests starten.

**Feedback:** T5, Reader-Modell, Kartentyp ohne sensible Kennungen, Schaltfläche,
genaue Meldung. Ein voller Schreib-/Kodierablauf benötigt einen eigenen
projektspezifischen Hardwareplan und ist durch T5 nicht freigegeben.

## Rückmeldung

**03-FEEDBACK.md** ausfüllen. Pro Test: bestanden / Fehler / nicht durchgeführt.
Bei Fehlern Schritt-ID, Istverhalten und Meldung notieren; nicht wiederholt
auf eine möglicherweise schreibende Aktion klicken. Screenshots sind optional,
ohne Schlüssel, Passphrasen oder private Sicherungen. Auch unklare Begriffe und
fehlende Hinweise melden — Verständlichkeit gehört zur Abnahme.
