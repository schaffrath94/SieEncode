# Unabhängige Sicherung der Schlüsselbibliothek – 1.0

Quellcode-Zwischenstand, noch nicht gebaut oder ausgeführt.

Der neue Kern `KeyLibraryBackupService` archiviert einen unveränderten signierten, verschlüsselten Kundenbibliotheksstand zusammen mit der öffentlichen signierten Registrierung. Er benötigt bei Erstellung einen vollständigen persönlichen Kundenzugang. Die separate Recovery-Prüfung benötigt nur das Archiv, eine **unabhängig** aufbewahrte exakte Sicherungsreferenz (Bibliothek, Gruppe, Verwaltungspin, Policy-Checkpoint, Katalogrevision, Archiv-SHA256) und das passende private Recovery-Zertifikat. Sie entschlüsselt und validiert sämtliche Kundenblöcke über den bestehenden Vault-Kern, ohne einen aktiven Vault, Trust-Store oder Sync-Cursor zu ändern. Das Ergebnis enthält nur IDs, Revision und Anzahlen.

- Keine privaten Zertifikate, Verwaltungsschlüssel, Klarwerte, Projektdateien oder Auditdaten im Archiv.
- Keine automatische Installation, Rücksetzung, erneute Freigabe, zentrale Wiederherstellung oder Kartenoperation.
- Die Referenz ist kein zentral signierter Annahmenachweis. Sie muss separat und vertrauenswürdig aufbewahrt werden; alte korrekt signierte Sicherungen sind nicht automatisch aktuell. Ein späterer Berechtigungsentzug entwertet bereits vorhandene Kopien nicht rückwirkend.
- Nur das signierte kundenseparierte Format; keine neue Altformatmigration.
- Quellrevision wird vor und während atomarer Sicherung und danach geprüft; die exakten Archivbytes werden nach dem Schreiben kontrolliert. Ein nachträglicher Konflikt liefert keine erfolgreiche Referenz. Dies ersetzt keine Windows-ACL- oder Medienprüfung.

Offen: Bedienung in der bestehenden Studio-Verwaltung, gesondert gesicherte Recovery- und Verwaltungsidentitäten, separat verankerter aktueller Sicherungsnachweis, kontrollierte Wiederherstellung in isolierte Umgebung und spätere Freigabe des Betriebsbestands, Zentralzustand einschließlich Audit/Cursor/Policy-Journal, Konflikte mit neueren Ständen, Windows-/Dateisystem-/Verlusttests und vollständige Abnahme. Mastering/Recovery/Rollback-Sperren bleiben unverändert.
