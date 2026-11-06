"""Package the Windows Player and write its SHA256 checksum."""
from pathlib import Path
import argparse
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--player-verified', action='store_true', help='Confirm that the actual Player passed Docs/ACEPTACION_UNITY.csv.')
args = parser.parse_args()
build = root / 'Builds/Windows'
required = [build / name for name in ['DrosophilaNavigationLab.exe', 'UnityPlayer.dll', 'build_info.json',
                                    'LEEME.txt', 'LICENCIA.md', 'THIRD_PARTY_NOTICES.md', 'MaimonLab_MIT_LICENSE.txt']]
missing = [p.name for p in required if not p.is_file()]
if missing or not (build / 'DrosophilaNavigationLab_Data').is_dir():
    parser.error('Missing real Unity build: ' + ', '.join(missing + ['check the complete _Data folder']))
if required[0].read_bytes()[:2] != b'MZ':
    parser.error('The executable is not a Windows PE file.')
if json.loads(required[2].read_text())['buildResult'] != 'Succeeded':
    parser.error('The build manifest does not report success.')
if not args.player_verified:
    parser.error('First test the actual Player; then use --player-verified.')
output = root / 'Release/DrosophilaNavigationLab_v0.2.0-pec2_Windows_x64.zip'
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
    for p in sorted(build.rglob('*')):
        if p.is_file():
            archive.write(p, 'DrosophilaNavigationLab/' + p.relative_to(build).as_posix())
digest = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(digest + '  ' + output.name + '\n')
print(output)
print('SHA256', digest)
