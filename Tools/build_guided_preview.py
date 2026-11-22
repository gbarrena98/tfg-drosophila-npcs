"""Build the PEC3 Windows alpha using the released Mono Player and the current interface and artwork."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BASE_TAG = 'v0.2.0-pec2'
BASE_URL = f'https://github.com/gbarrena98/tfg-drosophila-npcs/releases/download/{BASE_TAG}/Windows.zip'
BASE_SHA = '2fc375af6ced32ffaa69d2a7659de8cb2ec7cb86de3b7b1402897b774aa982c0'


def sha256(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-zip', type=Path, help='Existing copy of the published PEC2 Windows.zip')
    parser.add_argument('--dotnet', default='dotnet', help='Path to dotnet from the .NET 10 SDK')
    parser.add_argument('--output', type=Path, required=True, help='Destination ZIP')
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    app_source = (ROOT / 'Assets/Scripts/Runtime/LabApp.cs').read_text(encoding='utf-8')
    version = re.search(r'public const string AppVersion = "([^"]+)";', app_source).group(1)

    with tempfile.TemporaryDirectory(prefix='npc-preview-') as temp:
        work = Path(temp)
        source = args.base_zip
        if source is None:
            source = work / 'base.zip'
            urllib.request.urlretrieve(BASE_URL, source)
        if sha256(source) != BASE_SHA:
            raise ValueError('The archive does not match the published PEC2 Player.')
        with zipfile.ZipFile(source) as archive:
            archive.extractall(work)
        player = work / 'Windows'
        managed = player / 'DrosophilaNavigationLab_Data' / 'Managed'
        runtime = managed / 'NpcLab.Runtime.dll'
        sdk_version = '10.0.403'
        sdks = subprocess.check_output([args.dotnet, '--list-sdks'], text=True).splitlines()
        sdk_line = next((line for line in sdks if line.startswith(sdk_version + ' [')), None)
        if sdk_line is None:
            raise RuntimeError(f'The reproducible PEC3 build requires .NET SDK {sdk_version}.')
        compiler = Path(sdk_line.split('[', 1)[1].rstrip(']')) / sdk_version / 'Roslyn/bincore/csc.dll'

        core = managed / 'NpcLab.Core.dll'
        base_references = sorted((p for p in managed.glob('*.dll') if p not in (runtime, core)), key=lambda p: p.name)
        for group, target, refs in [('Core', core, base_references), ('Runtime', runtime, base_references + [core])]:
            sources = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / 'Assets/Scripts' / group).glob('*.cs'))
            subprocess.run([args.dotnet, str(compiler), '-nologo', '-noconfig', '-nostdlib+', '-target:library',
                            '-langversion:9', '-optimize+', '-deterministic+', '-warnaserror+', f'-pathmap:{ROOT}=/src',
                            '-out:' + str(target), *('-r:' + str(p) for p in refs), *sources], cwd=ROOT, check=True)
        instructions = (ROOT / 'Docs/LEEME_PLAYER.txt').read_text(encoding='utf-8')
        (player / 'LEEME.txt').write_bytes(instructions.encode('utf-8'))
        assets = player / 'DrosophilaNavigationLab_Data/StreamingAssets'
        assets.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / 'Assets/StreamingAssets/Drosophila.png', assets / 'Drosophila.png')
        notices = (ROOT / 'THIRD_PARTY_NOTICES.md').read_text(encoding='utf-8')
        (player / 'THIRD_PARTY_NOTICES.md').write_bytes(notices.encode('utf-8'))
        info_path = player / 'build_info.json'
        info = json.loads(info_path.read_bytes())
        info.update(appVersion=version, coreVersion=version, coreAssemblySha256=sha256(core),
                    buildMethod='incremental Mono core and runtime assemblies and streaming artwork',
                    illustrationSha256=sha256(assets / 'Drosophila.png'),
                    basePlayerTag=BASE_TAG, basePlayerZipSha256=BASE_SHA, runtimeAssemblySha256=sha256(runtime))
        info_path.write_bytes((json.dumps(info, indent=2, ensure_ascii=True) + '\n').encode('utf-8'))
        packaged = work / 'preview.zip'
        with zipfile.ZipFile(packaged, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
            for item in sorted(player.rglob('*'), key=lambda p: p.relative_to(player).as_posix()):
                if item.is_file():
                    entry = zipfile.ZipInfo('DrosophilaNavigationLab/' + item.relative_to(player).as_posix())
                    entry.compress_type = zipfile.ZIP_DEFLATED
                    entry.create_system = 3
                    entry.external_attr = 0o100644 << 16
                    archive.writestr(entry, item.read_bytes())
        shutil.copyfile(packaged, args.output)
        print(f'{args.output.name}  SHA256 {sha256(args.output)}')
        print('NpcLab.Runtime.dll  SHA256 ' + info['runtimeAssemblySha256'])


if __name__ == '__main__':
    main()
