"""Compile the active intl.txt with the matching Reborn runtime and attest hashes."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

RUNTIME_FILES = ("Game.exe", "Game.ini", "Scripts/PBIntl.rb",
                 "Scripts/ScriptLoader.rb", "Data/Scripts.rxdata")
HOOK_NAME = "zzzzzzzzCompileGermanDat.rb"
STATUS_NAME = "runtime-compile-status.json"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def compile(game: Path, stage: Path, proof: Path) -> dict:
    game = game.resolve()
    stage = stage.resolve()
    if game == stage or game in stage.parents or stage in game.parents:
        raise ValueError("Game source and isolated compiler stage must be separate folders")
    for relative in RUNTIME_FILES:
        source, staged = game / relative, stage / relative
        if not source.is_file() or not staged.is_file() or sha256(source) != sha256(staged):
            raise ValueError("Compiler stage runtime differs from game source: " + relative)

    source_text = game / "intl.txt"
    target_data = game / "Data/german.dat"
    if not source_text.is_file() or not target_data.is_file():
        raise FileNotFoundError("Game source must contain intl.txt and Data/german.dat")
    mods = stage / "patch/Mods"
    mods.mkdir(parents=True, exist_ok=True)
    hook = mods / HOOK_NAME
    previous_hook = hook.read_bytes() if hook.exists() else None
    status_path = stage / STATUS_NAME
    status_path.unlink(missing_ok=True)
    (stage / "intl.dat").unlink(missing_ok=True)
    shutil.copy2(source_text, stage / "intl.txt")

    hook_source = r'''begin
  require 'json'
  require 'digest'
  require 'fileutils'
  source = pbGetText('intl.txt')
  pbCompileText
  # pbCompileText serializes pbGetText's output. Compare a second serialization
  # from this same runtime; this avoids RGSS archive lookup and Marshal encoding
  # edge cases when comparing nested legacy strings directly.
  expected = Marshal.dump(source)
  compiled = File.binread('intl.dat')
  raise 'Compiled bytes differ from the current parsed intl.txt structure' unless compiled == expected
  count_entries = lambda do |value|
    case value
    when Hash then value.length + value.values.inject(0) { |sum, item| sum + count_entries.call(item) }
    when Array then value.inject(0) { |sum, item| sum + count_entries.call(item) }
    else 0
    end
  end
  FileUtils.mv('intl.dat', 'Data/german.dat')
  File.write('runtime-compile-status.json', JSON.generate({
    ok: true,
    compiler: 'Pokemon Reborn bundled runtime pbCompileText',
    entries: count_entries.call(source),
    intlSha256: Digest::SHA256.file('intl.txt').hexdigest,
    germanDatSha256: Digest::SHA256.hexdigest(compiled),
    germanDatBytes: compiled.bytesize
  }))
  Process.exit!(0)
rescue Exception => error
  File.write('runtime-compile-status.json', JSON.generate({
    ok: false, error: error.class.to_s + ': ' + error.message,
    backtrace: error.backtrace&.first(12)
  })) rescue nil
  Process.exit!(23)
end
'''
    try:
        with hook.open("w", encoding="utf-8", newline="\n") as stream:
            stream.write(hook_source)
        try:
            startupinfo = None
            if os.name == "nt":
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = subprocess.SW_HIDE
            process = subprocess.run([str(stage / "Game.exe")], cwd=stage,
                                     timeout=300, check=False,
                                     startupinfo=startupinfo,
                                     creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        except subprocess.TimeoutExpired as error:
            raise RuntimeError("Game runtime did not finish the isolated German compile") from error
        if not status_path.is_file():
            raise RuntimeError("Game runtime exited without a compile status report")
        status = json.loads(status_path.read_text(encoding="utf-8-sig"))
        if process.returncode != 0 or not status.get("ok"):
            raise RuntimeError("German runtime compilation failed: " + json.dumps(status, ensure_ascii=False))
        staged_data = stage / "Data/german.dat"
        staged_hash = sha256(staged_data)
        if status.get("intlSha256") != sha256(source_text) or status.get("germanDatSha256") != staged_hash:
            raise RuntimeError("Runtime compile report hashes do not match staged files")

        temp_output = target_data.with_name("german.dat.codex-tmp")
        shutil.copy2(staged_data, temp_output)
        os.replace(temp_output, target_data)
        report = {
            "success": True,
            "compiler": status["compiler"],
            "entries": status["entries"],
            "intlSha256": sha256(source_text),
            "germanDatSha256": sha256(target_data),
            "germanDatBytes": target_data.stat().st_size,
            "runtimeFiles": {relative: sha256(game / relative) for relative in RUNTIME_FILES},
        }
        proof.parent.mkdir(parents=True, exist_ok=True)
        proof.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return report
    finally:
        if previous_hook is None:
            hook.unlink(missing_ok=True)
        else:
            hook.write_bytes(previous_hook)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--stage", type=Path, required=True,
                        help="Disposable compiler stage with matching official runtime files")
    parser.add_argument("--proof", type=Path, required=True,
                        help="Write the current intl.txt/german.dat hash attestation here")
    args = parser.parse_args()
    print(json.dumps(compile(args.game, args.stage, args.proof), ensure_ascii=False))


if __name__ == "__main__":
    main()
