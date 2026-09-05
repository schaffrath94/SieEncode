# SieEncode – Grundprofile verwalten

## Fachliches Modell

Die Profilbibliothek darf mehrere Grundprofile enthalten. Ein Projekt bindet
bewusst genau ein Grundprofil und optional beliebig viele konfliktfreie
Kunden-/Zusatzprofile. Mehrere Grundprofile werden niemals gleichzeitig in eine
Karte komponiert.

## Gemeinsamer Speicherort

Unter **Einstellungen → Gemeinsame Profilbibliothek** wird ein übergeordneter
Profilordner festgelegt. Darunter verwaltet SieEncode getrennt:

- `base` – im GUI erstellte, importierte und versioniert bearbeitbare Grundprofile;
- `customer` – bearbeitbare Kunden- und Zusatzprofile;
- `history` – unveränderliche Vorgängerstände bei Versionswechseln.

Der Ordner kann auf einem für die beteiligten Benutzer erreichbaren gemeinsamen
Laufwerk liegen. Gleichzeitige Schreibvorgänge werden über Revisionen und
Dateisperren abgesichert.

## Import

Grundprofile können auf drei Wegen eingepflegt werden:

1. vollständig neu im grafischen Profileditor;
2. als SieEncode-Profil-JSON mit `profile.kind = base`;
3. aus einer vorhandenen `.rfPrj`, die schreibgeschützt analysiert und als
schlüsselwertfreier Profilvorschlag übernommen wird.

SieEncode liefert bewusst kein fachliches Grund- oder Kundenprofil mit. Vor dem
ersten Projekt wird mindestens ein eigenes Grundprofil benötigt.

Vor dem Speichern zeigt SieEncode ID, Name, Version, Application-Anzahl und einen
gekürzten SHA-256-Inhaltsfingerprint. Ungültige Profile, Typverwechslungen und
ID-Kollisionen werden abgewiesen.

## Änderungen und Projektbindung

Ändert sich der fachliche Inhalt eines vorhandenen Grundprofils, ist eine neue,
noch nicht verwendete Version erforderlich. Der Vorgänger wird archiviert.
Bereits gespeicherte Projekte besitzen Version und Inhaltsfingerprint ihrer
gewählten Profile. Sie wechseln daher niemals still auf einen neuen Stand. Eine
Abweichung muss im Projekteditor geprüft und bewusst neu gebunden werden.

## Schlüssel

Grundprofile beschreiben Kartenstruktur, Keytypen, Keyanzahl, KeySettings,
Dateirechte und Kodierrezepte. Produktive Schlüsselwerte gehören nicht in ein
Profil. Sie werden pro Projekt in der Sitzung erzeugt oder aus dem
passphrasengeschützten Vault geladen.
