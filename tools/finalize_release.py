"""Bind final executable and successful integration checks to the local release."""
import hashlib
import json
from pathlib import Path
import shutil

root=Path(__file__).resolve().parents[1]
release=root/'dist/release-7.0.0'
exe=root/'dist/launcher/launcher.exe'
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
self_test=read(root/'test-results/self-update.json')
installed=read(root/'test-results/migration-6.1.0/release-migration.json')
repaired=read(root/'test-results/migration-6.1.0/release-repair.json')
switched=read(root/'test-results/migration-6.1.0/release-switch.json')
manifest=read(release/'update.json')
archive=Path(manifest['game']['url']).name
assert self_test['success'] and self_test['launcherSha256']==sha(exe)
assert installed['success'] and repaired['success']
assert switched['success'] and switched['restoredFilesVerified']==installed['newFilesVerified']
assert installed['switches']==4 and installed['removedHistoricalFilesVerified']>0
assert repaired['unchangedFileTimestampPreserved']
assert installed['archiveSha256']==repaired['archiveSha256']==sha(release/archive)==manifest['game']['sha256']
assert switched['archiveSha256']==manifest['game']['sha256']
manifest['launcher'].update(sha256=sha(exe),size=exe.stat().st_size)
manifest['patchNotes']=(root/'release-notes-7.0.0.txt').read_text(encoding='utf-8-sig').strip()
shutil.copy2(root/'release-notes-7.0.0.txt',release/'patchnotes.txt')
shutil.copy2(exe,release/'launcher.exe')
shutil.copy2(exe,root/'launcher.exe')
shutil.copy2(root/'ERSTE-SCHRITTE.txt',root/'dist/launcher/ERSTE-SCHRITTE.txt')
shutil.copy2(root/'ERSTE-SCHRITTE.txt',release/'ERSTE-SCHRITTE.txt')
(release/'update.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for src,name in [('test-results/self-update.json','launcher-checks.json'),
                 ('test-results/migration-6.1.0/release-migration.json','release-install.json'),
                 ('test-results/migration-6.1.0/release-migration.json','release-migration.json'),
                 ('test-results/migration-6.1.0/release-repair.json','release-repair.json'),
                 ('test-results/migration-6.1.0/release-switch.json','release-switch.json')]:
    shutil.copy2(root/src,release/name)
(release/'SHA256SUMS.txt').write_text('\n'.join(sha(release/name)+'  '+name for name in
    ['launcher.exe',archive,'update.json','game-files.json'])+'\n',encoding='utf8')
screens=root/'docs/screenshots';screens.mkdir(parents=True,exist_ok=True)
for name in ['launcher-1280x800.png','launcher-minimum.png','launcher-previous.png']:
    shutil.copy2(Path(self_test['testDirectory'])/name,screens/name)
print(json.dumps({'success':True,'launcherSha256':manifest['launcher']['sha256'],'gameFilesVerified':installed['newFilesVerified'],'repairFilesVerified':repaired['installedFilesVerified']}))
