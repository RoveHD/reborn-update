"""Publish a verified launcher-only draft, then atomically activate its channel."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
REPO = 'RoveHD/reborn-update'


def gh(*arguments, body=None):
    request = None
    try:
        command = ['gh', *arguments]
        if body is not None:
            with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', suffix='.json', delete=False) as handle:
                json.dump(body, handle, ensure_ascii=False)
                request = Path(handle.name)
            command += ['--input', str(request)]
        result = subprocess.run(command, cwd=ROOT, check=True, capture_output=True, encoding='utf-8')
        return json.loads(result.stdout) if result.stdout.strip() else None
    finally:
        if request is not None:
            request.unlink(missing_ok=True)


def run(*arguments):
    subprocess.run(['gh', *arguments], cwd=ROOT, check=True)


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def preserved(manifest):
    return {key: value for key, value in manifest.items() if key not in ('launcher', 'patchNotes')}


def live_channel(ref):
    data = gh('api', f'repos/{REPO}/contents/update.json?ref={ref}')
    return json.loads(base64.b64decode(data['content']).decode('utf-8-sig'))


def main():
    if not __debug__:
        raise RuntimeError('Release verification requires Python assertions; do not use -O')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release-directory', type=Path, required=True)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    release = args.release_directory.resolve()
    manifest = json.loads((release / 'update.json').read_text(encoding='utf-8-sig'))
    version = manifest['launcher']['version']
    parts = version.split('.')
    if len(parts) != 3 or not all(part.isdigit() for part in parts):
        raise ValueError('Invalid launcher version')
    tag = 'v' + version
    checks = json.loads((release / 'checks.json').read_text(encoding='utf-8-sig'))
    assert checks['success'] and checks['launcherVersion'] == version, 'Missing version-bound installer proof'
    assert manifest['schemaVersion'] == 1
    exe = release / 'launcher.exe'
    assert sha(exe) == manifest['launcher']['sha256'] == checks['launcherSha256']
    assert exe.stat().st_size == manifest['launcher']['size']
    assert manifest['launcher']['url'] == f'https://github.com/{REPO}/releases/download/{tag}/launcher.exe'
    setup = release / f'Reborn-Launcher-{version}-Setup.exe'
    assert sha(setup) == checks['setupSha256']
    assets = ['launcher.exe', setup.name, 'update.json', 'ERSTE-SCHRITTE.txt', 'checks.json', 'patchnotes.txt', 'SHA256SUMS.txt']
    expected_sums = {}
    for line in (release / 'SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
        digest, name = line.split('  ', 1)
        assert name in assets and name != 'SHA256SUMS.txt', 'Unexpected checksum path'
        assert name not in expected_sums, 'Duplicate checksum path'
        expected_sums[name] = digest
        assert sha(release / name) == digest, 'Checksum mismatch: ' + name
    assert set(expected_sums) == set(assets) - {'SHA256SUMS.txt'}, 'Incomplete release checksums'
    head = gh('api', f'repos/{REPO}/git/ref/heads/main')['object']['sha']
    baseline = live_channel(head)
    assert preserved(manifest) == preserved(baseline), 'Game package, hotfixes or other channel fields changed'
    assert tuple(map(int, parts)) > tuple(map(int, baseline['launcher']['version'].split('.')))
    if not args.publish:
        print(json.dumps({'validated': tag, 'gameVersionPreserved': manifest['game']['version'], 'publish': False}))
        return

    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, encoding='utf-8').strip()
    gh('api', f'repos/{REPO}/commits/{source}')  # Source commit must already be public.
    releases = gh('api', f'repos/{REPO}/releases?per_page=100')
    draft = next((item for item in releases if item['tag_name'] == tag), None)
    assert draft is None or draft['draft'], 'Never overwrite an already published release'
    if draft is None:
        # Use the creation response: the release listing can briefly lag behind
        # a successful creation and must not determine the new draft's identity.
        draft = gh('api', '--method', 'POST', f'repos/{REPO}/releases', body={
            'tag_name': tag, 'target_commitish': source, 'draft': True,
            'name': f'Launcher {version} – Echte Tiefe und feste Standflächen',
            'body': (release / 'patchnotes.txt').read_text(encoding='utf-8')})
    assert draft['draft'] and draft['tag_name'] == tag
    assert draft['target_commitish'] == source, 'Draft belongs to different sources'
    for name in assets:
        path = release / name
        uploaded = next((item for item in draft['assets'] if item['name'] == name), None)
        digest = 'sha256:' + sha(path)
        if uploaded and uploaded.get('digest') == digest and uploaded['size'] == path.stat().st_size and uploaded['state'] == 'uploaded':
            continue
        run('release', 'upload', tag, str(path), '--repo', REPO, '--clobber')
    uploaded_release = gh('api', f'repos/{REPO}/releases/{draft["id"]}')
    assert uploaded_release['draft']
    for name in assets:
        asset = next(item for item in uploaded_release['assets'] if item['name'] == name)
        assert asset['state'] == 'uploaded' and asset['size'] == (release / name).stat().st_size
        assert asset.get('digest') == 'sha256:' + sha(release / name), 'GitHub digest mismatch: ' + name

    # A concurrent channel publisher must never be silently overwritten.
    head = gh('api', f'repos/{REPO}/git/ref/heads/main')['object']['sha']
    assert live_channel(head) == baseline, 'Update channel changed while uploading'
    parent = gh('api', f'repos/{REPO}/git/commits/{head}')
    tree = gh('api', '--method', 'POST', f'repos/{REPO}/git/trees', body={
        'base_tree': parent['tree']['sha'], 'tree': [
            {'path': name, 'mode': '100644', 'type': 'blob', 'content': (release / name).read_text(encoding='utf-8')}
            for name in ('update.json', 'patchnotes.txt')]})
    commit = gh('api', '--method', 'POST', f'repos/{REPO}/git/commits', body={
        'message': f'Activate verified launcher {version} update channel', 'tree': tree['sha'], 'parents': [head]})
    # The feed changes only after every immutable release asset is publicly available.
    run('release', 'edit', tag, '--repo', REPO, '--draft=false', '--latest')
    gh('api', '--method', 'PATCH', f'repos/{REPO}/git/refs/heads/main', body={'sha': commit['sha'], 'force': False})
    assert gh('api', f'repos/{REPO}/git/ref/heads/main')['object']['sha'] == commit['sha']
    assert live_channel(commit['sha']) == manifest
    report = {'success': True, 'release': f'https://github.com/{REPO}/releases/tag/{tag}', 'channelCommit': commit['sha'],
              'sourceCommit': source, 'assetsVerified': len(assets), 'gameVersionPreserved': manifest['game']['version']}
    (release / 'publish-result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
