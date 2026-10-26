"""Verify the current event catch balance with the bundled Ruby and real battle code."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "custom-mods/legendary-catch"
MOD = SOURCE / "patch/Mods/zzLegendaryCatchDifficulty.rb"
RUNNER = ROOT.parent / "update-0.9.9-20261025/tools/run_ruby.py"
HARNESS = ROOT / "tests/glitter_event_balance.rb"
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
output = ROOT / "test-results/event-catch-balance"
output.mkdir(parents=True, exist_ok=True)
runs = []
for order in ["original", "swm-first", "catch-first", "script-reload"]:
    result = subprocess.run([sys.executable, str(RUNNER), str(HARNESS), order, str(MOD)],
                            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = result.stdout.decode("utf-8", errors="replace")
    (output / (order + ".txt")).write_text(text, encoding="utf-8")
    match = re.search(r"GLITTER_BALL_OK (\d+) checks; TOTAL (\d+)", text)
    if result.returncode or not match:
        raise RuntimeError(order + " failed:\n" + text)
    runs.append(dict(order=order, exit_code=result.returncode, checks=int(match.group(2))))
    print(order + ": " + match.group(0), flush=True)
proof = dict(success=True, sha256=sha(MOD), mod_sha256=sha(MOD),
             harness_sha256=sha(HARNESS), base_harness_sha256=sha(ROOT / "tests/catch_event_balance.rb"),
             fixture_sha256=sha(SOURCE / "fangstufen.json"),
             species=len(json.loads((SOURCE / "fangstufen.json").read_text(encoding="utf-8"))),
             excludedEeveeEvolutions=["ABYSSEON", "DIVINEON"],
             total_checks=sum(run["checks"] for run in runs), runs=runs)
(SOURCE / "PRUEFUNG.json").write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")
print(json.dumps(proof), flush=True)
