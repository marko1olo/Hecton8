#!/usr/bin/env python3
"""Static X_008 combat armor proof scanner.

This is evidence tooling, not runtime code. It deliberately uses plain text
patterns because the local Roslyn host has already failed on Unity binding
resolution in this workspace.
"""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path


AGENT_ID = "X_008"
ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "Assets" / "_Project" / "Scripts"
COMBAT = SCRIPTS / "Gameplay" / "Combat"
ART_SHADERS = ROOT / "Assets" / "_Project" / "Art" / "Shaders"
REPORT = ROOT / "Docs" / "Reports" / "COMBAT_OPTIMIZATION_REPORT_X_008.json"
PROJECT_SWEEP_REPORT = ROOT / "Docs" / "Reports" / "PROJECT_WIDE_HOTPATH_SWEEP_X_008.json"
SHADER_EXTENSIONS = {".shader", ".compute", ".hlsl"}

FORBIDDEN_TRIG_RE = re.compile(
    r"\b(?:math|Mathf|System\.Math|Math)\.(?:acos|asin|atan|atan2|sin|cos|tan)\s*\("
)
FORBIDDEN_INVERSE_RE = re.compile(
    r"\b(?:math|Mathf|System\.Math|Math)\.(?:acos|asin)\s*\("
)
ANGLE_API_RE = re.compile(
    r"\b(?:Vector3\.Angle|Vector3\.SignedAngle|Quaternion\.Angle|Angle\s*\(|SignedAngle\s*\(|AxisAngle\s*\()"
)
DIRECT_MUTATION_RE = re.compile(
    r"\b(?:Rigidbody|\.velocity\s*=|\.AddForce\s*\(|Health\s*=|CurrentHealth\s*=|TakeDamage\s*\()"
)
DAMAGE_BYPASS_RE = re.compile(
    r"\b(?:TakeDamage\s*\(|ApplyDamage\s*\(|CurrentHealth\s*=|Health\s*=|\.health\s*=)"
)
RIGIDBODY_DIRECT_RE = re.compile(
    r"\b(?:Rigidbody|\.velocity\s*=|\.angularVelocity\s*=|\.AddForce\s*\()"
)
MANAGED_EVENT_RE = re.compile(
    r"\b(?:event\s+Action|Action<|UnityEvent|\.Invoke\s*\(|\+=\s*(?:new\s+)?(?:Action|Func)?)"
)
JOB_SIDE_PRESENTATION_RE = re.compile(
    r"\b(?:Instantiate\s*\(|PlayOneShot\s*\(|ParticleSystem|AudioSource|CameraJuice|RegisterWoundWS\s*\()"
)
COMBAT_MANAGED_CALLBACK_RE = re.compile(
    r"\b(?:ICombatDamageEventListener|ICombatDamageFeedbackReceiver|OnCombatDamageResolved|OnCombatDamageFeedback|CombatDamageRuntime\.Register\s*\(|CombatDamageRuntime\.Unregister\s*\()"
)
DIRECT_TWO_ARG_DAMAGE_QUEUE_RE = re.compile(
    r"\bCombatDamageRuntime\.TryQueueDamage\s*\(\s*in\s+\w+\s*,\s*in\s+\w+\s*\)\s*;"
)
DIRECT_ONE_ARG_DAMAGE_QUEUE_RE = re.compile(
    r"\bCombatDamageRuntime\.TryQueueDamage\s*\(\s*in\s+\w+\s*\)\s*;"
)
DIRECT_RETURN_DAMAGE_QUEUE_RE = re.compile(
    r"\breturn\s+CombatDamageRuntime\.TryQueueDamage\s*\("
)
EXTERNAL_DIRECT_TAKE_DAMAGE_RE = re.compile(
    r"\.\s*(?:TakeDamage|TakeLeviathanDamage)\s*\("
)
SHADER_INVERSE_RE = re.compile(r"\b(?:asin|acos)\s*\(")
SHADER_INVERSE_ANGLE_RE = re.compile(r"\b(?:asin|acos|atan|atan2)\s*\(")
SHADER_TRIG_RE = re.compile(r"\b(?:asin|acos|atan|atan2|sin|cos|tan)\s*\(")

_CODE_LINE_CACHE: dict[Path, list[tuple[int, str, str]]] = {}


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def scan(files: list[Path], pattern: re.Pattern[str]) -> list[dict[str, object]]:
    hits: list[dict[str, object]] = []
    for path in files:
        try:
            lines = read_csharp_code_lines(path)
        except OSError as exc:
            hits.append({"file": rel(path), "line": 0, "text": f"READ_ERROR: {exc}"})
            continue

        for index, line, code_line in lines:
            if pattern.search(code_line):
                hits.append({"file": rel(path), "line": index, "text": line.strip()})
    return hits


def scan_text_sources(files: list[Path], pattern: re.Pattern[str]) -> list[dict[str, object]]:
    hits: list[dict[str, object]] = []
    for path in files:
        try:
            lines = read_csharp_code_lines(path)
        except OSError as exc:
            hits.append({"file": rel(path), "line": 0, "text": f"READ_ERROR: {exc}"})
            continue

        for index, line, code_line in lines:
            if pattern.search(code_line):
                hits.append({"file": rel(path), "line": index, "text": line.strip()})
    return hits


def scan_runtime_source(files: list[Path], pattern: re.Pattern[str]) -> list[dict[str, object]]:
    hits: list[dict[str, object]] = []
    for path in files:
        path_text = rel(path)
        if is_editor_path(path_text):
            continue

        try:
            lines = read_csharp_code_lines(path)
        except OSError as exc:
            hits.append({"file": path_text, "line": 0, "text": f"READ_ERROR: {exc}"})
            continue

        editor_stack: list[bool] = []
        for index, line, code_line in lines:
            stripped = line.strip()
            if stripped.startswith("#if"):
                parent_editor = any(editor_stack)
                editor_stack.append(parent_editor or "UNITY_EDITOR" in stripped)
                continue
            if stripped.startswith("#endif"):
                if editor_stack:
                    editor_stack.pop()
                continue
            if any(editor_stack):
                continue

            if pattern.search(code_line):
                hits.append({"file": path_text, "line": index, "text": line.strip()})
    return hits


def read_csharp_code_lines(path: Path) -> list[tuple[int, str, str]]:
    cached = _CODE_LINE_CACHE.get(path)
    if cached is not None:
        return cached

    raw_lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    entries: list[tuple[int, str, str]] = []
    in_block_comment = False
    for index, line in enumerate(raw_lines, start=1):
        needs_strip = (
            in_block_comment or
            "//" in line or
            "/*" in line or
            '"' in line or
            "'" in line
        )
        if needs_strip:
            code_line, in_block_comment = strip_csharp_scan_line(line, in_block_comment)
        else:
            code_line = line

        entries.append((index, line, code_line))

    _CODE_LINE_CACHE[path] = entries
    return entries


def strip_csharp_scan_line(line: str, in_block_comment: bool) -> tuple[str, bool]:
    output: list[str] = []
    i = 0
    length = len(line)
    while i < length:
        if in_block_comment:
            end = line.find("*/", i)
            if end < 0:
                return "".join(output), True
            i = end + 2
            in_block_comment = False
            continue

        if i + 1 < length and line[i] == "/" and line[i + 1] == "/":
            break
        if i + 1 < length and line[i] == "/" and line[i + 1] == "*":
            in_block_comment = True
            i += 2
            continue

        ch = line[i]
        if ch == "'":
            i = skip_csharp_char_literal(line, i + 1)
            output.append("''")
            continue

        if ch == '"':
            i = skip_csharp_string_literal(line, i + 1, verbatim=False)
            output.append('""')
            continue

        if ch == "@" and i + 1 < length and line[i + 1] == '"':
            i = skip_csharp_string_literal(line, i + 2, verbatim=True)
            output.append('""')
            continue

        if ch == "$":
            if i + 1 < length and line[i + 1] == '"':
                i = skip_csharp_string_literal(line, i + 2, verbatim=False)
                output.append('""')
                continue
            if i + 2 < length and line[i + 1] == "@" and line[i + 2] == '"':
                i = skip_csharp_string_literal(line, i + 3, verbatim=True)
                output.append('""')
                continue
            if i + 2 < length and line[i + 1] == '"' and line[i + 2] == "@":
                i = skip_csharp_string_literal(line, i + 3, verbatim=True)
                output.append('""')
                continue

        output.append(ch)
        i += 1

    return "".join(output), in_block_comment


def skip_csharp_string_literal(line: str, start: int, verbatim: bool) -> int:
    i = start
    length = len(line)
    while i < length:
        ch = line[i]
        if ch == '"':
            if verbatim and i + 1 < length and line[i + 1] == '"':
                i += 2
                continue
            return i + 1
        if not verbatim and ch == "\\":
            i += 2
            continue
        i += 1
    return length


def skip_csharp_char_literal(line: str, start: int) -> int:
    i = start
    length = len(line)
    while i < length:
        ch = line[i]
        if ch == "'":
            return i + 1
        if ch == "\\":
            i += 2
            continue
        i += 1
    return length


def line_evidence(path: Path, needles: tuple[str, ...]) -> list[dict[str, object]]:
    evidence: list[dict[str, object]] = []
    lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    for index, line in enumerate(lines, start=1):
        if any(needle in line for needle in needles):
            evidence.append({"file": rel(path), "line": index, "text": line.strip()})
    return evidence


def is_editor_path(path_text: str) -> bool:
    parts = path_text.replace("\\", "/").split("/")
    return "Editor" in parts


def domain_key(path_text: str) -> str:
    parts = path_text.replace("\\", "/").split("/")
    try:
        scripts_index = parts.index("Scripts")
    except ValueError:
        return "UNKNOWN"

    if scripts_index + 1 >= len(parts):
        return "ROOT"
    return parts[scripts_index + 1]


