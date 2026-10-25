"""Prepare the reviewed game hotfix on the verified 7.0.0 baseline; never publish."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import shutil
import package_release as package

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parent
VERSION = '7.3.1'
SOURCE = ROOT / 'artifacts/game-source-7.0.0'
STAGE = ROOT / 'artifacts/game-source-7.3.1-release'
OUT = ROOT / 'dist/release-game-7.3.1-release'
FIXES = ['patch/Mods/Party.rb', 'patch/Mods/RE - Box-Markierungen.rb',
         'patch/Mods/zzLocalGermanVisible.rb', 'patch/Mods/RE - Beutel-Sortierung-und-TM-Infos.rb',
         'intl.txt', 'Data/german.dat', 'patch/Mods/RE - Deutsche-Zusatztexte.rb',
         'patch/Mods/zzzz_RE_BattleSound_099_Compatibility.rb', 'patch/Mods/Battle_Scene.rb']

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def main():
    if STAGE.exists() or OUT.exists():
        raise RuntimeError('Refusing to replace an existing prepared hotfix')
    channel_path = ROOT / 'update.json'
    channel_bytes = channel_path.read_bytes()
    channel = read(channel_path)
    baseline = ROOT / 'dist/release-7.0.0'
    inventory = read(baseline / 'game-files.json')
    previous = baseline / package.GAME_ARCHIVE
    assert channel['game']['version'] == '7.0.0'
    assert package.sha256(previous) == channel['game']['sha256']
    launcher = ROOT / 'dist/release-7.3.0/launcher.exe'
    assert package.sha256(launcher) == channel['launcher']['sha256']
    assert channel['launcher']['version'] == '7.3.0'
    assert 'patch/Mods/Party.rb' in inventory and FIXES[1] not in inventory
    print('Verified published baseline and launcher; creating isolated package source.', flush=True)

    def copy_entry(entry):
        relative, expected = entry
        assert package.allowed(relative)
        source = (SOURCE / relative).resolve()
        target = (STAGE / relative).resolve()
        assert SOURCE.resolve() in source.parents and STAGE.resolve() in target.parents
        assert source.stat().st_size == expected['size'] and package.sha256(source) == expected['sha256'], relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)

    with ThreadPoolExecutor(max_workers=6) as pool:
        list(pool.map(copy_entry, inventory.items()))
    for relative in FIXES:
        if relative.endswith('zzzz_RE_BattleSound_099_Compatibility.rb'):
            tested = ROOT / 'game-hotfixes/7.3.1' / relative
        else:
            tested = WORKSPACE / 'update-0.9.9-20261025/working-game' / relative
        if relative in FIXES[:4] or relative.endswith('RE - Deutsche-Zusatztexte.rb') or relative.endswith('Battle_Scene.rb'):
            active = Path('Z:/Pokemon und  Citra/Pokemon/Reborn-19.5.42_Allgen_0.9.5') / relative
            assert tested.read_bytes() == active.read_bytes(), 'Active/tested copies differ: ' + relative
        shutil.copy2(tested, STAGE / relative)
    print('Building and checking the complete update ZIP (nine reviewed changed paths).', flush=True)
    notes = (ROOT / 'release-notes-game-7.3.1.md').read_text(encoding='utf-8')
    package.make_release(STAGE, launcher, OUT, VERSION, notes, previous_archives=[previous])
    new_inventory = read(OUT / 'game-files.json')
    changed = sorted(path for path, data in new_inventory.items() if inventory.get(path) != data)
    assert changed == sorted(FIXES), changed
    assert set(inventory) <= set(new_inventory)
    manifest = read(OUT / 'update.json')
    manifest['launcher'] = channel['launcher']
    # Preserve all already-reviewed historical migrations, including old patch-only releases.
    removals = {(x['path'].casefold(), x['sha256']): x for x in channel['game']['removedFiles']}
    removals.update({(x['path'].casefold(), x['sha256']): x for x in manifest['game']['removedFiles']})
    manifest['game']['removedFiles'] = sorted(removals.values(), key=lambda x: (x['path'].casefold(), x['sha256']))
    (OUT / 'update.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    report = {
        'success': True, 'gameVersion': VERSION, 'launcherUnchanged': manifest['launcher'] == channel['launcher'],
        'baselineGameVersion': channel['game']['version'], 'onlyChangedPaths': changed,
        'removedCurrentFiles': [], 'published': False, 'liveChannelUnchanged': channel_path.read_bytes() == channel_bytes,
        'files': len(new_inventory), 'archiveSha256': manifest['game']['sha256']
    }
    assert report['liveChannelUnchanged']
    (OUT / 'hotfix-checks.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (OUT / 'SHA256SUMS.txt').write_text('\n'.join(package.sha256(OUT / name) + '  ' + name for name in
        ['launcher.exe', package.GAME_ARCHIVE, 'update.json', 'game-files.json', 'hotfix-checks.json']) + '\n', encoding='utf-8')
    print(json.dumps(report), flush=True)

if __name__ == '__main__':
    main()
