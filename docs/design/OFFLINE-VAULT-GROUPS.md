# Offline-Vaults mit zentral verwalteten Gruppen

Status: Entwicklung auf codex/offline-vault-groups, nicht Teil von 1.2.1.
Keine produktiven Vaults laut Benutzer. Testdateien und Identitaeten bleiben erhalten.

## Ziel und Vertrauensgrenzen

Personen koennen in mehreren Gruppen sein (Lokal, Lokal 2, Lokal 3) und mehrere
Geraeteidentitaeten besitzen. Projekte werden explizit einer Gruppe zugeordnet.
Administratoren verwalten Personen und Gruppen; delegierte Verantwortliche nur
ihre zugewiesenen Gruppen. Normale Nutzer entschluesseln offline.

Die gemeinsame Dateiablage transportiert Daten, ist aber keine Vertrauenswurzel.
Ein Administrator-Schluessel wird bei der Erstregistrierung ueber einen separat
bestaetigten Fingerabdruck gebunden. Schluessel aus einer geladenen Richtlinie
werden niemals automatisch vertrauenswuerdig. Verwaltungs- und Entschluesselungs-
schluessel bleiben getrennt. Keine gemeinsame private Identitaet je Gruppe.

Signierte Richtlinien binden Bibliotheks-ID, Version, Gruppen und Identitaets-
Fingerabdruecke. Projekt-ID, Gruppenzuordnung und Vaultinhalt brauchen zusaetzlich
authentisierte Bindungen. Eine reine Gruppenliste erzwingt noch keine Vaultrechte.
Mitgliederaenderungen erfordern kontrolliertes Neuverpacken bzw. bei Entzug neue
Inhaltsschluessel. Fuer einen administrativen Abgleich benoetigt der Ausfuehrende
auch den entsprechenden Entschluesselungs-/Recoveryzugang; Signierrecht allein
gewaehrt keinen Zugriff auf Schluessel.

Offline sind vorhandene entschluesselbare Kopien nicht rueckrufbar. Berechtigte
Nutzer koennen gelesene Schluessel weitergeben. Ein manipuliertes Programm oder
ein lokaler Administrator liegt ausserhalb der Durchsetzung durch die normale UI.
Ein lokaler Hoechststand erkennt bekannte Rueckspruenge, aber keinen global
neuesten Stand, keine eingefrorene Ablage und kein Zuruecksetzen des ganzen PCs.
Diese Grenzen werden angezeigt statt als sofortige Sperrung ausgegeben.

## Implementierungsfolge

1. Signierte, streng validierte Gruppenrichtlinie mit expliziter Vertrauenswurzel,
   Bibliotheksbindung, Mindestversion und Konflikterkennung gleicher Version.
2. Sichere Erstregistrierung und persistenter lokaler Vertrauens-/Versionsstand;
   Administratorwechsel und signierte, gruppenbegrenzte Delegationen.
3. Personen-/Geraeteverzeichnis, Gruppenverwaltung und nachvollziehbare Aenderungen.
4. Authentisierte Projektzuordnung, neue Vault-Anbindung und kontrollierter
   Empfaengerabgleich. Separater zertifikatsbasierter Recoveryzugang.
5. Integration in Arbeitsplatz-Einrichtung, Offline-Status und Abnahmematrix.

## Erster implementierter Baustein

OfflineGroupPolicyService signiert die exakten JSON-Nutzbytes mittels RSA-PSS/
SHA-256 (RSA mindestens 3072 Bit). Der Empfaenger liefert den extern bestaetigten
oeffentlichen Verwaltungsschluessel; keine Schluesselannahme aus der Nachricht.
Unbekannte Felder, doppelte JSON-Felder, doppelte Gruppen/Mitglieder, falsche
Bibliothek, bekannte alte Version und abweichender Inhalt derselben Version
werden abgewiesen. Eingabegroesse ist begrenzt. Gruppen duerfen sich ueberlappen.

Noch keine App-Anbindung, Delegation, Persistenz oder Aenderung bestehender Vaults.
Der Baustein ist keine fertige Zugriffskontrolle und kein neues Vaultformat.
