"""Audit only the user's Finished sprites against published AllGen 0.9.9.

Preserve every existing AllGen asset except the explicitly reviewed Rowlet sheet.
Old custom Eevee numbering is mapped to current dex IDs before comparing bytes.
"""
import hashlib
import json
from pathlib import Path
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/elias/OneDrive/Graphics/Characters/Finished')
OUTPUT = ROOT / 'game-hotfixes/7.3.12'
BASE = ROOT / 'dist/release-game-7.3.11/game-files.json'
REMAP = {'1036.png': '1037.png', '1036s.png': '1037s.png',
         '1037.png': '1038.png', '1037s.png': '1038s.png'}
REVIEWED = {
    '722.png': '40ff9ce20e3cbcda66e3961c86a1c2ebe579bdf1b7d79949d7dc73a6dca33991',
    '887s.png': '1f44936016e18de061b5c31d28ffe0962efb8ddf0677cb10fa0a0be61a621a98',
}


def metadata(path):
    data = path.read_bytes()
    return {'sha256': hashlib.sha256(data).hexdigest(), 'size': len(data)}


def main():
    if metadata(BASE)['sha256'] != '42fca7980750e19004032569b51584fb77017b13ca90f6980148d0878c59b780':
        raise ValueError('Unreviewed baseline inventory')
    inventory = json.loads(BASE.read_text('utf-8-sig'))
    audit, payload = [], []
    for source in sorted(SOURCE.iterdir()):
        if not source.is_file() or source.suffix.lower() != '.png':
            raise ValueError('Unexpected Finished entry: ' + source.name)
        name = REMAP.get(source.name, source.name)
        normal = 'Graphics/Characters/Followers/' + name
        overlay = 'patch/' + normal
        target = overlay if overlay in inventory else normal
        entry = metadata(source)
        with Image.open(source) as sprite:
            sprite.load()
            dimensions = list(sprite.size)
            if sprite.width % 4 or sprite.height % 4 or sprite.width < 128 or sprite.height < 128:
                raise ValueError('Unexpected 4x4 follower sheet: ' + source.name)
        identical = inventory.get(target) == entry
        if not identical:
            if REVIEWED.get(source.name) != entry['sha256']:
                raise ValueError('Unreviewed conflict/new asset: ' + source.name)
            # Avoid overriding newer official patch artwork or a four-digit asset.
            if overlay in inventory:
                raise ValueError('New AllGen overlay must be preserved: ' + overlay)
            padded = normal.replace('/' + name, '/' + name.zfill(len(name) + 1))
            if padded in inventory or 'patch/' + padded in inventory:
                raise ValueError('A preferred four-digit asset would shadow this sprite')
            destination = OUTPUT / 'payload' / target
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
            payload.append(dict(path=target, **entry))
        audit.append(dict(source=source.name, target=target, identicalToPublished=identical,
                          dimensions=dimensions, **entry))
    if {Path(x['path']).name for x in payload} != set(REVIEWED):
        raise ValueError('Reviewed additions differ')
    mod = ROOT / 'custom-mods/legendary-music/patch/Mods/zzLegendaryWildMusic.rb'
    mod_target = OUTPUT / 'payload/patch/Mods/zzLegendaryWildMusic.rb'
    mod_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(mod, mod_target)
    payload.append(dict(path='patch/Mods/zzLegendaryWildMusic.rb', **metadata(mod)))
    protected = {p: v for p, v in inventory.items() if p.startswith(('Graphics/', 'patch/Graphics/'))
                 and ('haxorus' in p.lower() or Path(p).stem.startswith('612'))}
    report = dict(success=True, source='Characters/Finished only', examined=len(audit),
                  unchanged=sum(x['identicalToPublished'] for x in audit),
                  changedGraphics=len(payload)-1, remappedCustomEevees=REMAP,
                  protectedHaxorusAssets=protected, files=audit,
                  excluded=['All directories outside Characters/Finished', 'altaria.png outside Finished', '*.krz'],
                  gameStarted=False)
    for name, value in [('approved-files.json', {'files': payload}), ('finished-graphics-audit.json', report)]:
        (OUTPUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k: report[k] for k in ('success', 'examined', 'unchanged', 'changedGraphics')}))


if __name__ == '__main__':
    main()
