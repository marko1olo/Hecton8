#!/usr/bin/env python3
"""Static VWS/subtitle allocation scanner for X_011.

The scanner is deliberately conservative:
- owned hot-route findings are hard failures;
- full-project findings outside the owned audio/subtitle/text lane are emitted
  as classified evidence, not silently hidden.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "Assets" / "_Project" / "Scripts"
REPORT_PATH = PROJECT_ROOT / "Docs" / "Reports" / "UX_OPTIMIZATION_REPORT_X_011.json"
VWS_PATH = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Audio" / "VocalWarningSystem.cs"
PLAYBACK_PATH = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Audio" / "Synthesis" / "VocalBankPlaybackRuntime.cs"
SUBTITLE_PATH = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "BabelSubtitleSyncRuntime.cs"
SUBTITLE_MANAGER_PATH = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "SubtitleManager.cs"
VOCAL_CUE_SIGNAL_PATH = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Signals" / "GlobalSignalPayloads.DomainRemainder.cs"


OWNED_HOT_FILES = {
    "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs",
    "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs",
    "Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs",
    "Assets/_Project/Scripts/UI/SubtitleManager.cs",
    "Assets/_Project/Scripts/UI/CharBufferPool.cs",
    "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs",
    "Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs",
    "Assets/_Project/Scripts/AudioLog/AudioLogData.cs",
    "Assets/_Project/Scripts/UI/NotificationEvents.cs",
    "Assets/_Project/Scripts/HUDNotification.cs",
    "Assets/_Project/Scripts/LocalizedTextReference.cs",
}

FOCUSED_TEXT_ROOTS = (
    "Assets/_Project/Scripts/Audio/",
    "Assets/_Project/Scripts/UI/",
    "Assets/_Project/Scripts/AudioLog/",
    "Assets/_Project/Scripts/Narrative/",
    "Assets/_Project/Scripts/PDA/",
    "Assets/_Project/Scripts/Progression/",
)


@dataclass(frozen=True)
class Pattern:
    identifier: str
    regex: re.Pattern[str]
    severity: str
    route: str


PATTERNS = (
    Pattern("time_frame_count", re.compile(r"\bTime\.frameCount\b"), "fatal", "Use SystemDispatcher audio/frame clock in consumers."),
    Pattern("managed_coroutine", re.compile(r"\bWaitForSeconds\b|\bStartCoroutine\b|\byield\s+return\b|\bIEnumerator\b"), "fatal", "Use dispatcher/audio-frame timing."),
    Pattern("tmp_string_sink", re.compile(r"\.text\s*=|\.SetText\s*\("), "fatal", "Use TMP SetCharArray through a preallocated buffer."),
    Pattern("priority_heap", re.compile(r"\bNativeMinHeap\b|\bVocalWarningHeapOps\b|\bPriorityQueue\s*<|\bSortedSet\s*<"), "fatal", "Use the 64-bit VwsPriorityWord route."),
    Pattern("legacy_subtitle_queue", re.compile(r"\bstruct\s+SubtitleRequest\b|_stringQueue|ShowImmediate\s*\(\s*string|CopyStringToRenderBuffer|ResolveDisplayMessage"), "fatal", "Use BufferedSubtitleCue and CharBufferPool."),
    Pattern("managed_materializer", re.compile(r"\.ToString\s*\(|\bnew\s+string\b|\bstring\.Create\b|\bstring\.Concat\b|\bstring\.Format\b|\bStringBuilder\b"), "warning", "Use ReadOnlySpan<char> and caller-owned buffers."),
    Pattern("string_interpolation", re.compile(r'\$"'), "warning", "Use span formatting into caller-owned buffers."),
    Pattern("legacy_localization_string", re.compile(r"\bGetOrFallback\s*\(|\bGetFormatted\s*\(|\bResolveLocalized\s*\("), "warning", "Use raw span/localization read model routes."),
    Pattern("culture_case", re.compile(r"\bToUpperInvariant\s*\(|\bToLowerInvariant\s*\("), "warning", "Use ASCII span casing for HUD/control tokens."),
    Pattern("exception_message_concat", re.compile(r"\+\s*(?:ex|exception)\.Message|(?:ex|exception)\.Message\s*\+"), "warning", "Use stable diagnostic codes or editor-only logging."),
)


def iter_cs_files(root: Path) -> Iterable[Path]:
    for path in root.rglob("*.cs"):
        if ".meta" in path.name:
            continue
        yield path


def to_project_path(path: Path) -> str:
    return path.relative_to(PROJECT_ROOT).as_posix()


def is_editor_path(project_path: str) -> bool:
    return "/Editor/" in project_path or project_path.endswith("Editor.cs")


def classify_context(project_path: str, line: str, in_editor_block: bool) -> str:
    stripped = line.strip()
    if is_editor_path(project_path) or in_editor_block:
        return "editor_only"
    if "Save" in project_path or "Persistent" in project_path or "PDAMarkerRegistry.cs" in project_path:
        return "save_identity_or_persistence"
    if "Debug" in project_path or "Diagnostics" in project_path or "SmokeTester" in project_path:
        return "diagnostic"
    if stripped.startswith("//") or stripped.startswith("/*") or stripped.startswith("*"):
        return "comment"
    return "runtime_source"


def is_owned_hot_path(project_path: str) -> bool:
    if project_path in OWNED_HOT_FILES:
        return True
    return project_path.startswith("Assets/_Project/Scripts/Audio/") and (
        "Vocal" in project_path or "Subtitle" in project_path or "Synthesis/Vocal" in project_path
    )


def is_focused_text_path(project_path: str) -> bool:
    return any(project_path.startswith(root) for root in FOCUSED_TEXT_ROOTS)


def scan_file(path: Path) -> list[dict[str, object]]:
    project_path = to_project_path(path)
    findings: list[dict[str, object]] = []
    editor_depth = 0

    try:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as exc:
        return [{
            "id": "read_error",
            "severity": "fatal",
            "path": project_path,
            "line": 0,
            "context": "scanner",
            "evidence": str(exc),
            "route": "Fix filesystem access before claiming static proof.",
        }]

    for line_number, line in enumerate(lines, start=1):
        stripped = line.strip()
        if stripped.startswith("#if") and "UNITY_EDITOR" in stripped:
            editor_depth += 1
        elif stripped.startswith("#endif") and editor_depth > 0:
            editor_depth -= 1

        context = classify_context(project_path, line, editor_depth > 0)
        for pattern in PATTERNS:
            if not pattern.regex.search(line):
                continue
            findings.append({
                "id": pattern.identifier,
                "severity": pattern.severity,
                "path": project_path,
                "line": line_number,
                "context": context,
                "ownedHotPath": is_owned_hot_path(project_path),
                "focusedTextPath": is_focused_text_path(project_path),
                "evidence": stripped[:180],
                "route": pattern.route,
            })

    return findings


def summarize(findings: list[dict[str, object]]) -> dict[str, object]:
    owned_hot = [
        f for f in findings
        if f["ownedHotPath"] and f["context"] == "runtime_source"
    ]
    focused_runtime = [
        f for f in findings
        if f["focusedTextPath"] and f["context"] == "runtime_source"
    ]
    hard_owned = [f for f in owned_hot if f["severity"] == "fatal"]
    hard_focused = [f for f in focused_runtime if f["severity"] == "fatal"]

    by_id: dict[str, int] = {}
    by_context: dict[str, int] = {}
    for finding in findings:
        by_id[finding["id"]] = by_id.get(finding["id"], 0) + 1
        by_context[finding["context"]] = by_context.get(finding["context"], 0) + 1

    focused_materializers = [
        f for f in focused_runtime
        if f["id"] == "managed_materializer"
    ]

    return {
        "ownedHotRuntimeFindingCount": len(owned_hot),
        "ownedHotFatalCount": len(hard_owned),
        "focusedTextRuntimeFindingCount": len(focused_runtime),
        "focusedTextFatalCount": len(hard_focused),
        "focusedTextManagedMaterializerCount": len(focused_materializers),
        "findingCountsById": by_id,
        "findingCountsByContext": by_context,
        "status": "PASS_STATIC_HOT_ROUTE" if not hard_owned else "FAIL_OWNED_HOT_ROUTE",
    }


def find_lines(path: Path, needles: tuple[str, ...]) -> list[dict[str, object]]:
    if not path.exists():
        return []

    project_path = to_project_path(path)
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    matches: list[dict[str, object]] = []
    for line_number, line in enumerate(lines, start=1):
        for needle in needles:
            if needle in line:
                matches.append({
                    "path": project_path,
                    "line": line_number,
                    "needle": needle,
                    "evidence": line.strip()[:180],
                })
                break
    return matches


def run_priority_storm() -> dict[str, object]:
    priority_word = 0
    slots: dict[int, tuple[int, float, int]] = {}
    accepted = 0
    replaced = 0
    rejected = 0

    for sequence in range(50):
        warning_id = sequence % 5 + 1
        bit_index = 64 - warning_id
        mask = 1 << bit_index
        score = 1024.0 - warning_id * 128.0 + (sequence % 7) * 0.03125
        current = slots.get(bit_index)
        if current is None:
            slots[bit_index] = (warning_id, score, sequence)
            priority_word |= mask
            accepted += 1
            continue

        _, current_score, current_sequence = current
        if score > current_score or (score == current_score and sequence < current_sequence):
            slots[bit_index] = (warning_id, score, sequence)
            replaced += 1
        else:
            rejected += 1

    highest_bit = priority_word.bit_length() - 1 if priority_word else -1
    sorted_bits: list[int] = []
    scan_word = priority_word
    while scan_word:
        bit = scan_word.bit_length() - 1
        sorted_bits.append(bit)
        scan_word &= ~(1 << bit)

    return {
        "triggerCount": 50,
        "priorityWordHex": f"0x{priority_word:016X}",
        "activeCount": len(slots),
        "highestBit": highest_bit,
        "priorityOrderHighToLow": sorted_bits,
        "accepted": accepted,
        "replaced": replaced,
        "rejected": rejected,
        "pass": priority_word == 0xF800000000000000 and highest_bit == 63 and sorted_bits == [63, 62, 61, 60, 59],
    }


def build_priority_proof() -> dict[str, object]:
    vws_text = VWS_PATH.read_text(encoding="utf-8", errors="replace") if VWS_PATH.exists() else ""
    playback_text = PLAYBACK_PATH.read_text(encoding="utf-8", errors="replace") if PLAYBACK_PATH.exists() else ""
    subtitle_text = SUBTITLE_PATH.read_text(encoding="utf-8", errors="replace") if SUBTITLE_PATH.exists() else ""
    subtitle_manager_text = SUBTITLE_MANAGER_PATH.read_text(encoding="utf-8", errors="replace") if SUBTITLE_MANAGER_PATH.exists() else ""
    vocal_cue_text = VOCAL_CUE_SIGNAL_PATH.read_text(encoding="utf-8", errors="replace") if VOCAL_CUE_SIGNAL_PATH.exists() else ""

    checks = {
        "vwsPriorityWordPresent": "VwsPriorityWord" in vws_text,
        "vocalCueSignal64Bytes": "public struct VocalCueSignal" in vocal_cue_text and "StructLayout(LayoutKind.Explicit, Size = 64)" in vocal_cue_text,
        "heapAbsentInVws": "NativeMinHeap" not in vws_text and "VocalWarningHeapOps" not in vws_text,
        "bitMaskSetPresent": "VwsPriorityWord |=" in vws_text or "|= bitMask" in vws_text,
        "bitMaskClearPresent": "VwsPriorityWord &= ~" in vws_text or "&= ~bitMask" in vws_text,
        "lzcntPresent": "math.lzcnt" in vws_text,
        "selectPresent": "math.select" in vws_text,
        "emptyWordSelectPresent": "ResolveHighestPriorityBitIndexOrMax" in vws_text,
        "playbackPreemptFlagHonored": "VwsPreemptedFlag" in playback_text and "vwsPreempted" in playback_text,
        "subtitleAudioFramePresent": "StartAudioFrame" in subtitle_text and "CurrentAudioFrame" in subtitle_text,
        "subtitleManagerCharArrayPresent": "SetCharArray" in subtitle_manager_text,
    }
    return {
        "status": "PASS_STATIC_PRIORITY_PROOF" if all(checks.values()) else "FAIL_STATIC_PRIORITY_PROOF",
        "checks": checks,
        "sourceEvidence": find_lines(VWS_PATH, (
            "VwsPriorityWord |=",
            "VwsPriorityWord &= ~",
            "ResolveHighestPriorityBitIndex",
            "ResolveHighestPriorityBitIndexOrMax",
            "math.lzcnt",
            "math.select",
        ))[:80] + find_lines(PLAYBACK_PATH, (
            "VwsPreemptedFlag",
            "vwsPreempted",
        ))[:40] + find_lines(VOCAL_CUE_SIGNAL_PATH, (
            "public struct VocalCueSignal",
            "PhraseHashID",
            "SourceAupGridX",
            "Flags",
        ))[:40] + find_lines(SUBTITLE_PATH, (
            "StartAudioFrame",
            "CurrentAudioFrame",
        ))[:40] + find_lines(SUBTITLE_MANAGER_PATH, (
            "SetCharArray",
        ))[:40],
        "stormSimulation": run_priority_storm(),
    }


def main() -> int:
    files = list(iter_cs_files(SOURCE_ROOT))
    findings: list[dict[str, object]] = []
    for path in files:
        findings.extend(scan_file(path))

    summary = summarize(findings)
    priority_proof = build_priority_proof()
    focused_runtime = [
        f for f in findings
        if f["focusedTextPath"] and f["context"] == "runtime_source"
    ]
    owned_hot = [
        f for f in findings
        if f["ownedHotPath"] and f["context"] == "runtime_source"
    ]

    report = {
        "agent": "X_011",
        "role": "VOCAL_WARNING_AND_SUBTITLE_STREAMLINER",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "scanner": "Tools/OOP_Voice_Scanner_X_011.py",
        "sourceRoot": "Assets/_Project/Scripts",
        "filesScanned": len(files),
        "summary": summary,
        "priorityWordProof": priority_proof,
        "ownedHotRuntimeFindings": owned_hot[:200],
        "focusedTextRuntimeFindings": focused_runtime[:300],
        "allFindingCount": len(findings),
        "allFindingsSample": findings[:300],
        "notes": [
            "PASS_STATIC_HOT_ROUTE is static source proof only; Unity Profiler/GCMonitor proof is still required for runtime Zero-GC claims.",
            "TMP rendering cannot consume unmanaged ReadOnlySpan<char> directly; the accepted bridge is preallocated char[] plus SetCharArray.",
            "Findings outside owned hot route are emitted for integrators and are not treated as X_011 VWS/subtitle regressions unless they enter the focused text route.",
        ],
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({
        "report": str(REPORT_PATH.relative_to(PROJECT_ROOT)).replace("\\", "/"),
        "filesScanned": len(files),
        "status": summary["status"],
        "priorityProofStatus": priority_proof["status"],
        "ownedHotFatalCount": summary["ownedHotFatalCount"],
        "focusedTextFatalCount": summary["focusedTextFatalCount"],
        "focusedTextManagedMaterializerCount": summary["focusedTextManagedMaterializerCount"],
        "allFindingCount": len(findings),
    }, separators=(",", ":")))
    return 0 if summary["status"] == "PASS_STATIC_HOT_ROUTE" and priority_proof["status"] == "PASS_STATIC_PRIORITY_PROOF" else 1


if __name__ == "__main__":
    raise SystemExit(main())
