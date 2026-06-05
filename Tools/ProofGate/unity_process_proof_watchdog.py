#!/usr/bin/env python3
"""Summarize Unity/process/proof state without taking the Unity slot.

Evidence class: STATIC_PROCESS_SAMPLE / STATIC_FILESYSTEM / STATIC_LOG.
This tool never launches Unity, enters Play Mode, builds, kills processes,
captures screenshots, deletes proof files, or accepts visual quality.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import validate_proof_packet as proofgate


SCHEMA = "hecton8.unity_process_proof_watchdog.v1"
STATUS_STATIC = "STATIC_STATUS"
STATUS_BLOCKED = "STATIC_BLOCKED"
STATUS_ERROR = "STATIC_TOOL_ERROR"
FORBIDDEN_OUTPUT_LABELS = (
    "PLAYMODE VERIFIED",
    "PROFILER VERIFIED",
    "PLAYER-CAPTURE VERIFIED",
    "VISUAL ACCEPTED",
    "RELEASE READY",
)
RAW_GROUP_RE = re.compile(r"^(h8_\d+)")


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso_utc(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def normalize_path(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def resolve_path(root: Path, raw: str) -> Path:
    path = Path(raw)
    if path.is_absolute():
        return path.resolve()
    return (root / path).resolve()


def load_process_sample(path: Path | None) -> list[dict[str, Any]]:
    if path is not None:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
        if isinstance(payload, dict):
            payload = payload.get("processes", [])
        if not isinstance(payload, list):
            raise ValueError("process sample JSON must be a list or contain a 'processes' list")
        return [item for item in payload if isinstance(item, dict)]

    command = (
        "Get-Process | Where-Object { "
        "$_.ProcessName -match '^(Unity|Unity Hub|dotnet|csc|VBCSCompiler|MSBuild|"
        "Unity\\.ILPP\\.Runner|UnityShaderCompiler|ShaderCompiler|AssetImportWorker)$' "
        "} | Select-Object ProcessName,Id,CPU,StartTime,Path | ConvertTo-Json -Compress"
    )
    completed = subprocess.run(
        ["powershell", "-NoProfile", "-Command", command],
        capture_output=True,
        text=True,
        timeout=10,
        check=False,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        return []
    payload = json.loads(completed.stdout)
    if isinstance(payload, dict):
        payload = [payload]
    if not isinstance(payload, list):
        return []
    return [item for item in payload if isinstance(item, dict)]


def process_name(process: dict[str, Any]) -> str:
    return str(process.get("ProcessName") or process.get("processName") or "").strip()


def process_path(process: dict[str, Any]) -> str:
    return str(process.get("Path") or process.get("path") or "").strip()


def classify_processes(processes: list[dict[str, Any]]) -> dict[str, Any]:
    normalized: list[dict[str, Any]] = []
    unity_busy = False
    compile_busy = False
    asset_import_busy = False
    shader_compile_busy = False
    build_busy = False

    for process in processes:
        name = process_name(process)
        lower_name = name.lower()
        lower_path = process_path(process).lower()
        normalized.append(
            {
                "processName": name,
                "id": process.get("Id") or process.get("id"),
                "cpuSeconds": process.get("CPU") or process.get("cpuSeconds"),
                "startTime": str(process.get("StartTime") or process.get("startTime") or ""),
                "path": process_path(process),
            }
        )
        if lower_name == "unity" or lower_path.endswith("unity.exe"):
            unity_busy = True
        if lower_name in {"dotnet", "csc", "vbcscompiler", "msbuild"} or "ilpp" in lower_name:
            compile_busy = True
        if lower_name in {"dotnet", "msbuild", "csc"}:
            build_busy = True
        if "assetimportworker" in lower_name:
            asset_import_busy = True
        if "shadercompiler" in lower_name:
            shader_compile_busy = True

    busy = unity_busy or compile_busy or asset_import_busy or shader_compile_busy or build_busy
    return {
        "unityBusy": unity_busy,
        "compileBusy": compile_busy,
        "assetImportBusy": asset_import_busy,
        "shaderCompileBusy": shader_compile_busy,
        "buildBusy": build_busy,
        "unitySlotRecommendation": "BUSY_DO_NOT_TAKE_SLOT" if busy else "NO_BUSY_PROCESS_SEEN_STATIC_SAMPLE",
        "processes": normalized,
    }


def png_summary(path: Path, root: Path) -> dict[str, Any]:
    return {
        "path": normalize_path(path, root),
        "fileName": path.name,
        "byteSize": path.stat().st_size,
        "lastWriteUtc": iso_utc(datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)),
        "prefixGroup": RAW_GROUP_RE.match(path.name).group(1) if RAW_GROUP_RE.match(path.name) else "",
        "isUnderAssets": proofgate.is_under_assets(path),
        "hasMetaSibling": path.with_suffix(path.suffix + ".meta").exists(),
        "proofUse": "DIAGNOSTIC_ONLY",
    }


def discover_raw_screenshots(repo_root: Path, screenshots_root: Path, mcp_root: Path) -> dict[str, Any]:
    roots = [mcp_root, screenshots_root]
    seen: set[Path] = set()
    pngs: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*.png"):
            resolved = path.resolve()
            if resolved in seen:
                continue
            if "HectonProofPackets" in resolved.parts:
                continue
            seen.add(resolved)
            pngs.append(resolved)
    pngs.sort(key=lambda p: p.stat().st_mtime, reverse=True)

    groups: dict[str, list[Path]] = {}
    for path in pngs:
        match = RAW_GROUP_RE.match(path.name)
        if match:
            groups.setdefault(match.group(1), []).append(path)

    newest_group: dict[str, Any] = {}
    newest_complete_group: dict[str, Any] = {}
    if groups:
        def build_group_summary(group_id: str, paths: list[Path]) -> dict[str, Any]:
            required_hits = {
                view_id: any(view_id in path.name for path in paths)
                for _, view_id, _ in proofgate.REQUIRED_VIEWS
            }
            return {
                "prefix": group_id,
                "count": len(paths),
                "newestLastWriteUtc": iso_utc(datetime.fromtimestamp(max(p.stat().st_mtime for p in paths), tz=timezone.utc)),
                "hasAllRequiredRouteNames": all(required_hits.values()),
                "requiredRouteNameHits": required_hits,
                "paths": [normalize_path(path, repo_root) for path in sorted(paths, key=lambda p: p.name)],
                "evidenceClass": "STATIC_FILESYSTEM_RAW_PNG_ONLY",
            }

        group_id, paths = max(groups.items(), key=lambda item: max(p.stat().st_mtime for p in item[1]))
        newest_group = build_group_summary(group_id, paths)
        complete_groups = [
            build_group_summary(candidate_id, candidate_paths)
            for candidate_id, candidate_paths in groups.items()
            if all(any(view_id in path.name for path in candidate_paths) for _, view_id, _ in proofgate.REQUIRED_VIEWS)
        ]
        if complete_groups:
            newest_complete_group = max(complete_groups, key=lambda item: item["newestLastWriteUtc"])

    return {
        "latestRawScreenshot": png_summary(pngs[0], repo_root) if pngs else {},
        "newestRawGroup": newest_group,
        "newestCompleteRawGroup": newest_complete_group,
        "rawScreenshotOnly": bool(newest_group) and not newest_group.get("manifestPath"),
    }


def parse_manifest(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def discover_proof_packets(repo_root: Path, proof_packets_root: Path, proofgate_path: Path, strict: bool) -> dict[str, Any]:
    manifests: list[Path] = []
    if proof_packets_root.exists():
        if (proof_packets_root / "manifest.json").exists():
            manifests.append(proof_packets_root / "manifest.json")
        manifests.extend(path for path in proof_packets_root.glob("*/manifest.json") if path.is_file())

    manifests.sort(key=lambda path: path.stat().st_mtime, reverse=True)
    packets: list[dict[str, Any]] = []
    for manifest_path in manifests:
        manifest = parse_manifest(manifest_path)
        screenshots = manifest.get("screenshots") if isinstance(manifest.get("screenshots"), list) else []
        view_ids = {str(record.get("view_id", "")) for record in screenshots if isinstance(record, dict)}
        packets.append(
            {
                "path": normalize_path(manifest_path.parent, repo_root),
                "manifestPath": normalize_path(manifest_path, repo_root),
                "manifestLastWriteUtc": iso_utc(datetime.fromtimestamp(manifest_path.stat().st_mtime, tz=timezone.utc)),
                "packetId": manifest.get("packet_id"),
                "sessionId": manifest.get("session_id"),
                "finalDisposition": manifest.get("final_disposition"),
                "globalQualityLabel": manifest.get("global_quality_label"),
                "screenshotCount": len(screenshots),
                "requiredViewCount": sum(1 for _, view_id, _ in proofgate.REQUIRED_VIEWS if view_id in view_ids),
                "candidateEvidenceClass": "STATIC_FILESYSTEM",
            }
        )

    result: dict[str, Any] = {
        "proofPacketCandidateFound": bool(packets),
        "latestProofPacketCandidate": packets[0] if packets else {},
        "latestProofGateResult": {},
        "proofGateAvailable": proofgate_path.exists(),
    }
    if not packets:
        return result
    if not proofgate_path.exists():
        result["latestProofGateResult"] = {"status": "PROOFGATE_TOOL_MISSING"}
        return result

    latest = packets[0]
    packet_id = latest.get("packetId")
    session_id = latest.get("sessionId")
    if not packet_id or not session_id:
        result["latestProofGateResult"] = {"status": "PROOFGATE_INPUT_MISSING"}
        return result

    parser = proofgate.build_parser()
    argv = [
        "--packet-root",
        str((repo_root / latest["path"]).resolve()),
        "--packet-id",
        str(packet_id),
        "--session-id",
        str(session_id),
    ]
    if latest.get("globalQualityLabel"):
        argv.extend(["--expected-quality", str(latest["globalQualityLabel"])])
    if strict:
        argv.append("--strict")
    args = parser.parse_args(argv)
    code, payload = proofgate.validate_packet(args)
    result["latestProofGateResult"] = {
        "exitCode": code,
        "status": payload.get("status"),
        "rejectCodes": payload.get("rejectCodes", []),
        "maySubmitForHumanVisualReview": payload.get("maySubmitForHumanVisualReview", False),
        "mayClaimPlayerCaptureVerified": payload.get("mayClaimPlayerCaptureVerified", False),
    }
    return result


def line_number_for_offset(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def scan_dirty_logs(repo_root: Path, roots: list[Path], max_files: int) -> dict[str, Any]:
    candidates: list[Path] = []
    for root in roots:
        if not root.exists() or max_files <= 0:
            continue
        candidates.extend(path for path in root.glob("*.log") if path.is_file())
        candidates.extend(path for path in root.glob("LOG_*.md") if path.is_file())
    candidates.sort(key=lambda path: path.stat().st_mtime, reverse=True)
    candidates = candidates[:max_files]

    token_counts: dict[str, int] = {}
    first_hits: dict[str, dict[str, Any]] = {}
    for path in candidates:
        text = path.read_text(encoding="utf-8", errors="ignore")
        for code, token in proofgate.FORBIDDEN_LOG_TOKENS:
            offset = text.find(token)
            if offset < 0:
                continue
            token_counts[code] = token_counts.get(code, 0) + text.count(token)
            first_hits.setdefault(
                code,
                {
                    "path": normalize_path(path, repo_root),
                    "line": line_number_for_offset(text, offset),
                    "token": token,
                },
            )

    return {
        "scannedFiles": [normalize_path(path, repo_root) for path in candidates],
        "scanMode": "FULL_FILE_STATIC_SCAN",
        "tokenCountsByCode": token_counts,
        "firstHitByCode": first_hits,
        "dirtyLogTokensFound": bool(token_counts),
    }


def build_status(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = resolve_path(Path.cwd(), args.repo_root)
    created = utc_now()
    process_sample_path = resolve_path(repo_root, args.process_sample_json) if args.process_sample_json else None
    processes = load_process_sample(process_sample_path)
    process_state = classify_processes(processes)

    screenshots_root = resolve_path(repo_root, args.screenshots_root)
    proof_packets_root = resolve_path(repo_root, args.proof_packets_root)
    mcp_root = resolve_path(repo_root, args.mcp_root)
    proofgate_path = resolve_path(repo_root, args.proofgate)
    logs_root = resolve_path(repo_root, args.logs_root)
    agent_logs_root = resolve_path(repo_root, args.agent_logs_root)

    raw_state = discover_raw_screenshots(repo_root, screenshots_root, mcp_root)
    proof_state = discover_proof_packets(repo_root, proof_packets_root, proofgate_path, args.strict)
    log_state = scan_dirty_logs(repo_root, [logs_root, agent_logs_root], args.max_log_files)

    blockers: list[str] = []
    warnings: list[str] = []
    if process_state["unitySlotRecommendation"] == "BUSY_DO_NOT_TAKE_SLOT":
        blockers.append("BUSY_DO_NOT_TAKE_SLOT")
    if not proof_state["proofGateAvailable"]:
        blockers.append("PROOFGATE_TOOL_MISSING")
    latest_gate = proof_state.get("latestProofGateResult", {})
    if latest_gate.get("status") == proofgate.REJECT_STATUS:
        blockers.extend(f"PROOFGATE_{code}" for code in latest_gate.get("rejectCodes", []))
    if not proof_state["proofPacketCandidateFound"] and raw_state.get("newestCompleteRawGroup"):
        blockers.append("RAW_PNG_SET_NO_MANIFEST")
    elif not proof_state["proofPacketCandidateFound"] and raw_state.get("latestRawScreenshot"):
        blockers.append("RAW_SCREENSHOT_NO_MANIFEST")
    if log_state["dirtyLogTokensFound"]:
        blockers.append("DIRTY_LOG_TOKENS_FOUND")
    if raw_state.get("latestRawScreenshot"):
        warnings.append("Latest screenshot is raw/static unless manifest-bound packet exists.")
    if any(label in json.dumps(latest_gate) for label in FORBIDDEN_OUTPUT_LABELS):
        blockers.append("FORBIDDEN_PROOF_LABEL_IN_OUTPUT")

    status = STATUS_BLOCKED if blockers else STATUS_STATIC
    payload: dict[str, Any] = {
        "schema": SCHEMA,
        "status": status,
        "evidenceClass": "STATIC_PROCESS_SAMPLE",
        "createdUtc": iso_utc(created),
        "createdLocal": created.astimezone().isoformat(),
        "repoRoot": normalize_path(repo_root, repo_root),
        **process_state,
        **raw_state,
        **proof_state,
        "latestLogs": log_state["scannedFiles"],
        "dirtyTokenSummary": log_state,
        "blockers": sorted(set(blockers)),
        "warnings": warnings,
        "mayClaimRuntimeProof": False,
        "mayClaimVisualAccepted": False,
        "mayClaimPlayerCaptureVerified": latest_gate.get("mayClaimPlayerCaptureVerified", False),
    }
    return payload


def write_json_report(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")


def write_markdown_report(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Unity Process Proof Watchdog",
        "",
        f"Status: `{payload['status']}`",
        f"Evidence: `{payload['evidenceClass']}`",
        f"Created UTC: `{payload['createdUtc']}`",
        "",
        "This is static status only. It is not runtime, profiler, or visual acceptance.",
        "",
        "## Unity Slot",
        f"- Recommendation: `{payload['unitySlotRecommendation']}`",
        f"- Unity busy: `{payload['unityBusy']}`",
        f"- Compile busy: `{payload['compileBusy']}`",
        f"- Shader compile busy: `{payload['shaderCompileBusy']}`",
        "",
        "## ProofGate",
        f"- Candidate found: `{payload['proofPacketCandidateFound']}`",
        f"- Gate status: `{payload.get('latestProofGateResult', {}).get('status', '')}`",
        f"- May claim PLAYER-CAPTURE VERIFIED: `{payload.get('mayClaimPlayerCaptureVerified', False)}`",
        "",
        "## Blockers",
    ]
    blockers = payload.get("blockers") or []
    lines.extend(f"- `{blocker}`" for blocker in blockers) if blockers else lines.append("- none")
    lines.append("")
    lines.append("## Latest Raw Screenshot")
    latest = payload.get("latestRawScreenshot") or {}
    lines.append(f"- `{latest.get('path', 'none')}`")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--screenshots-root", default="Docs/Screenshots")
    parser.add_argument("--proof-packets-root", default="Docs/Screenshots/HectonProofPackets")
    parser.add_argument("--mcp-root", default="Docs/Screenshots/MCP")
    parser.add_argument("--reports-root", default="Docs/Reports")
    parser.add_argument("--logs-root", default="Docs/Logs")
    parser.add_argument("--agent-logs-root", default="Docs/AgentLogs")
    parser.add_argument("--proofgate", default="Tools/ProofGate/validate_proof_packet.py")
    parser.add_argument("--process-sample-json")
    parser.add_argument("--max-log-files", type=int, default=12)
    parser.add_argument("--max-proof-packets", type=int, default=12)
    parser.add_argument("--json-out")
    parser.add_argument("--md-out")
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--no-write", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        payload = build_status(args)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    except Exception as exc:  # pragma: no cover - last-resort CLI guard.
        print(f"internal watchdog error: {exc}", file=sys.stderr)
        return 3

    repo_root = resolve_path(Path.cwd(), args.repo_root)
    if not args.no_write:
        if args.json_out:
            write_json_report(resolve_path(repo_root, args.json_out), payload)
        if args.md_out:
            write_markdown_report(resolve_path(repo_root, args.md_out), payload)

    print(payload["status"])
    for blocker in payload.get("blockers", []):
        print(blocker)
    return 1 if payload.get("blockers") else 0


if __name__ == "__main__":
    raise SystemExit(main())
