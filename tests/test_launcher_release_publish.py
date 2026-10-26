import base64
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('publisher', Path(__file__).parents[1] / 'tools/publish_launcher_release.py')
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class LauncherReleaseSafety(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.release = Path(self.temp.name)
        (self.release / 'launcher.exe').write_bytes(b'isolated launcher fixture')
        setup = self.release / 'Reborn-Launcher-2.0.0-Setup.exe'
        setup.write_bytes(b'isolated installer fixture')
        self.manifest = {'schemaVersion': 1, 'launcher': {
            'version': '2.0.0', 'url': 'https://github.com/RoveHD/reborn-update/releases/download/v2.0.0/launcher.exe',
            'sha256': publisher.sha(self.release / 'launcher.exe'), 'size': (self.release / 'launcher.exe').stat().st_size},
            'game': {'version': '7.3.5', 'sha256': 'a' * 64}, 'gameHotfixes': [{'fromVersion': '7.3.2', 'files': ['keep']}],
            'patchNotes': 'New launcher notes', 'additionalChannelSetting': {'keep': True}}
        self.baseline = copy.deepcopy(self.manifest)
        self.baseline['launcher']['version'] = '1.0.0'
        self.baseline['patchNotes'] = 'Old notes'
        self.checks = {'success': True, 'launcherVersion': '2.0.0', 'launcherSha256': self.manifest['launcher']['sha256'], 'setupSha256': publisher.sha(setup)}

    def stage(self):
        for name, data in [('update.json', self.manifest), ('checks.json', self.checks)]:
            (self.release / name).write_text(json.dumps(data), encoding='utf-8')
        for name in ['ERSTE-SCHRITTE.txt', 'patchnotes.txt']:
            (self.release / name).write_text('release fixture', encoding='utf-8')
        names = ['launcher.exe', 'Reborn-Launcher-2.0.0-Setup.exe', 'update.json', 'checks.json', 'ERSTE-SCHRITTE.txt', 'patchnotes.txt']
        (self.release / 'SHA256SUMS.txt').write_text('\n'.join(publisher.sha(self.release / name) + '  ' + name for name in names), encoding='utf-8')

    def run_publisher(self, publish=False, public_release=False):
        self.stage()
        def api(*args, **kwargs):
            endpoint = args[-1]
            if '/git/ref/heads/main' in endpoint:
                return {'object': {'sha': 'a' * 40}}
            if '/contents/update.json?' in endpoint:
                return {'content': base64.b64encode(json.dumps(self.baseline).encode()).decode()}
            if '/commits/' in endpoint:
                return {'sha': 'b' * 40}
            if '/releases?' in endpoint:
                return [{'tag_name': 'v2.0.0', 'draft': False}] if public_release else []
            raise AssertionError('Unexpected API action: ' + str(args))
        argv = ['publisher', '--release-directory', str(self.release)] + (['--publish'] if publish else [])
        with patch.object(sys, 'argv', argv), patch.object(publisher, 'gh', side_effect=api) as calls, \
                patch.object(publisher, 'run') as mutations, patch.object(publisher.subprocess, 'check_output', return_value='b' * 40), redirect_stdout(io.StringIO()):
            try:
                publisher.main()
            finally:
                mutations.assert_not_called()
                self.assertTrue(all(call.args[0] == 'api' and '--method' not in call.args for call in calls.call_args_list))

    def test_valid_dry_run_never_mutates_github(self):
        self.run_publisher()

    def test_game_change_rejected_before_publish(self):
        self.manifest['game']['version'] = 'unexpected'
        with self.assertRaisesRegex(AssertionError, 'Game package'):
            self.run_publisher(publish=True)

    def test_hotfix_removal_rejected_before_publish(self):
        self.manifest['gameHotfixes'] = []
        with self.assertRaisesRegex(AssertionError, 'Game package'):
            self.run_publisher(publish=True)

    def test_extra_channel_setting_change_rejected(self):
        self.manifest['additionalChannelSetting']['keep'] = False
        with self.assertRaisesRegex(AssertionError, 'Game package'):
            self.run_publisher(publish=True)

    def test_public_release_cannot_be_overwritten(self):
        with self.assertRaisesRegex(AssertionError, 'already published'):
            self.run_publisher(publish=True, public_release=True)

    def test_new_draft_uses_creation_response_without_relisting(self):
        self.stage()
        listing_reads = []
        def api(*args, **kwargs):
            endpoint = args[-1]
            if '/git/ref/heads/main' in endpoint:
                return {'object': {'sha': 'a' * 40}}
            if '/contents/update.json?' in endpoint:
                return {'content': base64.b64encode(json.dumps(self.baseline).encode()).decode()}
            if '/commits/' in endpoint:
                return {'sha': 'b' * 40}
            if '/releases?' in endpoint:
                listing_reads.append(endpoint)
                return []  # A stale listing must not be consulted again.
            if endpoint.endswith('/releases') and '--method' in args:
                self.assertEqual(kwargs['body']['tag_name'], 'v2.0.0')
                self.assertTrue(kwargs['body']['draft'])
                return {'id': 123, 'draft': True, 'tag_name': 'v2.0.0', 'target_commitish': 'b' * 40, 'assets': []}
            raise AssertionError('Unexpected API action: ' + str(args))
        argv = ['publisher', '--release-directory', str(self.release), '--publish']
        with patch.object(sys, 'argv', argv), patch.object(publisher, 'gh', side_effect=api), \
                patch.object(publisher.subprocess, 'check_output', return_value='b' * 40), \
                patch.object(publisher, 'run', side_effect=RuntimeError('upload reached')) as upload:
            with self.assertRaisesRegex(RuntimeError, 'upload reached'):
                publisher.main()
            self.assertEqual(len(listing_reads), 1)
            self.assertEqual(upload.call_args.args[:3], ('release', 'upload', 'v2.0.0'))


if __name__ == '__main__':
    unittest.main()
