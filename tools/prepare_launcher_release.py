"""Stage a tested launcher-only release without activating the live update channel."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--version', required=True)
args = parser.parse_args()
version = args.version
if len(version.split('.')) != 3 or not all(part.isdigit() for part in version.split('.')):
    parser.error('version must be three numeric components')
release = root / f'dist/release-{version}'
release.mkdir(parents=True, exist_ok=True)
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
exe = root / 'dist/launcher/launcher.exe'
setup = root / f'dist/installer/Reborn-Launcher-{version}-Setup.exe'
check = read(root / 'test-results/installer.json')
smoke = read(Path(check['testDirectory']) / 'installed-self-update.json')
assert check['success'] and check['launcherVersion'] == version
assert check['launcherSha256'] == sha(exe) and check['setupSha256'] == sha(setup)
assert smoke['success'] and smoke['calendarRolloverVerified'] and smoke['mouseEffectsVerified'] and smoke['musicTransitionsVerified']
assert len(smoke['themeAudioSeconds']) == 8
assert smoke['eventSoundCodecs'] == 50
assert len(smoke['halloweenAmbient']) == 12
assert len(smoke['propInteractions']) == 8 and len(smoke['themes']) == 16
assert smoke['nonScrollingThemeLayoutsVerified'] is True
assert smoke['balancedSceneLayoutVerified'] is True
assert smoke['lightingModeVerified'] is True
assert len(smoke['maximizedThemes']) == 8
lighting = read(root / 'test-results/lighting-release.json')
assert lighting['success'] and lighting['launcherSha256'] == sha(exe)
assert lighting['palettes'] == 8
assert all(lighting[key] for key in ['continuousColors', 'reversal', 'reducedMotion', 'artworkBlend'])
fireworks = read(root / 'test-results/fireworks-verified/result.json')
assert all(fireworks[key] for key in ['success', 'fullWindow', 'clickThrough', 'settings', 'themeReset', 'compact', 'hiddenStopped'])
props = read(root / 'test-results/prop-input-final/result.json')
assert all(props[key] for key in ['success', 'homeSceneChecks', 'windowPropHitTests', 'batReturnClickable', 'persistentProps', 'directNavigation', 'downloadCancel'])
manifest = read(root / 'update.json')
assert tuple(map(int, version.split('.'))) > tuple(map(int, manifest['launcher']['version'].split('.')))
# A launcher-only release retains the current game package, migrations and
# optional incremental hotfixes. Never pin these to an older game release.
assert manifest['game']['version'] and len(manifest['game']['sha256']) == 64
manifest['launcher'] = dict(version=version, url=f'https://github.com/RoveHD/reborn-update/releases/download/v{version}/launcher.exe', sha256=sha(exe), size=exe.stat().st_size)
manifest['patchNotes'] = (root / f'release-notes-{version}.md').read_text(encoding='utf-8').strip()
(release / 'patchnotes.txt').write_text(manifest['patchNotes'] + '\n', encoding='utf-8')
(release / 'update.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
for source in [exe, setup, root / 'ERSTE-SCHRITTE.txt']:
    shutil.copy2(source, release / source.name)
evidence = {k: v for k, v in check.items() if k != 'testDirectory'}
evidence.update(themeAudioSeconds=smoke['themeAudioSeconds'], calendarRolloverVerified=True, mouseEffectsVerified=True, musicTransitionsVerified=True)
evidence['lightingMorph'] = lighting
evidence['fireworksUi'] = fireworks
evidence['propInputRegression'] = props
evidence['gameVersionUnchanged'] = manifest['game']['version']
evidence['gameSha256Unchanged'] = manifest['game']['sha256']
evidence['gameHotfixesPreserved'] = len(manifest.get('gameHotfixes', []))
for key in ['propInteractions', 'eventSoundCodecs', 'halloweenAmbient', 'compactControlsVisible', 'presentationControlsVerified', 'nonScrollingThemeLayoutsVerified', 'balancedSceneLayoutVerified', 'lightingModeVerified', 'maximizedThemes']:
    if key in smoke:
        evidence[key] = smoke[key]
(release / 'checks.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
names = ['launcher.exe', setup.name, 'update.json', 'ERSTE-SCHRITTE.txt', 'checks.json', 'patchnotes.txt']
(release / 'SHA256SUMS.txt').write_text('\n'.join(sha(release / name) + '  ' + name for name in names) + '\n', encoding='utf-8')
screens = root / 'docs/screenshots'
screens.mkdir(exist_ok=True)
for theme in ['halloween', 'winter', 'spring', 'summer']:
    name = f'launcher-theme-{theme}.png'
    shutil.copy2(Path(check['testDirectory']) / f'launcher-theme-{theme}-night.png', screens / name)
print(json.dumps(dict(staged=str(release), launcherSha256=sha(exe), setupSha256=sha(setup))))
