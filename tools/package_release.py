"""Create coherent, hash-checked release assets without publishing anything."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import zipfile

GAME_DIRS = {'animations','audio','data','fonts','gems','graphics','patch','scripts','stdlib'}
GAME_FILES = {'game.exe','game.ini','cacert.pem','discord_game_sdk.dll','libcrypto-3-x64.dll',
              'libssl-3-x64.dll','libtolk.dll','nvdacontrollerclient.dll','x64-msvcrt-ruby310.dll',
              'zlib1.dll','intl.txt','modding.txt','readme.txt','rebornmap.png'}
PRIVATE_DIRS = {'save data','saves','save','saved games','.git','.reborn-updater','node_modules',
                '__pycache__','backups','screenshots','test-results'}
PRIVATE_FILES = {'settings.json','mkxp.json','version.txt','launcher.exe','pokemon reborn launcher.exe',
                 'errorlog.txt','debug.txt','updater.log','thumbs.db','desktop.ini'}
GAME_ARCHIVE = 'Reborn-AllGen-0.9.9.zip'
KNOWN_RETIRED_FILES = Path(__file__).with_name('known-retired-files.json')

def sha256(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()

def allowed(relative):
    parts=relative.replace('\\','/').split('/')
    lower=[p.casefold() for p in parts]
    if any(p in PRIVATE_DIRS or p in {'..','.'} for p in lower):return False
    if lower[-1] in PRIVATE_FILES or lower[-1].endswith(('.lnk','.log','.tmp','.bak','.dmp')):return False
    if re.match(r'^game(?:_\d+)?\.rxdata$',lower[-1]):return False
    if 'runtimesmoke' in lower[-1] or 'pbsreviewtest' in lower[-1] or 'germantexttest' in lower[-1]:return False
    return (len(parts)>1 and lower[0] in GAME_DIRS) or (len(parts)==1 and lower[0] in GAME_FILES)

def files_in(root):
    for directory,dirs,files in os.walk(str(root),followlinks=False):
        base=Path(directory)
        for name in list(dirs):
            p=base/name
            if p.is_symlink() or getattr(p.stat(),'st_file_attributes',0)&0x400:
                raise ValueError('Verknüpfter Ordner im Paket: '+str(p))
        dirs[:]=[d for d in dirs if d.casefold() not in PRIVATE_DIRS]
        for name in files:
            p=base/name;rel=p.relative_to(root).as_posix()
            if not allowed(rel):continue
            if p.is_symlink() or getattr(p.stat(),'st_file_attributes',0)&0x400:
                raise ValueError('Verknüpfte Datei im Paket: '+rel)
            yield rel,p

def known_retired_files():
    """Reviewed exact bytes, independent of which historical copies remain on disk."""
    entries = json.loads(KNOWN_RETIRED_FILES.read_text(encoding='utf-8'))
    if not isinstance(entries, list):
        raise ValueError('Ungültiges Register historischer Dateien')
    def relative(value):
        if not isinstance(value, str):
            raise ValueError('Ungültiger historischer Dateipfad')
        path = value.replace('\\', '/')
        for part in path.split('/'):
            if (not part or part in {'.', '..'} or part.endswith(('.', ' ')) or
                    any(ord(c) < 32 or c in '<>:"|?*' for c in part) or
                    re.fullmatch(r'CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]', part.split('.')[0], re.I)):
                raise ValueError('Unsicherer historischer Dateipfad: ' + value)
        if not allowed(path):
            raise ValueError('Geschützter historischer Dateipfad: ' + value)
        return path
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError('Ungültiger historischer Dateieintrag')
        path = relative(entry.get('path'))
        replacement = relative(entry.get('replacedBy'))
        digest = entry.get('sha256')
        if not isinstance(digest, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', digest):
            raise ValueError('Ungültige historische SHA-256-Prüfsumme: ' + path)
        if path.casefold() == replacement.casefold():
            raise ValueError('Historische Datei darf sich nicht selbst ersetzen: ' + path)
        yield {'path': path, 'sha256': digest.lower(), 'replacedBy': replacement}

def removed_files(game, inventory, previous=None, previous_archives=None):
    """Known historical bytes only; never infer deletions from unknown user files."""
    current = {path.casefold() for path in inventory}
    replacements = {}
    loader = game / 'Scripts/ScriptLoader.rb'
    if loader.is_file():
        text = loader.read_text(encoding='utf-8-sig')
        block = re.search(r'# BEGIN RE MOD NAMES(.*?)# END RE MOD NAMES', text, re.S)
        if block:
            for renamed, original in re.findall(r'"([^"/\\]+\.rb)"\s*=>\s*"([^"/\\]+\.rb)"', block.group(1)):
                target = 'patch/Mods/' + renamed
                if target.casefold() in current:
                    replacements[('patch/Mods/' + original).casefold()] = target
    removed = {}
    def add(rel, digest, replacement=None):
        if not allowed(rel) or rel.casefold() in current:
            return
        entry = {'path': rel, 'sha256': digest}
        replacement = replacement or replacements.get(rel.casefold())
        if replacement is not None:
            entry['replacedBy'] = replacement
        existing = removed.get((rel.casefold(), digest))
        if existing is not None and existing.get('replacedBy') is not None and replacement is not None:
            if existing['replacedBy'].casefold() != replacement.casefold():
                raise ValueError('Widersprüchlicher historischer Ersatzpfad: ' + rel)
        removed[(rel.casefold(), digest)] = entry
    directories = [previous] if isinstance(previous, Path) else (previous or [])
    for directory in directories:
        for rel, path in files_in(directory.resolve()):
            if rel.casefold() not in current:
                add(rel, sha256(path))
    for archive in previous_archives or []:
        with zipfile.ZipFile(archive) as z:
            executables = [e.filename for e in z.infolist() if not e.is_dir() and
                           e.filename.replace('\\', '/').split('/')[-1].casefold() == 'game.exe']
            if len(executables) == 1:
                anchor = executables[0].replace('\\', '/')
                prefix = anchor.rsplit('/', 1)[0] + '/' if '/' in anchor else ''
            elif not executables:
                # Historical releases were patches without the engine executable.
                suffix = 'Scripts/ScriptLoader.rb'
                anchors = [e.filename.replace('\\', '/') for e in z.infolist()
                           if e.filename.replace('\\', '/').endswith(suffix)]
                if len(anchors) != 1:
                    raise ValueError('Altes Patchpaket hat keinen eindeutigen Spielordner: ' + str(archive))
                prefix = anchors[0][:-len(suffix)]
            else:
                raise ValueError('Altes Paket hat mehrere Spielordner: ' + str(archive))
            for entry in z.infolist():
                path = entry.filename.replace('\\', '/')
                if entry.is_dir() or not path.startswith(prefix):
                    continue
                rel = path[len(prefix):]
                if not allowed(rel) or rel.casefold() in current:
                    continue
                h = hashlib.sha256()
                with z.open(entry) as stream:
                    for block in iter(lambda: stream.read(1024 * 1024), b''):
                        h.update(block)
                add(rel, h.hexdigest())
    for entry in known_retired_files():
        if entry['replacedBy'].casefold() in current:
            add(entry['path'], entry['sha256'], entry['replacedBy'])
    return sorted(removed.values(), key=lambda entry: (entry['path'].casefold(), entry['sha256']))

def make_release(game,launcher,out,version,notes,previous=None,previous_archives=None):
    if not re.fullmatch(r'\d+\.\d+\.\d+',version):raise ValueError('Version muss x.y.z sein')
    game=game.resolve();out=out.resolve();launcher=launcher.resolve()
    if out==game or game in out.parents:raise ValueError('Ausgabe muss außerhalb des Spiels liegen')
    required=['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
              'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat']
    for rel in required:
        if not (game/rel).is_file():raise ValueError('Unvollständige Spielquelle: '+rel)
    out.mkdir(parents=True,exist_ok=True)
    inventory={}
    case_seen=set()
    archive=out/GAME_ARCHIVE
    pending=out/(GAME_ARCHIVE+'.partial')
    with zipfile.ZipFile(pending,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
        for rel,p in sorted(files_in(game)):
            if rel.casefold() in case_seen:raise ValueError('Doppelter Windows-Pfad: '+rel)
            case_seen.add(rel.casefold())
            digest=sha256(p)
            z.write(p,rel)
            if sha256(p)!=digest:raise ValueError('Quelldatei während Paketbau verändert: '+rel)
            inventory[rel]={'sha256':digest,'size':p.stat().st_size}
    # Check every archived byte against the source inventory, not only ZIP CRC.
    with zipfile.ZipFile(pending) as z:
        for entry in z.infolist():
            h=hashlib.sha256()
            with z.open(entry) as f:
                for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
            if h.hexdigest()!=inventory[entry.filename]['sha256']:raise ValueError(entry.filename)
    pending.replace(archive)
    shutil.copy2(launcher,out/'launcher.exe')
    removed=removed_files(game, inventory, previous, previous_archives)
    base='https://github.com/RoveHD/reborn-update/releases/download/v'+version+'/'
    def asset(path):return {'version':version,'url':base+path.name,'sha256':sha256(path),'size':path.stat().st_size}
    manifest={'schemaVersion':1,'launcher':asset(out/'launcher.exe'),'game':asset(archive),'patchNotes':notes}
    manifest['game']['removedFiles']=sorted(removed,key=lambda x:x['path'])
    for name,data in [('update.json',manifest),('game-files.json',inventory),('package-checks.json',{
        'success':True,'source':str(game),'version':version,'files':len(inventory),
        'uncompressedBytes':sum(x['size'] for x in inventory.values()),'archiveBytes':archive.stat().st_size,
        'everyArchivedFileSha256Verified':True,'personalFilesIncluded':False,'removedFiles':removed})]:
        (out/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (out/'version.txt').write_text(version+'\n',encoding='utf8')
    (out/'patchnotes.txt').write_text(notes+'\n',encoding='utf8')
    (out/'SHA256SUMS.txt').write_text('\n'.join(sha256(out/name)+'  '+name for name in
        ['launcher.exe',GAME_ARCHIVE,'update.json','game-files.json'])+'\n',encoding='utf8')
    print(json.dumps({'success':True,'files':len(inventory),'archiveBytes':archive.stat().st_size,'removedFiles':len(removed)}))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game',type=Path,required=True)
    parser.add_argument('--launcher',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--version',default='7.0.0')
    parser.add_argument('--notes',type=Path,required=True)
    parser.add_argument('--previous-game',type=Path,action='append',help='Repeat for known historical game copies')
    parser.add_argument('--previous-archive',type=Path,action='append',help='Verified historical release ZIP; only hashes are read')
    args=parser.parse_args()
    make_release(args.game,args.launcher,args.output,args.version,args.notes.read_text(encoding='utf-8-sig'),args.previous_game,args.previous_archive)
