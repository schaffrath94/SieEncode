# Unabhängige Sicherung der Schlüsselbibliothek – 1.0

Quellcode-Zwischenstand, noch nicht gebaut oder ausgeführt.

Der neue Kern `KeyLibraryBackupService` archiviert einen unveränderten signierten, verschlüsselten Kundenbibliotheksstand zusammen mit der öffentlichen signierten Registrierung. Er benötigt bei Erstellung einen vollständigen persönlichen Kundenzugang. Die separate Recovery-Prüfung benötigt nur das Archiv, eine **unabhängig** aufbewahrte exakte Sicherungsreferenz (Bibliothek, Gruppe, Verwaltungspin, Policy-Checkpoint, Katalogrevision, Archiv-SHA256) und das passende private Recovery-Zertifikat. Sie entschlüsselt und validiert sämtliche Kundenblöcke über den bestehenden Vault-Kern, ohne einen aktiven Vault, Trust-Store oder Sync-Cursor zu ändern. Das Ergebnis enthält nur IDs, Revision und Anzahlen.

- Keine privaten Zertifikate, Verwaltungsschlüssel, Klarwerte, Projektdateien oder Auditdaten im Archiv.
- Keine automatische Installation, Rücksetzung, erneute Freigabe, zentrale Wiederherstellung oder Kartenoperation.
- Die Referenz ist kein zentral signierter Annahmenachweis. Sie muss separat und vertrauenswürdig aufbewahrt werden; alte korrekt signierte Sicherungen sind nicht automatisch aktuell. Ein späterer Berechtigungsentzug entwertet bereits vorhandene Kopien nicht rückwirkend.
- Nur das signierte kundenseparierte Format; keine neue Altformatmigration.
- Quellrevision wird vor und während atomarer Sicherung und danach geprüft; die exakten Archivbytes werden nach dem Schreiben kontrolliert. Ein nachträglicher Konflikt liefert keine erfolgreiche Referenz. Dies ersetzt keine Windows-ACL- oder Medienprüfung.

## Isolierte Rekonstruktion (Quellcode)

`RestoreIsolated` überprüft zuerst das Archiv gegen die unabhängig bestätigte Referenz und öffnet sämtliche Kundenblöcke mit dem separaten Recovery-Zugang. Anschließend schreibt es die exakten verschlüsselten Vault- und öffentlichen Registrierungsbytes in ein neu angelegtes Staging-Verzeichnis innerhalb eines bereits vorhandenen, zugriffsgeschützten Wiederherstellungsarbeitsbereichs. Die Dateien werden zurückgelesen, bytegenau verglichen und erneut vollständig entschlüsselt/validiert. Erst danach wird das Verzeichnis unter einem neuen Namen `recovery-not-activated-…` bereitgestellt. Die Datei `NOT-ACTIVATED.json` dokumentiert Referenz und Prüfstand; sie ist keine signierte Freigabe und kein Aktualitätsnachweis. Kein normaler Vault-Dateiname, keine Registrierung des Vertrauensspeichers, kein Cursor-Reset und kein Serverzugriff. Keine Klartextdateien.

Synthetische Regressionen prüfen Originaldateiverlust, leeren Kunden, mehrere historische Versionen mit exakten Geheimwerten, falschen Zugang, beschädigtes Archiv, getrennte Ausgabeverzeichnisse und unveränderte Betriebsdaten. Historische Sicherungen lassen sich auch nach einer späteren Richtlinienänderung prüfen, ohne den aktuellen Trust zurückzusetzen.

Die Workspace-ACL, übergeordnete Umleitungen, physische Medien und Verhalten bei Stromausfall sind nicht abgenommen; der gewählte Arbeitsbereich muss vertrauenswürdig sein. Ein unterbrochener Vorgang kann ein verschlüsseltes Staging-Verzeichnis hinterlassen; dieses ist keine erfolgreiche Wiederherstellung. Keine automatische Aktivierung.

Offen: Bedienung in der bestehenden Studio-Verwaltung, gesondert gesicherte Recovery- und Verwaltungsidentitäten, separat verankerter aktueller Sicherungsnachweis, Windows-Abnahme der isolierten Wiederherstellung und kontrollierte spätere Freigabe des Betriebsbestands, Zentralzustand einschließlich Audit/Cursor/Policy-Journal, Konflikte mit neueren Ständen, Windows-/Dateisystem-/Verlusttests und vollständige Abnahme. Mastering/Recovery/Rollback-Sperren bleiben unverändert.

## Wiederanlauf eines Arbeitsplatzes bei intakter Zentrale (Quellcode)

`KeyLibraryAutomaticSync.RestoreMissingWorkstationAsync` nutzt den vorhandenen signierten Download- und atomaren Installationsweg. Voraussetzung sind die unabhängig bereitgestellte aktuelle Gruppenregistrierung, ein darin tatsächlich aktives persönliches Gerät und ein separat bestätigter **exakter aktueller** Central Head. Eine vom gleichen Transport gelieferte Beobachtung ist kein unabhängiger Nachweis. Kein älterer Backup-Stand, keine bestehende lokale Vault-Datei, kein offener Richtlinienauftrag, kein Upload und kein Recovery-Zertifikat als persönlicher Zugang. Abweichende oder während des Downloads geänderte Head-Stände werden zurückgewiesen. Ein erfolgreicher Rückgabewert setzt die exakte installierte Dateirevision und den abgeglichenen Cursor voraus.

Synthetische Regressionen prüfen alten Head, falschen Recovery-Zugang, Head-Wechsel während des Downloads, beschränkte Kundensicht und Nichtüberschreiben eines vorhandenen Vaults. Noch nicht ausgeführt. Die Zentrale kann beim autorisierten Abruf wie bisher Replay-Metadaten schreiben, aber es erfolgt keine Bibliotheks- oder Policy-Änderung.

Der Workflow muss später exklusiv gegen andere Studio-Verwaltungsvorgänge serialisiert und in die bestehende UI eingebunden werden. Unabhängige Head-Bestätigung, Windows-Provisionierung, ACL und Crash-/Neustart-Abnahme bleiben offen. Wenn der Cursor nach Installation nicht gespeichert werden kann, darf der Vorgang keinen Erfolg melden; der vorhandene Vault wird nicht automatisch gelöscht oder überschrieben. Ein exakter zentraler Head ist kein extern signierter Serverbeleg und garantiert nicht, dass sich die Zentrale nach der letzten Prüfung nicht weiterentwickelt. Wiederherstellung des Zentralservers und vollständiger Systemverlust bleiben getrennte Arbeitspakete. Keine Kartenfreigabe.