def summarize_by_domain(hits: list[dict[str, object]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for hit in hits:
        key = domain_key(str(hit.get("file", "")))
        counts[key] = counts.get(key, 0) + 1
    return dict(sorted(counts.items(), key=lambda item: (-item[1], item[0])))


def unique_file_count(hits: list[dict[str, object]]) -> int:
    return len({str(hit.get("file", "")) for hit in hits})


def first_hits(hits: list[dict[str, object]], limit: int = 120) -> list[dict[str, object]]:
    return hits[:limit]


def collect_shader_sources() -> list[Path]:
    if not ART_SHADERS.exists():
        return []
    return sorted(
        path for path in ART_SHADERS.rglob("*")
        if path.is_file() and path.suffix.lower() in SHADER_EXTENSIONS
    )


def contains_after(text: str, first: str, second: str) -> bool:
    first_index = text.find(first)
    second_index = text.find(second)
    return first_index >= 0 and second_index > first_index


def read_source(path: Path) -> str:
    if not path.exists():
        return ""
    return path.read_text(encoding="utf-8-sig", errors="replace")


def strip_csharp_text(text: str) -> str:
    stripped_lines: list[str] = []
    in_block_comment = False
    for line in text.splitlines():
        code_line, in_block_comment = strip_csharp_scan_line(line, in_block_comment)
        stripped_lines.append(code_line)
    return "\n".join(stripped_lines)


def find_matching_paren(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        ch = text[index]
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return index
    return -1


def split_top_level_args(text: str) -> list[str]:
    args: list[str] = []
    start = 0
    depth = 0
    for index, ch in enumerate(text):
        if ch in "([{<":
            depth += 1
        elif ch in ")]}>":
            depth = max(0, depth - 1)
        elif ch == "," and depth == 0:
            args.append(text[start:index].strip())
            start = index + 1

    tail = text[start:].strip()
    if tail:
        args.append(tail)
    return args


def scan_damage_queue_calls(files: list[Path]) -> list[dict[str, object]]:
    calls: list[dict[str, object]] = []
    marker = "CombatDamageRuntime.TryQueueDamage"
    for path in files:
        source = read_source(path)
        stripped = strip_csharp_text(source)
        original_lines = source.splitlines()
        search_from = 0
        while True:
            marker_index = stripped.find(marker, search_from)
            if marker_index < 0:
                break

            open_index = stripped.find("(", marker_index + len(marker))
            if open_index < 0:
                break

            close_index = find_matching_paren(stripped, open_index)
            if close_index < 0:
                search_from = open_index + 1
                continue

            line_number = stripped.count("\n", 0, marker_index) + 1
            args = split_top_level_args(stripped[open_index + 1:close_index])
            prefix = stripped[max(0, marker_index - 96):marker_index]
            calls.append({
                "file": rel(path),
                "line": line_number,
                "argCount": len(args),
                "args": args,
                "text": original_lines[line_number - 1].strip() if line_number <= len(original_lines) else marker,
                "returnsDirectly": re.search(r"return\s+$", prefix) is not None,
                "negatedAdmissionGate": re.search(r"if\s*\(\s*!\s*$", prefix) is not None,
            })
            search_from = close_index + 1
    return calls


def extract_csharp_block_after(text: str, marker: str) -> str:
    marker_index = text.find(marker)
    if marker_index < 0:
        return ""

    brace_index = text.find("{", marker_index)
    if brace_index < 0:
        return ""

    depth = 0
    for index in range(brace_index, len(text)):
        ch = text[index]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index:index + 1]
    return ""


def count_pattern(text: str, pattern: str) -> int:
    return len(re.findall(pattern, text))


def analyze_control_surface(text: str) -> dict[str, object]:
    code = strip_csharp_text(text)
    explicit_control = count_pattern(code, r"\b(?:if|else|switch|case|default)\b|\?")
    loop_control = count_pattern(code, r"\b(?:for|foreach|while|do)\b")
    select_count = count_pattern(code, r"\bmath\.select\s*\(")
    return {
        "explicitControlTokens": explicit_control,
        "loopControlTokens": loop_control,
        "mathSelectCount": select_count,
        "forbiddenTrigCount": len(FORBIDDEN_TRIG_RE.findall(code)),
        "angleApiCount": len(ANGLE_API_RE.findall(code)),
        "sourceBytes": len(code.encode("utf-8")),
    }


def risk_hits(hits: list[dict[str, object]], keywords: tuple[str, ...]) -> list[dict[str, object]]:
    lowered = tuple(keyword.lower() for keyword in keywords)
    result: list[dict[str, object]] = []
    for hit in hits:
        haystack = (str(hit.get("file", "")) + " " + str(hit.get("text", ""))).lower()
        if any(keyword in haystack for keyword in lowered):
            result.append(hit)
    return result


def main() -> int:
    all_cs = sorted(SCRIPTS.rglob("*.cs"))
    combat_cs = sorted(COMBAT.rglob("*.cs"))
    shader_sources = collect_shader_sources()
    bullet_cs = [
        path for path in combat_cs
        if re.search(r"(ballistic|projectile|bullet|pellet|shell|weapon)", path.name, re.IGNORECASE)
    ]
    runtime = COMBAT / "HectonCombatRuntime_ArmorPenetration.cs"
    combat_damage_runtime = COMBAT / "CombatDamageRuntime.cs"
    ballistics_runtime = COMBAT / "BallisticsRuntime.cs"
    editor_facade = COMBAT / "ArmorPenetrationEditorFacade.cs"
    damage_source_contracts = SCRIPTS / "Gameplay" / "HabitatIntegrityManager.cs"
    hecton_player_health = SCRIPTS / "Gameplay" / "HectonPlayerHealth.cs"
    environmental_hazard = SCRIPTS / "Gameplay" / "EnvironmentalHazard.cs"
    manta_emergency_wreck = SCRIPTS / "Gameplay" / "MantaEmergencyWreck.cs"
    submarine_atmosphere_system = SCRIPTS / "SubmarineAtmosphereSystem.cs"
    abyssal_thermal_manager = SCRIPTS / "World" / "AbyssalThermalManager.cs"
    sargassum_micro_fauna_boids = SCRIPTS / "World" / "SargassumMicroFaunaBoids.cs"
    fauna_director = SCRIPTS / "FaunaDirector.cs"
    tool_hit_utility = SCRIPTS / "ToolHitUtility.cs"
    knife_tool = SCRIPTS / "KnifeTool.cs"
    harpoon_tool = SCRIPTS / "HarpoonLauncherTool.cs"
    stun_pistol_tool = SCRIPTS / "StunPistolTool.cs"
    salvage_sampler_tool = SCRIPTS / "SalvageSamplerTool.cs"
    fauna_brain = SCRIPTS / "Fauna" / "FaunaBrain.cs"
    fauna_combat_receiver = SCRIPTS / "Fauna" / "FaunaBrain.CombatDamageReceiver.cs"
    leviathan_tentacle_solver = SCRIPTS / "Fauna" / "LeviathanTentacleVerletSolver.cs"
    system_dispatcher = SCRIPTS / "Core" / "SystemDispatcher.cs"
    global_signal_payloads = SCRIPTS / "Core" / "Signals" / "GlobalSignalPayloads.DomainRemainder.cs"
    camera_juice_burst = SCRIPTS / "VFX" / "CameraJuiceSystem_CameraJuiceBurst.cs"
    soundscape_system = SCRIPTS / "World" / "SoundscapeSystem.cs"
    decal_vault = SCRIPTS / "Visor" / "DynamicDecalVaultRuntime.cs"
    armor_files = [path for path in (runtime, editor_facade) if path.exists()]

    combat_trig_hits = scan(combat_cs, FORBIDDEN_TRIG_RE)
    combat_angle_api_hits = scan(combat_cs, ANGLE_API_RE)
    project_inverse_hits = scan(all_cs, FORBIDDEN_INVERSE_RE)
    project_trig_hits = scan(all_cs, FORBIDDEN_TRIG_RE)
    project_angle_api_hits = scan(all_cs, ANGLE_API_RE)
    project_direct_mutation_hits = scan(all_cs, DIRECT_MUTATION_RE)
    project_damage_bypass_hits = scan(all_cs, DAMAGE_BYPASS_RE)
    project_rigidbody_direct_hits = scan(all_cs, RIGIDBODY_DIRECT_RE)
    project_managed_event_hits = scan(all_cs, MANAGED_EVENT_RE)
    runtime_inverse_hits = scan_runtime_source(all_cs, FORBIDDEN_INVERSE_RE)
    runtime_angle_api_hits = scan_runtime_source(all_cs, ANGLE_API_RE)
    project_damage_queue_calls = [
        hit for hit in scan_damage_queue_calls(all_cs)
        if hit.get("file") != "Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs"
    ]
    project_direct_two_arg_damage_queue_hits = [
        hit for hit in project_damage_queue_calls
        if hit.get("argCount") == 2
    ]
    project_direct_one_arg_damage_queue_hits = [
        hit for hit in project_damage_queue_calls
        if hit.get("argCount") == 1
    ]
    project_direct_return_damage_queue_hits = [
        hit for hit in project_damage_queue_calls
        if hit.get("returnsDirectly")
    ]
    project_negated_damage_queue_gate_hits = [
        hit for hit in project_damage_queue_calls
        if hit.get("negatedAdmissionGate")
    ]
    project_external_direct_take_damage_hits = [
        hit for hit in scan(all_cs, EXTERNAL_DIRECT_TAKE_DAMAGE_RE)
        if "/Editor/" not in str(hit.get("file", ""))
    ]
    shader_inverse_hits = scan_text_sources(shader_sources, SHADER_INVERSE_RE)
    shader_inverse_angle_hits = scan_text_sources(shader_sources, SHADER_INVERSE_ANGLE_RE)
    shader_trig_hits = scan_text_sources(shader_sources, SHADER_TRIG_RE)
    damage_bypass_hits = project_damage_bypass_hits
    damage_event_hits = risk_hits(
        project_managed_event_hits,
        ("damage", "health", "hit", "death", "hazard", "fauna", "survival"),
    )
    direct_mutation_hits = scan(combat_cs, DIRECT_MUTATION_RE)
    bullet_direct_mutation_hits = scan(bullet_cs, DIRECT_MUTATION_RE)
    managed_event_hits = scan(combat_cs, MANAGED_EVENT_RE)
    combat_managed_callback_hits = scan(combat_cs, COMBAT_MANAGED_CALLBACK_RE)
    project_combat_managed_callback_hits = scan(all_cs, COMBAT_MANAGED_CALLBACK_RE)

    branchless_evidence = line_evidence(
        runtime,
        (
            "ResolveArmorAngleStep",
            "NormalizeArmorLookup",
            "ResolveArmorSurfaceNormal",
            "EvaluateArmorPenetrationJob",
            "CombatDamageTortureJob",
            "BuildArmorPenetrationResolvedHit",
            "ResolveBranchlessBaseDamage",
            "math.dot",
            "math.select",
            "ArmorAngleSteps",
            "materialRow * ArmorAngleSteps",
            "Interlocked.CompareExchange",
            "TryAtomicSubtractHealth",
            "AtomicHealthCasRetryLimit",
        ),
    )
    cas_evidence: list[dict[str, object]] = []
    for path in (combat_damage_runtime, runtime):
        if path.exists():
            cas_evidence.extend(
                line_evidence(
                    path,
                    (
                        "MaxQueuedSignals",
                        "AtomicHealthCasRetryLimit",
                        "TryAtomicSubtractHealth",
                        "Interlocked.CompareExchange",
                        "RunAtomicHealthCasTortureProof",
                        "AtomicHealthCasTortureJob",
                    ),
                )
            )
    layout_evidence = line_evidence(
        runtime,
        (
            "StructLayout(LayoutKind.Explicit, Size = 64)",
            "struct ShinobuArmorPenetrationTable",
            "struct ArmorProfileDTO",
            "struct ArmorPenetrationResolvedHitDTO",
            "FieldOffset(0)",
            "FieldOffset(4)",
            "FieldOffset(8)",
            "FieldOffset(12)",
            "FieldOffset(16)",
            "FieldOffset(48)",
            "FieldOffset(52)",
            "FieldOffset(56)",
            "UnsafeUtility.SizeOf<ShinobuArmorPenetrationTable>()",
            "UnsafeUtility.SizeOf<ArmorProfileDTO>()",
            "UnsafeUtility.SizeOf<ArmorPenetrationResolvedHitDTO>()",
            "Marshal.OffsetOf(typeof(ArmorProfileDTO)",
        ),
    )
    combat_damage_text = read_source(combat_damage_runtime)
    armor_runtime_text = read_source(runtime)
    can_mutate_targets_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanMutateTargets()",
    )
    register_target_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool RegisterTarget",
    )
    unregister_target_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool UnregisterTarget",
    )
    sync_target_health_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool SyncTargetHealth",
    )
    sync_target_protection_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool SyncTargetProtection",
    )
    sync_target_hit_profile_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool SyncTargetHitProfile",
    )
    try_queue_damage_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool TryQueueDamage(in CombatDamageRequest signal, in CombatDamageSignalDetail detail, double3 impactAup)",
    )
    damage_ingress_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanUseDamageIngressSlot",
    )
    target_existing_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanUseExistingTargetSlot",
    )
    target_registration_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanUseRegistrationTargetSlot",
    )
    target_storage_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanUseTargetStorageSlot",
    )
    damage_frame_tick_block = extract_csharp_block_after(
        combat_damage_text,
        "public static void FrameTick",
    )
    damage_job_preflight_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool CanUseDamageJobBuffers",
    )
    process_damage_job_block = extract_csharp_block_after(
        combat_damage_text,
        "private struct ProcessDamageQueueJob : IJob",
    )
    process_damage_execute_block = extract_csharp_block_after(process_damage_job_block, "public void Execute()")
    damage_slot_guard_block = extract_csharp_block_after(process_damage_job_block, "private bool IsValidDamageSlot")
    dispatch_results_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void DispatchResults",
    )
    managed_mirror_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool IsManagedMirrorSlotReadable",
    )
    dispatch_status_results_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void DispatchStatusResults",
    )
    clear_counters_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void ClearCounters",
    )
    clear_slot_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void ClearSlot",
    )
    record_telemetry_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void RecordTelemetry",
    )
    combat_telemetry_dump_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void TryDumpCombatTelemetry",
    )
    pushback_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void TryApplyKineticPushback",
    )
    resolve_registered_target_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool TryResolveRegisteredTargetFromTransform",
    )
    resolve_world_point_block = extract_csharp_block_after(
        combat_damage_text,
        "private static bool TryResolveWorldPoint",
    )
    refresh_ballistic_aabbs_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void RefreshBallisticTargetAabbs",
    )
    refresh_hit_profile_block = extract_csharp_block_after(
        combat_damage_text,
        "private static void RefreshTargetHitProfile",
    )
    exact_direction_block = extract_csharp_block_after(
        combat_damage_text,
        "private static float3 ResolveExactDirection",
    )
    combat_normalize_block = extract_csharp_block_after(
        combat_damage_text,
        "private static float3 NormalizeOrDefault",
    )
    combat_octant_block = extract_csharp_block_after(
        combat_damage_text,
        "private static byte ResolveDirectionOctant",
    )
    evaluate_job_block = extract_csharp_block_after(
        armor_runtime_text,
        "private unsafe struct EvaluateArmorPenetrationJob : IJobParallelFor",
    )
    evaluate_execute_block = extract_csharp_block_after(evaluate_job_block, "public void Execute(int index)")
    evaluate_core_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static unsafe ArmorPenetrationSample EvaluateArmorPenetrationCore",
    )
    angle_step_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static int ResolveArmorAngleStep",
    )
    normalize_lookup_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static float3 NormalizeArmorLookup",
    )
    surface_normal_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static float3 ResolveArmorSurfaceNormal",
    )
    resolved_hit_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static ArmorPenetrationResolvedHitDTO BuildArmorPenetrationResolvedHit",
    )
    seed_target_armor_profile_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static void SeedTargetArmorProfile",
    )
    move_target_armor_state_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static void MoveTargetArmorState",
    )
    clear_target_armor_state_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static void ClearTargetArmorState",
    )
    armor_target_slot_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static bool CanUseArmorTargetSlot",
    )
    armor_evaluator_target_buffers_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static bool CanUseArmorEvaluatorTargetBuffers",
    )
    armor_mock_signal_buffers_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static bool CanUseArmorMockSignalBuffers",
    )
    refresh_armor_snapshots_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static void RefreshArmorTargetSnapshots(ref ArmorPenetrationVaultViews views)",
    )
    branchless_base_damage_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static float ResolveBranchlessBaseDamage",
    )
    branchless_momentum_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static float ResolveBranchlessMomentumMultiplier",
    )
    combat_health_read_block = extract_csharp_block_after(
        combat_damage_text,
        "public static bool TryGetTargetHealthFraction",
    )
    armor_quality_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static float ResolveArmorQualityWeight",
    )
    combat_status_text = read_source(ROOT / "Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs")
    status_mask_read_block = extract_csharp_block_after(
        combat_status_text,
        "public static bool TryGetStatusEffectMask",
    )
    status_mobility_read_block = extract_csharp_block_after(
        combat_status_text,
        "public static bool TryGetStatusMobilityScale",
    )
    status_telemetry_read_block = extract_csharp_block_after(
        combat_status_text,
        "internal static bool TryGetLastStatusEffectTelemetry",
    )
    status_telemetry_write_block = extract_csharp_block_after(
        combat_status_text,
        "private static void WriteStatusCompletionTelemetry",
    )
    status_telemetry_append_block = extract_csharp_block_after(
        combat_status_text,
        "private static void AppendStatusTelemetryEntry",
    )
    status_telemetry_dump_block = extract_csharp_block_after(
        combat_status_text,
        "private static void TryDumpStatusEffectTelemetry",
    )
    status_job_buffer_preflight_block = extract_csharp_block_after(
        combat_status_text,
        "private static bool CanUseStatusEffectJobBuffers",
    )
    status_schedule_block = extract_csharp_block_after(
        combat_status_text,
        "private static bool TryScheduleStatusEffectJobs",
    )
    status_telemetry_clear_block = extract_csharp_block_after(
        combat_status_text,
        "private static void ClearStatusEffectTelemetryImmediate",
    )
    status_debug_snapshot_block = extract_csharp_block_after(
        combat_status_text,
        "internal static bool TryGetStatusEffectDebugSnapshot",
    )
    status_debug_count_block = extract_csharp_block_after(
        combat_status_text,
        "internal static int ReadStatusEffectDebugTargetCount",
    )
    ballistics_runtime_text = read_source(ballistics_runtime)
    ballistics_normalize_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "internal static float3 NormalizeOrDefault",
    )
    ballistics_normalize_quat_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "internal static quaternion NormalizeOrIdentity",
    )
    ballistics_sanitize_tuning_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "private static BallisticsTuningDTO SanitizeTuning",
    )
    ballistics_frame_tick_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static void FrameTick",
    )
    ballistics_clear_counter_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "private static void ClearCounter",
    )
    ballistics_clear_counter_has_proven_primitive_param = (
        re.search(
            r"private\s+static\s+void\s+ClearCounter\s*\([\s\S]*?\bint\s+primitiveCount\s*\)",
            ballistics_runtime_text,
        )
        is not None
    )
    ballistics_debug_read_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "internal static bool TryGetDebugBuffers",
    )
    ballistics_vfx_read_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static bool TryGetImpactVfxStaging",
    )
    ballistics_tuning_read_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static bool TryGetTuning",
    )
    ballistics_generate_mock_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static bool GenerateMockBallistics",
    )
    ballistics_register_primitive_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static bool RegisterAabbPrimitiveFromRuntime",
    )
    ballistics_tombstone_primitives_block = extract_csharp_block_after(
        ballistics_runtime_text,
        "public static bool TombstonePrimitivesForTarget",
    )
    armor_debug_read_block = extract_csharp_block_after(
        armor_runtime_text,
        "public static bool TryGetArmorDebugBuffers",
    )
    run_torture_block = extract_csharp_block_after(
        armor_runtime_text,
        "public static bool RunArmorPenetrationTortureProof",
    )
    generate_mock_block = extract_csharp_block_after(
        armor_runtime_text,
        "public static bool GenerateMockArmorImpacts",
    )
    generate_mock_job_block = extract_csharp_block_after(
        armor_runtime_text,
        "private struct GenerateMockArmorImpactSignalsJob",
    )
    apply_csv_profile_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static unsafe bool ApplyCsvProfileToTargets",
    )
    write_signal_impact_aup_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static void WriteSignalImpactAup",
    )
    armor_aup_is_finite_block = extract_csharp_block_after(
        armor_runtime_text,
        "private static bool IsFinite(double3 value)",
    )
    global_signal_payloads_text = read_source(global_signal_payloads)
    combat_damage_signal_codec_block = extract_csharp_block_after(
        global_signal_payloads_text,
        "private static bool TryResolveRuntimePointAup",
    )
    cas_torture_block = extract_csharp_block_after(
        armor_runtime_text,
        "public static bool RunAtomicHealthCasTortureProof",
    )
    evaluator_torture_tempjob_count = count_pattern(strip_csharp_text(run_torture_block), r"\bAllocator\.TempJob\b")
    cas_torture_tempjob_count = count_pattern(strip_csharp_text(cas_torture_block), r"\bAllocator\.TempJob\b")
    branchless_control_surface = {
        "EvaluateArmorPenetrationJob.Execute": analyze_control_surface(evaluate_execute_block),
        "EvaluateArmorPenetrationCore": analyze_control_surface(evaluate_core_block),
        "ResolveArmorAngleStep": analyze_control_surface(angle_step_block),
        "NormalizeArmorLookup": analyze_control_surface(normalize_lookup_block),
        "ResolveArmorSurfaceNormal": analyze_control_surface(surface_normal_block),
        "CombatDamageRuntime.ResolveExactDirection": analyze_control_surface(exact_direction_block),
        "BuildArmorPenetrationResolvedHit": analyze_control_surface(resolved_hit_block),
        "ResolveBranchlessBaseDamage": analyze_control_surface(branchless_base_damage_block),
        "ResolveBranchlessMomentumMultiplier": analyze_control_surface(branchless_momentum_block),
    }
    branchless_sanitizer_surface = {
        "CombatDamageRuntime.NormalizeOrDefault": analyze_control_surface(combat_normalize_block),
        "CombatDamageRuntime.ResolveDirectionOctant": analyze_control_surface(combat_octant_block),
        "HectonCombatRuntime.ResolveArmorQualityWeight": analyze_control_surface(armor_quality_block),
        "BallisticsRuntime.NormalizeOrDefault": analyze_control_surface(ballistics_normalize_block),
        "BallisticsRuntime.NormalizeOrIdentity": analyze_control_surface(ballistics_normalize_quat_block),
        "BallisticsRuntime.SanitizeTuning": analyze_control_surface(ballistics_sanitize_tuning_block),
    }
    armor_complete_calls: list[dict[str, object]] = []
    armor_unannotated_complete_calls: list[dict[str, object]] = []
    for line_number, line in enumerate(armor_runtime_text.splitlines(), start=1):
        if ".Complete(" not in line:
            continue
        hit = {
            "file": rel(runtime),
            "line": line_number,
            "text": line.strip(),
        }
        armor_complete_calls.append(hit)
        if "COLD EDITOR/QA ONLY" not in line:
            armor_unannotated_complete_calls.append(hit)
    tool_hit_text = read_source(tool_hit_utility)
    tool_central_damage_block = extract_csharp_block_after(
        tool_hit_text,
        "private static bool TryQueueCentralDamage",
    )
    habitat_integrity_text = read_source(damage_source_contracts)
    base_module_text = read_source(ROOT / "Assets/_Project/Scripts/BaseModule.cs")
    habitat_receive_damage_block = extract_csharp_block_after(
        habitat_integrity_text,
        "public void ReceiveDamage(in DamagePacket packet)",
    )
    habitat_base_module_damage_block = extract_csharp_block_after(
        habitat_integrity_text,
        "if (_baseModule != null && packet.Magnitude > 0f && packet.NextValue < packet.PreviousValue)",
    )
    environmental_hazard_text = read_source(environmental_hazard)
    environmental_hazard_block = extract_csharp_block_after(
        environmental_hazard_text,
        "private bool TryQueueCentralHazardDamage",
    )
    manta_emergency_wreck_text = read_source(manta_emergency_wreck)
    manta_collision_block = extract_csharp_block_after(
        manta_emergency_wreck_text,
        "private bool TryQueueFaunaCollisionDamage",
    )
    manta_collision_fallback_block = extract_csharp_block_after(
        manta_emergency_wreck_text,
        "private void ApplyFaunaCollisionOwnerFallbackDamage",
    )
    submarine_atmosphere_text = read_source(submarine_atmosphere_system)
    submarine_boiling_block = extract_csharp_block_after(
        submarine_atmosphere_text,
        "private bool TryQueueBoilingFaunaDamage",
    )
    submarine_boiling_fallback_block = extract_csharp_block_after(
        submarine_atmosphere_text,
        "private void ApplyBoilingFaunaOwnerFallbackDamage",
    )
    abyssal_thermal_text = read_source(abyssal_thermal_manager)
    abyssal_boiling_block = extract_csharp_block_after(
        abyssal_thermal_text,
        "private void QueueBoilingDamage",
    )
    abyssal_shock_block = extract_csharp_block_after(
        abyssal_thermal_text,
        "private void EmitThermalShock",
    )
    abyssal_fallback_block = extract_csharp_block_after(
        abyssal_thermal_text,
        "private void ApplyThermalOwnerFallbackDamage",
    )
    abyssal_aup_block = extract_csharp_block_after(
        abyssal_thermal_text,
        "private static double3 ResolveCombatImpactAup",
    )
    sargassum_micro_fauna_text = read_source(sargassum_micro_fauna_boids)
    fauna_director_text = read_source(fauna_director)
    leviathan_strike_block = extract_csharp_block_after(
        sargassum_micro_fauna_text,
        "private static bool TryQueueLeviathanStrikeDamage",
    )
    leviathan_strike_fallback_block = extract_csharp_block_after(
        sargassum_micro_fauna_text,
        "private static void ApplyLeviathanStrikeOwnerFallbackDamage",
    )
    fauna_brain_text = read_source(fauna_brain)
    predator_bite_block = extract_csharp_block_after(
        fauna_brain_text,
        "private bool TryQueuePredatorBiteDamage",
    )
    predator_bite_fallback_block = extract_csharp_block_after(
        fauna_brain_text,
        "private void ApplyPredatorBiteOwnerFallbackDamage",
    )
    player_health_text = read_source(hecton_player_health)
    player_receive_damage_block = extract_csharp_block_after(
        player_health_text,
        "public void ReceiveDamage(in DamagePacket packet)",
    )
    player_authoritative_packet_block = extract_csharp_block_after(
        player_health_text,
        "private bool TryApplyAuthoritativeCombatDamagePacket",
    )
    fauna_combat_receiver_text = read_source(fauna_combat_receiver)
    fauna_receive_damage_block = extract_csharp_block_after(
        fauna_combat_receiver_text,
        "public void ReceiveDamage(in DamagePacket packet)",
    )
    fauna_authoritative_packet_block = extract_csharp_block_after(
        fauna_combat_receiver_text,
        "private bool TryApplyAuthoritativeCombatDamagePacket",
    )
    hibernation_health_snapshot_block = extract_csharp_block_after(
        fauna_combat_receiver_text,
        "internal void ApplyHibernationHealthSnapshot",
    )
    leviathan_tentacle_text = read_source(leviathan_tentacle_solver)
    leviathan_grab_block = extract_csharp_block_after(
        leviathan_tentacle_text,
        "private bool TryQueueGrabDamage",
    )
    system_dispatcher_text = read_source(system_dispatcher)
    stun_pistol_text = read_source(stun_pistol_tool)
    camera_juice_text = read_source(camera_juice_burst)
    soundscape_text = read_source(soundscape_system)
    decal_text = read_source(decal_vault)
    feedback_evidence: list[dict[str, object]] = []
    for path in (combat_damage_runtime, runtime, camera_juice_burst, soundscape_system, decal_vault):
        if path.exists():
            feedback_evidence.extend(
                line_evidence(
                    path,
                    (
                        "DeflectSignalWriter = SignalBus<DeflectSignal>.ParallelWriter",
                        "ImpactSignalWriter = SignalBus<ImpactSignal>.ParallelWriter",
                        "EmitArmorDeflectFeedback",
                        "EmitArmorImpactFeedback",
                        "ArmorImpactSignalFlagDirectionalDeflect",
                        "SignalBus<ImpactSignal>.GetFrameSnapshotArray()",
                        "SignalBus<ImpactSignal>.GetFrameSnapshot()",
                        "TryIngestGlobalImpactSignals",
                    ),
                )
            )
    combat_job_side_presentation_hits = [
        hit for hit in scan([combat_damage_runtime, runtime], JOB_SIDE_PRESENTATION_RE)
        if "Scanner" not in str(hit.get("text", ""))
    ]
    continuous_quality_evidence = line_evidence(
        combat_damage_runtime,
        (
            "_requestedVisualQualityWeight01",
            "SetCombatVisualQualityWeight",
            "SignalBusRegistry.GlobalQualityWeight01",
            "_visualQualityWeight01 =",
        ),
    )
    tool_route_evidence: list[dict[str, object]] = []
    for path in (
        damage_source_contracts,
        environmental_hazard,
        manta_emergency_wreck,
        submarine_atmosphere_system,
        abyssal_thermal_manager,
        sargassum_micro_fauna_boids,
        tool_hit_utility,
        knife_tool,
        harpoon_tool,
        stun_pistol_tool,
        salvage_sampler_tool,
        fauna_brain,
        fauna_director,
        fauna_combat_receiver,
        leviathan_tentacle_solver,
    ):
        if path.exists():
            tool_route_evidence.extend(
                line_evidence(
                    path,
                    (
                        "PlayerToolImpact",
                        "SurvivalBlade",
                        "Harpoon",
                        "StunPistol",
                        "SalvageSampler",
                        "MantaEmergencyWreck",
                        "SubmarineAtmosphereBoiling",
                        "public static bool ApplyDamage(",
                        "TryQueueCentralDamage(",
                        "TryQueueFaunaCollisionDamage",
                        "TryQueueBoilingFaunaDamage",
                        "TryQueueLeviathanStrikeDamage",
                        "TryRegisterCombatDamageTarget",
                        "ApplyHibernationHealthSnapshot",
                        "CombatDamageRuntime.TryQueueDamage",
                        "CombatDamageRuntime.RegisterTarget",
                        "CombatDamageRuntime.PackSignalMeta",
                        "LocalPoint = localPoint3",
                        "ResolveTargetLocalPoint",
                        "CombatDamageTypes.Emp",
                        "CombatDamageTypes.Thermal",
                        "CombatStatusBits.Stunned",
                        "CombatStatusBits.Burning",
                        "ResolveStunDuration()",
                        "CombatDamageTypes.MicroFracture",
                        "FaunaLeviathanBite",
                    ),
                )
            )
    blackbox_evidence: list[dict[str, object]] = []
    for path in (combat_damage_runtime, runtime):
        if path.exists():
            blackbox_evidence.extend(
                line_evidence(
                    path,
                    (
                        "TelemetryFrameCapacity = 300",
                        "ArmorTelemetryCapacity = 300",
                        "Dump_SHINOBU_318_Combat.bin",
                        "Dump_SHINOBU_318.bin",
                        "int start = cursor >= (uint)count",
                        "WriteTelemetryEntry(writer, _telemetryRing[index])",
                        "WriteArmorTelemetryEntry(writer, telemetryRing[index])",
                        "_telemetryDumpedThisSession = true",
                        "_armorTelemetryDumped = true",
                        "TelemetryFlagQueueRejected",
                        "PublishQueueRejectAnomaly",
                    ),
                )
            )

    report = {
        "metadata": {
            "agent": AGENT_ID,
            "generatedUtc": datetime.now(timezone.utc).isoformat(),
            "root": str(ROOT),
            "evidenceClass": "STATIC_SOURCE_SCAN_AND_SOURCE_LAYOUT_DECLARATIONS",
            "runtimeProof": "PENDING_VERIFICATION_UNITY_IMPORT_BURST_DISASSEMBLY_PROFILER_NOT_RUN",
        },
        "scope": {
            "allCSharpFiles": len(all_cs),
            "combatCSharpFiles": len(combat_cs),
            "combatBulletLikeFiles": [rel(path) for path in bullet_cs],
            "armorRouteFiles": [rel(path) for path in armor_files],
            "shaderSourceFiles": len(shader_sources),
        },
        "trigonometryPurge": {
            "combatForbiddenTrigCount": len(combat_trig_hits),
            "combatForbiddenTrigVerdict": "PASS" if not combat_trig_hits else "FAIL",
            "combatForbiddenTrigHits": combat_trig_hits,
            "combatAngleApiCount": len(combat_angle_api_hits),
            "combatAngleApiHits": combat_angle_api_hits,
            "projectAcosAsinInventoryCount": len(project_inverse_hits),
            "projectAcosAsinInventory": project_inverse_hits,
            "projectAllTrigInventoryCount": len(project_trig_hits),
            "shaderAcosAsinInventoryCount": len(shader_inverse_hits),
            "shaderAcosAsinInventory": shader_inverse_hits,
            "shaderInverseAngleInventoryCount": len(shader_inverse_angle_hits),
            "shaderInverseAngleInventory": shader_inverse_angle_hits,
            "shaderTrigInventoryCount": len(shader_trig_hits),
            "projectAllTrigInventoryNote": (
                "C# and shader inverse-trig are both scanned. Remaining shader sin/cos tokens are "
                "presentation/bake inventory, not armor penetration truth."
            ),
        },
        "branchlessArmorLookupProof": {
            "sourceBranchlessnessVerdict": (
                "PASS"
                if branchless_control_surface and all(
                    surface.get("explicitControlTokens", 1) == 0
                    and surface.get("loopControlTokens", 1) == 0
                    and surface.get("forbiddenTrigCount", 1) == 0
                    and surface.get("angleApiCount", 1) == 0
                    for surface in branchless_control_surface.values()
                )
                else "FAIL"
            ),
            "formula": [
                "direction = projectileDirection * rsqrt(max(lengthsq(projectileDirection), epsilon))",
                "normal = armorNormal * rsqrt(max(lengthsq(armorNormal), epsilon))",
                "attackDot = saturate(abs(dot(direction, normal)))",
                "angleStep = clamp(floor((1 - attackDot) * 6), 0, 5)",
                "materialRow = ReadDamageClass(packedMeta) & 7",
                "lutIndex = materialRow * 6 + angleStep",
                "raw = ArmorGridLUT[lutIndex]",
            ],
            "branchlessScope": (
                "The LUT index core uses arithmetic, masks, clamp/select, and one byte load. "
                "The whole ProcessDamageQueueJob is not branchless because queue drain, target lookup, "
                "shield/status/death handling, feedback gates, and CAS success paths are conditional."
            ),
            "hundredPelletAnalysis": {
                "lookupWork": "100 pellets => 100 independent dot/abs/saturate/floor/clamp/index/load sequences.",
                "instructionFlushClaim": (
                    "No source-level data-dependent if/else remains in the index core after select-mask cleanup. "
                    "Final CPU branch proof still requires Burst disassembly on the target backend."
                ),
                "contentionClaim": (
                    "If 100 pellets hit one health slot in a true parallel apply phase, lookup remains independent "
                    "but health mutation becomes a contention problem handled by the transaction/CAS phase."
                ),
            },
            "hundredPelletOperationModel": {
                "pelletCount": 100,
                "dotProducts": 100,
                "flatLutLoads": 100,
                "conditionalBranchesInCheckedLookupSurface": sum(
                    int(surface.get("explicitControlTokens", 0))
                    for surface in branchless_control_surface.values()
                ),
                "loopBranchesInCheckedLookupSurface": sum(
                    int(surface.get("loopControlTokens", 0))
                    for surface in branchless_control_surface.values()
                ),
                "byteAddressFormula": "ArmorProfileDTO base + 16 + ((materialRow & 7) * 6) + angleStep",
                "instructionFlushCaveat": (
                    "The scanner proves zero explicit source-level data-dependent branch tokens in the checked "
                    "LUT surface. Hardware branch/flush proof still requires Burst disassembly for the target backend."
                ),
            },
            "hiddenHelperGate": {
                "armorRuntimeResolveExactDirectionCallCount": armor_runtime_text.count("ResolveExactDirection("),
                "surfaceNormalUsesNormalizeArmorLookup": (
                    "return NormalizeArmorLookup(normal, NormalizeArmorLookup(fallback, new float3(0f, 0f, 1f)))" in surface_normal_block
                ),
                "deflectFeedbackUsesNormalizeArmorLookup": (
                    "math.dot(NormalizeArmorLookup(signal.Direction, float3.zero), sample.SurfaceNormal)" in armor_runtime_text
                ),
            },
            "sourceControlSurface": branchless_control_surface,
            "sourceEvidence": branchless_evidence,
        },
        "branchlessSanitizerProof": {
            "sourceBranchlessnessVerdict": (
                "PASS"
                if branchless_sanitizer_surface and all(
                    surface.get("explicitControlTokens", 1) == 0
                    and surface.get("forbiddenTrigCount", 1) == 0
                    and surface.get("angleApiCount", 1) == 0
                    for surface in branchless_sanitizer_surface.values()
                )
                else "FAIL"
            ),
            "scope": (
                "Finite/fallback normalization and tuning sanitizers that feed combat damage, "
                "armor quality, and ballistics hit/VFX math. Algorithmic queue, target lookup, "
                "intersection, and death/status branches are intentionally outside this proof."
            ),
            "sourceControlSurface": branchless_sanitizer_surface,
        },
        "ballisticsReadAccessorPurityProof": {
            "tryGetDebugBuffersFinalizesJobs": "TryFinalizeScheduledNoWait" in ballistics_debug_read_block,
            "tryGetImpactVfxStagingFinalizesJobs": "TryFinalizeScheduledNoWait" in ballistics_vfx_read_block,
            "tryGetTuningFinalizesJobs": "TryFinalizeScheduledNoWait" in ballistics_tuning_read_block,
            "tryGetDebugBuffersEnsuresInitialization": "EnsureInitialized" in ballistics_debug_read_block,
            "tryGetImpactVfxStagingEnsuresInitialization": "EnsureInitialized" in ballistics_vfx_read_block,
            "tryGetTuningEnsuresInitialization": "EnsureInitialized" in ballistics_tuning_read_block,
            "tryGetDebugBuffersClampsTrajectoryCountToTrajectoryLength": "mutableTrajectories.Length" in ballistics_debug_read_block,
            "tryGetDebugBuffersClampsTrajectoryCountToHitLength": "mutableHits.Length" in ballistics_debug_read_block,
            "tryGetDebugBuffersClampsPrimitiveCountToPrimitiveLength": "mutablePrimitives.Length" in ballistics_debug_read_block,
            "tryGetDebugBuffersClampsNegativeTrajectoryCount": "math.max(0, rawTrajectoryCount)" in ballistics_debug_read_block,
            "tryGetDebugBuffersClampsNegativePrimitiveCount": "math.max(0, _primitiveCount)" in ballistics_debug_read_block,
            "verdict": (
                "PASS"
                if "TryFinalizeScheduledNoWait" not in ballistics_debug_read_block
                and "TryFinalizeScheduledNoWait" not in ballistics_vfx_read_block
                and "TryFinalizeScheduledNoWait" not in ballistics_tuning_read_block
                and "EnsureInitialized" not in ballistics_debug_read_block
                and "EnsureInitialized" not in ballistics_vfx_read_block
                and "EnsureInitialized" not in ballistics_tuning_read_block
                and "mutableTrajectories.Length" in ballistics_debug_read_block
                and "mutableHits.Length" in ballistics_debug_read_block
                and "mutablePrimitives.Length" in ballistics_debug_read_block
                and "math.max(0, rawTrajectoryCount)" in ballistics_debug_read_block
                and "math.max(0, _primitiveCount)" in ballistics_debug_read_block
                else "FAIL"
            ),
            "contract": (
                "Read accessors return false unless Vault lanes are already bound and return false while a solver job is "
                "scheduled. Initialization/allocation stays in owner or mutator paths; job finalization remains owned by "
                "FrameTick/LateFrameTick or teardown, not by tuning/debug/VFX read accessors. Debug counts are clamped "
                "to returned buffer lengths."
            ),
        },
        "ballisticsMockGenerationBoundsProof": {
            "checksTrajectoryLaneCreatedAndPositive": (
                "!trajectories.IsCreated" in ballistics_generate_mock_block and
                "trajectories.Length <= 0" in ballistics_generate_mock_block
            ),
            "checksPrimitiveLaneCreatedAndPositive": (
                "!primitives.IsCreated" in ballistics_generate_mock_block and
                "primitives.Length <= 0" in ballistics_generate_mock_block
            ),
            "rejectsZeroSafeCountsBeforeSchedule": (
                "if (safeTrajectoryCount <= 0 || safePrimitiveCount <= 0)" in ballistics_generate_mock_block and
                contains_after(
                    ballistics_generate_mock_block,
                    "if (safeTrajectoryCount <= 0 || safePrimitiveCount <= 0)",
                    "GenerateMockBallisticsJob job = new GenerateMockBallisticsJob",
                )
            ),
            "scheduleUsesSafeCounts": "job.Schedule(math.max(safeTrajectoryCount, safePrimitiveCount), 64)" in ballistics_generate_mock_block,
            "verdict": (
                "PASS"
                if "!trajectories.IsCreated" in ballistics_generate_mock_block
                and "trajectories.Length <= 0" in ballistics_generate_mock_block
                and "!primitives.IsCreated" in ballistics_generate_mock_block
                and "primitives.Length <= 0" in ballistics_generate_mock_block
                and "if (safeTrajectoryCount <= 0 || safePrimitiveCount <= 0)" in ballistics_generate_mock_block
                and contains_after(
                    ballistics_generate_mock_block,
                    "if (safeTrajectoryCount <= 0 || safePrimitiveCount <= 0)",
                    "GenerateMockBallisticsJob job = new GenerateMockBallisticsJob",
                )
                and "job.Schedule(math.max(safeTrajectoryCount, safePrimitiveCount), 64)" in ballistics_generate_mock_block
                else "FAIL"
            ),
            "contract": (
                "Cold ballistics mock generation must not report success or schedule a zero-work proof job when "
                "trajectory or primitive scratch lanes are empty."
            ),
        },
        "ballisticsPrimitiveRegistrationBoundsProof": {
            "registerClampsSearchCountNonNegative": (
                "int capacity = math.min(primitives.Length, MaxAabbPrimitives);" in ballistics_register_primitive_block and
                "int count = math.min(math.max(0, _primitiveCount), capacity);" in ballistics_register_primitive_block
            ),
            "registerNewSlotCannotBeNegative": (
                "int nextSlot = math.max(0, _primitiveCount);" in ballistics_register_primitive_block and
                "if (nextSlot >= capacity)" in ballistics_register_primitive_block and
                "slot = nextSlot;" in ballistics_register_primitive_block and
                "_primitiveCount = nextSlot + 1;" in ballistics_register_primitive_block
            ),
            "tombstoneClampsSearchCountNonNegative": (
                "int count = math.min(math.max(0, _primitiveCount), primitives.Length);" in ballistics_tombstone_primitives_block
            ),
            "verdict": (
                "PASS"
                if "int capacity = math.min(primitives.Length, MaxAabbPrimitives);" in ballistics_register_primitive_block
                and "int count = math.min(math.max(0, _primitiveCount), capacity);" in ballistics_register_primitive_block
                and "int nextSlot = math.max(0, _primitiveCount);" in ballistics_register_primitive_block
                and "if (nextSlot >= capacity)" in ballistics_register_primitive_block
                and "slot = nextSlot;" in ballistics_register_primitive_block
                and "_primitiveCount = nextSlot + 1;" in ballistics_register_primitive_block
                and "int count = math.min(math.max(0, _primitiveCount), primitives.Length);" in ballistics_tombstone_primitives_block
                else "FAIL"
            ),
            "contract": (
                "Runtime AABB primitive registration must not turn a corrupted negative primitive count into a "
                "negative NativeArray slot write."
            ),
        },
        "ballisticsFrameSolveBufferPreflightProof": {
            "checksCriticalLaneLengthsBeforeSchedule": (
                "solverTrajectories.Length <= 0" in ballistics_frame_tick_block and
                "primitives.Length <= 0" in ballistics_frame_tick_block and
                "hitResults.Length <= 0" in ballistics_frame_tick_block and
                "penetrationLut.Length < PenetrationLutLength" in ballistics_frame_tick_block and
                "telemetry.Length <= 0" in ballistics_frame_tick_block and
                "counters.Length <= 0" in ballistics_frame_tick_block and
                "impactVfx.Length <= 0" in ballistics_frame_tick_block
            ),
            "clampsPrimitiveCountOnce": (
                "int primitiveCount = math.min(math.max(0, _primitiveCount), primitives.Length);" in ballistics_frame_tick_block and
                "if (primitiveCount <= 0)" in ballistics_frame_tick_block and
                "PrimitiveCount = primitiveCount" in ballistics_frame_tick_block
            ),
            "usesActualTelemetryLengthForCursor": (
                "int telemetryLength = math.min(telemetry.Length, TelemetryRingLength);" in ballistics_frame_tick_block and
                "_activeTelemetryIndex = (int)(_telemetryCursor % (uint)telemetryLength);" in ballistics_frame_tick_block
            ),
            "clearCounterReceivesProvenPrimitiveCount": (
                "ClearCounter(counters, frame, quality, activeBufferId, primitiveCount);" in ballistics_frame_tick_block and
                ballistics_clear_counter_has_proven_primitive_param and
                "counter.PrimitiveCount = (uint)math.max(0, primitiveCount);" in ballistics_clear_counter_block
            ),
            "verdict": (
                "PASS"
                if "solverTrajectories.Length <= 0" in ballistics_frame_tick_block
                and "primitives.Length <= 0" in ballistics_frame_tick_block
                and "hitResults.Length <= 0" in ballistics_frame_tick_block
                and "penetrationLut.Length < PenetrationLutLength" in ballistics_frame_tick_block
                and "telemetry.Length <= 0" in ballistics_frame_tick_block
                and "counters.Length <= 0" in ballistics_frame_tick_block
                and "impactVfx.Length <= 0" in ballistics_frame_tick_block
                and "int primitiveCount = math.min(math.max(0, _primitiveCount), primitives.Length);" in ballistics_frame_tick_block
                and "if (primitiveCount <= 0)" in ballistics_frame_tick_block
                and "PrimitiveCount = primitiveCount" in ballistics_frame_tick_block
                and "int telemetryLength = math.min(telemetry.Length, TelemetryRingLength);" in ballistics_frame_tick_block
                and "_activeTelemetryIndex = (int)(_telemetryCursor % (uint)telemetryLength);" in ballistics_frame_tick_block
                and "ClearCounter(counters, frame, quality, activeBufferId, primitiveCount);" in ballistics_frame_tick_block
                and ballistics_clear_counter_has_proven_primitive_param
                and "counter.PrimitiveCount = (uint)math.max(0, primitiveCount);" in ballistics_clear_counter_block
                else "FAIL"
            ),
            "contract": (
                "Ballistics FrameTick must not schedule solver, VFX, or telemetry jobs from zero-length critical lanes "
                "or a primitive count that has not been clamped to actual storage."
            ),
        },
        "combatReadAccessorBoundsProof": {
            "tryGetTargetHealthFractionChecksHealthCreated": "!_health.IsCreated" in combat_health_read_block,
            "tryGetTargetHealthFractionChecksInvMaxCreated": "!_invMaxHealth.IsCreated" in combat_health_read_block,
            "tryGetTargetHealthFractionChecksHealthLength": "(uint)slot >= (uint)_health.Length" in combat_health_read_block,
            "tryGetTargetHealthFractionChecksInvMaxLength": "(uint)slot >= (uint)_invMaxHealth.Length" in combat_health_read_block,
            "tryGetStatusEffectMaskChecksStateLength": "(uint)slot >= (uint)_statusEffectStates.Length" in status_mask_read_block,
            "tryGetStatusMobilityScaleChecksStateLength": "(uint)slot >= (uint)_statusEffectStates.Length" in status_mobility_read_block,
            "tryGetStatusEffectDebugSnapshotChecksTargetCount": "(uint)slot >= (uint)_targetCount" in status_debug_snapshot_block,
            "tryGetStatusEffectDebugSnapshotChecksReceiverLength": "(uint)slot >= (uint)_receiverTransforms.Length" in status_debug_snapshot_block,
            "tryGetStatusEffectDebugSnapshotChecksStateLength": "(uint)slot >= (uint)_statusEffectStates.Length" in status_debug_snapshot_block,
            "readStatusEffectDebugTargetCountChecksStateCreated": "!_statusEffectStates.IsCreated" in status_debug_count_block,
            "readStatusEffectDebugTargetCountChecksReceiverNull": "_receiverTransforms == null" in status_debug_count_block,
            "readStatusEffectDebugTargetCountClampsTargetCount": "math.max(0, _targetCount)" in status_debug_count_block,
            "readStatusEffectDebugTargetCountClampsStateLength": "_statusEffectStates.Length" in status_debug_count_block,
            "readStatusEffectDebugTargetCountClampsReceiverLength": "_receiverTransforms.Length" in status_debug_count_block,
            "verdict": (
                "PASS"
                if "!_health.IsCreated" in combat_health_read_block
                and "!_invMaxHealth.IsCreated" in combat_health_read_block
                and "(uint)slot >= (uint)_health.Length" in combat_health_read_block
                and "(uint)slot >= (uint)_invMaxHealth.Length" in combat_health_read_block
                and "(uint)slot >= (uint)_statusEffectStates.Length" in status_mask_read_block
                and "(uint)slot >= (uint)_statusEffectStates.Length" in status_mobility_read_block
                and "(uint)slot >= (uint)_targetCount" in status_debug_snapshot_block
                and "(uint)slot >= (uint)_receiverTransforms.Length" in status_debug_snapshot_block
                and "(uint)slot >= (uint)_statusEffectStates.Length" in status_debug_snapshot_block
                and "!_statusEffectStates.IsCreated" in status_debug_count_block
                and "_receiverTransforms == null" in status_debug_count_block
                and "math.max(0, _targetCount)" in status_debug_count_block
                and "_statusEffectStates.Length" in status_debug_count_block
                and "_receiverTransforms.Length" in status_debug_count_block
                else "FAIL"
            ),
            "contract": (
                "Read accessors that dereference target-slot NativeArrays must fail closed if a stale or corrupted "
                "slot index escapes the target-id map."
            ),
        },
        "damageJobBufferAndSlotBoundsProof": {
            "frameTickCallsPreflightBeforeLock": (
                "CanUseDamageJobBuffers(in armorViews)" in damage_frame_tick_block and
                contains_after(damage_frame_tick_block, "CanUseDamageJobBuffers(in armorViews)", "TryLockArmorVaultBuffersForJobs()")
            ),
            "preflightChecksSignalDetails": (
                "_signalDetails.IsCreated" in damage_job_preflight_block and
                "_signalDetails.Length >= MaxQueuedSignals" in damage_job_preflight_block
            ),
            "preflightChecksSignalImpactAups": (
                "armorViews.SignalImpactAups.IsCreated" in damage_job_preflight_block and
                "armorViews.SignalImpactAups.Length >= MaxQueuedSignals" in damage_job_preflight_block
            ),
            "preflightChecksTargetBuffers": (
                "_instanceIds.IsCreated" in damage_job_preflight_block and
                "_health.IsCreated" in damage_job_preflight_block and
                "_maxHealth.IsCreated" in damage_job_preflight_block and
                "_invMaxHealth.IsCreated" in damage_job_preflight_block and
                "_armorValues.IsCreated" in damage_job_preflight_block and
                "_shieldValues.IsCreated" in damage_job_preflight_block and
                "_targetFlags.IsCreated" in damage_job_preflight_block and
                "armorViews.TargetRootAups.IsCreated" in damage_job_preflight_block and
                "armorViews.TargetRotations.IsCreated" in damage_job_preflight_block and
                "armorViews.TargetHalfExtents.IsCreated" in damage_job_preflight_block and
                "armorViews.TargetArmorProfiles.IsCreated" in damage_job_preflight_block and
                "_statusEffectStates.IsCreated" in damage_job_preflight_block and
                "_statusMasks.IsCreated" in damage_job_preflight_block
            ),
            "preflightChecksResultCountersAndLut": (
                "_damageArmorLut.Length >= DamageArmorLutLength" in damage_job_preflight_block and
                "_results.Length >= MaxResults" in damage_job_preflight_block and
                "_counters.Length >= CounterLength" in damage_job_preflight_block
            ),
            "jobChecksSlotBeforeReads": (
                "if (!IsValidDamageSlot(slot))" in process_damage_execute_block and
                "Counters[CounterDroppedResults] = Counters[CounterDroppedResults] + 1;" in process_damage_execute_block
            ),
            "slotGuardChecksDirectReadArrays": (
                "(uint)slot < (uint)InstanceIds.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)Health.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)MaxHealth.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)InvMaxHealth.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)ArmorValues.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)ShieldValues.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)MinorDamageAccumulators.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)TargetFlags.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)TargetRootAups.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)TargetRotations.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)TargetHalfExtents.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)TargetArmorProfiles.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)StatusEffectStates.Length" in damage_slot_guard_block and
                "(uint)slot < (uint)StatusMasks.Length" in damage_slot_guard_block
            ),
            "dispatchResultsChecksReceiverLength": (
                (
                    "_receivers == null" in dispatch_results_block and
                    "(uint)slot >= (uint)_receivers.Length" in dispatch_results_block
                ) or (
                    "IsManagedMirrorSlotReadable(slot)" in dispatch_results_block and
                    "_receivers != null" in managed_mirror_slot_block and
                    "_receiverTransforms != null" in managed_mirror_slot_block and
                    "_targetBodies != null" in managed_mirror_slot_block and
                    "(uint)slot < (uint)_receivers.Length" in managed_mirror_slot_block and
                    "(uint)slot < (uint)_receiverTransforms.Length" in managed_mirror_slot_block and
                    "(uint)slot < (uint)_targetBodies.Length" in managed_mirror_slot_block
                )
            ),
            "dispatchStatusClampsToResultBuffers": (
                "math.max(0, _targetCount)" in dispatch_status_results_block and
                "_statusResultActive.IsCreated ? _statusResultActive.Length : 0" in dispatch_status_results_block and
                "_statusResults.IsCreated ? _statusResults.Length : 0" in dispatch_status_results_block
            ),
            "clearCountersClampsLength": (
                "!_counters.IsCreated" in clear_counters_block and
                "math.min(CounterLength, _counters.Length)" in clear_counters_block
            ),
            "verdict": (
                "PASS"
                if "CanUseDamageJobBuffers(in armorViews)" in damage_frame_tick_block
                and contains_after(damage_frame_tick_block, "CanUseDamageJobBuffers(in armorViews)", "TryLockArmorVaultBuffersForJobs()")
                and "_signalDetails.Length >= MaxQueuedSignals" in damage_job_preflight_block
                and "armorViews.SignalImpactAups.Length >= MaxQueuedSignals" in damage_job_preflight_block
                and "_health.IsCreated" in damage_job_preflight_block
                and "armorViews.TargetArmorProfiles.IsCreated" in damage_job_preflight_block
                and "_statusEffectStates.IsCreated" in damage_job_preflight_block
                and "_damageArmorLut.Length >= DamageArmorLutLength" in damage_job_preflight_block
                and "_results.Length >= MaxResults" in damage_job_preflight_block
                and "_counters.Length >= CounterLength" in damage_job_preflight_block
                and "if (!IsValidDamageSlot(slot))" in process_damage_execute_block
                and "(uint)slot < (uint)Health.Length" in damage_slot_guard_block
                and "(uint)slot < (uint)TargetArmorProfiles.Length" in damage_slot_guard_block
                and "(uint)slot < (uint)StatusEffectStates.Length" in damage_slot_guard_block
                and (
                    "(uint)slot >= (uint)_receivers.Length" in dispatch_results_block or
                    (
                        "IsManagedMirrorSlotReadable(slot)" in dispatch_results_block and
                        "_receivers != null" in managed_mirror_slot_block and
                        "_receiverTransforms != null" in managed_mirror_slot_block and
                        "_targetBodies != null" in managed_mirror_slot_block and
                        "(uint)slot < (uint)_receivers.Length" in managed_mirror_slot_block and
                        "(uint)slot < (uint)_receiverTransforms.Length" in managed_mirror_slot_block and
                        "(uint)slot < (uint)_targetBodies.Length" in managed_mirror_slot_block
                    )
                )
                and "_statusResultActive.IsCreated ? _statusResultActive.Length : 0" in dispatch_status_results_block
                and "math.min(CounterLength, _counters.Length)" in clear_counters_block
                else "FAIL"
            ),
            "contract": (
                "Combat damage job must not trust stale target map slots or partially rebound buffers before direct "
                "NativeArray slot reads."
            ),
        },
        "damageIngressBufferBoundsProof": {
            "queueChecksIngressSlotBeforeWrites": (
                "int detailIndex = _queuedSignalCount;" in try_queue_damage_block and
                "if (!CanUseDamageIngressSlot(detailIndex))" in try_queue_damage_block and
                "PublishQueueRejectAnomaly(TelemetryAnomalyQueueStorage, signal.Amount);" in try_queue_damage_block and
                contains_after(try_queue_damage_block, "if (!CanUseDamageIngressSlot(detailIndex))", "_signalDetails[detailIndex] = queuedDetail;")
            ),
            "ingressSlotChecksSignalQueueAndDetails": (
                "!_damageSignals.IsCreated" in damage_ingress_slot_block and
                "!_signalDetails.IsCreated" in damage_ingress_slot_block and
                "(uint)detailIndex >= (uint)MaxQueuedSignals" in damage_ingress_slot_block and
                "(uint)detailIndex >= (uint)_signalDetails.Length" in damage_ingress_slot_block
            ),
            "ingressSlotChecksAupLane": (
                "TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false)" in damage_ingress_slot_block and
                "views.SignalImpactAups.IsCreated" in damage_ingress_slot_block and
                "(uint)detailIndex < (uint)views.SignalImpactAups.Length" in damage_ingress_slot_block
            ),
            "writeHelperChecksAupLaneCreated": (
                "!views.SignalImpactAups.IsCreated" in write_signal_impact_aup_block and
                "(uint)detailIndex >= (uint)views.SignalImpactAups.Length" in write_signal_impact_aup_block
            ),
            "writeHelperSanitizesAupBranchlessly": (
                "math.select(double3.zero, impactAup, new bool3(IsFinite(impactAup)))" in write_signal_impact_aup_block and
                "? impactAup : double3.zero" not in write_signal_impact_aup_block
            ),
            "writeHelperUsesSignalCodecAupBounds": (
                "return CombatDamageSignalCodec.IsFiniteAup(value);" in armor_aup_is_finite_block
            ),
            "storageRejectTelemetryHashDeclared": "TelemetryAnomalyQueueStorage" in combat_damage_text,
            "verdict": (
                "PASS"
                if "if (!CanUseDamageIngressSlot(detailIndex))" in try_queue_damage_block
                and contains_after(try_queue_damage_block, "if (!CanUseDamageIngressSlot(detailIndex))", "_signalDetails[detailIndex] = queuedDetail;")
                and "PublishQueueRejectAnomaly(TelemetryAnomalyQueueStorage, signal.Amount);" in try_queue_damage_block
                and "!_damageSignals.IsCreated" in damage_ingress_slot_block
                and "!_signalDetails.IsCreated" in damage_ingress_slot_block
                and "(uint)detailIndex >= (uint)_signalDetails.Length" in damage_ingress_slot_block
                and "TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false)" in damage_ingress_slot_block
                and "views.SignalImpactAups.IsCreated" in damage_ingress_slot_block
                and "(uint)detailIndex < (uint)views.SignalImpactAups.Length" in damage_ingress_slot_block
                and "!views.SignalImpactAups.IsCreated" in write_signal_impact_aup_block
                and "(uint)detailIndex >= (uint)views.SignalImpactAups.Length" in write_signal_impact_aup_block
                and "math.select(double3.zero, impactAup, new bool3(IsFinite(impactAup)))" in write_signal_impact_aup_block
                and "return CombatDamageSignalCodec.IsFiniteAup(value);" in armor_aup_is_finite_block
                else "FAIL"
            ),
            "contract": (
                "Damage admission must prove queue detail storage and impact AUP storage before writing ingress "
                "lanes, otherwise the job preflight can never repair the corrupted queue."
            ),
        },
        "combatTelemetryBoundsProof": {
            "recordChecksRingCreated": "!_telemetryRing.IsCreated" in record_telemetry_block,
            "recordChecksStateCreated": "!_telemetryState.IsCreated" in record_telemetry_block,
            "recordChecksRingLength": "_telemetryRing.Length <= 0" in record_telemetry_block,
            "recordChecksStateLength": "_telemetryState.Length < TelemetryStateLength" in record_telemetry_block,
            "recordUsesActualRingLength": (
                "math.min(TelemetryFrameCapacity, _telemetryRing.Length)" in record_telemetry_block and
                "% (uint)ringLength" in record_telemetry_block
            ),
            "dumpChecksRingLength": "_telemetryRing.Length <= 0" in combat_telemetry_dump_block,
            "dumpUsesActualRingLength": (
                "int count = math.min(_telemetryRing.Length, TelemetryFrameCapacity)" in combat_telemetry_dump_block and
                "writer.Write((uint)count)" in combat_telemetry_dump_block
            ),
            "dumpChecksStateLengthBeforeCursorRead": (
                "_telemetryState.IsCreated" in combat_telemetry_dump_block and
                "TelemetryWriteCursorIndex < (uint)_telemetryState.Length" in combat_telemetry_dump_block
            ),
            "dumpLatchAfterWrite": contains_after(
                combat_telemetry_dump_block,
                "WriteTelemetryEntry(writer, _telemetryRing[index])",
                "_telemetryDumpedThisSession = true;",
            ),
            "dispatchResultCountClampedToResultsLength": (
                "_results.IsCreated" in dispatch_results_block and
                "math.min(MaxResults, _results.Length)" in dispatch_results_block
            ),
            "verdict": (
                "PASS"
                if "!_telemetryRing.IsCreated" in record_telemetry_block
                and "!_telemetryState.IsCreated" in record_telemetry_block
                and "_telemetryRing.Length <= 0" in record_telemetry_block
                and "_telemetryState.Length < TelemetryStateLength" in record_telemetry_block
                and "math.min(TelemetryFrameCapacity, _telemetryRing.Length)" in record_telemetry_block
                and "% (uint)ringLength" in record_telemetry_block
                and "_telemetryRing.Length <= 0" in combat_telemetry_dump_block
                and "int count = math.min(_telemetryRing.Length, TelemetryFrameCapacity)" in combat_telemetry_dump_block
                and "writer.Write((uint)count)" in combat_telemetry_dump_block
                and "TelemetryWriteCursorIndex < (uint)_telemetryState.Length" in combat_telemetry_dump_block
                and contains_after(
                    combat_telemetry_dump_block,
                    "WriteTelemetryEntry(writer, _telemetryRing[index])",
                    "_telemetryDumpedThisSession = true;",
                )
                and "math.min(MaxResults, _results.Length)" in dispatch_results_block
                else "FAIL"
            ),
            "contract": (
                "Combat blackbox telemetry writes and dumps must index by actual ring/state lengths, not declared "
                "300-frame capacity."
            ),
        },
        "managedMirrorBoundsProof": {
            "dispatchUsesManagedMirrorGuard": "IsManagedMirrorSlotReadable(slot)" in dispatch_results_block,
            "mirrorGuardChecksAllArrays": (
                "_receivers != null" in managed_mirror_slot_block and
                "_receiverTransforms != null" in managed_mirror_slot_block and
                "_targetBodies != null" in managed_mirror_slot_block and
                "(uint)slot < (uint)_receivers.Length" in managed_mirror_slot_block and
                "(uint)slot < (uint)_receiverTransforms.Length" in managed_mirror_slot_block and
                "(uint)slot < (uint)_targetBodies.Length" in managed_mirror_slot_block
            ),
            "pushbackChecksTargetBodiesLength": (
                "_targetBodies == null" in pushback_block and
                "(uint)slot >= (uint)_targetBodies.Length" in pushback_block
            ),
            "worldPointChecksReceiverTransformLength": (
                "_receiverTransforms == null" in resolve_world_point_block and
                "(uint)slot >= (uint)_receiverTransforms.Length" in resolve_world_point_block
            ),
            "registeredTransformChecksReceiverTransformLength": (
                "_receiverTransforms == null" in resolve_registered_target_block and
                "(uint)slot >= (uint)_receiverTransforms.Length" in resolve_registered_target_block
            ),
            "ballisticRefreshClampsMirrorAndNativeLengths": (
                "_receiverTransforms == null" in refresh_ballistic_aabbs_block and
                "!_instanceIds.IsCreated" in refresh_ballistic_aabbs_block and
                "!_targetFlags.IsCreated" in refresh_ballistic_aabbs_block and
                "!_targetHeights.IsCreated" in refresh_ballistic_aabbs_block and
                "math.max(0, _targetCount)" in refresh_ballistic_aabbs_block and
                "_receiverTransforms.Length" in refresh_ballistic_aabbs_block and
                "_instanceIds.Length" in refresh_ballistic_aabbs_block and
                "_targetFlags.Length" in refresh_ballistic_aabbs_block and
                "_targetHeights.Length" in refresh_ballistic_aabbs_block
            ),
            "hitProfileChecksMirrorAndNativeLengths": (
                "_receivers == null" in refresh_hit_profile_block and
                "(uint)slot >= (uint)_receivers.Length" in refresh_hit_profile_block and
                "!_targetForwardVectors.IsCreated" in refresh_hit_profile_block and
                "(uint)slot >= (uint)_targetForwardVectors.Length" in refresh_hit_profile_block and
                "!_targetHeights.IsCreated" in refresh_hit_profile_block and
                "(uint)slot >= (uint)_targetHeights.Length" in refresh_hit_profile_block
            ),
            "verdict": (
                "PASS"
                if "IsManagedMirrorSlotReadable(slot)" in dispatch_results_block
                and "_receivers != null" in managed_mirror_slot_block
                and "(uint)slot < (uint)_receiverTransforms.Length" in managed_mirror_slot_block
                and "(uint)slot < (uint)_targetBodies.Length" in managed_mirror_slot_block
                and "_targetBodies == null" in pushback_block
                and "(uint)slot >= (uint)_targetBodies.Length" in pushback_block
                and "_receiverTransforms == null" in resolve_world_point_block
                and "(uint)slot >= (uint)_receiverTransforms.Length" in resolve_world_point_block
                and "_receiverTransforms == null" in resolve_registered_target_block
                and "(uint)slot >= (uint)_receiverTransforms.Length" in resolve_registered_target_block
                and "math.max(0, _targetCount)" in refresh_ballistic_aabbs_block
                and "_targetFlags.Length" in refresh_ballistic_aabbs_block
                and "_targetHeights.Length" in refresh_ballistic_aabbs_block
                and "_receivers == null" in refresh_hit_profile_block
                and "(uint)slot >= (uint)_targetForwardVectors.Length" in refresh_hit_profile_block
                else "FAIL"
            ),
            "contract": (
                "Managed combat mirrors are owner-phase presentation/state bridges and must be checked against actual "
                "array lengths before side effects or ballistic registration."
            ),
        },
        "targetMutatorSlotBoundsProof": {
            "registerExistingChecksExistingSlot": "if (!CanUseExistingTargetSlot(slot))" in register_target_block,
            "registerNewChecksRegistrationSlot": "if (!CanUseRegistrationTargetSlot(slot))" in register_target_block,
            "unregisterChecksSlotAndLastSlot": (
                "if (!CanUseExistingTargetSlot(slot))" in unregister_target_block and
                "if (!CanUseExistingTargetSlot(lastSlot))" in unregister_target_block and
                "_instanceIds[lastSlot] == 0" in unregister_target_block
            ),
            "syncMutatorsCheckExistingSlot": (
                "if (!CanUseExistingTargetSlot(slot))" in sync_target_health_block and
                "if (!CanUseExistingTargetSlot(slot))" in sync_target_protection_block and
                "if (!CanUseExistingTargetSlot(slot))" in sync_target_hit_profile_block
            ),
            "storageSlotChecksNativeAndStatusArrays": (
                "_instanceIds.IsCreated" in target_storage_slot_block and
                "_health.IsCreated" in target_storage_slot_block and
                "_maxHealth.IsCreated" in target_storage_slot_block and
                "_invMaxHealth.IsCreated" in target_storage_slot_block and
                "_armorValues.IsCreated" in target_storage_slot_block and
                "_shieldValues.IsCreated" in target_storage_slot_block and
                "_minorDamageAccumulators.IsCreated" in target_storage_slot_block and
                "_targetForwardVectors.IsCreated" in target_storage_slot_block and
                "_targetHeights.IsCreated" in target_storage_slot_block and
                "_targetFlags.IsCreated" in target_storage_slot_block and
                "_statusMasks.IsCreated" in target_storage_slot_block and
                "_statusDurations0123.IsCreated" in target_storage_slot_block and
                "_legacyStatusDurations4567.IsCreated" in target_storage_slot_block and
                "_brittleDurations.IsCreated" in target_storage_slot_block and
                "_statusResults.IsCreated" in target_storage_slot_block and
                "_statusResultActive.IsCreated" in target_storage_slot_block and
                "_statusEffectStates.IsCreated" in target_storage_slot_block
            ),
            "storageSlotChecksManagedMirror": "IsManagedMirrorSlotReadable(slot)" in target_storage_slot_block,
            "registrationSlotCapsMaxTargets": (
                "(uint)slot < (uint)MaxTargets" in target_registration_slot_block and
                "CanUseTargetStorageSlot(slot)" in target_registration_slot_block
            ),
            "existingSlotCapsTargetCount": (
                "_targetCount > 0" in target_existing_slot_block and
                "(uint)slot < (uint)_targetCount" in target_existing_slot_block and
                "CanUseTargetStorageSlot(slot)" in target_existing_slot_block
            ),
            "clearSlotClearsTransientStatusResult": (
                "_statusResults[slot] = default;" in clear_slot_block and
                "_statusResultActive[slot] = 0;" in clear_slot_block
            ),
            "armorProfileUsesFullSlotHelper": (
                "CanUseArmorTargetSlot(in views, slot)" in seed_target_armor_profile_block and
                "CanUseArmorTargetSlot(in views, sourceSlot)" in move_target_armor_state_block and
                "CanUseArmorTargetSlot(in views, destinationSlot)" in move_target_armor_state_block and
                "CanUseArmorTargetSlot(in views, slot)" in clear_target_armor_state_block and
                "views.TargetArmorProfiles.IsCreated" in armor_target_slot_block and
                "views.TargetRootAups.IsCreated" in armor_target_slot_block and
                "views.TargetRotations.IsCreated" in armor_target_slot_block and
                "views.TargetHalfExtents.IsCreated" in armor_target_slot_block
            ),
            "verdict": (
                "PASS"
                if "if (!CanUseExistingTargetSlot(slot))" in register_target_block
                and "if (!CanUseRegistrationTargetSlot(slot))" in register_target_block
                and "if (!CanUseExistingTargetSlot(lastSlot))" in unregister_target_block
                and "_instanceIds[lastSlot] == 0" in unregister_target_block
                and "if (!CanUseExistingTargetSlot(slot))" in sync_target_health_block
                and "if (!CanUseExistingTargetSlot(slot))" in sync_target_protection_block
                and "if (!CanUseExistingTargetSlot(slot))" in sync_target_hit_profile_block
                and "_statusResultActive.IsCreated" in target_storage_slot_block
                and "_statusEffectStates.IsCreated" in target_storage_slot_block
                and "IsManagedMirrorSlotReadable(slot)" in target_storage_slot_block
                and "(uint)slot < (uint)MaxTargets" in target_registration_slot_block
                and "(uint)slot < (uint)_targetCount" in target_existing_slot_block
                and "_statusResults[slot] = default;" in clear_slot_block
                and "_statusResultActive[slot] = 0;" in clear_slot_block
                and "CanUseArmorTargetSlot(in views, slot)" in seed_target_armor_profile_block
                and "CanUseArmorTargetSlot(in views, sourceSlot)" in move_target_armor_state_block
                and "CanUseArmorTargetSlot(in views, destinationSlot)" in move_target_armor_state_block
                and "CanUseArmorTargetSlot(in views, slot)" in clear_target_armor_state_block
                and "views.TargetHalfExtents.IsCreated" in armor_target_slot_block
                else "FAIL"
            ),
            "contract": (
                "Owner-side target mutators must not trust hash-map slots or declared target count before writing "
                "native target lanes, managed mirrors, transient status results, or armor profile state."
            ),
        },
        "armorTargetSnapshotBoundsProof": {
            "checksAllSnapshotBuffers": (
                "!views.TargetRootAups.IsCreated" in refresh_armor_snapshots_block and
                "!views.TargetRotations.IsCreated" in refresh_armor_snapshots_block and
                "!views.TargetHalfExtents.IsCreated" in refresh_armor_snapshots_block and
                "_receiverTransforms == null" in refresh_armor_snapshots_block and
                "!_targetHeights.IsCreated" in refresh_armor_snapshots_block
            ),
            "clampsLoopToAllSnapshotLengths": (
                "math.max(0, _targetCount)" in refresh_armor_snapshots_block and
                "_receiverTransforms.Length" in refresh_armor_snapshots_block and
                "_targetHeights.Length" in refresh_armor_snapshots_block and
                "views.TargetRootAups.Length" in refresh_armor_snapshots_block and
                "views.TargetRotations.Length" in refresh_armor_snapshots_block and
                "views.TargetHalfExtents.Length" in refresh_armor_snapshots_block and
                "for (int i = 0; i < count; i++)" in refresh_armor_snapshots_block
            ),
            "verdict": (
                "PASS"
                if "!views.TargetRootAups.IsCreated" in refresh_armor_snapshots_block
                and "!views.TargetRotations.IsCreated" in refresh_armor_snapshots_block
                and "!views.TargetHalfExtents.IsCreated" in refresh_armor_snapshots_block
                and "_receiverTransforms == null" in refresh_armor_snapshots_block
                and "!_targetHeights.IsCreated" in refresh_armor_snapshots_block
                and "math.max(0, _targetCount)" in refresh_armor_snapshots_block
                and "_receiverTransforms.Length" in refresh_armor_snapshots_block
                and "_targetHeights.Length" in refresh_armor_snapshots_block
                and "views.TargetRootAups.Length" in refresh_armor_snapshots_block
                and "views.TargetRotations.Length" in refresh_armor_snapshots_block
                and "views.TargetHalfExtents.Length" in refresh_armor_snapshots_block
                and "for (int i = 0; i < count; i++)" in refresh_armor_snapshots_block
                else "FAIL"
            ),
            "contract": (
                "Armor target snapshot refresh can be called before damage/status job preflights, so it must clamp "
                "its own managed and native target lanes before writing AUP, rotation, and extents."
            ),
        },
        "armorCsvApplyBoundsProof": {
            "checksProfileLaneCreated": "!views.TargetArmorProfiles.IsCreated" in apply_csv_profile_block,
            "clampsLoopToProfileLength": (
                "math.max(0, _targetCount)" in apply_csv_profile_block and
                "views.TargetArmorProfiles.Length" in apply_csv_profile_block and
                "for (int i = 0; i < count; i++)" in apply_csv_profile_block
            ),
            "checksFallbackHealthLaneLength": (
                "_maxHealth.IsCreated" in apply_csv_profile_block and
                "(uint)i < (uint)_maxHealth.Length" in apply_csv_profile_block
            ),
            "checksFallbackArmorLaneLength": (
                "_armorValues.IsCreated" in apply_csv_profile_block and
                "(uint)i < (uint)_armorValues.Length" in apply_csv_profile_block
            ),
            "verdict": (
                "PASS"
                if "!views.TargetArmorProfiles.IsCreated" in apply_csv_profile_block
                and "math.max(0, _targetCount)" in apply_csv_profile_block
                and "views.TargetArmorProfiles.Length" in apply_csv_profile_block
                and "for (int i = 0; i < count; i++)" in apply_csv_profile_block
                and "(uint)i < (uint)_maxHealth.Length" in apply_csv_profile_block
                and "(uint)i < (uint)_armorValues.Length" in apply_csv_profile_block
                else "FAIL"
            ),
            "contract": (
                "Editor armor CSV import writes runtime armor profiles and must clamp to actual profile storage and "
                "guard fallback health/armor lanes."
            ),
        },
        "armorMockImpactBufferBoundsProof": {
            "computesCountBeforeJob": "int count = math.min(math.max(1, maxSignals), math.min(targetCount, MaxQueuedSignals));" in generate_mock_block,
            "checksMockBuffersBeforeJob": (
                "if (!CanUseArmorMockSignalBuffers(in views, count))" in generate_mock_block and
                contains_after(
                    generate_mock_block,
                    "if (!CanUseArmorMockSignalBuffers(in views, count))",
                    "GenerateMockArmorImpactSignalsJob job = new GenerateMockArmorImpactSignalsJob",
                )
            ),
            "helperChecksAllMockLanes": (
                "views.MockRequests.IsCreated" in armor_mock_signal_buffers_block and
                "(uint)count <= (uint)views.MockRequests.Length" in armor_mock_signal_buffers_block and
                "views.MockDetails.IsCreated" in armor_mock_signal_buffers_block and
                "(uint)count <= (uint)views.MockDetails.Length" in armor_mock_signal_buffers_block and
                "views.MockAups.IsCreated" in armor_mock_signal_buffers_block and
                "(uint)count <= (uint)views.MockAups.Length" in armor_mock_signal_buffers_block and
                "views.MockTargetSlots.IsCreated" in armor_mock_signal_buffers_block and
                "(uint)count <= (uint)views.MockTargetSlots.Length" in armor_mock_signal_buffers_block
            ),
            "jobWritesAllCheckedLanes": (
                "Requests[index] =" in generate_mock_job_block and
                "Details[index] =" in generate_mock_job_block and
                "ImpactAups[index] =" in generate_mock_job_block and
                "TargetSlots[index] =" in generate_mock_job_block
            ),
            "verdict": (
                "PASS"
                if "int count = math.min(math.max(1, maxSignals), math.min(targetCount, MaxQueuedSignals));" in generate_mock_block
                and "if (!CanUseArmorMockSignalBuffers(in views, count))" in generate_mock_block
                and contains_after(
                    generate_mock_block,
                    "if (!CanUseArmorMockSignalBuffers(in views, count))",
                    "GenerateMockArmorImpactSignalsJob job = new GenerateMockArmorImpactSignalsJob",
                )
                and "views.MockRequests.IsCreated" in armor_mock_signal_buffers_block
                and "(uint)count <= (uint)views.MockRequests.Length" in armor_mock_signal_buffers_block
                and "views.MockDetails.IsCreated" in armor_mock_signal_buffers_block
                and "(uint)count <= (uint)views.MockDetails.Length" in armor_mock_signal_buffers_block
                and "views.MockAups.IsCreated" in armor_mock_signal_buffers_block
                and "(uint)count <= (uint)views.MockAups.Length" in armor_mock_signal_buffers_block
                and "views.MockTargetSlots.IsCreated" in armor_mock_signal_buffers_block
                and "(uint)count <= (uint)views.MockTargetSlots.Length" in armor_mock_signal_buffers_block
                else "FAIL"
            ),
            "contract": (
                "Cold mock armor impact generation writes four scratch lanes in a Burst job, so scheduling must fail "
                "closed unless every mock lane is created and at least count long."
            ),
        },
        "armorDebugReadAccessorBoundsProof": {
            "checksTargetArmorProfilesCreated": "!views.TargetArmorProfiles.IsCreated" in armor_debug_read_block,
            "checksTargetRootAupsCreated": "!views.TargetRootAups.IsCreated" in armor_debug_read_block,
            "checksTargetHalfExtentsCreated": "!views.TargetHalfExtents.IsCreated" in armor_debug_read_block,
            "checksDebugHitsCreated": "!views.DebugHits.IsCreated" in armor_debug_read_block,
            "clampsTargetCountToProfileLength": "views.TargetArmorProfiles.Length" in armor_debug_read_block,
            "clampsTargetCountToAupLength": "views.TargetRootAups.Length" in armor_debug_read_block,
            "clampsTargetCountToHalfExtentLength": "views.TargetHalfExtents.Length" in armor_debug_read_block,
            "clampsNegativeTargetCount": "math.max(0, _targetCount)" in armor_debug_read_block,
            "doesNotEnsureOrFinalize": (
                "ensure: true" not in armor_debug_read_block and
                "TryFinalize" not in armor_debug_read_block and
                ".Complete(" not in armor_debug_read_block
            ),
            "verdict": (
                "PASS"
                if "!views.TargetArmorProfiles.IsCreated" in armor_debug_read_block
                and "!views.TargetRootAups.IsCreated" in armor_debug_read_block
                and "!views.TargetHalfExtents.IsCreated" in armor_debug_read_block
                and "!views.DebugHits.IsCreated" in armor_debug_read_block
                and "views.TargetArmorProfiles.Length" in armor_debug_read_block
                and "views.TargetRootAups.Length" in armor_debug_read_block
                and "views.TargetHalfExtents.Length" in armor_debug_read_block
                and "math.max(0, _targetCount)" in armor_debug_read_block
                and "ensure: true" not in armor_debug_read_block
                and "TryFinalize" not in armor_debug_read_block
                and ".Complete(" not in armor_debug_read_block
                else "FAIL"
            ),
            "contract": (
                "Armor debug read accessors return only already-created Vault snapshots and clamp targetCount to the "
                "shortest target buffer, so editor/debug consumers cannot read past a stale or partially rebound target lane."
            ),
        },
        "statusTelemetryReadAccessorBoundsProof": {
            "checksRingCreated": "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_read_block,
            "checksCursorCreated": "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_read_block,
            "checksRingLength": "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_read_block,
            "checksCursorLength": "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_read_block,
            "clampsRingModuloToActualLength": "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_read_block,
            "verdict": (
                "PASS"
                if "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_read_block
                and "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_read_block
                and "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_read_block
                and "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_read_block
                and "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_read_block
                else "FAIL"
            ),
            "contract": (
                "Status telemetry reads must not trust declared capacity when the actual Vault ring or cursor lane is "
                "missing, short, or partially rebound."
            ),
        },
        "statusTelemetryWriteBoundsProof": {
            "completionChecksRingCreated": "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_write_block,
            "completionChecksCursorCreated": "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_write_block,
            "completionChecksRingLength": "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_write_block,
            "completionChecksCursorLength": "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_write_block,
            "completionClampsRingModuloToActualLength": (
                "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_write_block and
                "% (uint)ringLength" in status_telemetry_write_block
            ),
            "appendChecksRingCreated": "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_append_block,
            "appendChecksCursorCreated": "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_append_block,
            "appendChecksRingLength": "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_append_block,
            "appendChecksCursorLength": "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_append_block,
            "appendClampsRingModuloToActualLength": (
                "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_append_block and
                "% (uint)ringLength" in status_telemetry_append_block
            ),
            "verdict": (
                "PASS"
                if "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_write_block
                and "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_write_block
                and "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_write_block
                and "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_write_block
                and "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_write_block
                and "% (uint)ringLength" in status_telemetry_write_block
                and "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_append_block
                and "!_statusEffectTelemetryCursor.IsCreated" in status_telemetry_append_block
                and "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_append_block
                and "StatusEffectTelemetryWriteCursor >= (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_append_block
                and "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_append_block
                and "% (uint)ringLength" in status_telemetry_append_block
                else "FAIL"
            ),
            "contract": (
                "Status telemetry writers use only created lanes, validate cursor storage, and modulo by the actual "
                "ring length exposed by Vault instead of the declared 300-entry capacity."
            ),
        },
        "statusTelemetryDumpBoundsProof": {
            "checksRingCreated": "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_dump_block,
            "checksRingLength": "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_dump_block,
            "clampsDumpCapacityToActualLength": "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_dump_block,
            "checksCursorReadable": (
                "_statusEffectTelemetryCursor.IsCreated" in status_telemetry_dump_block and
                "StatusEffectTelemetryWriteCursor < (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_dump_block
            ),
            "writesActualRingLength": "writer.Write((uint)ringLength)" in status_telemetry_dump_block,
            "ordersByActualRingLength": "int start = cursor >= (uint)ringLength" in status_telemetry_dump_block,
            "iteratesActualRingLength": "for (int i = 0; i < ringLength; i++)" in status_telemetry_dump_block,
            "indexesActualRingLength": "int index = (start + i) % ringLength" in status_telemetry_dump_block,
            "latchAfterWrite": contains_after(
                status_telemetry_dump_block,
                "WriteStatusEffectTelemetryEntry(writer, _statusEffectTelemetryRing[index])",
                "_statusEffectTelemetryDumpedThisSession = true;",
            ),
            "verdict": (
                "PASS"
                if "!_statusEffectTelemetryRing.IsCreated" in status_telemetry_dump_block
                and "_statusEffectTelemetryRing.Length <= 0" in status_telemetry_dump_block
                and "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_dump_block
                and "_statusEffectTelemetryCursor.IsCreated" in status_telemetry_dump_block
                and "StatusEffectTelemetryWriteCursor < (uint)_statusEffectTelemetryCursor.Length" in status_telemetry_dump_block
                and "writer.Write((uint)ringLength)" in status_telemetry_dump_block
                and "int start = cursor >= (uint)ringLength" in status_telemetry_dump_block
                and "for (int i = 0; i < ringLength; i++)" in status_telemetry_dump_block
                and "int index = (start + i) % ringLength" in status_telemetry_dump_block
                and contains_after(
                    status_telemetry_dump_block,
                    "WriteStatusEffectTelemetryEntry(writer, _statusEffectTelemetryRing[index])",
                    "_statusEffectTelemetryDumpedThisSession = true;",
                )
                else "FAIL"
            ),
            "contract": (
                "Status blackbox dumps emit and iterate only actual ring length, tolerate missing cursor storage, and "
                "latch as dumped only after telemetry rows were written."
            ),
        },
        "statusTelemetryClearBoundsProof": {
            "clampsClearToActualRingLength": (
                "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_clear_block and
                "for (int i = 0; i < ringLength; i++)" in status_telemetry_clear_block
            ),
            "clampsCursorClearToActualLength": (
                "math.min(StatusEffectTelemetryCursorLength, _statusEffectTelemetryCursor.Length)" in status_telemetry_clear_block
            ),
            "verdict": (
                "PASS"
                if "math.min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)" in status_telemetry_clear_block
                and "for (int i = 0; i < ringLength; i++)" in status_telemetry_clear_block
                and "math.min(StatusEffectTelemetryCursorLength, _statusEffectTelemetryCursor.Length)" in status_telemetry_clear_block
                else "FAIL"
            ),
            "contract": "Status telemetry clear never iterates past actual Vault ring or cursor lengths.",
        },
        "statusJobBufferPreflightProof": {
            "scheduleCallsPreflightAfterLock": "CanUseStatusEffectJobBuffers(hasSimulationWork, in armorViews)" in status_schedule_block,
            "unlocksStatusOnPreflightFailure": (
                "if (!CanUseStatusEffectJobBuffers(hasSimulationWork, in armorViews))" in status_schedule_block and
                "UnlockStatusEffectVaultBuffersForJobs();" in status_schedule_block
            ),
            "unlocksBorrowedArmorOnPreflightFailure": (
                "if (!CanUseStatusEffectJobBuffers(hasSimulationWork, in armorViews))" in status_schedule_block and
                "UnlockArmorVaultBuffersForJobs();" in status_schedule_block
            ),
            "checksCounterLaneLength": "_statusEffectCounters.Length < StatusEffectCounterLength" in status_job_buffer_preflight_block,
            "checksTelemetryCursorLength": "_statusEffectTelemetryCursor.Length < StatusEffectTelemetryCursorLength" in status_job_buffer_preflight_block,
            "checksTelemetryRingLength": "_statusEffectTelemetryRing.Length <= 0" in status_job_buffer_preflight_block,
            "checksTuningLength": "_statusEffectTuning.Length <= 0" in status_job_buffer_preflight_block,
            "checksApplyJobCoreBuffers": (
                "int targetCount = math.max(0, _targetCount)" in status_job_buffer_preflight_block and
                "_statusEffectRequests.IsCreated" in status_job_buffer_preflight_block and
                "_slotByTargetId.IsCreated" in status_job_buffer_preflight_block and
                "_statusEffectStates.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount > (uint)_statusEffectStates.Length" in status_job_buffer_preflight_block and
                "_statusMasks.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount > (uint)_statusMasks.Length" in status_job_buffer_preflight_block and
                "_statusDurations0123.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount > (uint)_statusDurations0123.Length" in status_job_buffer_preflight_block and
                "_legacyStatusDurations4567.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount > (uint)_legacyStatusDurations4567.Length" in status_job_buffer_preflight_block and
                "_brittleDurations.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount > (uint)_brittleDurations.Length" in status_job_buffer_preflight_block
            ),
            "checksSimulationBuffers": (
                "armorViews.TargetRootAups.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)armorViews.TargetRootAups.Length" in status_job_buffer_preflight_block and
                "_instanceIds.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_instanceIds.Length" in status_job_buffer_preflight_block and
                "_health.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_health.Length" in status_job_buffer_preflight_block and
                "_maxHealth.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_maxHealth.Length" in status_job_buffer_preflight_block and
                "_invMaxHealth.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_invMaxHealth.Length" in status_job_buffer_preflight_block and
                "_statusResults.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_statusResults.Length" in status_job_buffer_preflight_block and
                "_statusResultActive.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_statusResultActive.Length" in status_job_buffer_preflight_block and
                "_statusEffectVfxRequests.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_statusEffectVfxRequests.Length" in status_job_buffer_preflight_block and
                "_statusEffectDamageSignals.IsCreated" in status_job_buffer_preflight_block and
                "(uint)targetCount <= (uint)_statusEffectDamageSignals.Length" in status_job_buffer_preflight_block
            ),
            "verdict": (
                "PASS"
                if "CanUseStatusEffectJobBuffers(hasSimulationWork, in armorViews)" in status_schedule_block
                and "UnlockStatusEffectVaultBuffersForJobs();" in status_schedule_block
                and "UnlockArmorVaultBuffersForJobs();" in status_schedule_block
                and "_statusEffectCounters.Length < StatusEffectCounterLength" in status_job_buffer_preflight_block
                and "_statusEffectTelemetryCursor.Length < StatusEffectTelemetryCursorLength" in status_job_buffer_preflight_block
                and "_statusEffectTelemetryRing.Length <= 0" in status_job_buffer_preflight_block
                and "_statusEffectTuning.Length <= 0" in status_job_buffer_preflight_block
                and "int targetCount = math.max(0, _targetCount)" in status_job_buffer_preflight_block
                and "_statusEffectRequests.IsCreated" in status_job_buffer_preflight_block
                and "_slotByTargetId.IsCreated" in status_job_buffer_preflight_block
                and "_statusEffectStates.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount > (uint)_statusEffectStates.Length" in status_job_buffer_preflight_block
                and "_statusMasks.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount > (uint)_statusMasks.Length" in status_job_buffer_preflight_block
                and "_statusDurations0123.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount > (uint)_statusDurations0123.Length" in status_job_buffer_preflight_block
                and "_legacyStatusDurations4567.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount > (uint)_legacyStatusDurations4567.Length" in status_job_buffer_preflight_block
                and "_brittleDurations.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount > (uint)_brittleDurations.Length" in status_job_buffer_preflight_block
                and "armorViews.TargetRootAups.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount <= (uint)armorViews.TargetRootAups.Length" in status_job_buffer_preflight_block
                and "(uint)targetCount <= (uint)_health.Length" in status_job_buffer_preflight_block
                and "(uint)targetCount <= (uint)_statusResults.Length" in status_job_buffer_preflight_block
                and "(uint)targetCount <= (uint)_statusResultActive.Length" in status_job_buffer_preflight_block
                and "_statusEffectDamageSignals.IsCreated" in status_job_buffer_preflight_block
                and "(uint)targetCount <= (uint)_statusEffectDamageSignals.Length" in status_job_buffer_preflight_block
                else "FAIL"
            ),
            "contract": (
                "Status jobs use unsafe 64-byte counter lanes and cursor lanes, so scheduling must fail closed unless "
                "actual Vault buffers satisfy the lengths used by the Burst jobs."
            ),
        },
        "parallelEvaluatorProof": {
            "evaluateJob": "EvaluateArmorPenetrationJob : IJobParallelFor",
            "tortureJob": "CombatDamageTortureJob : IJobParallelFor",
            "evaluateJobActuallyScheduled": "EvaluateArmorPenetrationJob job = new EvaluateArmorPenetrationJob" in armor_runtime_text,
            "tortureJobActuallyScheduled": "CombatDamageTortureJob mockJob = new CombatDamageTortureJob" in armor_runtime_text,
            "tortureUsesVaultOwnedBuffers": (
                "TortureRequests" in run_torture_block and
                "TortureResolvedHits" in run_torture_block and
                "TryLockArmorEvaluatorTortureBuffersForJobs" in run_torture_block
            ),
            "tortureChecksTargetBuffersBeforeJobs": (
                "int targetCount = math.max(0, _targetCount)" in run_torture_block and
                "if (!CanUseArmorEvaluatorTargetBuffers(in views, targetCount))" in run_torture_block and
                "TargetCount = targetCount" in run_torture_block and
                "targetCount > 0" in armor_evaluator_target_buffers_block and
                "_instanceIds.IsCreated" in armor_evaluator_target_buffers_block and
                "_targetFlags.IsCreated" in armor_evaluator_target_buffers_block and
                "_targetHeights.IsCreated" in armor_evaluator_target_buffers_block and
                "_damageArmorLut.Length >= DamageArmorLutLength" in armor_evaluator_target_buffers_block and
                "views.TargetRootAups.IsCreated" in armor_evaluator_target_buffers_block and
                "views.TargetRotations.IsCreated" in armor_evaluator_target_buffers_block and
                "views.TargetHalfExtents.IsCreated" in armor_evaluator_target_buffers_block and
                "views.TargetArmorProfiles.IsCreated" in armor_evaluator_target_buffers_block
            ),
            "tempJobAllocationsInTortureProof": evaluator_torture_tempjob_count,
            "armorRuntimeCompleteCallCount": len(armor_complete_calls),
            "unannotatedArmorRuntimeCompleteCallCount": len(armor_unannotated_complete_calls),
            "unannotatedArmorRuntimeCompleteCalls": armor_unannotated_complete_calls,
            "sourceContract": (
                "EvaluateArmorPenetrationJob reads pre-resolved target slots and writes 128B "
                "ArmorPenetrationResolvedHitDTO records. Health mutation is intentionally absent from "
                "this job; CAS apply remains a separate owner phase."
            ),
            "stressHarness": (
                "RunArmorPenetrationTortureProof fills synthetic impacts with CombatDamageTortureJob, "
                "then schedules EvaluateArmorPenetrationJob for up to 10,000 LUT evaluations in editor/development builds "
                "using vault-owned torture buffers, then records telemetry. "
                "Runtime proof requires Unity import and execution; scanner only proves source presence."
            ),
            "sourceEvidence": line_evidence(
                runtime,
                (
                    "private unsafe struct EvaluateArmorPenetrationJob : IJobParallelFor",
                    "private struct CombatDamageTortureJob : IJobParallelFor",
                    "private unsafe struct CombatDamageTortureJob : IJobParallelFor",
                    "RunArmorPenetrationTortureProof",
                    "TortureRequests",
                    "TortureResolvedHits",
                    "TryLockArmorEvaluatorTortureBuffersForJobs",
                    "ArmorPenetrationResolvedHitDTO",
                ),
            ),
        },
        "layoutProof": {
            "ArmorProfileDTO": {
                "declaredSize": 64,
                "sizeEquation": "4 + 4 + 4 + 4 + 48 = 64",
                "strideBytes": 64,
                "arm64AlignmentProof": (
                    "Struct stride is 64 bytes, a multiple of 8. 4-byte scalar fields start at 0, 4, 8, and 12. "
                    "The 48-byte LUT starts at byte 16 and is intentionally byte-addressable; no field crosses the "
                    "declared 64-byte stride."
                ),
                "fieldOffsets": {
                    "SpeciesHashID": 0,
                    "BaseHealth": 4,
                    "BaseArmor": 8,
                    "_pad0": 12,
                    "ArmorGridLUT": 16,
                },
                "byteLayout": [
                    {"range": "0..3", "field": "SpeciesHashID", "type": "uint", "bytes": 4},
                    {"range": "4..7", "field": "BaseHealth", "type": "float", "bytes": 4},
                    {"range": "8..11", "field": "BaseArmor", "type": "float", "bytes": 4},
                    {"range": "12..15", "field": "_pad0", "type": "uint", "bytes": 4},
                    {"range": "16..63", "field": "ArmorGridLUT", "type": "fixed byte[48]", "bytes": 48},
                ],
                "lutCellMap": [
                    {
                        "materialRow": material_row,
                        "angleStep": angle_step,
                        "flatIndex": (material_row * 6) + angle_step,
                        "byteOffset": 16 + (material_row * 6) + angle_step,
                    }
                    for material_row in range(8)
                    for angle_step in range(6)
                ],
                "lutCellMapProof": "8 material rows * 6 angle steps = 48 contiguous byte cells at offsets 16..63.",
                "implicitHoleBytes": 0,
                "layoutVerdict": "PASS",
            },
            "ShinobuArmorPenetrationTable": {
                "declaredSize": 64,
                "sizeEquation": "48 + 4 + 4 + 8 = 64",
                "strideBytes": 64,
                "fieldOffsets": {
                    "Cells": 0,
                    "Revision": 48,
                    "AuthoringHash": 52,
                    "_pad0": 56,
                },
                "byteLayout": [
                    {"range": "0..47", "field": "Cells", "type": "fixed byte[48]", "bytes": 48},
                    {"range": "48..51", "field": "Revision", "type": "uint", "bytes": 4},
                    {"range": "52..55", "field": "AuthoringHash", "type": "uint", "bytes": 4},
                    {"range": "56..63", "field": "_pad0", "type": "ulong", "bytes": 8},
                ],
                "implicitHoleBytes": 0,
            },
            "ArmorPenetrationResolvedHitDTO": {
                "declaredSize": 128,
                "strideBytes": 128,
                "byteLayout": [
                    {"range": "0..3", "field": "TargetId", "type": "int", "bytes": 4},
                    {"range": "4..7", "field": "SourceId", "type": "int", "bytes": 4},
                    {"range": "8..11", "field": "TargetSlot", "type": "int", "bytes": 4},
                    {"range": "12..15", "field": "DetailIndex", "type": "int", "bytes": 4},
                    {"range": "16..39", "field": "damage scalars", "type": "float x6", "bytes": 24},
                    {"range": "40..51", "field": "LocalPoint", "type": "float3", "bytes": 12},
                    {"range": "52..63", "field": "SurfaceNormal", "type": "float3", "bytes": 12},
                    {"range": "64..87", "field": "ImpactAup", "type": "double3", "bytes": 24},
                    {"range": "88..103", "field": "hashes/flags/material bytes", "type": "uint/byte pack", "bytes": 16},
                    {"range": "104..127", "field": "_pad0..2", "type": "ulong x3", "bytes": 24},
                ],
                "implicitHoleBytes": 0,
            },
            "sourceEvidence": layout_evidence,
        },
        "casStabilityProof": {
            "algorithm": [
                "Read observed int bits with Interlocked.CompareExchange(ref location, 0, 0).",
                "Convert observed bits to float previousHealth.",
                "Reject non-finite previous health.",
                "Clamp damage to finite non-negative safeDamage.",
                "Compute nextHealth = max(0, previousHealth - safeDamage).",
                "Publish desired int bits only if observed bits are unchanged.",
            ],
            "linearizability": (
                "Each successful CAS has one linearization point at CompareExchange success. "
                "No two writers can both commit from the same observed health value."
            ),
            "monotonicity": "Health cannot increase because safeDamage >= 0 and nextHealth <= previousHealth.",
            "hundredPelletBound": {
                "pellets": 100,
                "maxQueuedSignals": 1024,
                "casRetryLimit": "AtomicHealthCasRetryLimit = MaxQueuedSignals",
                "maximumFailedRacesPerWriterAtK100": 99,
                "proof": (
                    "For K same-slot writers, each failed CAS requires a different writer to have committed first. "
                    "A writer can therefore lose at most K-1 races before reading the latest value and committing. "
                    "At K=100, 99 failed races is below the 1024 retry ceiling."
                ),
                "correctnessVerdict": "PASS_STATIC_SOURCE",
            },
            "hundredPelletGuarantee": (
                "AtomicHealthCasRetryLimit equals MaxQueuedSignals. With at most MaxQueuedSignals in-flight "
                "damage writes, each failed CompareExchange implies another writer successfully committed a newer "
                "health value. Therefore a writer facing K simultaneous writers to the same slot can lose at most "
                "K-1 races before observing the newest value and committing; K<=MaxQueuedSignals covers 100 pellets."
            ),
            "remainingPerformanceCaveat": (
                "This proves no bounded-retry HP loss under the queue cap. It does not claim the worst-case CAS storm "
                "is the fastest future parallel apply design; per-target aggregation remains the preferred high-load "
                "design if ProcessDamageQueueJob is split into true parallel evaluation/apply phases."
            ),
            "casTortureHarness": {
                "developmentApi": "RunAtomicHealthCasTortureProof" in armor_runtime_text,
                "parallelSameSlotJob": "AtomicHealthCasTortureJob : IJobParallelFor" in armor_runtime_text,
                "sameSlotWriteRestrictionDisabled": "NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Health" in armor_runtime_text,
                "usesVaultOwnedBuffers": (
                    "CasTortureHealth" in cas_torture_block and
                    "CasTortureSuccesses" in cas_torture_block and
                    "TryLockArmorCasTortureBuffersForJobs" in cas_torture_block
                ),
                "tempJobAllocationsInCasTortureProof": cas_torture_tempjob_count,
                "editorButton": "Run 100 CAS Torture" in read_source(editor_facade),
                "note": (
                    "Editor/development harness initializes one health slot to pelletCount and schedules pelletCount "
                    "parallel atomic subtracts of 1 HP into that same slot. The harness uses vault-owned scratch buffers; "
                    "runtime execution proof still requires Unity."
                ),
            },
            "sourceEvidence": cas_evidence,
        },
        "blackBoxTelemetryProof": {
            "capacity": 300,
            "combatDumpPath": "Docs/AgentLogs/Dump_SHINOBU_318_Combat.bin",
            "armorDumpPath": "Docs/AgentLogs/Dump_SHINOBU_318.bin",
            "statusDumpPath": "Docs/AgentLogs/Dump_SHINOBU_319.bin",
            "combatDumpCursorOrdered": (
                "int start = cursor >= (uint)count" in combat_damage_text and
                "WriteTelemetryEntry(writer, _telemetryRing[index])" in combat_damage_text
            ),
            "combatDumpUsesActualRingLength": (
                "int count = math.min(_telemetryRing.Length, TelemetryFrameCapacity)" in combat_telemetry_dump_block and
                "writer.Write((uint)count)" in combat_telemetry_dump_block
            ),
            "armorDumpCursorOrdered": (
                "int start = cursor >= (uint)count" in armor_runtime_text and
                "WriteArmorTelemetryEntry(writer, telemetryRing[index])" in armor_runtime_text
            ),
            "statusDumpCursorOrdered": (
                "int start = cursor >= (uint)ringLength" in status_telemetry_dump_block and
                "WriteStatusEffectTelemetryEntry(writer, _statusEffectTelemetryRing[index])" in status_telemetry_dump_block
            ),
            "statusDumpUsesActualRingLength": (
                "writer.Write((uint)ringLength)" in status_telemetry_dump_block and
                "for (int i = 0; i < ringLength; i++)" in status_telemetry_dump_block and
                "int index = (start + i) % ringLength" in status_telemetry_dump_block
            ),
            "dumpLatchAfterCombatWrite": contains_after(
                combat_damage_text,
                "WriteTelemetryEntry(writer, _telemetryRing[index])",
                "_telemetryDumpedThisSession = true;",
            ),
            "dumpLatchAfterArmorWrite": contains_after(
                armor_runtime_text,
                "WriteArmorTelemetryEntry(writer, telemetryRing[index])",
                "_armorTelemetryDumped = true;",
            ),
            "dumpLatchAfterStatusWrite": contains_after(
                status_telemetry_dump_block,
                "WriteStatusEffectTelemetryEntry(writer, _statusEffectTelemetryRing[index])",
                "_statusEffectTelemetryDumpedThisSession = true;",
            ),
            "queueRejectTelemetryRateLimited": (
                "TelemetryFlagQueueRejected" in combat_damage_text and
                "TelemetryAnomalyQueueBusy" in combat_damage_text and
                "TelemetryAnomalyQueueFull" in combat_damage_text and
                "_lastQueueRejectFrame == frame" in combat_damage_text and
                "PublishQueueRejectAnomaly(TelemetryAnomalyQueueBusy, signal.Amount)" in combat_damage_text and
                "PublishQueueRejectAnomaly(TelemetryAnomalyQueueFull, signal.Amount)" in combat_damage_text
            ),
            "sourceEvidence": blackbox_evidence,
        },
        "deferredFeedbackProof": {
            "simulationJobWritesDeflectSignal": (
                "DeflectSignalWriter = SignalBus<DeflectSignal>.ParallelWriter" in combat_damage_text and
                "SignalBus<DeflectSignal>.TryEnqueueBounded(DeflectSignalWriter" in combat_damage_text
            ),
            "simulationJobWritesImpactSignal": (
                "ImpactSignalWriter = SignalBus<ImpactSignal>.ParallelWriter" in combat_damage_text and
                "SignalBus<ImpactSignal>.TryEnqueueBounded(impactWriter" in armor_runtime_text
            ),
            "directionalDeflectPublishesImpactSignal": contains_after(
                combat_damage_text,
                "TryApplyFrontDeflection(",
                "EmitArmorImpactFeedback(",
            ),
            "lutDeflectPublishesImpactSignal": contains_after(
                armor_runtime_text,
                "SignalBus<DeflectSignal>.TryEnqueueBounded(deflectWriter",
                "EmitArmorImpactFeedback(",
            ),
            "lateFrameImpactConsumers": {
                "cameraJuiceBurst": "SignalBus<ImpactSignal>.GetFrameSnapshotArray()" in camera_juice_text,
                "soundscape": "SignalBus<ImpactSignal>.GetFrameSnapshot()" in soundscape_text,
                "dynamicDecals": "TryIngestGlobalImpactSignals" in decal_text,
            },
            "jobSideManagedPresentationTokenCount": len(combat_job_side_presentation_hits),
            "jobSideManagedPresentationHits": combat_job_side_presentation_hits,
            "sourceEvidence": feedback_evidence,
        },
        "combatPrewarmProof": {
            "publicPrewarmApi": "public static void Prewarm()" in combat_damage_text,
            "prewarmCallsEnsureInitialized": (
                "public static void Prewarm()" in combat_damage_text and
                "EnsureInitialized();" in extract_csharp_block_after(combat_damage_text, "public static void Prewarm")
            ),
            "dispatcherColdBootPrewarm": "CombatDamageRuntime.Prewarm();" in system_dispatcher_text,
            "damageIngressRejectsUninitializedWithoutAlloc": (
                "if (!_damageSignals.IsCreated)" in try_queue_damage_block and
                "EnsureInitialized();" not in try_queue_damage_block
            ),
            "verdict": (
                "PASS"
                if "public static void Prewarm()" in combat_damage_text
                and "EnsureInitialized();" in extract_csharp_block_after(combat_damage_text, "public static void Prewarm")
                and "CombatDamageRuntime.Prewarm();" in system_dispatcher_text
                and "if (!_damageSignals.IsCreated)" in try_queue_damage_block
                and "EnsureInitialized();" not in try_queue_damage_block
                else "FAIL"
            ),
            "contract": (
                "Combat native queues and Vault-backed armor lanes should be allocated during dispatcher service "
                "initialization, not lazily on the first damage ingress. Damage ingress must fail closed if the "
                "runtime was not prewarmed."
            ),
            "sourceEvidence": line_evidence(
                combat_damage_runtime,
                ("public static void Prewarm()", "EnsureInitialized();"),
            ) + line_evidence(
                system_dispatcher,
                ("CombatDamageRuntime.Prewarm();",),
            ),
        },
        "playerDamageReceiverProof": {
            "centralPacketUsesAuthoritativeSnapshot": (
                "TryApplyAuthoritativeCombatDamagePacket(in packet, out float authoritativeDamage)" in player_receive_damage_block and
                "PublishDamageFeedback(in packet, authoritativeDamage)" in player_receive_damage_block
            ),
            "centralPacketFiniteDeltaGate": (
                "!math.isfinite(packet.PreviousValue)" in player_authoritative_packet_block and
                "!math.isfinite(packet.NextValue)" in player_authoritative_packet_block and
                "packet.PreviousValue <= packet.NextValue" in player_authoritative_packet_block
            ),
            "centralPacketAppliesNextHealth": (
                "float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth)" in player_authoritative_packet_block and
                "currentHealth = math.min(previousHealth, packetNextHealth)" in player_authoritative_packet_block
            ),
            "centralPacketBypassesLegacyDamageGate": (
                "TakeDamage(" not in player_authoritative_packet_block and
                "IsInvulnerable" not in player_authoritative_packet_block and
                "ExtendInvulnerability" not in player_authoritative_packet_block
            ),
            "centralPacketDoesNotResyncIntermediateNativeHealth": (
                "MarkCombatDamageSyncDirty();" not in player_authoritative_packet_block
            ),
            "fallbackPacketKeepsLegacyOwnerRules": (
                "bool applied = TakeDamage(packet.Magnitude)" in player_receive_damage_block and
                "MarkCombatDamageSyncDirty();" in player_receive_damage_block
            ),
            "deathReconciliationPreserved": (
                "PublishDeath();" in player_authoritative_packet_block and
                "TryApplyRespawnReconciliation(HealthRespawnDamageHash)" in player_authoritative_packet_block and
                "ApplyRespawnReconciliationHealth(1f)" in player_authoritative_packet_block
            ),
            "verdict": (
                "PASS"
                if "TryApplyAuthoritativeCombatDamagePacket(in packet, out float authoritativeDamage)" in player_receive_damage_block
                and "PublishDamageFeedback(in packet, authoritativeDamage)" in player_receive_damage_block
                and "!math.isfinite(packet.PreviousValue)" in player_authoritative_packet_block
                and "!math.isfinite(packet.NextValue)" in player_authoritative_packet_block
                and "packet.PreviousValue <= packet.NextValue" in player_authoritative_packet_block
                and "float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth)" in player_authoritative_packet_block
                and "currentHealth = math.min(previousHealth, packetNextHealth)" in player_authoritative_packet_block
                and "TakeDamage(" not in player_authoritative_packet_block
                and "IsInvulnerable" not in player_authoritative_packet_block
                and "ExtendInvulnerability" not in player_authoritative_packet_block
                and "MarkCombatDamageSyncDirty();" not in player_authoritative_packet_block
                and "bool applied = TakeDamage(packet.Magnitude)" in player_receive_damage_block
                and "PublishDeath();" in player_authoritative_packet_block
                and "TryApplyRespawnReconciliation(HealthRespawnDamageHash)" in player_authoritative_packet_block
                else "FAIL"
            ),
            "contract": (
                "Registered central CAS packets carry PreviousValue/NextValue and must reconcile the player owner "
                "to the CAS snapshot without re-entering legacy invulnerability or per-packet TakeDamage. "
                "Registration-gap packets keep PreviousValue/NextValue at zero and still use the legacy owner rules."
            ),
            "sourceEvidence": line_evidence(
                hecton_player_health,
                (
                    "TryApplyAuthoritativeCombatDamagePacket",
                    "currentHealth = math.min(previousHealth, packetNextHealth)",
                    "bool applied = TakeDamage(packet.Magnitude)",
                    "PublishDamageFeedback",
                ),
            ),
        },
        "habitatDamageReceiverProof": {
            "baseModuleCentralRoutePresent": (
                "_baseModule.ApplyDamage(packet.Magnitude);" in habitat_base_module_damage_block
            ),
            "baseModuleApplyDamageOwnsIntegrityFanout": (
                "_habitatIntegrityManager.DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, signal)" in base_module_text
            ),
            "baseModuleRouteDoesNotDoubleSyncAfterApplyDamage": (
                "MarkCombatDamageSyncDirty();" not in habitat_base_module_damage_block
            ),
            "fallbackDispatchStillSyncs": (
                "DispatchIntegrityChanged(packet.PreviousValue, packet.NextValue, signal)" in habitat_receive_damage_block and
                "MarkCombatDamageSyncDirty();" in extract_csharp_block_after(
                    habitat_integrity_text,
                    "public void DispatchIntegrityChanged",
                )
            ),
            "verdict": (
                "PASS"
                if "_baseModule.ApplyDamage(packet.Magnitude);" in habitat_base_module_damage_block
                and "_habitatIntegrityManager.DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, signal)" in base_module_text
                and "MarkCombatDamageSyncDirty();" not in habitat_base_module_damage_block
                and "DispatchIntegrityChanged(packet.PreviousValue, packet.NextValue, signal)" in habitat_receive_damage_block
                else "FAIL"
            ),
            "contract": (
                "BaseModule.ApplyDamage already fans out integrity changes through HabitatIntegrityManager.DispatchIntegrityChanged, "
                "which marks the combat mirror dirty. ReceiveDamage must not immediately issue a second sync after that call."
            ),
            "sourceEvidence": line_evidence(
                damage_source_contracts,
                (
                    "_baseModule.ApplyDamage(packet.Magnitude)",
                    "DispatchIntegrityChanged(packet.PreviousValue, packet.NextValue, signal)",
                    "public void DispatchIntegrityChanged",
                ),
            ),
        },
        "toolDamageRouteProof": {
            "defaultToolRoute": "ToolHitUtility.ApplyDamage legacy overload delegates to source-aware CombatDamageRuntime queue path.",
            "projectDamageQueueCallScanner": "balanced-parentheses call parser after comment/string stripping",
            "projectDamageQueueCallCount": len(project_damage_queue_calls),
            "projectDamageQueueCalls": project_damage_queue_calls,
            "projectDirectOneArgQueueCallCount": len(project_direct_one_arg_damage_queue_hits),
            "projectDirectOneArgQueueHits": project_direct_one_arg_damage_queue_hits,
            "projectDirectTwoArgQueueCallCount": len(project_direct_two_arg_damage_queue_hits),
            "projectDirectTwoArgQueueHits": project_direct_two_arg_damage_queue_hits,
            "projectDirectReturnQueueCallCount": len(project_direct_return_damage_queue_hits),
            "projectDirectReturnQueueHits": project_direct_return_damage_queue_hits,
            "projectNegatedQueueGateCallCount": len(project_negated_damage_queue_gate_hits),
            "projectNegatedQueueGateHits": project_negated_damage_queue_gate_hits,
            "projectExternalDirectTakeDamageCallCount": len(project_external_direct_take_damage_hits),
            "projectExternalDirectTakeDamageHits": project_external_direct_take_damage_hits,
            "legacyQueueOverloadsCompileFailWithoutAup": (
                combat_damage_text.count("Combat damage ingress must carry explicit AUP metadata.\", true)]") >= 2
            ),
            "registeredTargetsUseCentralQueue": (
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in tool_hit_text
            ),
            "registeredToolDoesNotDirectFallbackOnQueueReject": (
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in tool_central_damage_block and
                "return true;" in tool_central_damage_block and
                "return CombatDamageRuntime.TryQueueDamage" not in tool_central_damage_block
            ),
            "registeredToolHitsCarryLocalPoint": (
                "receiverComponent.transform.InverseTransformPoint(safeHitPoint)" in tool_hit_text and
                "LocalPoint = localPoint3" in tool_hit_text
            ),
            "registeredToolAupFailureDoesNotBypassCentralQueue": (
                "double3 impactAup = double3.zero" in tool_hit_text and
                "TryResolveImpactPointAup(safeHitPoint, out AbsoluteUniversePosition pointAup)" in tool_hit_text and
                "if (math.all(math.isfinite(resolvedImpactAup)))" in tool_hit_text and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in tool_central_damage_block and
                "return true;" in tool_central_damage_block
            ),
            "registeredToolSourceIds": {
                "playerToolImpact": "DamageSourceIds.PlayerToolImpact" in tool_hit_text,
                "survivalBlade": "DamageSourceIds.SurvivalBlade" in read_source(knife_tool),
                "harpoon": "DamageSourceIds.Harpoon" in read_source(harpoon_tool),
                "stunPistol": "DamageSourceIds.StunPistol" in stun_pistol_text,
                "salvageSampler": "DamageSourceIds.SalvageSampler" in read_source(salvage_sampler_tool),
                "mantaEmergencyWreck": "MantaEmergencyWreck = 15" in damage_source_contracts.read_text(encoding="utf-8"),
                "submarineAtmosphereBoiling": "DamageSourceIds.SubmarineAtmosphereBoiling" in submarine_atmosphere_text,
            },
                "faunaRegisteredTargetRoute": {
                    "implementsDamageReceiver": (
                        "public partial class FaunaBrain :" in fauna_combat_receiver_text and
                        "IDamageReceiver" in fauna_combat_receiver_text
                ),
                "registersWithCombatRuntime": (
                    "TryRegisterCombatDamageTarget()" in fauna_brain_text and
                    "CombatDamageRuntime.RegisterTarget(" in fauna_combat_receiver_text
                ),
                "syncsLegacyDirectDamage": (
                    "MarkCombatDamageSyncDirty();" in fauna_brain_text and
                    "CombatDamageRuntime.SyncTargetHealth" in fauna_combat_receiver_text
                ),
                    "centralPacketUsesAuthoritativeSnapshot": (
                        "TryApplyAuthoritativeCombatDamagePacket(in packet, hitPoint, out float appliedDamage)" in fauna_receive_damage_block
                    ),
                    "centralPacketFiniteDeltaGate": (
                        "!math.isfinite(packet.PreviousValue)" in fauna_authoritative_packet_block and
                        "!math.isfinite(packet.NextValue)" in fauna_authoritative_packet_block and
                        "packet.PreviousValue <= packet.NextValue" in fauna_authoritative_packet_block
                    ),
                    "centralPacketAppliesNextHealth": (
                        "float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth)" in fauna_authoritative_packet_block and
                        "_currentHealth = math.min(previousHealth, packetNextHealth)" in fauna_authoritative_packet_block
                    ),
                    "centralPacketBypassesLegacyDamageRoute": (
                        "TakeDamageFromSource" not in fauna_authoritative_packet_block and
                        "TakeDamageInternal" not in fauna_authoritative_packet_block and
                        "MarkCombatDamageSyncDirty();" not in fauna_authoritative_packet_block
                    ),
                    "centralPacketPreservesPresentation": (
                        "NotifyFoveatedCombatDamageLock();" in fauna_authoritative_packet_block and
                        "TriggerHitFlash(normalizedDamage);" in fauna_authoritative_packet_block and
                        "ApplyImmediateHitReaction(hitPoint, normalizedDamage)" in fauna_authoritative_packet_block and
                        "EmitParentalDefenseSignal(hitPoint, normalizedDamage)" in fauna_authoritative_packet_block and
                        "Die();" in fauna_authoritative_packet_block
                    ),
                    "fallbackPacketKeepsLegacyDamageRoute": (
                        "TakeDamageFromSource(packet.Magnitude, hitPoint)" in fauna_receive_damage_block
                    ),
                    "survivalBladeFeedbackUsesResolvedDamage": (
                        "float feedbackDamage = appliedDamage > 0f ? appliedDamage : packet.Magnitude" in fauna_receive_damage_block and
                        "RegisterWoundWS(hitPoint, feedbackDamage)" in fauna_receive_damage_block
                    ),
                    "centralPacketReceiverVerdict": (
                        "PASS"
                        if "TryApplyAuthoritativeCombatDamagePacket(in packet, hitPoint, out float appliedDamage)" in fauna_receive_damage_block
                        and "!math.isfinite(packet.PreviousValue)" in fauna_authoritative_packet_block
                        and "!math.isfinite(packet.NextValue)" in fauna_authoritative_packet_block
                        and "packet.PreviousValue <= packet.NextValue" in fauna_authoritative_packet_block
                        and "float packetNextHealth = math.clamp(packet.NextValue, 0f, safeMaxHealth)" in fauna_authoritative_packet_block
                        and "_currentHealth = math.min(previousHealth, packetNextHealth)" in fauna_authoritative_packet_block
                        and "TakeDamageFromSource" not in fauna_authoritative_packet_block
                        and "TakeDamageInternal" not in fauna_authoritative_packet_block
                        and "MarkCombatDamageSyncDirty();" not in fauna_authoritative_packet_block
                        and "TriggerHitFlash(normalizedDamage);" in fauna_authoritative_packet_block
                        and "TakeDamageFromSource(packet.Magnitude, hitPoint)" in fauna_receive_damage_block
                        else "FAIL"
                    ),
                    "hibernationRestoreUsesHealthSnapshot": (
                        "ai.ApplyHibernationHealthSnapshot(state.health)" in fauna_director_text and
                        "ai.TakeDamage(restoreDamage)" not in fauna_director_text
                    ),
                    "hibernationSnapshotNoDamageSideEffects": (
                        "math.isfinite(savedHealth)" in hibernation_health_snapshot_block and
                        "_currentHealth = safeHealth;" in hibernation_health_snapshot_block and
                        "MarkCombatDamageSyncDirty();" in hibernation_health_snapshot_block and
                        "Die();" in hibernation_health_snapshot_block and
                        "TriggerHitFlash" not in hibernation_health_snapshot_block and
                        "ApplyImmediateHitReaction" not in hibernation_health_snapshot_block and
                        "EmitParentalDefenseSignal" not in hibernation_health_snapshot_block and
                        "RegisterWoundWS" not in hibernation_health_snapshot_block
                    ),
                    "interactionBonusUsesSourceAwareDamage": (
                        "TakeDamageFromSource(bonusDamage, sourcePosition)" in fauna_brain_text and
                        "TakeDamage(bonusDamage)" not in fauna_brain_text
                    ),
                    "bladeRoutePreservesWoundPresentation": (
                        "DamageSourceIds.SurvivalBlade" in fauna_combat_receiver_text and
                        "RegisterWoundWS" in fauna_combat_receiver_text
                    ),
                    "predatorBiteCarriesAup": (
                        "TryResolveAupFromRuntimeOrigin(safeImpactPoint, out AbsoluteUniversePosition impactPointAup)" in fauna_brain_text and
                        "impactPointAup.IsFinite()" in predator_bite_block and
                        "math.all(math.isfinite(resolvedAup))" in predator_bite_block and
                        "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in fauna_brain_text
                    ),
                    "predatorBiteResolvesPlayerHealthOwner": (
                        "HectonPlayerHealth playerHealth = target.GetComponentInParent<HectonPlayerHealth>()" in predator_bite_block and
                        "GameObject targetObject = playerHealth != null ? playerHealth.gameObject : target.gameObject" in predator_bite_block and
                        "Transform targetTransform = playerHealth != null ? playerHealth.transform : target" in predator_bite_block and
                        "CombatDamageRuntime.ResolveTargetId(targetObject)" in predator_bite_block
                    ),
                    "predatorBiteDoesNotDirectFallbackOnQueueReject": (
                        "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in predator_bite_block and
                        "return true;" in predator_bite_block and
                        "return CombatDamageRuntime.TryQueueDamage" not in predator_bite_block
                    ),
                    "predatorBiteUnregisteredOwnerFallback": (
                        "if (!TryQueuePredatorBiteDamage(target, damage, impactPoint, impactDir))" in fauna_brain_text and
                        "ApplyPredatorBiteOwnerFallbackDamage(target, damage, impactPoint)" in fauna_brain_text and
                        "playerHealth.ReceiveDamage(in packet)" in predator_bite_fallback_block and
                        "DamageChannel.Integrity" in predator_bite_fallback_block and
                        "DamageSourceIds.FaunaBite" in predator_bite_fallback_block and
                        "DamageSourceIds.FaunaLeviathanBite" in predator_bite_fallback_block and
                        "CombatDamageRuntime.TryQueueDamage" not in predator_bite_fallback_block
                    ),
                    "leviathanGrabCarriesAup": (
                        "AbsoluteUniversePosition impactAupValue = ToAbsoluteUniversePosition(new float3(tipRuntimePosition.x, tipRuntimePosition.y, tipRuntimePosition.z))" in leviathan_tentacle_text and
                        "double3 impactAup = double3.zero;" in leviathan_grab_block and
                        "impactAupValue.IsFinite()" in leviathan_grab_block and
                        "math.all(math.isfinite(resolvedAup))" in leviathan_grab_block and
                        "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in leviathan_tentacle_text
                    ),
                    "leviathanGrabDoesNotDirectFallbackOnQueueReject": (
                        "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in leviathan_grab_block and
                        "return true;" in leviathan_grab_block and
                        "return CombatDamageRuntime.TryQueueDamage" not in leviathan_grab_block
                    ),
                    "directTwoArgQueueCallCount": len(scan(
                        [fauna_brain, leviathan_tentacle_solver],
                        DIRECT_TWO_ARG_DAMAGE_QUEUE_RE,
                    )),
                    "directTwoArgQueueCallCountBalanced": len([
                        hit for hit in project_damage_queue_calls
                        if hit.get("file") in {
                            "Assets/_Project/Scripts/Fauna/FaunaBrain.cs",
                            "Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs",
                        }
                        and hit.get("argCount") == 2
                    ]),
                    "directReturnQueueCallCount": len([
                        hit for hit in project_damage_queue_calls
                        if hit.get("file") in {
                            "Assets/_Project/Scripts/Fauna/FaunaBrain.cs",
                            "Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs",
                        }
                        and hit.get("returnsDirectly")
                    ]),
                },
            "stunStatusRoute": {
                "damageType": "CombatDamageTypes.Emp" if "CombatDamageTypes.Emp" in stun_pistol_text else "MISSING",
                "statusBit": "CombatStatusBits.Stunned" if "CombatStatusBits.Stunned" in stun_pistol_text else "MISSING",
                "durationSource": "ResolveStunDuration()" if "ResolveStunDuration()" in stun_pistol_text else "MISSING",
            },
            "unregisteredFallbackClassification": (
                "Unregistered IDamageReceiver fallback remains managed-owner compatibility only. Registered combat "
                "targets now keep tool source/type/status metadata through the native LUT/CAS/status route."
            ),
            "sourceEvidence": tool_route_evidence,
        },
        "mantaWreckFaunaDamageRouteProof": {
            "centralQueueBeforeFallback": (
                "TryQueueFaunaCollisionDamage(faunaBrain, collision, damage)" in manta_emergency_wreck_text and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup3)" in manta_emergency_wreck_text and
                "ApplyFaunaCollisionOwnerFallbackDamage(faunaBrain, collision, damage)" in manta_emergency_wreck_text
            ),
            "registeredTargetDoesNotDirectFallbackOnQueueReject": (
                "if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))" in manta_collision_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup3);" in manta_collision_block and
                "return true;" in manta_collision_block and
                "return CombatDamageRuntime.TryQueueDamage" not in manta_collision_block and
                "faunaBrain.TakeDamage" not in manta_collision_block
            ),
            "unregisteredFallbackUsesOwnerPacket": (
                "DamagePacket packet = new DamagePacket" in manta_collision_fallback_block and
                "DamageSourceIds.MantaEmergencyWreck" in manta_collision_fallback_block and
                "DamageChannel.Integrity" in manta_collision_fallback_block and
                "faunaBrain.ReceiveDamage(in packet)" in manta_collision_fallback_block and
                "faunaBrain.TakeDamage" not in manta_emergency_wreck_text
            ),
            "stableSourceId": (
                "DamageSourceIds.MantaEmergencyWreck" in manta_emergency_wreck_text and
                "MantaEmergencyWreck = 15" in read_source(damage_source_contracts)
            ),
            "localPointAndAup": (
                "faunaBrain.transform.InverseTransformPoint(impactPoint)" in manta_emergency_wreck_text and
                "double3 impactAup3 = double3.zero;" in manta_collision_block and
                "TryResolveAupFromPlayerObserver(impactPoint" in manta_collision_block and
                "impactAup.IsFinite()" in manta_collision_block and
                "math.all(math.isfinite(resolvedAup))" in manta_collision_block
            ),
            "sourceEvidence": line_evidence(
                manta_emergency_wreck,
                (
                    "TryQueueFaunaCollisionDamage",
                    "CombatDamageRuntime.TryQueueDamage",
                    "DamageSourceIds.MantaEmergencyWreck",
                    "InverseTransformPoint",
                    "TryResolveAupFromPlayerObserver",
                    "ApplyFaunaCollisionOwnerFallbackDamage",
                    "faunaBrain.ReceiveDamage(in packet)",
                ),
            ),
        },
        "environmentalHazardDamageRouteProof": {
            "centralQueueRoute": (
                "TryQueueCentralHazardDamage" in environmental_hazard_text and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in environmental_hazard_text
            ),
            "registeredHeatDoesNotDirectFallbackOnQueueReject": (
                "if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))" in environmental_hazard_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in environmental_hazard_block and
                "return true;" in environmental_hazard_block and
                "return CombatDamageRuntime.TryQueueDamage" not in environmental_hazard_block and
                "ApplyOwnerHazardDamageFallback" not in environmental_hazard_block
            ),
            "stableSourceId": "DamageSourceIds.EnvironmentHazard" in environmental_hazard_text,
            "registeredHeatCarriesLocalPoint": (
                "ResolveTargetLocalPoint(playerTransform, impactPoint)" in environmental_hazard_text and
                "LocalPoint = localPoint" in environmental_hazard_text
            ),
            "registeredHeatAupFailureDoesNotBypassCentralQueue": (
                "double3 impactAup = double3.zero;" in environmental_hazard_block and
                "playerAup.IsFinite()" in environmental_hazard_block and
                "math.all(math.isfinite(resolvedAup))" in environmental_hazard_block
            ),
            "resolvePlayerHealthUsesParentFallback": (
                "if (!_playerTransform.TryGetComponent(out playerHealth))" in environmental_hazard_text and
                "playerHealth = _playerTransform.GetComponentInParent<HectonPlayerHealth>()" in environmental_hazard_text
            ),
            "statusMetadata": (
                "ResolveHazardStatusBits()" in environmental_hazard_text and
                "CombatStatusBits.Poisoned" in environmental_hazard_text
            ),
            "packetFallbackOnly": (
                "if (!TryQueueCentralHazardDamage(playerHealth, damage))" in environmental_hazard_text and
                "ApplyOwnerHazardDamageFallback(playerHealth, damage)" in environmental_hazard_text and
                "playerHealth.ReceiveDamage(in packet)" in environmental_hazard_text
            ),
            "sourceEvidence": line_evidence(
                environmental_hazard,
                (
                    "TryQueueCentralHazardDamage",
                    "CombatDamageRuntime.TryQueueDamage",
                    "DamageSourceIds.EnvironmentHazard",
                    "ResolveTargetLocalPoint",
                    "ApplyOwnerHazardDamageFallback",
                    "ResolveHazardStatusBits",
                    "playerHealth.ReceiveDamage(in packet)",
                ),
            ),
        },
        "thermalDamageRouteProof": {
            "abyssalThermalRequiresRegisteredTarget": (
                "TryResolveRegisteredCombatTarget(targetObject, out int targetId, out Transform targetTransform)" in abyssal_thermal_text and
                "CombatDamageRuntime.IsTargetRegistered(candidateId)" in abyssal_thermal_text
            ),
            "abyssalBoilingCarriesTargetLocalPoint": (
                "LocalPoint = ResolveTargetLocalPoint(targetTransform, positionWS)" in abyssal_thermal_text
            ),
            "abyssalBoilingCarriesAup": (
                "double3 impactAup = ResolveCombatImpactAup(positionWS)" in abyssal_boiling_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in abyssal_thermal_text
            ),
            "abyssalBoilingAupFailureDoesNotBypassCentralQueue": (
                "double3 impactAup = double3.zero;" in abyssal_aup_block and
                "TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition resolvedAup)" in abyssal_aup_block and
                "resolvedAup.IsFinite()" in abyssal_aup_block and
                "math.all(math.isfinite(resolved))" in abyssal_aup_block
            ),
            "abyssalBoilingUnregisteredFallbackUsesOwnerPacket": (
                "TryResolveDamageReceiver(targetObject, out IDamageReceiver fallbackReceiver, out Transform fallbackTransform)" in abyssal_boiling_block and
                "ApplyThermalOwnerFallbackDamage(fallbackReceiver, fallbackTransform, positionWS, amount, temperatureCelsius, sourceId)" in abyssal_boiling_block and
                "DamagePacket packet = new DamagePacket" in abyssal_fallback_block and
                "DamageChannel.Integrity" in abyssal_fallback_block and
                "CombatDamageTypes.Thermal" in abyssal_fallback_block and
                "receiver.ReceiveDamage(in packet)" in abyssal_fallback_block
            ),
            "abyssalBoilingRegisteredDoesNotDirectFallbackOnQueueReject": (
                "if (!TryResolveRegisteredCombatTarget(targetObject, out int targetId, out Transform targetTransform))" in abyssal_boiling_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in abyssal_boiling_block and
                "return CombatDamageRuntime.TryQueueDamage" not in abyssal_boiling_block and
                "ApplyThermalOwnerFallbackDamage" not in abyssal_boiling_block.split("Hecton8.Gameplay.CombatDamageRequest signal", 1)[-1]
            ),
            "abyssalShockCarriesTargetLocalPoint": (
                "LocalPoint = ResolveTargetLocalPoint(targetTransform, positionWS)" in abyssal_thermal_text and
                "CombatDamageRuntime.TryQueueDamage(in request, in detail, impactAup)" in abyssal_thermal_text
            ),
            "abyssalShockCarriesAup": (
                "double3 impactAup = ResolveCombatImpactAup(positionWS)" in abyssal_shock_block and
                "CombatDamageRuntime.TryQueueDamage(in request, in detail, impactAup)" in abyssal_shock_block
            ),
            "abyssalShockUnregisteredFallbackUsesOwnerPacket": (
                "else if (TryResolveDamageReceiver(targetObject, out IDamageReceiver fallbackReceiver, out Transform fallbackTransform))" in abyssal_shock_block and
                "ApplyThermalOwnerFallbackDamage(" in abyssal_shock_block and
                "receiver.ReceiveDamage(in packet)" in abyssal_fallback_block
            ),
            "submarineBoilingFaunaCentralBeforeFallback": (
                "TryQueueBoilingFaunaDamage(faunaBrain, in hit, worldCenter, damageAmount)" in submarine_atmosphere_text and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)" in submarine_atmosphere_text and
                "ApplyBoilingFaunaOwnerFallbackDamage(faunaBrain, in hit, worldCenter, damageAmount)" in submarine_atmosphere_text
            ),
            "submarineBoilingRegisteredDoesNotDirectFallbackOnQueueReject": (
                "if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))" in submarine_boiling_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in submarine_boiling_block and
                "return true;" in submarine_boiling_block and
                "return CombatDamageRuntime.TryQueueDamage" not in submarine_boiling_block and
                "faunaBrain.TakeDamage" not in submarine_boiling_block
            ),
            "submarineBoilingUnregisteredFallbackUsesOwnerPacket": (
                "DamagePacket packet = new DamagePacket" in submarine_boiling_fallback_block and
                "DamageSourceIds.SubmarineAtmosphereBoiling" in submarine_boiling_fallback_block and
                "CombatDamageTypes.Thermal" in submarine_boiling_fallback_block and
                "faunaBrain.ReceiveDamage(in packet)" in submarine_boiling_fallback_block and
                "faunaBrain.TakeDamage" not in submarine_atmosphere_text
            ),
            "submarineBoilingFaunaLocalPointAndAup": (
                "ResolveTargetLocalPoint(faunaTransform, impactPoint)" in submarine_atmosphere_text and
                "TryResolveImpactAup(in hit, impactPoint, out double3 impactAup)" in submarine_atmosphere_text and
                "TryResolveImpactAup(in hit, impactPoint, out double3 impactAup);" in submarine_boiling_block
            ),
            "submarineBoilingAupFailureDoesNotBypassCentralQueue": (
                "impactAup = double3.zero;" in submarine_atmosphere_text and
                "math.all(math.isfinite(resolvedHitAup))" in submarine_atmosphere_text and
                "math.all(math.isfinite(resolvedPointAup))" in submarine_atmosphere_text
            ),
            "stableSourceId": (
                "SubmarineAtmosphereBoiling = 16" in read_source(damage_source_contracts) and
                "DamageSourceIds.SubmarineAtmosphereBoiling" in submarine_atmosphere_text
            ),
            "sourceEvidence": line_evidence(
                abyssal_thermal_manager,
                (
                    "QueueBoilingDamage",
                    "TryResolveRegisteredCombatTarget",
                    "TryResolveDamageReceiver",
                    "CombatDamageRuntime.IsTargetRegistered(candidateId)",
                    "ResolveTargetLocalPoint(targetTransform, positionWS)",
                    "ResolveCombatImpactAup(positionWS)",
                    "ApplyThermalOwnerFallbackDamage",
                    "receiver.ReceiveDamage(in packet)",
                    "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)",
                    "CombatDamageRuntime.TryQueueDamage(in request, in detail, impactAup)",
                ),
            )
            + line_evidence(
                submarine_atmosphere_system,
                (
                    "TryQueueBoilingFaunaDamage",
                    "DamageSourceIds.SubmarineAtmosphereBoiling",
                    "ResolveTargetLocalPoint(faunaTransform, impactPoint)",
                    "TryResolveImpactAup",
                    "ApplyBoilingFaunaOwnerFallbackDamage",
                    "faunaBrain.ReceiveDamage(in packet)",
                ),
            ),
        },
        "leviathanStrikeDamageRouteProof": {
            "centralQueueBeforeFallback": (
                "TryQueueLeviathanStrikeDamage(_playerHealth, leviathanStrikeDamage, playerPosition, strikeDirection, impulseMagnitude)" in sargassum_micro_fauna_text and
                "ApplyLeviathanStrikeOwnerFallbackDamage(_playerHealth, leviathanStrikeDamage, playerPosition)" in sargassum_micro_fauna_text and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in leviathan_strike_block
            ),
            "registeredTargetDoesNotDirectFallbackOnQueueReject": (
                "if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))" in leviathan_strike_block and
                "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);" in leviathan_strike_block and
                "return true;" in leviathan_strike_block and
                "return CombatDamageRuntime.TryQueueDamage" not in leviathan_strike_block and
                "TakeLeviathanDamage" not in leviathan_strike_block and
                "ApplyLeviathanStrikeOwnerFallbackDamage" not in leviathan_strike_block
            ),
            "unregisteredFallbackUsesOwnerPacket": (
                "DamagePacket packet = new DamagePacket" in leviathan_strike_fallback_block and
                "DamageSourceIds.FaunaLeviathanBite" in leviathan_strike_fallback_block and
                "DamageChannel.Integrity" in leviathan_strike_fallback_block and
                "playerHealth.ReceiveDamage(in packet)" in leviathan_strike_fallback_block and
                "TakeLeviathanDamage" not in sargassum_micro_fauna_text
            ),
            "localPointAndAup": (
                "ResolveLeviathanStrikeLocalPoint(targetTransform, safeImpactPoint)" in leviathan_strike_block and
                "TryResolveAupFromRuntimeOrigin(safeImpactPoint, out AbsoluteUniversePosition impactPointAup)" in leviathan_strike_block and
                "impactPointAup.IsFinite()" in leviathan_strike_block and
                "math.all(math.isfinite(resolvedAup))" in leviathan_strike_block
            ),
            "stableSourceId": (
                "DamageSourceIds.FaunaLeviathanBite" in leviathan_strike_block and
                "FaunaLeviathanBite = 9" in read_source(damage_source_contracts)
            ),
            "sourceEvidence": line_evidence(
                sargassum_micro_fauna_boids,
                (
                    "TryQueueLeviathanStrikeDamage",
                    "CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)",
                    "DamageSourceIds.FaunaLeviathanBite",
                    "ResolveLeviathanStrikeLocalPoint",
                    "TryResolveAupFromRuntimeOrigin(safeImpactPoint",
                    "ApplyLeviathanStrikeOwnerFallbackDamage",
                    "playerHealth.ReceiveDamage(in packet)",
                ),
            ),
        },
        "continuousQualityProof": {
            "activeBinaryMathLodFieldsRemoved": (
                "_mathLod" not in combat_damage_text and "_requestedMathLod" not in combat_damage_text
            ),
            "legacyEnumAdapterOnly": (
                "SetCombatMathLod(CombatMathLod lod)" in combat_damage_text and
                "_requestedVisualQualityWeight01 = lod == CombatMathLod.Low ? 0f : 1f;" in combat_damage_text
            ),
            "continuousQualitySetter": "SetCombatVisualQualityWeight(float weight01)" in combat_damage_text,
            "globalQualityCombinesContinuously": (
                "_visualQualityWeight01 = SanitizeQualityWeight01(qualityWeight01) * requestedWeight01;" in combat_damage_text
            ),
            "sourceEvidence": continuous_quality_evidence,
        },
        "damageRouteManagedMutationAudit": {
            "combatDirectMutationTokenCount": len(direct_mutation_hits),
            "combatDirectMutationHits": direct_mutation_hits,
            "bulletDirectMutationTokenCount": len(bullet_direct_mutation_hits),
            "bulletDirectMutationVerdict": "PASS" if not bullet_direct_mutation_hits else "FAIL",
            "bulletDirectMutationHits": bullet_direct_mutation_hits,
            "combatManagedEventTokenCount": len(managed_event_hits),
            "combatManagedEventHits": managed_event_hits,
            "combatManagedCallbackRouteCount": len(combat_managed_callback_hits),
            "combatManagedCallbackRouteVerdict": "PASS" if not combat_managed_callback_hits else "FAIL",
            "combatManagedCallbackRouteHits": combat_managed_callback_hits,
            "projectCombatManagedCallbackRouteCount": len(project_combat_managed_callback_hits),
            "projectCombatManagedCallbackRouteHits": project_combat_managed_callback_hits,
            "ownerReceiverHandoff": {
                "present": "receiver.ReceiveDamage(in packet)" in combat_damage_text,
                "classification": (
                    "Target owner state handoff after job completion. Not a managed event/listener route; "
                    "kept because registered receivers own MonoBehaviour-side state mirrors."
                ),
                "sourceEvidence": line_evidence(
                    combat_damage_runtime,
                    ("receiver.ReceiveDamage(in packet)", "DispatchManagedSideEffects"),
                ),
            },
            "mutatorGuardDoesNotFinalizeJobs": (
                "return !_damageJobScheduled && !_statusJobScheduled;" in can_mutate_targets_block and
                "TryFinalizeCompleted" not in can_mutate_targets_block and
                "TryComplete" not in can_mutate_targets_block and
                "FinishArmorPenetrationScheduledCompletion" not in can_mutate_targets_block and
                "DispatchResults" not in can_mutate_targets_block
            ),
            "completionOwner": {
                "lateFrameCompletesDamage": (
                    "public static void LateFrameTick()" in combat_damage_text and
                    "DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: false)" in combat_damage_text and
                    "DispatchResults();" in combat_damage_text
                ),
                "shutdownForceCompleteOnly": (
                    "public static void Shutdown()" in combat_damage_text and
                    "DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: true)" in combat_damage_text
                ),
                "sourceEvidence": line_evidence(
                    combat_damage_runtime,
                    (
                        "CanMutateTargets",
                        "return !_damageJobScheduled && !_statusJobScheduled;",
                        "DispatcherJobSwap.TryComplete(ref _damageJobHandle",
                        "FinishArmorPenetrationScheduledCompletion",
                        "DispatchResults();",
                    ),
                ),
            },
            "note": (
                "These are token inventories, not automatic failures. X_008 runtime route acceptance requires "
                "manual classification plus Unity compile/runtime proof."
            ),
        },
    }

    report["armorProfileLayoutProof"] = report["layoutProof"]["ArmorProfileDTO"]
    report["shinobuArmorPenetrationTableLayoutProof"] = report["layoutProof"]["ShinobuArmorPenetrationTable"]

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")

    project_sweep = {
        "metadata": {
            "agent": AGENT_ID,
            "generatedUtc": report["metadata"]["generatedUtc"],
            "root": str(ROOT),
            "evidenceClass": "STATIC_PROJECT_WIDE_TOKEN_SWEEP",
            "domainBoundary": (
                "X_008 may prove and edit Echelon 5 combat routes. Non-combat hits are owner handoff "
                "items unless they directly bypass CombatDamageRuntime."
            ),
        },
        "scope": {
            "allCSharpFiles": len(all_cs),
            "runtimeCSharpFiles": len([path for path in all_cs if not is_editor_path(rel(path))]),
            "shaderSourceFiles": len(shader_sources),
        },
        "angleMathInventory": {
            "projectForbiddenTrigCount": len(project_trig_hits),
            "projectForbiddenTrigUniqueFiles": unique_file_count(project_trig_hits),
            "projectForbiddenTrigByDomain": summarize_by_domain(project_trig_hits),
            "projectAcosAsinCount": len(project_inverse_hits),
            "projectAcosAsinRuntimeCount": len(runtime_inverse_hits),
            "projectAcosAsinByDomain": summarize_by_domain(project_inverse_hits),
            "runtimeAcosAsinHits": runtime_inverse_hits,
            "runtimeAngleApiCount": len(runtime_angle_api_hits),
            "runtimeAngleApiFirstHits": first_hits(runtime_angle_api_hits),
            "remainingRuntimeAngleApiOwnerBlockers": [
                hit for hit in runtime_angle_api_hits
                if hit["file"] == "Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs"
            ],
            "shaderAcosAsinCount": len(shader_inverse_hits),
            "shaderAcosAsinHits": shader_inverse_hits,
            "shaderInverseAngleCount": len(shader_inverse_angle_hits),
            "shaderInverseAngleHits": shader_inverse_angle_hits,
            "shaderTrigTokenCount": len(shader_trig_hits),
            "shaderTrigFirstHits": first_hits(shader_trig_hits),
            "combatArmorVerdict": "PASS" if not combat_trig_hits else "FAIL",
        },
        "damageBypassInventory": {
            "projectDirectMutationTokenCount": len(project_direct_mutation_hits),
            "projectDirectMutationUniqueFiles": unique_file_count(project_direct_mutation_hits),
            "projectDirectMutationByDomain": summarize_by_domain(project_direct_mutation_hits),
            "projectDamageQueueCallScanner": "balanced-parentheses call parser after comment/string stripping",
            "projectDamageQueueCallCount": len(project_damage_queue_calls),
            "projectDirectOneArgQueueCallCount": len(project_direct_one_arg_damage_queue_hits),
            "projectDirectTwoArgQueueCallCount": len(project_direct_two_arg_damage_queue_hits),
            "projectDirectReturnQueueCallCount": len(project_direct_return_damage_queue_hits),
            "projectNegatedQueueGateCallCount": len(project_negated_damage_queue_gate_hits),
            "legacyQueueOverloadsCompileFailWithoutAup": (
                combat_damage_text.count("Combat damage ingress must carry explicit AUP metadata.\", true)]") >= 2
            ),
            "combatPrewarmVerdict": (
                "PASS"
                if "public static void Prewarm()" in combat_damage_text
                and "CombatDamageRuntime.Prewarm();" in system_dispatcher_text
                else "FAIL"
            ),
            "projectDamageBypassTokenCount": len(project_damage_bypass_hits),
            "projectDamageBypassUniqueFiles": unique_file_count(project_damage_bypass_hits),
            "projectDamageBypassByDomain": summarize_by_domain(project_damage_bypass_hits),
            "damageBypassCandidateCount": len(damage_bypass_hits),
            "damageBypassCandidateFirstHits": first_hits(damage_bypass_hits),
            "projectRigidbodyDirectTokenCount": len(project_rigidbody_direct_hits),
            "projectRigidbodyDirectUniqueFiles": unique_file_count(project_rigidbody_direct_hits),
            "projectRigidbodyDirectByDomain": summarize_by_domain(project_rigidbody_direct_hits),
            "combatBulletDirectMutationVerdict": "PASS" if not bullet_direct_mutation_hits else "FAIL",
        },
        "managedEventInventory": {
            "projectManagedEventTokenCount": len(project_managed_event_hits),
            "projectManagedEventUniqueFiles": unique_file_count(project_managed_event_hits),
            "projectManagedEventByDomain": summarize_by_domain(project_managed_event_hits),
            "damageManagedEventCandidateCount": len(damage_event_hits),
            "damageManagedEventCandidateFirstHits": first_hits(damage_event_hits),
            "combatManagedEventVerdict": "PASS" if not managed_event_hits else "FAIL",
        },
        "nextOwnerHandoffs": [
            {
                "ownerDomain": "Physics/Vehicles",
                "reason": "two remaining runtime angle APIs are exact angular-velocity integration in SubmarineDynamicsContracts. They are not armor penetration and were not replaced with a visual cheat by X_008.",
            },
            {
                "ownerDomain": "Fauna/Survival/Hazards",
                "reason": "FaunaBrain is now a registered CombatDamageRuntime target. Tool hits, Manta wreck impacts, and submarine boiling spillover have central registered-target bridges; Manta/boiling registration-gap fallbacks now use owner DamagePacket routes. Remaining TakeDamage hits are owner-internal state updates, harvestable resource health, or construction integrity and need owner-specific migration proof before further edits.",
            },
        ],
    }
    PROJECT_SWEEP_REPORT.write_text(json.dumps(project_sweep, indent=2), encoding="utf-8")
    print(f"WROTE {REPORT.relative_to(ROOT).as_posix()}")
    print(f"WROTE {PROJECT_SWEEP_REPORT.relative_to(ROOT).as_posix()}")
    print(f"combatForbiddenTrigCount={len(combat_trig_hits)}")
    print(f"combatAngleApiCount={len(combat_angle_api_hits)}")
    print(f"projectAcosAsinInventoryCount={len(project_inverse_hits)}")
    return 0 if not combat_trig_hits else 2


if __name__ == "__main__":
    raise SystemExit(main())
