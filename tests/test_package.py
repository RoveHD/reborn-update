import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zipfile

spec=importlib.util.spec_from_file_location('package_release',Path(__file__).resolve().parents[1]/'tools/package_release.py')
package=importlib.util.module_from_spec(spec);spec.loader.exec_module(package)

class PackagingTests(unittest.TestCase):
    def test_personal_data_filter(self):
        for name in ['Save Data/Game_3.rxdata','save data/Save_3.rxdata','Game.rxdata',
                     'settings.json','mkxp.json','launcher.exe','Save Folder Shortcut.lnk',
                     'patch/Mods/zzzzzzPBSReviewTest.rb','patch/Mods/zzzzRuntimeSmoke.rb',
                     '.git/config','Audio/../settings.json','screenshots/photo.png']:
            self.assertFalse(package.allowed(name),name)
        for name in ['Game.exe','intl.txt','Data/german.dat','Data/Map001.rxdata',
                     'patch/Mods/Retired Mega Stone Compatibility.rb','Audio/BGM/theme.ogg']:
            self.assertTrue(package.allowed(name),name)

    def test_coherent_zip_and_manifest(self):
        with tempfile.TemporaryDirectory(prefix='reborn-package-test-') as temp:
            root=Path(temp);game=root/'game';previous=root/'before';out=root/'out'
            for rel in ['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
                        'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat',
                        'Save Data/Game.rxdata','settings.json','mkxp.json']:
                p=game/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(rel.encode())
            old=previous/'patch/Mods/retired.rb';old.parent.mkdir(parents=True);old.write_bytes(b'old code')
            launcher=root/'launcher.exe';launcher.write_bytes(b'MZ-fixture')
            package.make_release(game,launcher,out,'7.0.0','Deutsch',previous)
            m=json.loads((out/'update.json').read_text())
            self.assertEqual(m['game']['sha256'],package.sha256(out/package.GAME_ARCHIVE))
            self.assertEqual(m['launcher']['sha256'],package.sha256(launcher))
            self.assertEqual(m['game']['removedFiles'],[{'path':'patch/Mods/retired.rb','sha256':package.sha256(old)}])
            with zipfile.ZipFile(out/package.GAME_ARCHIVE) as z:
                self.assertEqual(len(z.namelist()),9)
                self.assertIn('Data/german.dat',z.namelist())
                self.assertNotIn('settings.json',z.namelist())
            self.assertTrue(json.loads((out/'package-checks.json').read_text())['everyArchivedFileSha256Verified'])

    def test_incomplete_source_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            with self.assertRaises(ValueError):package.make_release(root,root/'launcher.exe',root.parent/'not-created','7.0.0','')

    def test_historical_hashes_and_renamed_mod_conflicts(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); game=root/'game'; a=root/'a'; b=root/'b'
            loader=game/'Scripts/ScriptLoader.rb';loader.parent.mkdir(parents=True)
            loader.write_text('# BEGIN RE MOD NAMES\n"RE - Bag.rb" => "Bag.rb",\n# END RE MOD NAMES',encoding='utf8')
            for folder,content in [(a,b'old one'),(b,b'old two')]:
                path=folder/'patch/Mods/Bag.rb';path.parent.mkdir(parents=True);path.write_bytes(content)
                (folder/'settings.json').write_text('private')
                (folder/'Game.exe').write_bytes(b'still included')
            archive=root/'old.zip'
            with zipfile.ZipFile(archive,'w') as z:
                z.writestr('Reborn/Game.exe',b'old executable')
                z.writestr('Reborn/patch/Mods/Bag.rb',b'old three')
                z.writestr('Reborn/Save Data/Game.rxdata',b'private')
            current={'Game.exe':{},'patch/Mods/RE - Bag.rb':{}}
            rows=package.removed_files(game,current,[a,b,a],[archive])
            self.assertEqual(len(rows),3)
            self.assertEqual(len({row['sha256'] for row in rows}),3)
            self.assertTrue(all(row['path']=='patch/Mods/Bag.rb' and row['replacedBy']=='patch/Mods/RE - Bag.rb' for row in rows))

    def test_legacy_patch_archive_without_game_executable(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); archive=root/'legacy.zip'
            with zipfile.ZipFile(archive,'w') as z:
                z.writestr('Scripts/ScriptLoader.rb',b'old loader')
                z.writestr('patch/Mods/retired.rb',b'old mod')
                z.writestr('Save Data/Game.rxdata',b'private')
            rows=package.removed_files(root,{'Scripts/ScriptLoader.rb':{}},previous_archives=[archive])
            self.assertEqual(len(rows),1)
            self.assertEqual(rows[0]['path'],'patch/Mods/retired.rb')

    def test_reviewed_german_hashes_are_reproducible_without_historical_copies(self):
        entries = list(package.known_retired_files())
        self.assertEqual(len(entries), 3)
        with tempfile.TemporaryDirectory() as temp:
            game = Path(temp)
            replacement = 'patch/Mods/RE - Deutsche-Zusatztexte.rb'
            rows = package.removed_files(game, {replacement: {}})
            self.assertEqual(rows, sorted(entries, key=lambda e: (e['path'].casefold(), e['sha256'])))
            self.assertEqual(package.removed_files(game, {}), [])
            self.assertEqual(package.removed_files(game, {replacement: {}, entries[0]['path']: {}}), [])
        manifest = json.loads((Path(__file__).resolve().parents[1] / 'update.json').read_text(encoding='utf8'))
        self.assertTrue(all(entry in manifest['game']['removedFiles'] for entry in entries))

    def test_registry_duplicates_and_historical_sources_merge(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); game = root / 'game'; old = root / 'old'
            relative = 'patch/Mods/retired.rb'; replacement = 'patch/Mods/RE - Retired.rb'
            source = old / relative; source.parent.mkdir(parents=True); source.write_bytes(b'known old bytes')
            loader = game / 'Scripts/ScriptLoader.rb'; loader.parent.mkdir(parents=True)
            loader.write_text('# BEGIN RE MOD NAMES\n"RE - Retired.rb" => "retired.rb",\n# END RE MOD NAMES', encoding='utf8')
            entry = {'path': relative, 'sha256': package.sha256(source), 'replacedBy': replacement}
            registry = root / 'registry.json'
            registry.write_text(json.dumps([entry, dict(entry, sha256=entry['sha256'].upper())]), encoding='utf8')
            with mock.patch.object(package, 'KNOWN_RETIRED_FILES', registry):
                self.assertEqual(package.removed_files(game, {replacement: {}}, [old]), [entry])
                loader.unlink()
                self.assertEqual(package.removed_files(game, {replacement: {}}, [old]), [entry])

    def test_registry_rejects_unsafe_paths_hashes_and_self_replacement(self):
        valid = {'path': 'patch/Mods/old.rb', 'sha256': 'a' * 64, 'replacedBy': 'patch/Mods/new.rb'}
        invalid = [None, {}, dict(valid, sha256='a' * 63), dict(valid, sha256=None),
                   dict(valid, replacedBy=valid['path'].upper())]
        for key in ['path', 'replacedBy']:
            for value in ['../outside.rb', '/patch/Mods/a.rb', 'C:\\outside.rb',
                          'patch//Mods/a.rb', 'patch/Mods/a.rb.', 'patch/Mods/CON.rb',
                          'patch/Mods/a?.rb', 'patch/Mods/a\x00.rb', 'Save Data/Game_3.rxdata',
                          'settings.json', None]:
                invalid.append(dict(valid, **{key: value}))
        with tempfile.TemporaryDirectory() as temp:
            registry = Path(temp) / 'registry.json'
            with mock.patch.object(package, 'KNOWN_RETIRED_FILES', registry):
                for entry in invalid:
                    with self.subTest(entry=entry):
                        registry.write_text(json.dumps([entry]), encoding='utf8')
                        with self.assertRaises(ValueError):
                            list(package.known_retired_files())
                registry.write_text('{}', encoding='utf8')
                with self.assertRaises(ValueError):
                    list(package.known_retired_files())

if __name__=='__main__':unittest.main()
