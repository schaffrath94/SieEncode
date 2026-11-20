"""Synthetic technical report concept, independent of production exporters."""
import argparse
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT

parser = argparse.ArgumentParser()
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.parent.mkdir(parents=True, exist_ok=True)
for name, filename in [('UI','segoeui.ttf'),('Bold','segoeuib.ttf'),('Mono','consola.ttf')]:
    pdfmetrics.registerFont(TTFont(name, 'C:/Windows/Fonts/'+filename))
c = canvas.Canvas(str(args.output), pagesize=(595.28,841.89))
c.setTitle('SieEncode - Technischer Vollbericht - synthetischer Entwurf')
style = ParagraphStyle('text', fontName='UI', fontSize=10, leading=15, alignment=TA_LEFT, textColor=HexColor('#23364A'))
y = 0
def paragraph(text):
    global y
    p=Paragraph(text,style); _,h=p.wrap(499,700)
    if y-h<65: raise ValueError('Page overflow')
    p.drawOn(c,48,y-h); y-=h+14
def section(title):
    global y
    y-=12;c.setFillColor(HexColor('#205DC0'));c.setFont('Bold',13);c.drawString(48,y,title);y-=24
def table(headers, rows, widths):
    global y
    data=[[Paragraph(str(v),style) for v in row] for row in [headers]+rows]
    t=Table(data,colWidths=widths)
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),HexColor('#EAF1FC')),('VALIGN',(0,0),(-1,-1),'TOP'),('BOTTOMPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),9),('LINEBELOW',(0,0),(-1,-1),.4,HexColor('#D6DFE9'))]))
    _,h=t.wrap(499,700)
    if y-h<65: raise ValueError('Table overflow')
    t.drawOn(c,48,y-h);y-=h+12
def page(number,title):
    global y
    if number>1:c.showPage()
    c.setFillColor(HexColor('#205DC0'));c.rect(30,705,4,85,fill=1,stroke=0)
    c.setFont('Bold',11);c.drawString(48,788,'SieEncode / Technischer Vollbericht')
    c.setFillColor(HexColor('#172B40'));c.setFont('Bold',21);c.drawString(48,752,title)
    c.setFillColor(HexColor('#52657B'));c.setFont('UI',9);c.drawString(48,728,'DEMO - synthetische Daten - keine Hardwareabnahme - 06.09.2026')
    c.drawString(48,35,'Bericht DEMO-0001 | Entwurf 2 | Keine Schlüsselwerte enthalten')
    c.drawRightString(547,35,f'{number} / 5');y=690

page(1,'Prüfkontext und Karteninformationen')
paragraph('Projekt: Demo - Mitarbeiterausweis / Version 1.0. Dieser Bericht zeigt den geplanten technischen Umfang. Er wurde unabhängig vom produktiven PDF-Exporter erstellt.')
section('Prüfumfang und Aussagekraft')
table(['Bereich','Stand'],[['Quelle','Synthetisches Projekt und synthetischer Teilbefund'],['Reader / Runtime / Buildhash','Nicht verbunden / nicht ausgeführt / kein Messlauf'],['Gesamtbewertung','Eine Größenabweichung; ein Inhalt nicht prüfbar. Keine Freigabe.'],['Statuskonvention','Gelesen, kein Zugriff, nicht geprüft und nicht unterstützt werden getrennt geführt.']],[155,344])
section('Chip und PICC')
table(['Merkmal','Befund / Quelle'],[['Familie','MIFARE DESFire (Demoannahme)'],['UID, Hardware- und Softwareversion','Nicht erhoben'],['Produktionswoche / Jahr / Seriennummer','Nicht erhoben'],['Gesamtspeicher / freier Speicher','Nicht erhoben'],['PICC-Keysettings / Keyversionen','Nicht erhoben; Unterstützung und Zugriff im echten Lauf ausweisen']],[220,279])

page(2,'Anwendungen und Dateieinstellungen')
section('Anwendung A0B001')
table(['Merkmal','Soll','Ist (Demo)'],[['Schlüsseltyp / Anzahl','AES / 2','AES / 2'],['KeySettings','0F','Nicht geprüft'],['Key 0 / Key 1 Version','Projektvorgabe erforderlich','Nicht erhoben'],['Authentifizierung','Vaultreferenz demo/app/read','Synthetischer Teilzugriff']],[165,167,167])
section('Dateien und alle vier Zugriffsrechte')
table(['Merkmal','Datei 00','Datei 01'],[['Dateityp','Standard','Standard'],['Kommunikation','Enciphered','Enciphered'],['Größe Soll / Ist','16 / 16 Byte','32 / 16 Byte'],['Read / Write','Key 1 / Key 0','Key 1 / Key 0'],['ReadWrite / ChangeAccessRights','F / Key 0','F / Key 0'],['Metadaten / Inhalt','Gelesen / gelesen','Gelesen / kein Zugriff']],[165,167,167])
paragraph('Rechte-Legende: E = freier Zugriff; F = gesperrt; 0 bis D = Schlüsselnummer. Die Rechtewerte sind synthetische Beispiele. Nicht gelesene Einstellungen dürfen nicht aus dem Sollprofil als Istwerte übernommen werden.')
paragraph('Value Files sowie Linear/Cyclic Record Files: Erweiterung geplant. Im späteren Vollbericht sind Typ, Parameter und Auslesestatus auch bei fehlender Unterstützung sichtbar.')

page(3,'Inhalte und Rohdaten')
section('A0B001 / Datei 00 - Istinhalt')
paragraph('16 von 16 Byte gelesen (synthetisch). Offset hexadezimal; nicht druckbare Bytes erscheinen in ASCII als Punkt.')
table(['Offset','HEX','ASCII'],[['0000','44 45 4D 4F 2D 30 30 30<br/>31 00 00 00 00 00 00 00','DEMO-0001.......']],[58,300,141])
section('A0B001 / Datei 01 - Istinhalt')
paragraph('Kein Zugriff. Es werden keine Nullbytes oder leeren Inhalte als Messergebnis ausgegeben. Der Inhaltsvergleich ist nicht prüfbar.')
section('Regel für umfangreiche Daten')
paragraph('Der technische Vollbericht enthält alle für den Export ausgewählten, tatsächlich gelesenen Bytes mit Offset und ASCII. Lange Inhalte laufen über Folgeseiten mit wiederholtem Objektbezug und Spaltenkopf. Keine stille Kürzung auf 256 Byte; ausgelassene oder bewusst ausgeschlossene Bereiche werden ausdrücklich angegeben.')
section('Soll-Datenrezept - Datei 00')
table(['Feld','Position / Format','Beispiel'],[['Kennung','Offset 0 / 9 Byte ASCII','DEMO-0001'],['Füllung','Offset 9 / 7 Byte','00 00 00 00 00 00 00'],['Validierung','Gesamtlänge / Überlappung','16 Byte / keine Überlappung']],[130,220,149])

page(4,'Soll / Ist und Ausführungsplan')
section('Vergleich mit Projektstand 1.0')
table(['Prüfpunkt','Soll / Ist','Ergebnis'],[['Datei 00 Größe','16 / 16 Byte','Übereinstimmung'],['Datei 01 Größe','32 / 16 Byte','ABWEICHUNG'],['Datei 01 Inhalt','Vorgabe / kein Zugriff','NICHT PRÜFBAR'],['Keyversionen','Nicht festgelegt / nicht erhoben','NICHT GEPRÜFT']],[175,175,149])
paragraph('Die strukturelle Abweichung ist ein Befund. Fehlender Inhaltszugriff ist keine bestätigte Inhaltsabweichung. Aus dem Teilbefund folgt keine vollständige Kartenfreigabe.')
section('Geplanter Kodierablauf - nicht ausgeführt')
table(['Schritt','Voraussetzung / Ziel','Kontrolle'],[['01 Authentifizieren','A0B001 / Key 0 / Vaultreferenz','Authentifizierung'],['02 WriteData','Datei 00 / 16 Byte','Status und Readback'],['03 WriteData','Datei 01 / 32 Byte','Status und Readback'],['04 Abschluss','Alle Pflichtschritte erfolgreich','Gesamtergebnis']],[132,220,147])
paragraph('Der Plan ist vom Istbericht getrennt. Geplante Schreibvorgänge erscheinen niemals als bereits erfolgreich ausgeführte Aktionen. Mastering, Erweiterung und Recovery erhalten jeweils einen eigenen konkreten Plan.')

page(5,'Nachweise und Dokumentation')
section('Herkunft und Reproduzierbarkeit')
table(['Nachweis','Wert'],[['Projekt / Profil','Demo - Mitarbeiterausweis / Demo-Grundprofil 1.0'],['Reportquelle','Synthetischer Gestaltungsdatensatz DEMO-0001'],['Build / Runtime / Artefakthashes','Nicht vorhanden: kein realer Anwendungslauf'],['Hardwareabnahme / Bedienlauf','Nicht erfolgt'],['Geheimnisse','Nur Referenzen; keine Schlüsselwerte, Vaults oder Zertifikate']],[190,309])
section('Optionale Dokumentationsfelder')
table(['Feld','Eintrag'],[['Datum / Ort','06.09.2026 / Demo-Testplatz'],['Notizen','Gestaltungsbeispiel für den technischen Bericht.'],['Kartenbilder','Optionaler Anhang; kein Bild in diesem Beispieldatensatz.'],['Prüfer / Unterschrift','________________________________']],[190,309])
section('Noch umzusetzen und abzunehmen')
paragraph('Gemeinsames Berichtsdatenmodell für Soll, Ist und Vergleich; vollständige Seitenteilung großer Inhalte; Exportumfang unabhängig vom UI-Modus; echte Runtime-Datenquellen; Integrationstests und Hardwareprüfung. Dieser Entwurf belegt weder eine neue Runtime-Funktion noch die Funktionsparität einer umgebauten Oberfläche.')
c.save()
print(args.output)
