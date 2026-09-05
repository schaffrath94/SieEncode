"""Generate a synthetic three-report design sample, not a card acceptance report."""
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph, Table, TableStyle
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.parent.mkdir(parents=True, exist_ok=True)
fonts = Path('C:/Windows/Fonts')
pdfmetrics.registerFont(TTFont('SE', str(fonts/'segoeui.ttf')))
pdfmetrics.registerFont(TTFont('SE-Bold', str(fonts/'segoeuib.ttf')))
pdfmetrics.registerFont(TTFont('SE-Mono', str(fonts/'consola.ttf')))
W,H = 595.276,841.89
ink=colors.HexColor('#193047'); muted=colors.HexColor('#52657A')
blue=colors.HexColor('#205DC0'); line=colors.HexColor('#DCE3EC')
soft=colors.HexColor('#EEF3FA'); green=colors.HexColor('#166C51')
amber=colors.HexColor('#945007'); paper=colors.white
c=canvas.Canvas(str(args.output), pagesize=(W,H), pageCompression=1)
c.setTitle('SieEncode - Berichtsentwurf: Soll, Ist und Vergleich')
c.setAuthor('SieEncode')
c.setSubject('Synthetisches Gestaltungsbeispiel. Keine Hardwareabnahme.')

def text(x,y,s,size=10,font='SE',color=ink):
    c.setFillColor(color);c.setFont(font,size);c.drawString(x,y,s)

def para(x,top,width,value,size=10,color=ink,bold=False):
    style=ParagraphStyle('s',fontName='SE-Bold' if bold else 'SE',fontSize=size,leading=size*1.45,textColor=color)
    p=Paragraph(value,style);_,height=p.wrap(width,1000)
    p.drawOn(c,x,top-height)
    return top-height

def header(number,kind,title,subtitle):
    c.setFillColor(blue);c.rect(0,H-12,W,12,fill=1,stroke=0)
    text(36,H-49,'SieEncode',21,'SE-Bold')
    c.setFont('SE-Bold',9);c.setFillColor(blue);c.drawRightString(W-36,H-43,kind.upper())
    c.setFont('SE',8);c.setFillColor(muted);c.drawRightString(W-36,H-59,f'DEMO-2026-001 / {number:02d}')
    text(36,H-97,title,24,'SE-Bold')
    para(36,H-111,W-72,subtitle,10,muted)
    c.setStrokeColor(line);c.line(36,42,W-36,42)
    text(36,27,'ENTWURF / SYNTHETISCHE DATEN / KEINE HARDWAREABNAHME',7,'SE-Bold',muted)
    c.setFont('SE',8);c.drawRightString(W-36,27,f'{number} / 3')

def section(y,title,caption=None):
    c.setFillColor(soft);c.roundRect(36,y-25,W-72,25,4,fill=1,stroke=0)
    text(46,y-17,title,10,'SE-Bold')
    if caption:
        c.setFont('SE',8);c.setFillColor(muted);c.drawRightString(W-46,y-17,caption)
    return y-38

def fields(y,left,right):
    start=y
    for x,items in ((36,left),(305,right)):
        yy=start
        for label,value in items:
            text(x,yy,label,8,'SE',muted)
            yy=para(x,yy-5,245,value,10)-14
        y=min(y,yy)
    return y

def notice(top,title,body,color=blue):
    c.setFillColor(soft);c.roundRect(36,top-69,W-72,69,5,fill=1,stroke=0)
    c.setFillColor(color);c.rect(36,top-69,3,69,fill=1,stroke=0)
    text(49,top-20,title,11,'SE-Bold',color)
    para(49,top-29,W-99,body,9,muted)
    return top-83

def table(top,headers,rows,widths,statuses=None):
    style=ParagraphStyle('cell',fontName='SE',fontSize=9,leading=13,textColor=ink)
    data=[[Paragraph(str(v),style) for v in headers]]+[[Paragraph(str(v),style) for v in row] for row in rows]
    t=Table(data,colWidths=widths,hAlign='LEFT',repeatRows=1)
    cmds=[('BACKGROUND',(0,0),(-1,0),soft),('VALIGN',(0,0),(-1,-1),'TOP'),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('LINEBELOW',(0,0),(-1,-1),.5,line)]
    t.setStyle(TableStyle(cmds));_,height=t.wrap(W-72,H)
    if top-height<60:raise ValueError('Table exceeds page body')
    t.drawOn(c,36,top-height)
    return top-height-17

header(1,'Sollreport','Projektvorgaben','Demo-Projekt: Mitarbeiterausweis / Projektstand 1.0 / Bericht vom 05.09.2026')
y=notice(685,'Geplanter Kartenaufbau','Dieser Bericht beschreibt ausschließlich Projektvorgaben. Eine reale Karte wurde nicht gelesen.')
y=section(y,'01  Projekt und Kartenprofil')
y=fields(y,[('Projekt','Demo - Mitarbeiterausweis'),('Kartenfamilie','MIFARE DESFire'),('Anwendung','A0B001 - Demo-Zutritt')],[('Profil','Synthetisches Demo-Profil 1.0'),('UID / Chipversion / Produktionsdatum','Nicht anwendbar im Sollreport'),('Schlüsselkonzept','AES / 2 Anwendungsschlüssel; keine Schlüsselwerte')])
y=section(y,'02  Dateien und Zugriffsrechte','Anwendung A0B001')
y=table(y,['Datei / Typ','Größe / Kommunikation','Lesen','Schreiben'],[
    ['00 - Kennung<br/>Standarddatei','16 Byte<br/>Verschlüsselt','Schlüssel 1','Schlüssel 0'],
    ['01 - Berechtigung<br/>Standarddatei','32 Byte<br/>Verschlüsselt','Schlüssel 1','Schlüssel 0']], [177,146,100,100])
