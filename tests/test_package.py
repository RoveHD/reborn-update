import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
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

if __name__=='__main__':unittest.main()
