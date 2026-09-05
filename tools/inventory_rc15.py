"""Reproducible structural RC15 inventory. This is not behavioral acceptance."""
from pathlib import Path
import argparse, json, re, subprocess, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
ref = 'rc15-source-baseline'
def git(*args):
    return subprocess.check_output(['git', '-C', str(root), *args], encoding='utf-8')
files = git('ls-tree', '-r', '--name-only', ref, '--', 'src').splitlines()
events = {'Click','Checked','Unchecked','SelectionChanged','TextChanged','MouseDoubleClick','SelectedItemChanged','Closing','Loaded','KeyDown'}
controls=[]; hooks=[]; methods=[]
for path in files:
    if '/RFiDGear.Builder.App/' not in path or not path.endswith(('.xaml','.cs')): continue
    source = git('show', f'{ref}:{path}')
    if path.endswith('.xaml'):
        tree=ET.fromstring(source)
        def walk(node, container):
            name=node.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name','')
            if name.endswith('Page') or name.startswith('Page'): container=name
            attrs={key.split('}')[-1]:value for key,value in node.attrib.items()}
            ev={key:value for key,value in attrs.items() if key in events}
            if ev or (name and node.tag.split('}')[-1] in {'TextBox','ComboBox','DataGrid','TreeView','ListBox','ItemsControl','Expander','ToggleButton'}):
                controls.append({'file':path,'container':container,'control':node.tag.split('}')[-1],'name':name,'label':attrs.get('Content',attrs.get('Header','')),'events':ev,'tag':attrs.get('Tag',''),'automationId':attrs.get('AutomationProperties.AutomationId','')})
            for child in node: walk(child,container)
        walk(tree,Path(path).name)
    else:
        for number,line in enumerate(source.splitlines(),1):
            if re.search(r'\.(?:Click|Checked|Unchecked|SelectionChanged|TextChanged)\s*\+=',line):
                hooks.append({'file':path,'line':number,'binding':line.strip()})
            match=re.search(r'\b(?:private|public|internal)\s+(?:static\s+)?(?:async\s+)?(?:void|Task(?:<[^>]+>)?|bool)\s+(\w+(?:_Click|_Changed|_SelectionChanged|_MouseDoubleClick))\s*\(',line)
            if match: methods.append({'file':path,'line':number,'method':match[1]})
cli='src/RFiDGear.Builder.Cli/Program.cs'
commands=re.findall(r'case "([^"]+)"',git('show',f'{ref}:{cli}'))
startup='src/RFiDGear.Builder.App/App.xaml.cs'
switches=sorted(set(re.findall(r'"(--[\w-]+)"',git('show',f'{ref}:{startup}'))))
inventory={'baseline':git('rev-parse',f'{ref}^{{}}').strip(),'scope':'XAML action/input surfaces, conventional C# UI handlers, dynamic event wiring, CLI commands and app startup switches; structural evidence only','controls':controls,'dynamicEventBindings':hooks,'uiHandlers':methods,'cliCommands':commands,'startupSwitches':switches}
dest=root/'docs/design/rc15-inventory.json'
if not args.check:
    dest.write_text(json.dumps(inventory,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    rows=['# RC15: technische Bedieninventur','',f'Baseline: `{inventory["baseline"]}`. Automatisch aus dem RC15-Tag erzeugt.','', 'Diese Liste belegt vorhandene Einstiegspunkte, nicht vollständige Funktionsgleichheit oder Hardwarefreigabe. Fachliche Zielzuordnung: FUNKTIONSPARITAET.md.','', '## XAML-Aktionen und Eingabeflächen','', '| Datei | Bereich | Element | Beschriftung | Ereignis / Ziel |','|---|---|---|---|---|']
    esc=lambda s:str(s).replace('|','/').replace('\n',' ')
    for row in controls:
        rows.append('| '+' | '.join(esc(v) for v in [Path(row['file']).name,row['container'],row['name'] or row['control'],row['label'],', '.join(f'{k}: {v}' for k,v in row['events'].items())+(f" / {row['tag']}" if row['tag'] else '')])+' |')
    rows+=['','## Dynamisch erzeugte Bedienelemente','']
    rows += [f"- `{row['file']}:{row['line']}`: `{row['binding'].replace('`','')}`" for row in hooks]
    rows+=['','## CLI-Befehle','']+[f'- `{cmd}`' for cmd in commands]
    rows+=['','## Startparameter der Anwendung','']+[f'- `{switch}`' for switch in switches]
    (root/'docs/design/RC15-BEDIENINVENTUR.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')
else:
    saved=json.loads(dest.read_text(encoding='utf-8'))
    if saved!=inventory: raise SystemExit('Stored inventory differs from immutable baseline. Review before updating.')
    # A warning gate for the first migration phase. Intentional renames/moves need
    # explicit reviewed mapping rather than claiming that a handler name proves parity.
    missing=[]
    current_cs='\n'.join(p.read_text(encoding='utf-8-sig') for p in (root/'src/RFiDGear.Builder.App').glob('*.cs'))
    for item in methods:
        if not re.search(r'\b'+re.escape(item['method'])+r'\s*\(',current_cs): missing.append(item['method'])
    current_cli=(root/cli).read_text(encoding='utf-8-sig')
    for command in commands:
        if f'case "{command}"' not in current_cli: missing.append('CLI: '+command)
    current_startup=(root/startup).read_text(encoding='utf-8-sig')
    for switch in switches:
        if f'"{switch}"' not in current_startup: missing.append('Startup: '+switch)
    if missing: raise SystemExit('Review unmapped missing entry points: '+', '.join(missing))
print(json.dumps({'xamlSurfaces':len(controls),'dynamicBindings':len(hooks),'uiHandlers':len(methods),'cliCommands':len(commands),'startupSwitches':len(switches),'check':'passed' if args.check else 'generated'}))
