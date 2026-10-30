import importlib.util
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import zipfile
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import verify_release_parity

spec=importlib.util.spec_from_file_location('package_release',Path(__file__).resolve().parents[1]/'tools/package_release.py')
package=importlib.util.module_from_spec(spec);spec.loader.exec_module(package)

def write_translation_proof(root, game):
    runtime_files=('Game.exe','Game.ini','Scripts/PBIntl.rb','Scripts/ScriptLoader.rb','Data/Scripts.rxdata')
    for relative in runtime_files:
        path=game/relative
        if not path.is_file():
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(('fixture:'+relative).encode())
    def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
    proof={'success':True,'compiler':'Pokemon Reborn bundled runtime pbCompileText','entries':1,
           'intlSha256':digest(game/'intl.txt'),'germanDatSha256':digest(game/'Data/german.dat'),
           'runtimeFiles':{relative:digest(game/relative) for relative in runtime_files}}
    destination=root/'translation-build/german-dat-build.json'
    destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_text(json.dumps(proof),encoding='utf8')


def write_release_baseline(root, game):
    inventory = verify_release_parity.source_inventory(game)
    baseline_dir = root / 'release-baselines'
    baseline_dir.mkdir(parents=True, exist_ok=True)
    baseline_path = baseline_dir / 'game-files-7.3.23.json'
    raw = (json.dumps(inventory, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    baseline_path.write_bytes(raw)
    ledger = {'schemaVersion': 1, 'version': '7.3.23', 'allGenVersion': '0.9.9',
              'baselineInventory': baseline_path.name,
              'baselineInventorySha256': hashlib.sha256(raw).hexdigest(), 'approvedChanges': []}
    (baseline_dir / 'approvals-7.3.23.json').write_text(json.dumps(ledger), encoding='utf-8')

class PackagingTests(unittest.TestCase):
    def test_fixed_723_baseline_rejects_allgen_095_items_removal_and_new_resource(self):
        project = Path(__file__).resolve().parents[1]
        baseline = json.loads((project / 'release-baselines/game-files-7.3.23.json').read_text(encoding='utf-8'))
        with tempfile.TemporaryDirectory(prefix='reborn-baseline-regression-') as temp:
            root = Path(temp); (root / 'release-baselines').mkdir()
            source_baseline = project / 'release-baselines/game-files-7.3.23.json'
            (root / 'release-baselines/game-files-7.3.23.json').write_bytes(source_baseline.read_bytes())
            ledger = json.loads((project / 'release-baselines/approvals-7.3.23.json').read_text(encoding='utf-8'))
            (root / 'release-baselines/approvals-7.3.23.json').write_text(json.dumps(ledger), encoding='utf-8')

            rollback = dict(baseline)
            allgen = 'patch/Mods/AllGen Reborn.rb'
            rollback[allgen] = dict(rollback[allgen], sha256='0' * 64)
            with self.assertRaisesRegex(ValueError, 'approved .23 baseline'):
                verify_release_parity.verify_release_baseline(rollback, root)

            missing_items = dict(baseline); del missing_items['patch/Data/items.dat']
            with self.assertRaisesRegex(ValueError, 'approved .23 baseline'):
                verify_release_parity.verify_release_baseline(missing_items, root)

            extra_resource = dict(baseline)
            extra_resource['Graphics/unreviewed.png'] = {'sha256': '1' * 64, 'size': 10}
            with self.assertRaisesRegex(ValueError, 'approved .23 baseline'):
                verify_release_parity.verify_release_baseline(extra_resource, root)

    def test_baseline_requires_approval_ledger_and_exact_ledger_hashes(self):
        project = Path(__file__).resolve().parents[1]
        baseline = json.loads((project / 'release-baselines/game-files-7.3.23.json').read_text(encoding='utf-8'))
        with tempfile.TemporaryDirectory(prefix='reborn-baseline-ledger-') as temp:
            root = Path(temp); folder = root / 'release-baselines'; folder.mkdir()
            (folder / 'game-files-7.3.23.json').write_bytes(
                (project / 'release-baselines/game-files-7.3.23.json').read_bytes())
            with self.assertRaises(FileNotFoundError):
                verify_release_parity.verify_release_baseline(baseline, root)
            ledger = json.loads((project / 'release-baselines/approvals-7.3.23.json').read_text(encoding='utf-8'))
            ledger['approvedChanges'] = [{'path': 'patch/Mods/AllGen Reborn.rb',
                'beforeSha256': baseline['patch/Mods/AllGen Reborn.rb']['sha256'],
                'afterSha256': '0' * 64, 'size': baseline['patch/Mods/AllGen Reborn.rb']['size'],
                'reason': 'test'}]
            (folder / 'approvals-7.3.23.json').write_text(json.dumps(ledger), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'approved .23 baseline'):
                verify_release_parity.verify_release_baseline(baseline, root)

    def test_package_build_fails_closed_when_approval_ledger_is_missing(self):
        with tempfile.TemporaryDirectory(prefix='reborn-no-approval-test-') as temp:
            root = Path(temp); game = root / 'game'; out = root / 'out'
            for relative in ['Game.exe', 'Game.ini', 'Data/german.dat', 'intl.txt', 'Scripts/Summary.rb',
                             'patch/Data/moves.dat', 'patch/Data/items.dat', 'patch/Data/abil.dat', 'patch/Data/mons.dat']:
                path = game / relative; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(relative.encode())
            launcher = root / 'launcher.exe'; launcher.write_bytes(b'MZ-fixture')
            write_translation_proof(root, game)
            write_release_baseline(root, game)
            (root / 'release-baselines/approvals-7.3.23.json').unlink()
            with self.assertRaises(FileNotFoundError):
                package.make_release(game, launcher, out, '7.3.24', 'test', project_root=root)
            self.assertFalse(out.exists())

    def test_release_mod_source_must_be_installed_byte_for_byte(self):
        with tempfile.TemporaryDirectory(prefix='reborn-mod-source-test-') as temp:
            root=Path(temp);game=root/'game';project=root/'project'
            source=project/'custom-mods/example/patch/Mods/zzExample.rb'
            target=game/'patch/Mods/zzExample.rb'
            resource=project/'custom-mods/example/patch/Mods/zzExample.png'
            installed_resource=game/'patch/Mods/zzExample.png'
            audio_source=project/'custom-mods/example/Audio/voice.ogg'
            installed_audio=game/'Audio/voice.ogg'
            source.parent.mkdir(parents=True);target.parent.mkdir(parents=True)
            source.write_bytes(b'approved mod')
            target.write_bytes(b'approved mod')
            resource.write_bytes(b'asset')
            installed_resource.write_bytes(b'asset')
            audio_source.parent.mkdir(parents=True)
            audio_source.write_bytes(b'audio asset')
            installed_audio.parent.mkdir(parents=True)
            installed_audio.write_bytes(b'audio asset')
            registry=project/'custom-mods/release-mods.json'
            registry.write_text(json.dumps({'schemaVersion':1,'mods':[
                {'source':'custom-mods/example/patch/Mods/zzExample.rb','disposition':'mirror','runtime':'patch/Mods/zzExample.rb'},
                {'source':'custom-mods/example/patch/Mods/zzExample.png','disposition':'mirror','runtime':'patch/Mods/zzExample.png'},
                {'source':'custom-mods/example/Audio/voice.ogg','disposition':'mirror','runtime':'Audio/voice.ogg'}]}))
            backup=project/'custom-mods/example/backup/old.rb'
            backup.parent.mkdir(parents=True);backup.write_bytes(b'not shipped')
            report=verify_release_parity.verify_project_mod_sources(game,project)
            self.assertEqual(report,{'checked':True,'files':3,'matched':3,'deletedSourcesChecked':0,
                                     'latestHotfix':{'checked':False,'version':None,'files':0}})
            original_cwd=Path.cwd()
            try:
                # The temp folder may be on another Windows volume than the
                # checkout, so resolve relative fixture paths from their own root.
                os.chdir(root)
                self.assertEqual(verify_release_parity.verify_project_mod_sources(
                    Path('game'),Path('project')),report)
            finally:
                os.chdir(original_cwd)
            source.write_bytes(b'new local-only fix')
            with self.assertRaisesRegex(ValueError,'not synced into the game source'):
                verify_release_parity.verify_project_mod_sources(game,project)
            audio_source.write_bytes(b'changed local audio')
            with self.assertRaisesRegex(ValueError,'not synced into the game source'):
                verify_release_parity.verify_project_mod_sources(game,project)
            audio_source.write_bytes(b'audio asset')
            registry.write_text(json.dumps({'schemaVersion':1,'mods':[
                {'source':'custom-mods/example/patch/Mods/zzExample.rb','disposition':'mirror','runtime':'patch/Mods/zzExample.rb'},
                {'source':'custom-mods/example/patch/Mods/zzExample.png','disposition':'mirror','runtime':'patch/Mods/zzExample.png'},
                {'source':'custom-mods/example/Audio/voice.ogg','disposition':'mirror','runtime':'Audio/voice.ogg'},
                {'source':'custom-mods/example/patch/Mods/new.rb','disposition':'mirror','runtime':'patch/Mods/new.rb'}]}))
            with self.assertRaisesRegex(ValueError,'coverage mismatch'):
                verify_release_parity.verify_project_mod_sources(game,project)

    def test_deleted_tracked_mod_cannot_leave_stale_runtime_copy(self):
        with tempfile.TemporaryDirectory(prefix='reborn-deleted-mod-test-') as temp:
            root=Path(temp);game=root/'game';project=root/'project'
            source=project/'custom-mods/example/patch/Mods/zzRetired.rb'
            target=game/'patch/Mods/zzRetired.rb'
            source.parent.mkdir(parents=True);target.parent.mkdir(parents=True)
            source.write_bytes(b'retired mod');target.write_bytes(b'retired mod')
            registry=project/'custom-mods/release-mods.json';registry.parent.mkdir(parents=True,exist_ok=True)
            registry.write_text(json.dumps({'schemaVersion':1,'mods':[
                {'source':'custom-mods/example/patch/Mods/zzRetired.rb','disposition':'mirror','runtime':'patch/Mods/zzRetired.rb'}]}))
            subprocess.run(['git','init',str(project)],check=True,capture_output=True)
            subprocess.run(['git','-C',str(project),'add','custom-mods'],check=True,capture_output=True)
            subprocess.run(['git','-C',str(project),'-c','user.name=Test','-c','user.email=test@example.invalid',
                            'commit','-m','track fixture'],check=True,capture_output=True)
            source.unlink()
            registry.write_text(json.dumps({'schemaVersion':1,'mods':[]}))
            with self.assertRaisesRegex(ValueError,'stale runtime file remains'):
                verify_release_parity.verify_project_mod_sources(game,project)

    def test_merged_mod_source_requires_both_reviewed_hashes(self):
        with tempfile.TemporaryDirectory(prefix='reborn-merged-mod-test-') as temp:
            root=Path(temp);game=root/'game';project=root/'project'
            source=project/'custom-mods/old-translation/patch/Mods/old.rb'
            target=game/'patch/Mods/RE_Deutsch.rb'
            source.parent.mkdir(parents=True);target.parent.mkdir(parents=True)
            source.write_bytes(b'legacy text bridge');target.write_bytes(b'consolidated German runtime')
            import hashlib
            registry=project/'custom-mods/release-mods.json'
            registry.write_text(json.dumps({'schemaVersion':1,'mods':[{
                'source':'custom-mods/old-translation/patch/Mods/old.rb','disposition':'merged',
                'runtime':'patch/Mods/RE_Deutsch.rb','reason':'Reviewed and incorporated in the consolidated localization mod.',
                'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                'runtimeSha256':hashlib.sha256(target.read_bytes()).hexdigest()}]}))
            report=verify_release_parity.verify_project_mod_sources(game,project)
            self.assertEqual(report['matched'],1)
            source.write_bytes(b'new local translation omitted from runtime')
            with self.assertRaisesRegex(ValueError,'reviewed merge fingerprint changed'):
                verify_release_parity.verify_project_mod_sources(game,project)

    def test_personal_data_filter(self):
        for name in ['Save Data/Game_3.rxdata','save data/Save_3.rxdata','Game.rxdata',
                     'settings.json','mkxp.json','launcher.exe','Save Folder Shortcut.lnk',
                     'patch/Mods/zzzzzzPBSReviewTest.rb','patch/Mods/zzzzRuntimeSmoke.rb',
                     '.git/config','Audio/../settings.json','screenshots/photo.png',
                     'patch/Mods/backup/RE_OldMod.rb','Data/backup/german.dat']:
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
            write_translation_proof(root,game)
            write_release_baseline(root,game)
            package.make_release(game,launcher,out,'7.0.0','Deutsch',previous,project_root=root)
            m=json.loads((out/'update.json').read_text())
            self.assertEqual(m['game']['sha256'],package.sha256(out/package.GAME_ARCHIVE))
            self.assertEqual(m['launcher']['sha256'],package.sha256(launcher))
            self.assertEqual(m['game']['removedFiles'],[{'path':'patch/Mods/retired.rb','sha256':package.sha256(old)}])
            with zipfile.ZipFile(out/package.GAME_ARCHIVE) as z:
                self.assertEqual(len(z.namelist()),12)
                self.assertIn('Data/german.dat',z.namelist())
                self.assertNotIn('settings.json',z.namelist())
            self.assertTrue(json.loads((out/'package-checks.json').read_text())['everyArchivedFileSha256Verified'])
            parity=json.loads((out/'source-parity.json').read_text())
            self.assertTrue(parity['success'] and parity['localSourceEqualsInventory'] and parity['archiveEqualsInventory'])
            self.assertTrue((out/'source-game.path').is_file())
            checked=subprocess.run([sys.executable,str(Path(verify_release_parity.__file__)),
                                    '--release-dir',str(out),'--project-root',str(root)],capture_output=True,text=True)
            self.assertEqual(checked.returncode,0,checked.stderr)
            with self.assertRaisesRegex(ValueError,'Local releasable source differs|translation source/build differs'):
                (game/'intl.txt').write_bytes(b'late local translation')
                verify_release_parity.verify(out,game,project_root=root)

    def test_parity_verifies_installed_managed_files_and_ignores_user_extras(self):
        with tempfile.TemporaryDirectory(prefix='reborn-parity-test-') as temp:
            root=Path(temp);game=root/'game';out=root/'out';installed=root/'installed'
            for rel in ['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
                        'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat']:
                source=game/rel;source.parent.mkdir(parents=True,exist_ok=True);source.write_bytes(rel.encode())
            launcher=root/'launcher.exe';launcher.write_bytes(b'MZ-fixture')
            write_translation_proof(root,game)
            write_release_baseline(root,game)
            package.make_release(game,launcher,out,'7.0.1','Deutsch',project_root=root)
            for rel,data in verify_release_parity.source_inventory(game).items():
                target=installed/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes((game/rel).read_bytes())
            (installed/'Save Data').mkdir(parents=True)
            (installed/'Save Data'/'Game.rxdata').write_bytes(b'private save')
            report=verify_release_parity.verify(out,game,installed,project_root=root)
            self.assertTrue(report['installedGameChecked'] and report['unknownInstalledFilesIgnoredByComparison'])
            (installed/'intl.txt').write_bytes(b'wrong installed translation')
            with self.assertRaisesRegex(ValueError,'Installed game differs'):
                verify_release_parity.verify(out,game,installed,root)

    def test_new_local_runtime_file_after_package_blocks_release(self):
        with tempfile.TemporaryDirectory(prefix='reborn-local-drift-test-') as temp:
            root=Path(temp);game=root/'game';out=root/'out'
            for rel in ['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
                        'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat']:
                source=game/rel;source.parent.mkdir(parents=True,exist_ok=True);source.write_bytes(rel.encode())
            launcher=root/'launcher.exe';launcher.write_bytes(b'MZ-fixture')
            write_translation_proof(root,game)
            write_release_baseline(root,game)
            package.make_release(game,launcher,out,'7.0.4','Deutsch',project_root=root)

            # A runtime file created after packaging must be treated as a local-only
            # change and prevent publishing until the archive is rebuilt.
            local_only=game/'patch/Mods/RE_NewApprovedFix.rb'
            local_only.parent.mkdir(parents=True,exist_ok=True)
            local_only.write_bytes(b'approved runtime fix')
            with self.assertRaisesRegex(ValueError,'approved .23 baseline|Local releasable source differs from package inventory'):
                verify_release_parity.verify(out,game,project_root=root)

    def test_github_upload_gate_requires_fresh_source_parity_proof(self):
        with tempfile.TemporaryDirectory(prefix='reborn-ci-parity-test-') as temp:
            root=Path(temp);game=root/'game';out=root/'out'
            for rel in ['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
                        'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat']:
                source=game/rel;source.parent.mkdir(parents=True,exist_ok=True);source.write_bytes(rel.encode())
            launcher=root/'launcher.exe';launcher.write_bytes(b'MZ-fixture')
            write_translation_proof(root,game)
            write_release_baseline(root,game)
            package.make_release(game,launcher,out,'7.0.2','Deutsch',project_root=root)
            report=verify_release_parity.verify_archive_only(out, project_root=root)
            self.assertTrue(report['success'] and report['locallySourceVerified'])
            (game/'intl.txt').write_bytes(b'changed after package')
            # The CI side has no source tree, but rejects any stale/missing proof
            # or bytes that no longer match the proof-bound inventory/archive.
            (out/'source-parity.json').unlink()
            with self.assertRaisesRegex(FileNotFoundError,'source-parity.json'):
                verify_release_parity.verify_archive_only(out, project_root=root)
            with self.assertRaisesRegex(ValueError,'Local releasable source differs|translation source/build differs'):
                verify_release_parity.verify(out,game,project_root=root)

    def test_github_upload_gate_hashes_delta_against_full_target(self):
        with tempfile.TemporaryDirectory(prefix='reborn-delta-parity-test-') as temp:
            root=Path(temp);game=root/'game';out=root/'out'
            for rel in ['Game.exe','Game.ini','Data/german.dat','intl.txt','Scripts/Summary.rb',
                        'patch/Data/moves.dat','patch/Data/items.dat','patch/Data/abil.dat','patch/Data/mons.dat']:
                path=game/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(rel.encode())
            launcher=root/'launcher.exe';launcher.write_bytes(b'MZ-fixture')
            write_translation_proof(root,game)
            write_release_baseline(root,game)
            package.make_release(game,launcher,out,'7.0.3','Deutsch',project_root=root)
            delta_name='Reborn-Hotfix-7.0.2-to-7.0.3.zip';delta_path=out/delta_name
            with zipfile.ZipFile(delta_path,'w',zipfile.ZIP_DEFLATED) as archive:
                archive.writestr('Data/german.dat',(game/'Data/german.dat').read_bytes())
            manifest=json.loads((out/'update.json').read_text(encoding='utf8'))
            manifest['gameHotfixesV2']=[{'fromVersion':'7.0.2','version':'7.0.3',
                'url':'https://example.invalid/'+delta_name,'sha256':package.sha256(delta_path),
                'size':delta_path.stat().st_size,'files':[{'path':'Data/german.dat','beforeExists':True,
                'beforeSha256':'a'*64,'sha256':package.sha256(game/'Data/german.dat'),
                'size':(game/'Data/german.dat').stat().st_size}]}]
            (out/'update.json').write_text(json.dumps(manifest),encoding='utf8')
            report=verify_release_parity.verify_archive_only(out, project_root=root)
            self.assertEqual(report['deltasVerified'],[delta_name])
            delta_path.write_bytes(b'tampered delta')
            with self.assertRaisesRegex(ValueError,'V2 delta archive hash/size differs'):
                verify_release_parity.verify_archive_only(out, project_root=root)

    def test_latest_approved_hotfix_payload_must_match_full_game_source(self):
        with tempfile.TemporaryDirectory(prefix='reborn-hotfix-source-test-') as temp:
            root=Path(temp);game=root/'game';project=root/'project'
            rel='patch/Mods/RE_Example.rb'
            payload=project/'game-hotfixes/7.3.23/payload'/rel
            payload.parent.mkdir(parents=True);payload.write_bytes(b'approved runtime bytes')
            (game/rel).parent.mkdir(parents=True);(game/rel).write_bytes(b'older runtime bytes')
            (project/'game-hotfixes/7.3.23/approved-files.json').write_text(json.dumps({'files':[{
                'path':rel,'sha256':package.sha256(payload),'size':payload.stat().st_size}]}),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'not byte-identical'):
                verify_release_parity.verify_latest_hotfix_payload(game,project)
            (game/rel).write_bytes(payload.read_bytes())
            report=verify_release_parity.verify_latest_hotfix_payload(game,project)
            self.assertEqual(report,{'checked':True,'version':'7.3.23','files':1})

    def test_stale_german_dat_build_proof_blocks_release(self):
        with tempfile.TemporaryDirectory(prefix='reborn-german-proof-test-') as temp:
            root=Path(temp);game=root/'game'
            for relative,content in [('intl.txt',b'[1]\nkey\nvalue\n'),('Data/german.dat',b'compiled'),
                                     ('Game.exe',b'exe'),('Game.ini',b'ini'),
                                     ('Scripts/PBIntl.rb',b'compiler'),('Scripts/ScriptLoader.rb',b'loader'),
                                     ('Data/Scripts.rxdata',b'scripts')]:
                path=game/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(content)
            write_translation_proof(root,game)
            (game/'intl.txt').write_bytes(b'[1]\nkey\nchanged\n')
            with self.assertRaisesRegex(ValueError,'German translation source/build differs'):
                verify_release_parity.verify_translation_build(game,root)

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
        self.assertGreaterEqual(len(entries), 3)
        self.assertEqual(len({(entry['path'], entry['sha256']) for entry in entries}), len(entries))
        self.assertTrue({'patch/Mods/zzLocalGermanText.rb',
                         'patch/Mods/RE - Alte-Megasteine-Kompatibilitaet.rb',
                         'patch/Mods/RE - Begegnungen-Neue-Pokemon.rb'} <= {entry['path'] for entry in entries})
        with tempfile.TemporaryDirectory() as temp:
            game = Path(temp)
            replacement = entries[0]['replacedBy']
            rows = package.removed_files(game, {replacement: {}})
            expected = [entry for entry in entries if entry['replacedBy'].casefold() == replacement.casefold()]
            self.assertEqual(rows, sorted(expected, key=lambda e: (e['path'].casefold(), e['sha256'])))
            self.assertEqual(package.removed_files(game, {entries[0]['path']: {}}), [])
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

    def test_retired_replacement_applies_to_each_historical_hash(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); game = root / 'game'; old = root / 'old'
            relative = 'patch/Mods/zzLocalGermanText.rb'
            replacement = 'patch/Mods/RE_Deutsch.rb'
            source = old / relative
            source.parent.mkdir(parents=True)
            source.write_bytes(b'different known release bytes')
            known = {'path': relative, 'sha256': 'a' * 64, 'replacedBy': replacement}
            with mock.patch.object(package, 'known_retired_files', return_value=iter([known])):
                rows = package.removed_files(game, {replacement: {}}, [old])
            self.assertEqual({r['sha256'] for r in rows}, {'a' * 64, package.sha256(source)})
            self.assertEqual({r.get('replacedBy') for r in rows}, {replacement})

    def test_conflicting_replacements_across_distinct_hashes_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            rows = [{'path': 'patch/Mods/retired.rb', 'sha256': h * 64, 'replacedBy': target}
                    for h, target in [('a', 'patch/Mods/one.rb'), ('b', 'patch/Mods/two.rb')]]
            with mock.patch.object(package, 'known_retired_files', return_value=iter(rows)):
                with self.assertRaisesRegex(ValueError, 'Ersatzpfad'):
                    package.removed_files(Path(temp), {r['replacedBy']: {} for r in rows})

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
