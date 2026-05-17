#!/usr/bin/env python3
"""Run the METRIC_PHI_ANALYST Python verifier sweep.

Evidence class is CLI/static only. This tool does not claim Unity import,
Play Mode, profiler, GCMonitor, frame-time, visual, or player-build proof.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_JSON_REPORT = ROOT_DIR / "Docs" / "Reports" / "METRIC_PHI_VERIFY_SWEEP.json"
DEFAULT_MD_REPORT = ROOT_DIR / "Docs" / "Reports" / "METRIC_PHI_VERIFY_SWEEP.md"
OUTPUT_LIMIT = 12_000
RETRY_DELAY_SECONDS = 2.0


@dataclass(frozen=True)
class SweepCommand:
    label: str
    argv: tuple[str, ...]
    required: bool = True
    timeout_seconds: int = 300
    evidence_class: str = "CLI_PYTHON_STATIC_DATA"


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT_DIR).as_posix()
    except ValueError:
        return path.as_posix()


def output_snippet(text: str) -> str:
    if len(text) <= OUTPUT_LIMIT:
        return text
    half = OUTPUT_LIMIT // 2
    return text[:half] + "\n...<output truncated by RunMetricPhiVerifySweep.py>...\n" + text[-half:]


def py_args(python_exe: str, *args: str) -> tuple[str, ...]:
    return (python_exe, "-B", *args)


def command_specs(python_exe: str, xxhash_path: Path | None) -> list[SweepCommand]:
    commands = [
        SweepCommand("VerifyAiNavigationTuning", py_args(python_exe, "Tools/VerifyAiNavigationTuning.py")),
        SweepCommand(
            "VerifyH8HashCollisionsCanonical",
            py_args(
                python_exe,
                "Tools/VerifyH8HashCollisions.py",
                "--write-json",
                "Docs/Reports/H8_Hash_Catalog_Audit.json",
                "--write-report",
                "Docs/Reports/H8_Hash_Catalog_Audit.md",
            ),
        ),
        SweepCommand("VerifyCraftingCosts", py_args(python_exe, "Tools/VerifyCraftingCosts.py")),
        SweepCommand("VerifyCraftingSourceContracts", py_args(python_exe, "Tools/VerifyCraftingSourceContracts.py")),
        SweepCommand("VerifyDaltonGasToxicity", py_args(python_exe, "Tools/VerifyDaltonGasToxicity.py")),
        SweepCommand(
            "VerifyDataInquisition",
            py_args(
                python_exe,
                "Tools/VerifyDataInquisition.py",
                "--report",
                "Docs/Reports/METRIC_PHI_DATA_INQUISITION_SWEEP.json",
            ),
        ),
        SweepCommand(
            "VerifyH8HashCollisions",
            py_args(
                python_exe,
                "Tools/VerifyH8HashCollisions.py",
                "--write-json",
                "Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.json",
                "--write-report",
                "Docs/Reports/H8HashCollision_METRIC_PHI_ANALYST.md",
            ),
        ),
        SweepCommand("VerifyHullStressBudget", py_args(python_exe, "Tools/VerifyHullStressBudget.py")),
        SweepCommand("VerifyLore", py_args(python_exe, "Tools/VerifyLore.py", "--check")),
        SweepCommand("VerifyQuestDagBinaryIndependent", py_args(python_exe, "Tools/VerifyQuestDagBinaryIndependent.py")),
        SweepCommand(
            "VerifyOpticsBaker",
            py_args(
                python_exe,
                "Tools/VerifyOpticsBaker.py",
                "--report",
                "Docs/Reports/METRIC_PHI_OPTICS_VERIFY.json",
            ),
        ),
        SweepCommand("VerifyOreLcgBaker", py_args(python_exe, "Tools/VerifyOreLcgBaker.py")),
        SweepCommand("VerifyOreLcgBinaryIndependent", py_args(python_exe, "Tools/VerifyOreLcgBinaryIndependent.py")),
        SweepCommand("VerifyOrganicEntropy", py_args(python_exe, "Tools/VerifyOrganicEntropy.py")),
        SweepCommand("VerifyPdaTechnicalLogs", py_args(python_exe, "Tools/VerifyPdaTechnicalLogs.py")),
        SweepCommand("VerifyQuestDag", py_args(python_exe, "Tools/VerifyQuestDag.py")),
        SweepCommand("VerifySabineBaker", py_args(python_exe, "Tools/VerifySabineBaker.py")),
        SweepCommand("VerifySnellRefractionLut", py_args(python_exe, "Tools/VerifySnellRefractionLut.py")),
        SweepCommand(
            "VerifyTideBaker",
            py_args(
                python_exe,
                "Tools/VerifyTideBaker.py",
                "--report",
                "Docs/Reports/METRIC_PHI_TIDE_VERIFY.json",
            ),
        ),
        SweepCommand("VerifyTideInquisition", py_args(python_exe, "Tools/VerifyTideInquisition.py"), timeout_seconds=1800),
        SweepCommand("VerifyUpgradeCurveBaker", py_args(python_exe, "Tools/VerifyUpgradeCurveBaker.py")),
        SweepCommand("VerifyVisualLodMatrix", py_args(python_exe, "Tools/VerifyVisualLodMatrix.py")),
        SweepCommand("VerifyVramBudgets", py_args(python_exe, "Tools/VerifyVramBudgets.py")),
        SweepCommand("VerifyVrComfortData", py_args(python_exe, "Tools/VerifyVrComfortData.py")),
        SweepCommand("VerifyNetSyncMerkleProtocol", py_args(python_exe, "Tools/Architecture/VerifyNetSyncMerkleProtocol.py")),
        SweepCommand("VerifyBlueNoiseSpectrum", py_args(python_exe, "Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py")),
        SweepCommand("VerifyTaxonomy", py_args(python_exe, "Tools/Taxonomy/verify_taxonomy.py")),
        SweepCommand("EconomyValidator", py_args(python_exe, "Tools/EconomyValidator.py")),
        SweepCommand("BabelCompiler", py_args(python_exe, "Tools/BabelCompiler.py"), timeout_seconds=600),
        SweepCommand("VerifyBabel", py_args(python_exe, "Tools/VerifyBabel.py", "--hash-audit")),
        SweepCommand("VerifyBabelDictionary", py_args(python_exe, "Tools/VerifyBabelDictionary.py")),
        SweepCommand(
            "VerifyBinaryHygiene",
            py_args(
                python_exe,
                "Tools/VerifyBinaryHygiene.py",
                "--report",
                "Docs/Reports/METRIC_PHI_BINARY_HYGIENE_SWEEP.json",
            ),
        ),
        SweepCommand(
            "CalculateHPhi",
            py_args(
                python_exe,
                "Tools/CalculateHPhi.py",
                "--workers",
                "4",
                "--source-roots",
                "Assets",
                "Packages",
                "Tools",
            ),
            timeout_seconds=1800,
        ),
    ]

    if xxhash_path is None:
        commands.append(
            SweepCommand(
                "VerifyReplayHasherReference",
                py_args(python_exe, "Tools/Security/VerifyReplayHasherReference.py"),
                timeout_seconds=120,
                evidence_class="OFFICIAL_EMBEDDED_VECTOR_REFERENCE",
            )
        )
    else:
        commands.append(
            SweepCommand(
                "VerifyReplayHasherReference",
                py_args(
                    python_exe,
                    "Tools/Security/VerifyReplayHasherReference.py",
                    "--xxhash-path",
                    str(xxhash_path),
                    "--fuzz-count",
                    "256",
                ),
                timeout_seconds=300,
            )
        )

    commands.append(SweepCommand("VerifyMetricPhiDataTruth", py_args(python_exe, "Tools/VerifyMetricPhiDataTruth.py")))
    return commands


def resolve_xxhash_path(raw_path: str) -> Path | None:
    if raw_path:
        return Path(raw_path).resolve()
    env_path = os.environ.get("METRIC_PHI_XXHASH_PATH", "")
    return Path(env_path).resolve() if env_path else None


def xxhash_path_status(candidate: Path | None) -> tuple[Path | None, str]:
    if candidate is None:
        return None, "OFFICIAL_EMBEDDED_VECTOR_REFERENCE"
    if not candidate.is_dir():
        return None, f"REJECTED_NOT_DIRECTORY:{candidate}"
    module_roots = (candidate / "xxhash", candidate / "xxhash.py")
    has_python_entry = any(path.exists() for path in module_roots)
    has_native_entry = any(candidate.glob("xxhash*.pyd")) or any(candidate.glob("xxhash*.so"))
    if not has_python_entry and not has_native_entry:
        return None, f"REJECTED_NO_IMPORTABLE_MODULE:{candidate}"
    return candidate, "EXTERNAL_XXHASH_PATH_ACCEPTED"


def run_command(spec: SweepCommand) -> dict[str, Any]:
    start = time.perf_counter()
    env = os.environ.copy()
    env["PYTHONDONTWRITEBYTECODE"] = "1"
    try:
        completed = subprocess.run(
            spec.argv,
            cwd=ROOT_DIR,
            capture_output=True,
            text=True,
            timeout=spec.timeout_seconds,
            check=False,
            env=env,
        )
        elapsed = time.perf_counter() - start
        return {
            "label": spec.label,
            "argv": list(spec.argv),
            "required": spec.required,
            "evidenceClass": spec.evidence_class,
            "returnCode": completed.returncode,
            "elapsedSeconds": round(elapsed, 3),
            "passed": completed.returncode == 0 or not spec.required,
            "stdout": output_snippet(completed.stdout),
            "stderr": output_snippet(completed.stderr),
        }
    except subprocess.TimeoutExpired as exc:
        elapsed = time.perf_counter() - start
        stdout = exc.stdout if isinstance(exc.stdout, str) else (exc.stdout or b"").decode("utf-8", errors="replace")
        stderr = exc.stderr if isinstance(exc.stderr, str) else (exc.stderr or b"").decode("utf-8", errors="replace")
        return {
            "label": spec.label,
            "argv": list(spec.argv),
            "required": spec.required,
            "evidenceClass": spec.evidence_class,
            "returnCode": "TIMEOUT",
            "elapsedSeconds": round(elapsed, 3),
            "passed": False,
            "stdout": output_snippet(stdout),
            "stderr": output_snippet(stderr),
        }


def retry_required_failures(results: list[dict[str, Any]], specs_by_label: dict[str, SweepCommand]) -> list[str]:
    failed_indexes = [
        index
        for index, row in enumerate(results)
        if row["required"] and not row["passed"] and row["label"] in specs_by_label
    ]
    if not failed_indexes:
        return []
    time.sleep(RETRY_DELAY_SECONDS)
    recovered: list[str] = []
    for index in failed_indexes:
        initial = results[index]
        retry = run_command(specs_by_label[initial["label"]])
        retry["attempt"] = 2
        retry["initialFailure"] = {
            "returnCode": initial["returnCode"],
            "elapsedSeconds": initial["elapsedSeconds"],
            "stdout": initial["stdout"],
            "stderr": initial["stderr"],
        }
        if retry["passed"]:
            retry["transientFailureRecovered"] = True
            results[index] = retry
            recovered.append(retry["label"])
        else:
            initial["retry"] = retry
    return recovered


def atomic_write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = text.encode("utf-8")
    if path.exists() and path.read_bytes() == encoded:
        return
    temp_path = path.with_name(f"{path.name}.{os.getpid()}.tmp")
    temp_path.write_bytes(encoded)
    last_error: PermissionError | None = None
    for _attempt in range(5):
        try:
            temp_path.replace(path)
            return
        except PermissionError as exc:
            last_error = exc
            time.sleep(0.25)
    try:
        path.write_bytes(encoded)
    except PermissionError:
        if last_error is not None:
            raise last_error
        raise
    finally:
        try:
            temp_path.unlink(missing_ok=True)
        except OSError:
            pass


def unlink_with_retry(path: Path) -> bool:
    for _attempt in range(5):
        try:
            path.unlink(missing_ok=True)
            return True
        except PermissionError:
            time.sleep(0.25)
    return not path.exists()


def cleanup_selfcheck_sidecars(json_output_path: Path) -> list[Path]:
    leftovers: list[Path] = []
    for path in json_output_path.parent.glob(f"{json_output_path.stem}.selfcheck.*.json"):
        if not unlink_with_retry(path):
            leftovers.append(path)
    return leftovers


def build_markdown(payload: dict[str, Any]) -> str:
    lines = [
        "# METRIC_PHI Verify Sweep",
        "",
        f"Status: {payload['status']}",
        "Evidence class: CLI_PYTHON_STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.",
        "",
        "| Command | Required | Status | Return | Seconds |",
        "|---|---:|---:|---:|---:|",
    ]
    for result in payload["results"]:
        status = "PASS" if result["passed"] else "FAIL"
        lines.append(
            f"| `{result['label']}` | {result['required']} | {status} | `{result['returnCode']}` | {result['elapsedSeconds']} |"
        )
    lines.extend(["", "## Failed Required Commands", ""])
    failures = [row for row in payload["results"] if row["required"] and not row["passed"]]
    if failures:
        for failure in failures:
            lines.append(f"- `{failure['label']}` returned `{failure['returnCode']}`")
    else:
        lines.append("- none")
    recovered = payload["summary"].get("transientRetryPasses", [])
    if recovered:
        lines.extend(["", "## Transient Retry Passes", ""])
        for label in recovered:
            lines.append(f"- `{label}` failed once and passed on recorded retry")
    return "\n".join(lines) + "\n"


def write_reports(payload: dict[str, Any], json_path: Path, md_path: Path) -> None:
    atomic_write_text(json_path, json.dumps(payload, indent=2, sort_keys=True) + "\n")
    atomic_write_text(md_path, build_markdown(payload))


def make_payload(
    args: argparse.Namespace,
    results: list[dict[str, Any]],
    recovered_labels: list[str],
    xxhash_path: Path | None,
    requested_xxhash_path: Path | None,
    xxhash_path_mode: str,
    self_check_pending: bool,
) -> dict[str, Any]:
    required_failures = [row for row in results if row["required"] and not row["passed"]]
    return {
        "schema": "H8.MetricPhi.VerifySweep.v1",
        "tool": "Tools/RunMetricPhiVerifySweep.py",
        "status": "VERIFY_SWEEP_PASS" if not required_failures else "VERIFY_SWEEP_FAIL",
        "generatedAt": datetime.now().isoformat(timespec="seconds"),
        "python": args.python_exe,
        "xxhashPath": str(xxhash_path) if xxhash_path else None,
        "requestedXxhashPath": str(requested_xxhash_path) if requested_xxhash_path else None,
        "xxhashPathMode": xxhash_path_mode,
        "summary": {
            "totalCommands": len(results),
            "requiredFailures": len(required_failures),
            "failedRequiredLabels": [row["label"] for row in required_failures],
            "transientRetryPasses": recovered_labels,
            "selfCheckPending": self_check_pending,
        },
        "results": results,
        "residualRisk": [
            "CLI Python only; no Unity runtime, profiler, GCMonitor, frame-time, visual, or player-build proof.",
            "Some verifiers mutate report artifacts by design; this sweep records command evidence after mutation.",
            "BabelCompiler runs after mutating data verifiers so source SHA-256 drift is rejected before H-Phi refresh.",
            "CalculateHPhi runs before VerifyMetricPhiDataTruth so stale generated C# source is rejected.",
            "VerifyMetricPhiDataTruth validates a self-check sidecar before final report write.",
        ],
    }


def run(args: argparse.Namespace) -> int:
    requested_xxhash_path = resolve_xxhash_path(args.xxhash_path)
    xxhash_path, xxhash_path_mode = xxhash_path_status(requested_xxhash_path)
    json_output_path = Path(args.json_output).resolve()
    markdown_output_path = Path(args.markdown_output).resolve()
    self_check_input_path = json_output_path.with_name(f"{json_output_path.stem}.selfcheck.{os.getpid()}.json")

    stale_selfchecks = cleanup_selfcheck_sidecars(json_output_path)
    if stale_selfchecks:
        print(
            "METRIC_PHI_VERIFY_SWEEP_STALE_SELFCHECK_CLEANUP_FAILED: "
            + ", ".join(rel(path) for path in stale_selfchecks),
            file=sys.stderr,
        )
        return 3

    specs = command_specs(args.python_exe, xxhash_path)
    adjusted_specs: list[SweepCommand] = []
    for spec in specs:
        if spec.label == "VerifyMetricPhiDataTruth":
            adjusted_specs.append(
                SweepCommand(
                    spec.label,
                    (*spec.argv, "--sweep-input", str(self_check_input_path)),
                    spec.required,
                    spec.timeout_seconds,
                    spec.evidence_class,
                )
            )
        else:
            adjusted_specs.append(spec)
    specs = adjusted_specs
    specs_by_label = {spec.label: spec for spec in specs}
    self_specs = [spec for spec in specs if spec.label == "VerifyMetricPhiDataTruth"]
    non_self_specs = [spec for spec in specs if spec.label != "VerifyMetricPhiDataTruth"]

    results: list[dict[str, Any]] = []
    for index, spec in enumerate(non_self_specs, 1):
        print(f"[{index}/{len(specs)}] {spec.label}")
        results.append(run_command(spec))

    recovered_labels = retry_required_failures(results, specs_by_label)
    preliminary_payload = make_payload(
        args,
        results,
        recovered_labels,
        xxhash_path,
        requested_xxhash_path,
        xxhash_path_mode,
        bool(self_specs),
    )
    atomic_write_text(self_check_input_path, json.dumps(preliminary_payload, indent=2, sort_keys=True) + "\n")

    for self_index, spec in enumerate(self_specs, len(results) + 1):
        print(f"[{self_index}/{len(specs)}] {spec.label}")
        results.append(run_command(spec))

    recovered_labels.extend(retry_required_failures(results, specs_by_label))
    final_payload = make_payload(
        args,
        results,
        recovered_labels,
        xxhash_path,
        requested_xxhash_path,
        xxhash_path_mode,
        False,
    )
    write_reports(final_payload, json_output_path, markdown_output_path)
    cleanup_selfcheck_sidecars(json_output_path)

    required_failures = final_payload["summary"]["requiredFailures"]
    print(f"METRIC_PHI_VERIFY_SWEEP_STATUS: {final_payload['status']}")
    print(f"commands={len(results)} required_failures={required_failures}")
    print(f"report={rel(json_output_path)}")
    return 0 if required_failures == 0 else 2


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run all METRIC_PHI Python verifier commands.")
    parser.add_argument("--python-exe", default=sys.executable)
    parser.add_argument("--xxhash-path", default="")
    parser.add_argument("--json-output", default=str(DEFAULT_JSON_REPORT))
    parser.add_argument("--markdown-output", default=str(DEFAULT_MD_REPORT))
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    return run(parse_args(sys.argv[1:] if argv is None else argv))


if __name__ == "__main__":
    raise SystemExit(main())
