"""Create an optional small add/replace/delete delta between verified releases."""
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
    added = set(after) - set(before)
    removed = set(before) - set(after)
    changed = {p for p in set(before) & set(after) if before[p] != after[p]}
    updates = sorted(added | changed)
    operations = sorted(added | changed | removed)
    if not operations or any(not package.allowed(p) for p in operations):
        raise ValueError('No eligible game-file changes')

    # A deletion must already be declared in the target full package and match
    # the exact immediately previous bytes, even when old releases carry other
    # historical hashes for that same retired path.
    declared_removals = new['game'].get('removedFiles', [])
    for path in removed:
        if not any(item.get('path') == path and item.get('sha256', '').lower() == before[path]['sha256'].lower()
                   for item in declared_removals):
            raise ValueError('Target manifest does not authorize the exact removed-file hash: ' + path)
    source = target / package.GAME_ARCHIVE
    if package.sha256(source) != new['game']['sha256']:
        raise ValueError('Target package hash differs')
    name = 'Reborn-Hotfix-{}-to-{}.zip'.format(old['game']['version'], new['game']['version'])
    result = target / name
    files = []
    with zipfile.ZipFile(source) as src, zipfile.ZipFile(result, 'w', zipfile.ZIP_DEFLATED) as dst:
        for path in updates:
            data = src.read(path)
            if hashlib.sha256(data).hexdigest() != after[path]['sha256'] or len(data) != after[path]['size']:
                raise ValueError('Target file mismatch: ' + path)
            dst.writestr(path, data)
        for path in operations:
            if path in removed:
                files.append(dict(path=path, beforeExists=True, delete=True,
                                  beforeSha256=before[path]['sha256'], sha256='', size=0))
            else:
                files.append(dict(path=path, beforeExists=path in before,
                                  beforeSha256=before[path]['sha256'] if path in before else '',
                                  sha256=after[path]['sha256'], size=after[path]['size']))
    with zipfile.ZipFile(result) as check:
        assert check.testzip() is None and check.namelist() == updates
    entry = dict(fromVersion=old['game']['version'], version=new['game']['version'],
                 url=new['game']['url'].rsplit('/', 1)[0] + '/' + name,
                 sha256=package.sha256(result), size=result.stat().st_size, files=files)
    v2 = new.setdefault('gameHotfixesV2', [])
    if any(item.get('fromVersion') == entry['fromVersion'] for item in v2):
        raise ValueError('A V2 delta already exists for this exact baseline version')
    v2.append(entry)
    (target / 'update.json').write_text(json.dumps(new, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    sums = target / 'SHA256SUMS.txt'
    lines = [line for line in sums.read_text(encoding='utf-8').splitlines()
             if not line.endswith('  update.json') and not line.endswith('  ' + name)]
    lines += [package.sha256(target / 'update.json') + '  update.json', entry['sha256'] + '  ' + name]
    sums.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps(entry, indent=2))
    return entry


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--target', type=Path, required=True)
    args = parser.parse_args()
    build(args.baseline, args.target)
