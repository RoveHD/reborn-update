"""Create an optional small replacement-file hotfix between verified releases."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

import package_release as package


def build(baseline, target):
    read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
    old, new = read(baseline / 'update.json'), read(target / 'update.json')
    before, after = read(baseline / 'game-files.json'), read(target / 'game-files.json')
    # The first hotfix protocol replaces existing files only. Other changes use
    # the complete package until a separately tested protocol supports them.
    if set(before) != set(after):
        raise ValueError('Added/deleted files require the complete package')
    changed = sorted(p for p in after if before[p] != after[p])
    if not changed or any(not package.allowed(p) for p in changed):
        raise ValueError('No eligible replacement files')
    source = target / package.GAME_ARCHIVE
    if package.sha256(source) != new['game']['sha256']:
        raise ValueError('Target package hash differs')
    name = 'Reborn-Hotfix-{}-to-{}.zip'.format(old['game']['version'], new['game']['version'])
    result = target / name
    files = []
    with zipfile.ZipFile(source) as src, zipfile.ZipFile(result, 'w', zipfile.ZIP_DEFLATED) as dst:
        for path in changed:
            data = src.read(path)
            if hashlib.sha256(data).hexdigest() != after[path]['sha256'] or len(data) != after[path]['size']:
                raise ValueError('Target file mismatch: ' + path)
            dst.writestr(path, data)
            files.append(dict(path=path, beforeSha256=before[path]['sha256'], **after[path]))
    with zipfile.ZipFile(result) as check:
        assert check.testzip() is None and check.namelist() == changed
    entry = dict(fromVersion=old['game']['version'], version=new['game']['version'],
                 url=new['game']['url'].rsplit('/', 1)[0] + '/' + name,
                 sha256=package.sha256(result), size=result.stat().st_size, files=files)
    new['gameHotfixes'] = [entry]
    (target / 'update.json').write_text(json.dumps(new, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    sums = target / 'SHA256SUMS.txt'
    lines = [line for line in sums.read_text(encoding='utf-8').splitlines()
             if not line.endswith('  update.json') and not line.endswith('  ' + name)]
    lines += [package.sha256(target / 'update.json') + '  update.json', entry['sha256'] + '  ' + name]
    sums.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps(entry, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--target', type=Path, required=True)
    args = parser.parse_args()
    build(args.baseline, args.target)
