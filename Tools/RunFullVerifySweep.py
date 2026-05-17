#!/usr/bin/env python3
"""Run available top-level Tools/Verify*.py scripts for this agent."""

from __future__ import annotations

import json
import subprocess
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "AgentLogs" / "VerifySweep_CRAFTING_COST_BALANCER.json"
SCRIPT_TIMEOUT_SECONDS = 360
SCRIPT_ARGS = {
    "VerifyLore.py": ("--check",),
    "VerifyBinaryHygiene.py": ("--report", "Docs/AgentLogs/BinaryHygiene_CRAFTING_COST_BALANCER.json"),
    "VerifyDataInquisition.py": ("--report", "Docs/AgentLogs/Data_Inquisition_CRAFTING_COST_BALANCER.json"),
    "VerifyH8HashCollisions.py": ("--write-json", "Docs/AgentLogs/H8Hash_Collision_CRAFTING_COST_BALANCER.json"),
}


def run_script(script_path: Path) -> dict[str, object]:
    start = time.perf_counter()
    try:
        result = subprocess.run(
            [sys.executable, "-B", str(script_path), *SCRIPT_ARGS.get(script_path.name, ())],
            cwd=ROOT,
            text=True,
            capture_output=True,
            timeout=SCRIPT_TIMEOUT_SECONDS,
        )
        return {
            "script": str(script_path.relative_to(ROOT)).replace("\\", "/"),
            "returncode": result.returncode,
            "seconds": round(time.perf_counter() - start, 3),
            "stdout_tail": result.stdout[-4000:],
            "stderr_tail": result.stderr[-4000:],
        }
    except subprocess.TimeoutExpired as exc:
        stdout = exc.stdout if isinstance(exc.stdout, str) else ""
        stderr = exc.stderr if isinstance(exc.stderr, str) else ""
        return {
            "script": str(script_path.relative_to(ROOT)).replace("\\", "/"),
            "returncode": 124,
            "seconds": round(time.perf_counter() - start, 3),
            "stdout_tail": stdout[-4000:],
            "stderr_tail": (stderr[-4000:] if stderr else "TIMEOUT"),
        }


def main() -> int:
    scripts = sorted((ROOT / "Tools").glob("Verify*.py"))
    results = []
    for script in scripts:
        record = run_script(script)
        print(f"{record['script']}: rc={record['returncode']} seconds={record['seconds']}", flush=True)
        results.append(record)
    failed = [record for record in results if int(record["returncode"]) != 0]
    report = {
        "agent": "CRAFTING_COST_BALANCER",
        "domain": "DATA/ECONOMY",
        "script_count": len(results),
        "failed_count": len(failed),
        "failed_scripts": [record["script"] for record in failed],
        "scripts": results,
        "note": "Top-level Verify*.py files were restored for the crafting/data gates available on disk; concurrently removed untracked cross-domain verifier scripts are outside this agent's write scope.",
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print(f"wrote {REPORT_PATH.relative_to(ROOT)}", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
