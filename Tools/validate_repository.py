"""Static source-project integrity checks. These do not run the Unity Editor."""
from pathlib import Path
import csv
import hashlib
import json
import re

root = Path(__file__).resolve().parents[1]
errors = []
def require(ok, text):
    if not ok:
        errors.append(text)

guid_paths = {}
assets = root / 'Assets'
for p in sorted(assets.rglob('*')):
    if p.suffix == '.meta':
        target = p.with_name(p.name[:-5])
        require(target.exists(), 'Orphan meta: ' + str(p.relative_to(root)))
        continue
    meta = p.with_name(p.name + '.meta')
    require(meta.is_file(), 'Missing meta: ' + str(p.relative_to(root)))
    if meta.exists():
        m = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)
        require(bool(m), 'Invalid GUID: ' + meta.name)
        if m:
            g = m.group(1)
            require(g not in guid_paths, 'Duplicate GUID: ' + g)
            guid_paths[g] = p

scene = root / 'Assets/Scenes/NavigationLab.unity'
require(scene.is_file(), 'Missing entry scene')
if scene.exists():
    for g in re.findall(r'guid: ([a-f0-9]{32})', scene.read_text()):
        require(g in guid_paths, 'Unresolved scene GUID: ' + g)
require('Assets/Scenes/NavigationLab.unity' in (root / 'ProjectSettings/EditorBuildSettings.asset').read_text(), 'Scene missing from build settings')
manifest = json.loads((root / 'Packages/manifest.json').read_text())
require(all('com.unity.modules.' + n in manifest['dependencies'] for n in ['imgui','jsonserialize','physics']), 'Required built-in module missing')
definitions = sorted((root / 'Assets/Resources/Configurations').glob('*.json'))
require(len(definitions) == 90, 'Expected 90 development configurations')
for p in definitions:
    d = json.loads(p.read_text())
    require(d['configurationId'] == p.stem, 'Configuration ID mismatch: ' + p.name)
    require(d['datasetRole'] == 'development-pilot', 'Development role mismatch: ' + p.name)
    require(d['settings']['dt'] == .02 and d['settings']['maxSeconds'] == 60, 'Unexpected temporal configuration')
provenance = json.loads((root / 'Validation/Golden/provenance.json').read_text())
source = root / 'Research/Bloque1/src/reference_model.py'
require(hashlib.sha256(source.read_bytes()).hexdigest() == provenance['model_file_sha256'], 'Python reference hash mismatch')
for name in ['rates_python.csv','angular_python.csv']:
    with (root / 'Validation/Golden' / name).open() as f:
        rows = list(csv.reader(f))
    require(len(rows)-1 == provenance['static_cases' if name.startswith('rates') else 'dynamic_rows'], 'Golden row count mismatch')
for required in ['README.md','.gitignore','.gitattributes','Docs/GITHUB.md','Docs/VALIDACION.md','Docs/ACEPTACION_UNITY.csv','LICENCIA.md','THIRD_PARTY_NOTICES.md']:
    require((root / required).is_file(), 'Missing handover file: ' + required)
if errors:
    for error in errors:
        print('FAIL:', error)
    raise SystemExit(1)
print(f'PASS: {len(guid_paths)} unique asset GUIDs; scene references; 90 configurations; Python provenance; required handover files.')