y=para(36,y,W-72,'Für beide Dateien: Lesen/Schreiben mit Schlüssel 0; Rechte ändern mit Schlüssel 0. Der spätere technische Anhang ergänzt die Rohwerte eindeutig bezeichnet.',9,muted)-18
y=section(y,'03  Beispielnutzdaten','Datei 00 / 16 Byte / synthetisch')
text(44,y-3,'Offset   HEX',8,'SE-Bold',muted)
text(44,y-24,'0000     44 45 4D 4F 2D 30 30 30 31 00 00 00 00 00 00 00',9,'SE-Mono')
text(44,y-45,'ASCII    DEMO-0001.......',10,'SE-Mono')
para(36,y-66,W-72,'Die Datenansicht ist optional. Schlüsselwerte werden grundsätzlich nicht ausgegeben.',9,muted)
c.showPage()

header(2,'Istreport','Transponderanalyse','Demokarte / Leselauf DEMO-READ-001 / Synthetischer Befund vom 05.09.2026, 10:30')
y=notice(685,'Teilweise lesbar','Dateieinstellungen sind vorhanden. Ein geschützter Dateiinhalt und zusätzliche Chipinformationen konnten im Beispiel nicht geprüft werden.',amber)
y=section(y,'01  Karte und Untersuchungsumfang')
y=fields(y,[('Kartenfamilie / Kartenbezug','MIFARE DESFire / Demokarte 01'),('Inventur','1 Anwendung, 2 Dateien erkannt'),('Hardware / Software / Produktion','Nicht geprüft; keine Werte aus diesem Leselauf')],[('Reader','Demo-Reader / keine reale Verbindung'),('PICC-Key-Version / freier Speicher','Nicht geprüft'),('Zuordnung','Projektunabhängige Untersuchung')])
y=section(y,'02  Anwendung und Dateien','A0B001 / synthetisches Beispiel')
y=fields(y,[('Anwendungsschlüssel','2 / AES'),('Key-Versionen','Nicht geprüft')],[('Anwendungseinstellungen','Gelesen im synthetischen Befund'),('Untersuchungsgrenze','Keine schreibenden Operationen')])
y=table(y,['Datei','Einstellungen','Inhalt / Umfang','Status'],[
    ['00 - Kennung','Standard / 16 Byte<br/>Verschlüsselt','16 von 16 Byte<br/>vollständig gelesen','Gelesen'],
    ['01 - Berechtigung','Standard / 16 Byte<br/>Verschlüsselt','Nicht gelesen<br/>Schlüsselzugriff fehlt','Kein Zugriff']], [108,153,162,100])
y=section(y,'03  Gelesene Daten','Datei 00 / Offset 0000')
text(44,y-7,'44 45 4D 4F 2D 30 30 30 31 00 00 00 00 00 00 00',10,'SE-Mono')
text(44,y-29,'DEMO-0001.......',10,'SE-Mono')
para(36,y-48,W-72,'Für Datei 01 werden keine leeren oder erfundenen Daten ausgegeben. Fehlender Zugriff ist kein Beleg für eine leere Datei.',9,muted)
c.showPage()

header(3,'Soll/Ist-Vergleich','Abweichungen erkennen','Projektstand 1.0 gegen Leselauf DEMO-READ-001 / Demokarte 01 / 05.09.2026')
y=notice(685,'Keine vollständige Abnahme','Eine Dateigröße weicht von der Vorgabe ab. Ein erforderlicher Dateiinhalt ist nicht prüfbar.',amber)
y=section(y,'01  Vergleich und Ergebnis')
y=table(y,['Merkmal','Soll','Ist','Ergebnis'],[
    ['Anwendung A0B001','Vorhanden','Vorhanden','Übereinstimmung'],
    ['Datei 00 / Größe','16 Byte','16 Byte','Übereinstimmung'],
    ['Datei 00 / Inhalt','DEMO-0001<br/>plus Nullbytes','Bytegleich<br/>16/16 Byte','Übereinstimmung'],
    ['Datei 01 / Größe','32 Byte','16 Byte','<font color="#945007"><b>Abweichung</b></font>'],
    ['Datei 01 / Inhalt','Vorgabe<br/>vorhanden','Kein Zugriff','<font color="#945007"><b>Nicht prüfbar</b></font>'],
    ['Produktionsdatum','Keine Vorgabe','Nicht geprüft','Nicht anwendbar']], [157,105,114,147])
y=section(y,'02  Nächster Schritt')
y=para(36,y,W-72,'<b>Datei 01 prüfen:</b> Die gelesene Größe passt nicht zum gewählten Projektstand. Zuerst Projektzuordnung und vorgesehenen Kartenaufbau prüfen. Der Bericht löst keine automatische Korrektur oder Formatierung aus.',10)-14
y=para(36,y,W-72,'<b>Inhalt separat nachweisen:</b> Für die geschützte Datei einen berechtigten Zugriff bereitstellen und die Untersuchung wiederholen. Bis dahin bleibt die Abnahme unvollständig.',10)-20
y=section(y,'03  Dokumentation','Optional zuschaltbar')
y=fields(y,[('Notiz','Synthetisches Beispiel für eine unvollständige Prüfung.'),('Datum / Ort','____________________________')],[('Bilder','Nicht ausgewählt; keine leere Bildseite'),('Geprüft durch / Unterschrift','____________________________')])
para(36,y+2,W-72,'Ein Unterschriftsfeld ersetzt weder einen technischen Nachweis noch eine digitale Signatur.',8,muted)
c.save()
print(args.output)
