"""Exercise the release mod with the game's bundled Ruby and real catch harness."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
MOD = ROOT / "game-hotfixes/7.3.9/patch/Mods/zzLegendaryCatchDifficulty.rb"
HARNESS = ROOT / "tests/glitter_ball_7_3_9.rb"
BASE_HARNESS = ROOT / "tests/catch_gate_7_3_5.rb"
RUNNER = ROOT.parent / "update-0.9.9-20261025/tools/run_ruby.py"
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
baseline = ROOT / "game-hotfixes/7.3.5/patch/Mods/zzLegendaryCatchDifficulty.rb"
assert MOD.read_bytes().startswith(baseline.read_bytes()), "Published catch rules changed"
runs = []
for order in ["original", "swm-first", "catch-first", "script-reload"]:
    result = subprocess.run([sys.executable, str(RUNNER), str(HARNESS), order, str(MOD)],
                            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    output = result.stdout.decode("utf-8", errors="replace")
    match = re.search(r"GLITTER_BALL_OK (\d+) checks; TOTAL (\d+)", output)
    if result.returncode != 0 or not match:
        raise RuntimeError(order + " failed:\n" + output)
    runs.append(dict(order=order, exit_code=result.returncode, checks=int(match.group(2)), output=output))
    print(order + ": " + match.group(0), flush=True)
proof = dict(success=True, sha256=sha(MOD), mod_sha256=sha(MOD),
             harness_sha256=sha(HARNESS), base_harness_sha256=sha(BASE_HARNESS),
             total_checks=sum(run["checks"] for run in runs), runs=runs,
             original_published_mod_preserved=True, actual_throw_threshold_verified=True)
(ROOT / "game-hotfixes/7.3.9/catch-checks.json").write_text(
    json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("MOD_SHA256 " + proof["sha256"], flush=True)
