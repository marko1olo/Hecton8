#!/usr/bin/env python3
"""Static guard for the HECTON-8 MapMagic hydraulic erosion source route."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"

DEFAULT_MAPMAGIC_NODE = SOURCE_ROOT / "Plugins" / "MapMagic" / "HectonHydraulicErosionMapMagicNode.cs"
DEFAULT_ANOMALY_NODE = SOURCE_ROOT / "Plugins" / "MapMagic" / "HectonAnomalyMapMagicNode.cs"
DEFAULT_SPLATMAP_NODE = SOURCE_ROOT / "Plugins" / "MapMagic" / "HectonTerrainSplatmapMapMagicNode.cs"
DEFAULT_BIOME_POSTPROCESS_NODE = SOURCE_ROOT / "Plugins" / "MapMagic" / "HectonBiomeMatrixMapMagicPostProcessNode.cs"
DEFAULT_EROSION_HARNESS = SOURCE_ROOT / "Editor" / "ErosionTestHarness.cs"
DEFAULT_ANOMALY_ENGINE = SOURCE_ROOT / "World" / "HectonAnomalyEngine.cs"
DEFAULT_EROSION_JOB = SOURCE_ROOT / "World" / "HydraulicErosionJob.cs"
DEFAULT_GRAPH_INTEGRATOR = SOURCE_ROOT / "Editor" / "PlanetaryCanvasMapMagicGraphIntegrator.cs"
DEFAULT_SOURCE_ROOT = SOURCE_ROOT
DEFAULT_MAPMAGIC_PLUGIN_ROOT = SOURCE_ROOT / "Plugins" / "MapMagic"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
    except ValueError:
        return str(path)


def add_if(condition: bool, issues: list[str], message: str) -> None:
    if condition:
        issues.append(message)


def require_contains(text: str, issues: list[str], code: str, pattern: str, description: str) -> None:
    if not re.search(pattern, text, re.MULTILINE | re.DOTALL):
        issues.append(f"{code}: missing {description}")


def require_absent(text: str, issues: list[str], code: str, pattern: str, description: str) -> None:
    if re.search(pattern, text, re.MULTILINE | re.DOTALL):
        issues.append(f"{code}: forbidden {description}")


def check_mapmagic_node(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    require_contains(
        text,
        issues,
        prefix,
        r"HydraulicErosionScheduler\.ScheduleFourPhaseSliced\s*\(",
        "direct ScheduleFourPhaseSliced call",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"ScheduleFourPhaseSlicedWithDeltaApply|NativeQueue\s*<\s*HydraulicErosionHeightDelta\s*>|heightDeltas|heightDeltaBudget|HeightDeltaQueueLabel|HeightDeltaBudgetLabel|RegisterTempJobQueue|RegisterTempJobBudget",
        "queued delta apply state in production MapMagic node",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"QueueHeightDeltas\s*=\s*(?!0\b)\d+|DeferHeightDeltaApplication\s*=\s*(?!0\b)\d+",
        "queued delta flags enabled in production MapMagic node",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.UnregisterNativeArray",
        "pointer-based NativeArray unregister",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"DisposeTracked\s*\(\s*ref\s+\w+\s*\)",
        "old one-argument DisposeTracked call",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"out\s+int\s+heightARegistrationId",
        "stable Sentinel registration id outputs",
    )
    for registration_name in (
        "heightARegistrationId",
        "heightBRegistrationId",
        "sedimentRegistrationId",
        "siltRegistrationId",
        "wearRegistrationId",
    ):
        require_contains(
            text,
            issues,
            prefix,
            rf"\b{registration_name}\b",
            f"stable Sentinel id {registration_name}",
        )
    require_contains(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.Unregister\s*\(\s*registrationId\s*\)",
        "id-based NativeArray unregister",
    )


def check_anomaly_node(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    require_absent(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.UnregisterNativeArray",
        "pointer-based NativeArray unregister",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"DisposeTracked\s*\(\s*ref\s+\w+\s*\)",
        "old one-argument DisposeTracked call",
    )
    for registration_name in (
        "heightmapRegistrationId",
        "basinMaskRegistrationId",
        "basinLipMaskRegistrationId",
        "candidateMaskRegistrationId",
        "basinRecordsRegistrationId",
        "featureRecordsRegistrationId",
        "fissureMaskRegistrationId",
        "floodHeapRegistrationId",
        "visitedStampRegistrationId",
        "acceptedCellsRegistrationId",
    ):
        require_contains(
            text,
            issues,
            prefix,
            rf"\b{registration_name}\b",
            f"stable Sentinel id {registration_name}",
        )
    require_contains(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.Unregister\s*\(\s*registrationId\s*\)",
        "id-based NativeArray unregister",
    )


def check_tempjob_id_cleanup_node(path: Path, registration_names: tuple[str, ...], issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    require_absent(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.UnregisterNativeArray",
        "pointer-based NativeArray unregister",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"DisposeTracked\s*\(\s*ref\s+\w+\s*\)",
        "old one-argument DisposeTracked call",
    )
    for registration_name in registration_names:
        require_contains(
            text,
            issues,
            prefix,
            rf"\b{registration_name}\b",
            f"stable Sentinel id {registration_name}",
        )
    require_contains(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.Unregister\s*\(\s*registrationId\s*\)",
        "id-based NativeArray unregister",
    )


def check_erosion_harness(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    require_contains(
        text,
        issues,
        prefix,
        r"QUEUED_DELTA_APPLY_QUARANTINED[\s\S]{0,360}ScheduleFourPhaseSlicedWithDeltaApply\s*\(",
        "queued delta quarantine marker before harness queued call",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.UnregisterNativeArray",
        "pointer-based NativeArray unregister",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"DisposeTracked\s*\(\s*ref\s+\w+\s*\)",
        "old one-argument DisposeTracked call",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"NativeMemorySentinel\.Unregister\s*\(\s*registrationId\s*\)",
        "id-based NativeArray unregister",
    )


def check_anomaly_engine(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    closed_index = text.find("ScheduleClosedBasinDetection")
    ridge_index = text.find("ScheduleRidgeFeatureDetection")
    add_if(closed_index < 0, issues, f"{prefix}: missing ScheduleClosedBasinDetection")
    add_if(ridge_index < 0, issues, f"{prefix}: missing ScheduleRidgeFeatureDetection")
    if closed_index >= 0:
        closed_end = ridge_index if ridge_index > closed_index else len(text)
        closed_segment = text[closed_index:closed_end]
        add_if(
            "ShouldUseEditorDirectExecution(dependency)" not in closed_segment,
            issues,
            f"{prefix}: missing closed-basin anomaly scheduling guard call",
        )
    if ridge_index >= 0:
        add_if(
            text.find("ShouldUseEditorDirectExecution(dependency)", ridge_index) < 0,
            issues,
            f"{prefix}: missing ridge anomaly scheduling guard call",
        )
    method_match = re.search(
        r"private\s+static\s+bool\s+ShouldUseEditorDirectExecution\s*\([^)]*\)\s*\{(?P<body>[\s\S]*?)\n\s*\}",
        text,
    )
    if method_match is None:
        issues.append(f"{prefix}: missing ShouldUseEditorDirectExecution method")
        return

    body = method_match.group("body")
    dependency_index = body.find("dependency.IsCompleted")
    thread_index = body.find("Thread.CurrentThread.ManagedThreadId")
    compile_index = body.find("UnityEditor.EditorApplication.isCompiling")
    update_index = body.find("UnityEditor.EditorApplication.isUpdating")
    add_if(dependency_index < 0, issues, f"{prefix}: missing dependency.IsCompleted direct-execution gate")
    add_if(thread_index < 0, issues, f"{prefix}: missing editor main-thread id check")
    add_if(compile_index < 0 or update_index < 0, issues, f"{prefix}: missing EditorApplication compile/update direct-execution decision")
    add_if(
        dependency_index >= 0 and thread_index >= 0 and dependency_index > thread_index,
        issues,
        f"{prefix}: dependency completion gate must execute before editor main-thread id check",
    )
    add_if(
        dependency_index >= 0 and compile_index >= 0 and dependency_index > compile_index,
        issues,
        f"{prefix}: dependency completion gate must execute before EditorApplication.isCompiling",
    )
    add_if(
        dependency_index >= 0 and update_index >= 0 and dependency_index > update_index,
        issues,
        f"{prefix}: dependency completion gate must execute before EditorApplication.isUpdating",
    )
    add_if(
        thread_index >= 0 and compile_index >= 0 and thread_index > compile_index,
        issues,
        f"{prefix}: thread id guard must execute before EditorApplication.isCompiling",
    )
    add_if(
        thread_index >= 0 and update_index >= 0 and thread_index > update_index,
        issues,
        f"{prefix}: thread id guard must execute before EditorApplication.isUpdating",
    )


def check_erosion_job(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)

    budget_match = re.search(
        r"SAFETY_JUSTIFICATION_PARAGRAPH_1:[\s\S]{0,1400}public\s+NativeArray\s*<\s*int\s*>\s+HeightDeltaBudget\s*;",
        text,
    )
    if budget_match is None:
        issues.append(f"{prefix}: missing documented HeightDeltaBudget field block")
    else:
        block = budget_match.group(0)
        for marker in (
            "NativeDisableContainerSafetyRestriction",
            "NativeDisableParallelForRestriction",
            "SAFETY_JUSTIFICATION_PARAGRAPH_1",
            "SAFETY_JUSTIFICATION_PARAGRAPH_2",
            "SAFETY_JUSTIFICATION_PARAGRAPH_3",
            "TryEnqueueHeightDeltaBounded",
        ):
            add_if(marker not in block, issues, f"{prefix}: HeightDeltaBudget block missing {marker}")

    require_contains(
        text,
        issues,
        prefix,
        r"TryEnqueueHeightDeltaBounded[\s\S]{0,360}!writerBudget\.IsCreated\s*\|\|\s*writerBudget\.Length\s*<\s*2",
        "writer budget IsCreated/Length guard before enqueue",
    )


def check_graph_integrator(path: Path, issues: list[str]) -> None:
    text = read_text(path)
    prefix = rel(path)
    require_contains(
        text,
        issues,
        prefix,
        r"private\s+static\s+void\s+LinkGraph\s*\(\s*Graph\s+graph\s*,\s*IOutlet\s*<\s*object\s*>\s+outlet\s*,\s*IInlet\s*<\s*object\s*>\s+inlet\s*\)[\s\S]{0,180}graph\.Link\s*\(\s*outlet\s*,\s*inlet\s*\)",
        "typed Graph.Link wrapper preventing overload ambiguity",
    )
    wrapper_match = re.search(
        r"private\s+static\s+void\s+LinkGraph\s*\([^)]*\)\s*\{[\s\S]*?\n\s*\}",
        text,
    )
    text_without_wrapper = text[: wrapper_match.start()] + text[wrapper_match.end() :] if wrapper_match else text
    if re.search(r"\bgraph\.Link\s*\(", text_without_wrapper):
        issues.append(f"{prefix}: direct graph.Link call outside LinkGraph wrapper can reintroduce overload ambiguity")

    require_contains(
        text,
        issues,
        prefix,
        r"LinkGraph\s*\(\s*graph\s*,\s*erosionNode\.erodedHeightOut\s*,\s*heightOutput\s*\)",
        "erosion output linked to height output",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"LinkGraph\s*\(\s*graph\s*,\s*erosionNode\.erodedHeightOut\s*,\s*splatNode\.heightIn\s*\)",
        "erosion output linked to splat height input",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"LinkGraph\s*\(\s*graph\s*,\s*erosionNode\.sedimentMaskOut\s*,\s*splatNode\.sedimentIn\s*\)",
        "erosion sediment linked to splat sediment input",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"LinkGraph\s*\(\s*graph\s*,\s*erosionNode\.erodedHeightOut\s*,\s*anomalyNode\.heightIn\s*\)",
        "erosion output linked to anomaly height input",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"LinkGraph\s*\(\s*graph\s*,\s*anomalyNode\.brineMaskOut\s*,\s*layer\s*\)",
        "anomaly brine mask linked to mud texture layer",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"erosionNode\.enabled\s*=\s*true",
        "erosion node enabled in production recovery defaults",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"anomalyNode\.enabled\s*=\s*true",
        "anomaly node enabled in production recovery defaults",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"ResolveHeightSource\s*\(\s*graph\s*,\s*heightOutput\s*,\s*tectonicNode\s*,\s*erosionNode\s*,\s*splatNode\s*,\s*anomalyNode\s*\)",
        "anomaly node excluded from upstream height source resolution",
    )
    require_contains(
        text,
        issues,
        prefix,
        r"generator\s*!=\s*anomalyNode",
        "anomaly node rejected as upstream height source",
    )
    require_absent(
        text,
        issues,
        prefix,
        r"graph\.Link\s*\(\s*null\s*,\s*anomalyNode\.heightIn\s*\)|anomalyNode\.enabled\s*=\s*false|erosionNode\.enabled\s*=\s*false",
        "diagnostic disabled erosion/anomaly graph bypass",
    )


def check_queued_delta_callers(source_root: Path, erosion_job: Path, erosion_harness: Path, issues: list[str]) -> None:
    if not source_root.exists():
        issues.append(f"{rel(source_root)}: missing source root for queued delta caller scan")
        return

    allowed = {erosion_job.resolve(), erosion_harness.resolve()}
    for path in source_root.rglob("*.cs"):
        try:
            resolved = path.resolve()
        except OSError:
            continue
        text = read_text(path)
        if "ScheduleFourPhaseSlicedWithDeltaApply" not in text:
            continue
        if resolved in allowed:
            continue
        issues.append(f"{rel(path)}: forbidden ScheduleFourPhaseSlicedWithDeltaApply caller outside scheduler/harness quarantine")


def check_mapmagic_plugin_cleanup_scan(plugin_root: Path, issues: list[str]) -> None:
    if not plugin_root.exists():
        issues.append(f"{rel(plugin_root)}: missing MapMagic plugin root for cleanup scan")
        return

    for path in plugin_root.rglob("*.cs"):
        text = read_text(path)
        if "NativeMemorySentinel.UnregisterNativeArray" in text:
            issues.append(f"{rel(path)}: forbidden NativeMemorySentinel.UnregisterNativeArray in MapMagic plugin cleanup")
        if re.search(r"DisposeTracked\s*\(\s*ref\s+\w+\s*\)", text):
            issues.append(f"{rel(path)}: forbidden old one-argument DisposeTracked call in MapMagic plugin cleanup")


def collect_issues(args: argparse.Namespace) -> list[str]:
    issues: list[str] = []
    check_mapmagic_node(args.mapmagic_node, issues)
    check_anomaly_node(args.anomaly_node, issues)
    check_tempjob_id_cleanup_node(
        args.splatmap_node,
        ("heightsRegistrationId", "sedimentRegistrationId", "weightsRegistrationId", "slopeWeightsRegistrationId"),
        issues,
    )
    check_tempjob_id_cleanup_node(
        args.biome_postprocess_node,
        ("bufferARegistrationId", "bufferBRegistrationId"),
        issues,
    )
    check_erosion_harness(args.erosion_harness, issues)
    check_anomaly_engine(args.anomaly_engine, issues)
    check_erosion_job(args.erosion_job, issues)
    check_graph_integrator(args.graph_integrator, issues)
    check_queued_delta_callers(args.source_root, args.erosion_job, args.erosion_harness, issues)
    check_mapmagic_plugin_cleanup_scan(args.mapmagic_plugin_root, issues)
    return issues


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mapmagic-node", type=Path, default=DEFAULT_MAPMAGIC_NODE)
    parser.add_argument("--anomaly-node", type=Path, default=DEFAULT_ANOMALY_NODE)
    parser.add_argument("--splatmap-node", type=Path, default=DEFAULT_SPLATMAP_NODE)
    parser.add_argument("--biome-postprocess-node", type=Path, default=DEFAULT_BIOME_POSTPROCESS_NODE)
    parser.add_argument("--erosion-harness", type=Path, default=DEFAULT_EROSION_HARNESS)
    parser.add_argument("--anomaly-engine", type=Path, default=DEFAULT_ANOMALY_ENGINE)
    parser.add_argument("--erosion-job", type=Path, default=DEFAULT_EROSION_JOB)
    parser.add_argument("--graph-integrator", type=Path, default=DEFAULT_GRAPH_INTEGRATOR)
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    parser.add_argument("--mapmagic-plugin-root", type=Path, default=DEFAULT_MAPMAGIC_PLUGIN_ROOT)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    issues = collect_issues(args)
    if issues:
        print(f"MAPMAGIC_EROSION_SOURCE_ROUTE_FAIL issues={len(issues)}")
        for issue in issues:
            print(f"- {issue}")
        return 1

    print("MAPMAGIC_EROSION_SOURCE_ROUTE_OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
