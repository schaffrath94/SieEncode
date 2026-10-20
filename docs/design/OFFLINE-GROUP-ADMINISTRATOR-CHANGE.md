# Wechsel der Hauptverwaltung: noch offener Implementierungsumfang

Stand 2026-10-20. Dieser Vertrag beschreibt ausstehende Arbeit, keinen bereits
verfügbaren Bedienweg. Bezug: Implementierungsfolge Punkt 2 in
[Offline-Vault-Gruppen](OFFLINE-VAULT-GROUPS.md).

## Verifizierter Iststand

`OfflineGroupAdministrationStore.RestorePrivateBackup` stellt den zum aktuell
registrierten Fingerabdruck passenden privaten Schlüssel wieder her. Das hilft
beim Rechnerwechsel, entzieht einer alten Kopie des Schlüssels aber keine Rechte.
`OfflineGroupTrustStore.VerifyPackageUpdate` verlangt denselben öffentlichen
Verwaltungsschlüssel wie bei der Registrierung. Ein anderer Schlüssel wird
absichtlich abgewiesen. Es gibt keinen gesonderten Übergangsimport.

Die bisherige Wiederherstellung darf deshalb nicht als Widerruf eines früheren
Hauptverwalters oder als Schlüsselrotation bezeichnet werden.

## Vorgesehener kontrollierter Übergang

Ein Schlüsselwechsel benötigt eine eigene, ausdrücklich bestätigte Aktion und
ein eigenes öffentliches Übergangsformat. Gewöhnliche Registrierungsdateien
dürfen den Vertrauensanker weiterhin niemals ersetzen.

Der Übergang bindet mindestens Bibliotheks-ID, alten öffentlichen Schlüssel,
exakten Ausgangscheckpoint, neuen öffentlichen Schlüssel und neue Richtlinie.
Die bisherige Verwaltung signiert diese Bindung; die neue Verwaltung muss
zusätzlich den Besitz ihres privaten Schlüssels nachweisen. Die genaue
kanonische Serialisierung und Signaturdomänentrennung sind vor Implementierung
festzulegen und mit Manipulationstests zu prüfen.

Empfänger prüfen den bisherigen lokal angehefteten Schlüssel, Ausgangscheckpoint
und beide Signaturen. Der neue Fingerabdruck muss unabhängig bestätigt werden.
Die neue Richtlinie muss die Revision strikt erhöhen. Bibliotheks-ID,
Personen-/Geräteverzeichnis, Gruppen und Recovery-Zugänge bleiben bei einem
reinen Verwaltungswechsel erhalten. Bestehende Vollmachten benötigen eine
ausdrückliche Entscheidung; keine stillschweigende Rechteausweitung.

Importiert werden neuer Vertrauensanker und Richtlinie gemeinsam über den
bestehenden konfliktgeprüften lokalen Dateiaustausch. Eine Unterbrechung darf
keinen gemischten Zustand hinterlassen. Lokaler privater Verwaltungsspeicher
und öffentlicher Vertrauensspeicher sind getrennte Dateien: ihre Übergabe muss
mit wiederaufnehmbaren Zuständen entworfen werden, nicht als gemeinsame atomare
Transaktion ausgegeben werden. Vorhandene Schlüsseldateien werden nicht
stillschweigend überschrieben oder gelöscht.

## Vertrauens- und Offline-Grenzen

- Erst nach bestätigtem Import lehnt ein Arbeitsplatz weitere Richtlinien des
  alten Schlüssels ab, auch wenn sie eine höhere Revision tragen.
- Offline gebliebene Arbeitsplätze vertrauen zunächst weiter dem alten Schlüssel.
- Alte Vaultkopien und bereits gelesene Kartenschlüssel werden dadurch nicht
  zurückgerufen. Eine Verwaltungsrotation ist keine Rotation von Vaultinhalten.
- Bei verlorenem oder kompromittiertem altem Verwaltungsschlüssel darf es keinen
  automatischen Ersatzanker aus der gemeinsamen Ablage geben. Ein gesonderter
  Wiederaufnahmeweg mit externer Vertrauensprüfung bleibt zu definieren.
- Abweichende Ausgangsstände müssen sichtbar behandelt werden. Den lokalen
  Versionsstand dafür zurückzusetzen ist kein zulässiger Konfliktlösungsweg.

## Erforderliche Nachweise vor Freigabe

1. Gültiger Übergang über öffentliche Dateien und anschließendem Neustart;
   normale Gruppen- und separate Recovery-Zugriffe bleiben erhalten.
2. Ablehnung bei falscher Bibliothek, altem Schlüssel, Ausgangscheckpoint,
   unbestätigtem neuem Fingerabdruck oder manipulierter Signatur/Richtlinie.
3. Alte Verwaltung nach Import auch mit höherer Revision abweisen; neue
   Verwaltung akzeptieren; Wiederholungsimport und parallele Änderungen
   konfliktfest behandeln.
4. Fehler vor bzw. während des lokalen Schreibens lassen den vorherigen
   konsistenten Zustand bestehen oder erlauben dokumentierte Wiederaufnahme.
5. Keine zusätzliche Gruppenmitgliedschaft durch Verwaltungswechsel.
   Delegierte Rechte bleiben begrenzt; alte private Sicherungen können den
   neuen Vertrauensanker nicht zurücksetzen.
6. Offline belassener Arbeitsplatz demonstriert ausdrücklich die verbleibende
   alte Vertrauensentscheidung. Keine sofortige globale Sperre behaupten.
7. UI führt durch neue private Sicherung, Fingerabdruckabgleich, öffentliche
   Verteilung und lokalen Import; keine privaten Schlüssel in Exporten/Logs.

Nächster Implementierungsschritt: Übergangsformat und reiner Verifikationskern
mit positiven und negativen Vertragstests; anschließend private Übergabe,
persistenter Import und UI. Bestehende Updateprüfungen bleiben dabei erhalten.
