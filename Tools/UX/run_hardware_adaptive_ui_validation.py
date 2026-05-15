#!/usr/bin/env python3
"""Run the UX hardware-adaptive UI validation suite.

This is the single local command for static/Python validation. It does not
replace Unity import, GCMonitor, Frame Debugger, or in-engine capture evidence.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools.UX.validate_aggregate_report import validate_aggregate_report
from Tools.UX.validate_status_log_consistency import validate_status_log_consistency


REPORT_PATH = Path("Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json")
UNITY_ENVIRONMENT_PROBE_PATH = Path("Docs/AgentLogs/UI_UnityEnvironmentProbe_UX_ENGINEER.json")
STATUS_PATH = Path("Docs/Tasks/Status_UX_ENGINEER.md")
RATIONALE_PATH = Path("Docs/AgentLogs/Rationale_UX_ENGINEER.md")
LOG_PATH = Path("Docs/AgentLogs/LOG_UX_ENGINEER.md")
BLOCKER_PATH = Path("Docs/AgentLogs/Blocker_UX_ENGINEER.md")
CURRENT_BATCH_PATH = Path("Docs/Tasks/CURRENT_BATCH.md")
ARCHIVE_BATCH_PROMPT_PATH = Path("Docs/Archive/Batch006/Tasks/CURRENT_BATCH.md")
PROMPT_MARKER = '<AGENT_PROMPT id="HARDWARE_ADAPTIVE_UI_BAKER"'
PROMPT_BLOCK_PATTERN = re.compile(r'<AGENT_PROMPT id="HARDWARE_ADAPTIVE_UI_BAKER"[\s\S]*?</AGENT_PROMPT>')
PROMPT_TASK_PATTERN = re.compile(r"^\d+\.\s+", re.MULTILINE)
PROMPT_STATUS_PATTERN = re.compile(r'STATUS:\s+MUST BE\s+"([^"]+)"')

COMMANDS = (
    (
        "readability",
        (
            sys.executable,
            "Tools/UX/ui_readability_test.py",
            "--write-report",
        ),
    ),
    (
        "shader_sample_audit",
        (
            sys.executable,
            "Tools/UX/ui_shader_sample_audit.py",
            "--write-report",
        ),
    ),
    (
        "icon_baker_self_test",
        (
            sys.executable,
            "Tools/IconBaker.py",
            "--self-test",
            "--output",
            "Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest",
            "--manifest",
            "Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest/IconBakeManifest.json",
        ),
    ),
    (
        "unity_template_audit",
        (
            sys.executable,
            "Tools/UX/validate_unity_verification_template.py",
            "--write-report",
        ),
    ),
    (
        "unity_report_audit",
        (
            sys.executable,
            "Tools/UX/validate_unity_verification_report.py",
            "--write-audit",
        ),
    ),
    (
        "unity_environment_probe",
        (
            sys.executable,
            "Tools/UX/probe_unity_environment.py",
            "--write-report",
        ),
    ),
    (
        "unit_harness",
        (
            sys.executable,
            "-m",
            "unittest",
            "Tools.UX.test_hardware_adaptive_ui",
            "Tools.UX.test_unity_verification_gates",
            "Tools.UX.test_unity_report_update_cli",
            "Tools.UX.test_python_cache_cleanup",
            "Tools.UX.test_unity_environment_probe",
            "Tools.UX.test_validate_aggregate_report",
            "Tools.UX.test_status_log_consistency",
            "-v",
        ),
    ),
    (
        "python_cache_cleanup",
        (
            sys.executable,
            "Tools/UX/clean_python_cache.py",
            "--write-report",
        ),
    ),
)

HASHED_ARTIFACTS = (
    "Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs",
    "Assets/_Project/Shaders/UI/Hecton_DiegeticVisorCurvedHUD.shader",
    "Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader",
    "Assets/_Project/Art/Shaders/Hecton_HUD_AcousticRadarOverlay.shader",
    "Assets/_Project/Art/Shaders/Hecton_DiegeticPanelUnlit.shader",
    "Docs/Design/HardwareAdaptiveUIScaler.json",
    "Docs/Design/HardwareAdaptiveUIScaler.md",
    "Docs/Design/HardwareAdaptiveUIScaler_Runbook.md",
    "Docs/Design/HardwareAdaptiveUIScaler_UnityVerificationTemplate.json",
    "Docs/AgentLogs/UnityVerification_UX_ENGINEER.json",
    "Tools/IconBaker.py",
    "Tools/UX/ui_readability_test.py",
    "Tools/UX/ui_shader_sample_audit.py",
    "Tools/UX/validate_unity_verification_template.py",
    "Tools/UX/validate_unity_verification_report.py",
    "Tools/UX/unity_compile_log_audit.py",
    "Tools/UX/update_unity_verification_report.py",
    "Tools/UX/clean_python_cache.py",
    "Tools/UX/probe_unity_environment.py",
    "Tools/UX/validate_aggregate_report.py",
    "Tools/UX/validate_status_log_consistency.py",
    "Tools/UX/run_unity_import_check.ps1",
    "Tools/UX/test_hardware_adaptive_ui.py",
    "Tools/UX/test_unity_verification_gates.py",
    "Tools/UX/test_unity_report_update_cli.py",
    "Tools/UX/test_python_cache_cleanup.py",
    "Tools/UX/test_unity_environment_probe.py",
    "Tools/UX/test_validate_aggregate_report.py",
    "Tools/UX/test_status_log_consistency.py",
    "Tools/UX/run_hardware_adaptive_ui_validation.py",
)

EVIDENCE_CLASSES = (
    "STATIC_SOURCE",
    "STATIC_DOC",
    "CLI_COMPILE",
)

RUNTIME_EVIDENCE_CLASSES_MISSING = (
    "UNITY_CONSOLE",
    "PLAYMODE",
    "PROFILER",
    "FRAME_DEBUGGER",
    "PLAYER_BUILD",
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            digest.update(block)
    return digest.hexdigest()


def _run_command(root: Path, name: str, command: tuple[str, ...]) -> dict[str, object]:
    env = os.environ.copy()
    env["PYTHONDONTWRITEBYTECODE"] = "1"
    if name == "unit_harness":
        env["H8_UX_AGGREGATE_RUNNING"] = "1"

    start = time.perf_counter()
    completed = subprocess.run(
        command,
        cwd=root,
        env=env,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    elapsed_ms = (time.perf_counter() - start) * 1000.0

    return {
        "name": name,
        "command": list(command),
        "exitCode": completed.returncode,
        "elapsedMs": round(elapsed_ms, 3),
        "stdoutTail": completed.stdout[-4000:],
        "stderrTail": completed.stderr[-4000:],
    }


def _extract_unit_harness_test_count(results: list[dict[str, object]]) -> int:
    for result in results:
        if result.get("name") != "unit_harness":
            continue
        output = str(result.get("stdoutTail", "")) + "\n" + str(result.get("stderrTail", ""))
        match = re.search(r"Ran\s+(\d+)\s+tests", output)
        return int(match.group(1)) if match else -1
    return -1


def _count_pycache_dirs(root: Path) -> int:
    if not root.exists():
        return 0
    return sum(1 for path in root.rglob("__pycache__") if path.is_dir())


def _extract_prompt_metadata(text: str) -> dict[str, object] | None:
    match = PROMPT_BLOCK_PATTERN.search(text)
    if not match:
        return None

    prompt_block = match.group(0)
    status_match = PROMPT_STATUS_PATTERN.search(prompt_block)
    return {
        "taskCount": len(PROMPT_TASK_PATTERN.findall(prompt_block)),
        "requiredStatus": status_match.group(1) if status_match else "",
        "sha256": hashlib.sha256(prompt_block.encode("utf-8")).hexdigest(),
    }


def _resolve_prompt_source(root: Path) -> dict[str, object]:
    active_path = root / CURRENT_BATCH_PATH
    if active_path.exists():
        active_text = active_path.read_text(encoding="utf-8", errors="replace")
        active_metadata = _extract_prompt_metadata(active_text)
        if active_metadata is not None:
            return {
                "status": "ACTIVE_CURRENT_BATCH",
                "path": str(CURRENT_BATCH_PATH),
                "activeCurrentBatchExists": True,
                **active_metadata,
            }
        return {
            "status": "ACTIVE_CURRENT_BATCH_MISSING_PROMPT",
            "path": str(CURRENT_BATCH_PATH),
            "activeCurrentBatchExists": True,
            "taskCount": 0,
            "requiredStatus": "",
            "sha256": "",
        }

    archive_path = root / ARCHIVE_BATCH_PROMPT_PATH
    if archive_path.exists():
        archive_text = archive_path.read_text(encoding="utf-8", errors="replace")
        archive_metadata = _extract_prompt_metadata(archive_text)
        if archive_metadata is not None:
            return {
                "status": "ARCHIVE_FALLBACK_ACTIVE_CURRENT_BATCH_MISSING",
                "path": str(ARCHIVE_BATCH_PROMPT_PATH),
                "activeCurrentBatchExists": False,
                **archive_metadata,
            }

    return {
        "status": "PROMPT_SOURCE_MISSING",
        "path": "",
        "activeCurrentBatchExists": active_path.exists(),
        "taskCount": 0,
        "requiredStatus": "",
        "sha256": "",
    }


def main() -> int:
    root = ROOT
    results = [_run_command(root, name, command) for name, command in COMMANDS]
    failures = [result for result in results if result["exitCode"] != 0]
    python_cache_count_after = _count_pycache_dirs(root / "Tools")
    prompt_source = _resolve_prompt_source(root)

    hashes: dict[str, str] = {}
    missing_artifacts: list[str] = []
    for relative_path in HASHED_ARTIFACTS:
        path = root / relative_path
        if path.exists():
            hashes[relative_path] = _sha256(path)
        else:
            missing_artifacts.append(relative_path)

    report = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.aggregate_validation.v2",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not failures and not missing_artifacts and python_cache_count_after == 0 else "FAIL",
        "unityRuntimeStatus": "PENDING_UNITY_VERIFICATION",
        "evidenceClasses": list(EVIDENCE_CLASSES),
        "runtimeEvidenceClassesMissing": list(RUNTIME_EVIDENCE_CLASSES_MISSING),
        "promptSourceStatus": prompt_source["status"],
        "promptSourcePath": prompt_source["path"],
        "activeCurrentBatchExists": prompt_source["activeCurrentBatchExists"],
        "promptTaskCount": prompt_source["taskCount"],
        "promptRequiredStatus": prompt_source["requiredStatus"],
        "promptSha256": prompt_source["sha256"],
        "commandCount": len(results),
        "commands": results,
        "missingArtifacts": missing_artifacts,
        "artifactHashCount": len(hashes),
        "artifactSha256": hashes,
        "unitHarnessTestCount": _extract_unit_harness_test_count(results),
        "pythonCacheCountAfter": python_cache_count_after,
        "note": "Static/Python validation only. Unity import, GCMonitor, Frame Debugger, and in-engine captures remain separate gates.",
    }

    environment_probe_path = root / UNITY_ENVIRONMENT_PROBE_PATH
    if environment_probe_path.exists():
        environment_probe = json.loads(environment_probe_path.read_text(encoding="utf-8"))
        self_validation_failures = validate_aggregate_report(report, environment_probe)
    else:
        self_validation_failures = [f"missing Unity environment probe report: {UNITY_ENVIRONMENT_PROBE_PATH}"]

    report["aggregateSelfValidation"] = {
        "status": "PASS" if not self_validation_failures else "FAIL",
        "failures": self_validation_failures,
    }
    if self_validation_failures:
        report["status"] = "FAIL"

    status_log_failures = validate_status_log_consistency(
        (root / STATUS_PATH).read_text(encoding="utf-8"),
        (root / RATIONALE_PATH).read_text(encoding="utf-8"),
        (root / LOG_PATH).read_text(encoding="utf-8"),
        (root / BLOCKER_PATH).read_text(encoding="utf-8"),
        report,
    )
    report["statusLogSelfValidation"] = {
        "status": "PASS" if not status_log_failures else "FAIL",
        "failures": status_log_failures,
    }
    if status_log_failures:
        report["status"] = "FAIL"

    report_path = root / REPORT_PATH
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    if failures:
        for failure in failures:
            print(f"{failure['name']} failed with exit {failure['exitCode']}")
        return 1

    if missing_artifacts:
        for artifact in missing_artifacts:
            print(f"missing artifact: {artifact}")
        return 1

    if python_cache_count_after != 0:
        print(f"python cache cleanup left {python_cache_count_after} __pycache__ directories")
        return 1

    if self_validation_failures:
        for failure in self_validation_failures:
            print(f"aggregate self-validation failed: {failure}")
        return 1

    if status_log_failures:
        for failure in status_log_failures:
            print(f"status/log self-validation failed: {failure}")
        return 1

    print(f"Hardware adaptive UI aggregate validation PASS: {REPORT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
