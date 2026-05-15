#!/usr/bin/env python3
"""Run the UX hardware-adaptive UI validation suite.

This is the single local command for static/Python validation. It does not
replace Unity import, GCMonitor, Frame Debugger, or in-engine capture evidence.
"""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import time
from pathlib import Path


REPORT_PATH = Path("Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json")

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
    "Tools/UX/run_unity_import_check.ps1",
    "Tools/UX/test_hardware_adaptive_ui.py",
    "Tools/UX/test_unity_verification_gates.py",
    "Tools/UX/test_unity_report_update_cli.py",
    "Tools/UX/test_python_cache_cleanup.py",
    "Tools/UX/test_unity_environment_probe.py",
    "Tools/UX/run_hardware_adaptive_ui_validation.py",
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


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    results = [_run_command(root, name, command) for name, command in COMMANDS]
    failures = [result for result in results if result["exitCode"] != 0]

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
        "status": "PASS" if not failures and not missing_artifacts else "FAIL",
        "unityRuntimeStatus": "PENDING_UNITY_VERIFICATION",
        "commands": results,
        "missingArtifacts": missing_artifacts,
        "artifactSha256": hashes,
        "note": "Static/Python validation only. Unity import, GCMonitor, Frame Debugger, and in-engine captures remain separate gates.",
    }

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

    print(f"Hardware adaptive UI aggregate validation PASS: {REPORT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
