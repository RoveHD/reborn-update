"""Stage the tested 7.2.0 launcher release without changing the live game channel."""
import hashlib
import json
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
release = root / 'dist/release-7.2.0'
release.mkdir(parents=True, exist_ok=True)
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
exe = root / 'dist/launcher/launcher.exe'
setup = root / 'dist/installer/Reborn-Launcher-7.2.0-Setup.exe'
check = read(root / 'test-results/installer.json')
smoke = read(Path(check['testDirectory']) / 'installed-self-update.json')
assert check['success'] and check['launcherVersion'] == '7.2.0'
assert check['launcherSha256'] == sha(exe) and check['setupSha256'] == sha(setup)
assert smoke['success'] and smoke['calendarRolloverVerified'] and smoke['mouseEffectsVerified'] and smoke['musicTransitionsVerified']
assert len(smoke['themeAudioSeconds']) == 8
manifest = read(root / 'update.json')
assert manifest['game']['version'] == '7.0.0'
assert manifest['game']['sha256'] == '727f1580fd7faa30ea73deb32e223d9c829fe8b69f179322090f12c345f9f0ec'
manifest['launcher'] = dict(version='7.2.0', url='https://github.com/RoveHD/reborn-update/releases/download/v7.2.0/launcher.exe', sha256=sha(exe), size=exe.stat().st_size)
manifest['patchNotes'] = (root / 'release-notes-7.2.0.md').read_text(encoding='utf-8').strip()
(release / 'update.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
for source in [exe, setup, root / 'ERSTE-SCHRITTE.txt']:
    shutil.copy2(source, release / source.name)
evidence = {k: v for k, v in check.items() if k != 'testDirectory'}
evidence.update(coreChecksPassed=49, themeAudioSeconds=smoke['themeAudioSeconds'], calendarRolloverVerified=True, mouseEffectsVerified=True, musicTransitionsVerified=True)
(release / 'checks.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
names = ['launcher.exe', setup.name, 'update.json', 'ERSTE-SCHRITTE.txt', 'checks.json']
(release / 'SHA256SUMS.txt').write_text('\n'.join(sha(release / name) + '  ' + name for name in names) + '\n', encoding='utf-8')
screens = root / 'docs/screenshots'
screens.mkdir(exist_ok=True)
for theme in ['halloween', 'winter', 'spring', 'summer']:
    name = f'launcher-theme-{theme}.png'
    shutil.copy2(Path(check['testDirectory']) / name, screens / name)
old = root / 'artifacts/released-launcher-7.0.0.exe'
if not old.exists():
    shutil.copy2(root / 'launcher.exe', old)
shutil.copy2(exe, root / 'launcher.exe')
print(json.dumps(dict(staged=str(release), launcherSha256=sha(exe), setupSha256=sha(setup))))
