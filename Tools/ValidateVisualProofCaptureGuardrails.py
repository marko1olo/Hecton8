#!/usr/bin/env python3
"""Validate static guardrails for HECTON-8 visual proof capture tooling."""

from __future__ import annotations

import re
import argparse
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_PATH = ROOT / "Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs"
SURFACE_POLISH_RUNNER_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceRoutePersistentPolishRunner.cs"
SURFACE_CREST_FIXER_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceCrestOceanMaterialAssignmentFixer.cs"
H8_EDITOR_BRIDGE_1297_PATH = ROOT / "Assets/_Project/Scripts/Editor/H8EditorBridge1297.cs"
SURFACE_1929_POLISH_RUNNER_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceRoute1929PolishProofRunner.cs"
SURFACE_1930_AUTHORING_BRIDGE_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceRoute1930AuthoringBridge.cs"
SURFACE_1931_AUTHORING_BRIDGE_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceSceneAuthoring1931Bridge.cs"
SURFACE_1932_AUTHORING_RUNNER_PATH = ROOT / "Assets/_Project/Scripts/Editor/SurfaceRoute1932AuthoringRunner.cs"
RISK_REVIEW_PATH = ROOT / "Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md"
NEXT_ACTION_PATH = ROOT / "Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv"
OWNER_36_PATH = ROOT / "taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md"
OWNER_37_PATH = ROOT / "taskslocal/asset_system_20260605/ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md"
FILE_MAP_PATH = ROOT / "Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv"
LIVE_AUTORUN_MARKER_GLOBS = (
    "Docs/Screenshots/MCP/*.autorun",
    "Docs/Screenshots/MCP/h8_visual_proof_request*",
)
HIDDEN_AUTORUN_SOURCE_TERMS = (
    "[InitializeOnLoad]",
    "[InitializeOnLoadMethod]",
    "InitializeOnLoadMethod",
    "EditorApplication.delayCall",
    "EditorApplication.update",
    "H8_VISUAL_PROOF_REQUEST",
    "VisualProofRequest",
    "RunRequestedVisualProof",
    "ResolveRequestedVisualProof",
    "h8_visual_proof_request",
)

EXPECTED_PUBLIC_EXECUTE_ROUTES = frozenset(
    (
        "ApplySurfaceLightingMaterialPolishAndExit",
        "ApplySurfaceSceneCrestTerrainWiringAndExit",
        "CaptureRouteUnderwaterPatchAAndExit",
        "CaptureShallowUnderwaterPatchAAndExit",
        "CaptureSurfaceAfterQuarantineAndExit",
        "CaptureSurfaceAndExit",
        "CaptureSurfaceCrestAprilRouteProbeAndExit",
        "CaptureSurfaceCrestCleanTerrainProbeAndExit",
        "CaptureSurfaceCrestCoastHorizonProbeAndExit",
        "CaptureSurfaceCrestDaylightProbeAndExit",
        "CaptureSurfaceCrestFlatSkyHorizonProbeAndExit",
        "CaptureSurfaceCrestOceanExtentProbeAndExit",
        "CaptureSurfaceCrestPureOceanFlatSkyProbeAndExit",
        "CaptureSurfaceCrestPureOceanUniformSkyProbeAndExit",
        "CaptureSurfaceCrestRecoveryProbeAndExit",
        "CaptureSurfaceCrestSkyCardHorizonProbeAndExit",
        "CaptureSurfaceFlatSkyOnlyProbeAndExit",
        "CaptureSurfaceOwnerLightingAfterPolishAndExit",
        "CaptureSurfaceOwnerLightingAfterSceneWiringAndExit",
        "CaptureSurfaceOwnerLightingNonMutatingAndExit",
        "CaptureSurfacePatchAAndExit",
        "QuarantineSurfaceRejectsAndExit",
    )
)

EXPECTED_SURFACE_POLISH_RUNNER_PUBLIC_ROUTES = frozenset(
    (
        "DeferredApplyAndExit",
        "DeferredCaptureAndExit",
        "ApplyAndExit",
        "CaptureAndExit",
        "WriteDisabledPersistentPolishRouteAndExit",
    )
)

EXPECTED_SURFACE_CREST_FIXER_PUBLIC_ROUTES = frozenset(
    (
        "AssignAndExit",
        "ForceTextReserializeWorldSceneAndExit",
        "ApplySurfaceRoutePersistentPolishAndExit",
        "InvokeSurfaceRoutePrivatePolishAndExit",
    )
)

EXPECTED_H8_EDITOR_BRIDGE_1297_PUBLIC_ROUTES = frozenset(("RunAndExit",))

EXPECTED_SURFACE_1929_POLISH_RUNNER_PUBLIC_ROUTES = frozenset(
    (
        "ApplyAndExit",
        "CaptureAndExit",
    )
)

EXPECTED_SURFACE_1930_AUTHORING_BRIDGE_PUBLIC_ROUTES = frozenset(
    (
        "ApplyAndExit",
        "CaptureAndExit",
    )
)

EXPECTED_SURFACE_1931_AUTHORING_BRIDGE_PUBLIC_ROUTES = EXPECTED_SURFACE_1930_AUTHORING_BRIDGE_PUBLIC_ROUTES

EXPECTED_SURFACE_1932_AUTHORING_RUNNER_PUBLIC_ROUTES = EXPECTED_SURFACE_1930_AUTHORING_BRIDGE_PUBLIC_ROUTES


@dataclass(frozen=True)
class SourceRisk:
    token: str
    category: str
    line_number: int


@dataclass(frozen=True)
class SourceAssetReference:
    path: str
    line_number: int
    exists: bool


@dataclass(frozen=True)
class HarnessViolation:
    token: str
    category: str
    line_number: int
    line_excerpt: str


@dataclass(frozen=True)
class HarnessGateResult:
    status: str
    violations: tuple[HarnessViolation, ...]
    diagnostic_only: bool


SOURCE_RISK_TOKENS = (
    ("EditorSceneManager.SaveScene", "scene_save"),
    ("EditorSceneManager.MarkSceneDirty", "scene_dirty_mark"),
    ("ApplyModifiedPropertiesWithoutUndo", "serialized_object_mutation"),
    ("new Material(", "editor_material_clone"),
    ("CreatePrimitive", "editor_probe_geometry"),
    ("editor_only_unsaved", "diagnostic_unsaved_capture"),
)

HARNESS_REJECTED_STATUS = "REJECT_CANONICAL_HARNESS_SOURCE"
HARNESS_PASS_STATUS = "PASS_CANONICAL_HARNESS_SOURCE"
DIAGNOSTIC_PASS_STATUS = "PASS_DIAGNOSTIC_REJECTION_SOURCE"

CANONICAL_PROOF_MARKERS = (
    "HectonProofPackets",
    "ACCEPTED_BY_HARNESS",
    "may_submit_as_runtime_proof",
    "manifest.json",
    "manifest.sha256",
)

DIAGNOSTIC_REJECTION_MARKERS = (
    "diagnostic",
    "rejection-only",
    "rejection_only",
    "editor_only_unsaved",
    "reject",
)

HARNESS_BANNED_PATTERNS: tuple[tuple[re.Pattern[str], str, str], ...] = (
    (re.compile(r"\bEditorSceneManager\s*\.\s*SaveScene\s*\("), "scene_save", "EditorSceneManager.SaveScene"),
    (re.compile(r"\bEditorSceneManager\s*\.\s*MarkSceneDirty\s*\("), "scene_dirty_mark", "EditorSceneManager.MarkSceneDirty"),
    (re.compile(r"\bAssetDatabase\s*\.\s*ImportAsset\s*\("), "asset_import_mutation", "AssetDatabase.ImportAsset"),
    (re.compile(r"\bApplyModifiedPropertiesWithoutUndo\s*\("), "serialized_object_mutation", "ApplyModifiedPropertiesWithoutUndo"),
    (re.compile(r"\bSetActive\s*\("), "active_state_mutation", "SetActive"),
    (re.compile(r"\b(?:behaviour|\w*Behaviour\w*)\s*\.\s*enabled\s*="), "behaviour_enabled_mutation", "behaviour.enabled ="),
    (re.compile(r"\b(?:renderer|\w*Renderer\w*)\s*\.\s*enabled\s*="), "renderer_enabled_mutation", "renderer.enabled ="),
    (re.compile(r"\btransform\s*\.\s*(?:position|rotation|localScale|localPosition|localRotation)\s*="), "transform_mutation", "transform write"),
    (re.compile(r"\b\w*Camera\w*\s*\.\s*(?:nearClipPlane|farClipPlane|cullingMask)\s*(?:[|&^+\-*/]?=)"), "camera_render_state_mutation", "camera clip/culling write"),
    (re.compile(r"\b(?:System\s*\.\s*)?Reflection\b|\bBindingFlags\b|\bGetMethod\s*\(|\.\s*Invoke\s*\("), "private_reflection_invoke", "Reflection/private Invoke"),
    (re.compile(r"\b(?:System\s*\.\s*)?Threading\s*\.\s*Thread\s*\.\s*Sleep\s*\("), "editor_thread_sleep", "Thread.Sleep"),
    (re.compile(r"\bInitializeOnLoadMethod\b|\[\s*InitializeOnLoadMethod\s*\]"), "editor_initialize_on_load_autorun", "InitializeOnLoadMethod"),
    (re.compile(r"\bEditorApplication\s*\.\s*delayCall\s*(?:\+=|-=)"), "editor_delay_call_autorun", "EditorApplication.delayCall"),
    (re.compile(r"\bEditorApplication\s*\.\s*QueuePlayerLoopUpdate\s*\("), "editor_loop_pump", "EditorApplication.QueuePlayerLoopUpdate"),
    (re.compile(r"\bSceneView\s*\.\s*RepaintAll\s*\("), "editor_loop_pump", "SceneView.RepaintAll"),
    (re.compile(r"\bEditorSceneManager\s*\.\s*OpenScene\s*\("), "scene_open_mutation", "EditorSceneManager.OpenScene"),
    (re.compile(r"\bCamera\s*\.\s*main\b"), "scene_search_camera_heuristic", "Camera.main"),
    (re.compile(r"\b(?:Object\s*\.\s*)?Find(?:AnyObjectByType|FirstObjectByType|ObjectOfType|ObjectsByType|ObjectsOfType)\s*<"), "scene_search_heuristic", "Find*Object*"),
    (re.compile(r"\bResources\s*\.\s*FindObjectsOfTypeAll\s*<"), "scene_search_heuristic", "Resources.FindObjectsOfTypeAll"),
    (re.compile(r"\bGameObject\s*\.\s*Find(?:GameObjectWithTag|GameObjectsWithTag)?\s*\("), "scene_search_heuristic", "GameObject.Find*"),
    (re.compile(r"\bCompareTag\s*\(\s*\"Player\"\s*\)"), "witness_name_tag_heuristic", "CompareTag(\"Player\")"),
    (re.compile(r"\bprivate\s+static\s+(?:Component|Material|GameObject|UnityEngine\s*\.\s*Object)\s+[_A-Za-z]\w*\s*;"), "static_probe_object_state", "private static Unity object state"),
    (re.compile(r"\bDestroyImmediate\s*\("), "object_destroy_mutation", "DestroyImmediate"),
    (re.compile(r"\bnew\s+Material\s*\("), "editor_material_clone", "new Material("),
    (re.compile(r"\bHideFlags\s*\.\s*HideAndDontSave\b"), "temporary_hidden_material", "HideAndDontSave"),
    (re.compile(r"\bsharedMaterial\s*="), "shared_material_mutation", "sharedMaterial ="),
    (re.compile(r"\bmaterialTemplate\s*="), "terrain_material_mutation", "materialTemplate ="),
    (re.compile(r"\.\s*terrainLayers\s*="), "terrain_layer_mutation", "terrainLayers ="),
    (re.compile(r"\bterrain\s*\.\s*(?:drawInstanced|heightmapPixelError|basemapDistance)\s*="), "terrain_presentation_mutation", "terrain presentation write"),
    (re.compile(r"\bSetSerialized(?:Float|Int|Bool)\s*\(\s*serialized\s*,\s*\"globals\."), "mapmagic_globals_serialized_mutation", "SetSerialized*(globals.*)"),
    (re.compile(r"\bmapMagicObject\s*\.\s*globals\s*\.\s*(?:height|heightMainApply|heightDraftApply|heightInterpolation)\s*="), "mapmagic_global_height_mutation", "mapMagicObject.globals write"),
    (re.compile(r"\btiles\s*\.\s*Pin\s*\("), "mapmagic_tile_pin", "tiles.Pin"),
    (re.compile(r"\btiles\s*\.\s*ChangeDists\s*\("), "mapmagic_tile_distance_mutation", "tiles.ChangeDists"),
    (re.compile(r"\bRefresh\s*\("), "mapmagic_refresh", "Refresh("),
    (re.compile(r"\bStartGenerate\s*\("), "mapmagic_generation", "StartGenerate("),
    (re.compile(r"\bPumpMapMagicGeneration\s*\("), "mapmagic_generation_pump", "PumpMapMagicGeneration("),
    (re.compile(r"\bmapMagicObject\s*\.\s*Update\s*\("), "mapmagic_update_pump", "mapMagicObject.Update("),
    (re.compile(r"\bCoroutineManager\s*\.\s*Update\s*\("), "mapmagic_coroutine_pump", "CoroutineManager.Update("),
    (re.compile(r"\bCamera\s*\.\s*Render\s*\("), "raw_camera_render", "Camera.Render"),
    (re.compile(r"\bcamera\s*\.\s*Render\s*\("), "raw_camera_render", "camera.Render"),
    (re.compile(r"\bReadPixels\s*\("), "raw_read_pixels", "ReadPixels"),
    (re.compile(r"\bEncodeToPNG\s*\("), "raw_png_encode", "EncodeToPNG"),
    (re.compile(r"Docs/Screenshots/MCP|Docs\\Screenshots\\MCP"), "raw_mcp_output", "Docs/Screenshots/MCP"),
    (re.compile(r"Docs[\\/]+Screenshots[\\/]+(?!HectonProofPackets\b)"), "noncanonical_screenshot_output", "Docs/Screenshots outside HectonProofPackets"),
    (re.compile(r"\bh8_191[234](?=\b|_)"), "legacy_diagnostic_capture_id", "h8_1912/1913/1914"),
    (re.compile(r"\bdisabled_diagnostic_route\b|REJECTED_DISABLED_DIRECT_EXECUTE_METHOD"), "disabled_diagnostic_route", "disabled diagnostic route"),
    (re.compile(r"\b(?:AutoRunFlagPath|AutorunFlagPath|AutoRunMarker|AutorunMarker)\b|\.autorun\b|autorun", re.IGNORECASE), "autorun_marker_file", "autorun marker"),
    (re.compile(r"\bFile\s*\.\s*(?:Exists|Delete|Create|WriteAllText|WriteAllBytes)\s*\([^;\n]*(?:AutoRun|Autorun|autorun|\.autorun)"), "autorun_marker_lifecycle", "autorun marker lifecycle"),
)

ASSET_PATH_PATTERN = re.compile(r'"(Assets/[^"]+)"')
STALE_SOURCE_TERMS = (
    "SurfaceWaterReadabilityShaderPath",
    "H8_SurfaceWaterReadability_1428.shader",
)

REQUIRED_DOC_TERMS = {
    RISK_REVIEW_PATH: (
        "H8VisualProofCapture1912.cs",
        "EditorSceneManager.SaveScene",
        "ApplyModifiedPropertiesWithoutUndo",
        "editor_only_unsaved",
        "SurfaceHorizonHazeShaderPath",
    ),
    NEXT_ACTION_PATH: (
        "h8_1475_proof_tool_integrity",
        "editor_only_unsaved",
        "stale or missing asset path",
    ),
    OWNER_36_PATH: (
        "H8VisualProofCapture1912",
        "diagnostic/editor-mutating capture paths",
        "canonical h8_1475 proof tooling",
    ),
    OWNER_37_PATH: (
        "H8VisualProofCapture1912",
        "Anti-False-Proof",
        "editor_only_unsaved",
    ),
    FILE_MAP_PATH: (
        "H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md",
        "ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md",
    ),
}


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def find_source_risks(source: str) -> list[SourceRisk]:
    risks: list[SourceRisk] = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        for token, category in SOURCE_RISK_TOKENS:
            if token in line:
                risks.append(SourceRisk(token=token, category=category, line_number=line_number))
    return risks


def find_harness_violations(source: str) -> list[HarnessViolation]:
    violations: list[HarnessViolation] = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        for pattern, category, token in HARNESS_BANNED_PATTERNS:
            if pattern.search(line):
                violations.append(
                    HarnessViolation(
                        token=token,
                        category=category,
                        line_number=line_number,
                        line_excerpt=line.strip(),
                    )
                )
    return violations


def is_diagnostic_rejection_only_source(source: str) -> bool:
    lowered = source.lower()
    has_diagnostic_marker = any(marker.lower() in lowered for marker in DIAGNOSTIC_REJECTION_MARKERS)
    has_canonical_marker = any(marker.lower() in lowered for marker in CANONICAL_PROOF_MARKERS)
    return has_diagnostic_marker and not has_canonical_marker


def validate_harness_candidate_source(
    source: str,
    *,
    allow_diagnostic_rejection: bool = False,
    strict: bool = False,
) -> HarnessGateResult:
    violations = tuple(find_harness_violations(source))
    diagnostic_only = is_diagnostic_rejection_only_source(source)
    if not violations:
        return HarnessGateResult(HARNESS_PASS_STATUS, violations, diagnostic_only)
    if allow_diagnostic_rejection and diagnostic_only and not strict:
        return HarnessGateResult(DIAGNOSTIC_PASS_STATUS, violations, diagnostic_only)
    return HarnessGateResult(HARNESS_REJECTED_STATUS, violations, diagnostic_only)


def find_asset_references(source: str, root: Path = ROOT) -> list[SourceAssetReference]:
    references: list[SourceAssetReference] = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        for match in ASSET_PATH_PATTERN.finditer(line):
            asset_path = match.group(1)
            references.append(
                SourceAssetReference(
                    path=asset_path,
                    line_number=line_number,
                    exists=(root / asset_path).exists(),
                )
            )
    return references


def validate_asset_references(references: list[SourceAssetReference]) -> None:
    missing = [reference for reference in references if not reference.exists]
    if missing:
        details = ", ".join(f"{reference.path}@{reference.line_number}" for reference in missing)
        raise SystemExit(f"FAIL: missing capture-tool asset path(s): {details}")


def validate_required_terms(required_terms: dict[Path, tuple[str, ...]]) -> None:
    for path, terms in required_terms.items():
        text = load_text(path)
        for term in terms:
            if term not in text:
                raise SystemExit(f"FAIL: {display_path(path)} missing guardrail term: {term}")


def validate_no_stale_source_terms(source: str, required_terms: dict[Path, tuple[str, ...]]) -> None:
    for stale_term in STALE_SOURCE_TERMS:
        if stale_term in source:
            continue

        for path in required_terms:
            text = load_text(path)
            if stale_term in text:
                raise SystemExit(
                    f"FAIL: {display_path(path)} still cites stale source term absent from current source: {stale_term}"
                )


def find_live_autorun_markers(root: Path = ROOT) -> list[Path]:
    markers: list[Path] = []
    for marker_glob in LIVE_AUTORUN_MARKER_GLOBS:
        markers.extend(sorted(root.glob(marker_glob)))
    return markers


def validate_no_live_autorun_markers(root: Path = ROOT) -> None:
    markers = find_live_autorun_markers(root)
    if markers:
        details = ", ".join(display_path(path) for path in markers)
        raise SystemExit(f"FAIL: live visual-proof autorun marker(s) present: {details}")


def validate_no_hidden_autorun_source(source: str) -> None:
    present_terms = [term for term in HIDDEN_AUTORUN_SOURCE_TERMS if term in source]
    if present_terms:
        details = ", ".join(present_terms)
        raise SystemExit(f"FAIL: hidden visual-proof autorun source term(s) present: {details}")


def validate_skycard_direct_execute_route_disabled(source: str) -> None:
    method_match = re.search(
        r"public\s+static\s+void\s+CaptureSurfaceCrestSkyCardHorizonProbeAndExit\s*\(\s*\)\s*\{(?P<body>.*?)^\s*\}",
        source,
        re.DOTALL | re.MULTILINE,
    )
    if not method_match:
        raise SystemExit("FAIL: missing h8_1919 skycard direct executeMethod route")

    body = method_match.group("body")
    disabled_call = 'WriteDisabledDiagnosticRouteAndExit("h8_1919_surface_crest_skycard_horizon_probe")'
    if disabled_call not in body or "CaptureSurfaceCrestProbeAndExit" in body:
        raise SystemExit("FAIL: h8_1919 skycard direct executeMethod route must stay disabled")


def validate_flat_sky_direct_execute_route_disabled(source: str) -> None:
    method_match = re.search(
        r"public\s+static\s+void\s+CaptureSurfaceFlatSkyOnlyProbeAndExit\s*\(\s*\)\s*\{(?P<body>.*?)^\s*\}",
        source,
        re.DOTALL | re.MULTILINE,
    )
    if not method_match:
        raise SystemExit("FAIL: missing h8_1924 flat-sky direct executeMethod route")

    body = method_match.group("body")
    disabled_call = 'WriteDisabledDiagnosticRouteAndExit("h8_1924_surface_flat_sky_only_probe")'
    forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "ConfigureFlatBrightSkyProbe",
        "DisableAllSceneRenderersAndTerrainsForSkyOnlyProbe",
        "RenderCamera",
    )
    if disabled_call not in body or any(term in body for term in forbidden_terms):
        raise SystemExit("FAIL: h8_1924 flat-sky direct executeMethod route must stay disabled")


def validate_pure_ocean_direct_execute_routes_disabled(source: str) -> None:
    disabled_routes = (
        (
            "CaptureSurfaceCrestFlatSkyHorizonProbeAndExit",
            "h8_1922_surface_crest_flat_sky_horizon_probe",
            "h8_1922 flat-sky horizon",
        ),
        (
            "CaptureSurfaceCrestPureOceanFlatSkyProbeAndExit",
            "h8_1923_surface_crest_pure_ocean_flat_sky_probe",
            "h8_1923 pure-ocean flat-sky",
        ),
        (
            "CaptureSurfaceCrestPureOceanUniformSkyProbeAndExit",
            "h8_1925_surface_crest_pure_ocean_uniform_sky_probe",
            "h8_1925 pure-ocean uniform-sky",
        ),
    )
    forbidden_terms = (
        "CaptureSurfaceCrestProbeAndExit",
        "EditorSceneManager.OpenScene",
        "ConfigureFlatBrightSkyProbe",
        "ConfigureUniformBrightSkyProbe",
        "RenderCamera",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabledDiagnosticRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_persistent_scene_wiring_routes_disabled(source: str) -> None:
    disabled_routes = (
        (
            "ApplySurfaceSceneCrestTerrainWiringAndExit",
            "h8_1926_surface_scene_crest_terrain_wiring_apply",
            "h8_1926 persistent scene wiring apply",
        ),
        (
            "CaptureSurfaceOwnerLightingAfterSceneWiringAndExit",
            "h8_1927_surface_owner_lighting_after_scene_wiring",
            "h8_1927 after-scene-wiring capture",
        ),
        (
            "CaptureSurfaceOwnerLightingAfterPolishAndExit",
            "h8_1928_surface_owner_lighting_after_polish",
            "h8_1928 after-polish capture",
        ),
        (
            "ApplySurfaceLightingMaterialPolishAndExit",
            "h8_1928_surface_lighting_material_polish_apply",
            "h8_1928 lighting material polish apply",
        ),
    )
    forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
        "ApplyPersistentSurfaceCrestOceanWiring",
        "ApplySurfaceSceneCrestTerrainWiringInternalAndExit",
        "ApplySurfaceLightingMaterialPolishInternalAndExit",
        "MarkGeneratedTerrainObjectsDirty",
        "MarkMapMagicObjectDirty",
        "CaptureSurfaceAfterSceneWiringAndExit",
        "RenderCamera",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabledDiagnosticRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_unsafe_public_routes_disabled(source: str) -> None:
    disabled_routes = (
        (
            "CaptureSurfaceCrestRecoveryProbeAndExit",
            "disabled_legacy_surface_crest_recovery_probe",
            "legacy surface crest recovery probe",
        ),
        (
            "CaptureSurfaceCrestAprilRouteProbeAndExit",
            "h8_1915_surface_crest_april_route_probe",
            "h8_1915 surface crest april route probe",
        ),
        (
            "CaptureSurfaceCrestCleanTerrainProbeAndExit",
            "h8_1916_surface_crest_clean_terrain_probe",
            "h8_1916 surface crest clean terrain probe",
        ),
        (
            "CaptureSurfaceCrestDaylightProbeAndExit",
            "h8_1917_surface_crest_daylight_probe",
            "h8_1917 surface crest daylight probe",
        ),
        (
            "CaptureSurfaceCrestCoastHorizonProbeAndExit",
            "h8_1918_surface_crest_coast_horizon_probe",
            "h8_1918 surface crest coast horizon probe",
        ),
        (
            "CaptureSurfaceOwnerLightingNonMutatingAndExit",
            "h8_1921_surface_owner_lighting_nonmutating",
            "h8_1921 owner lighting nonmutating capture",
        ),
        (
            "CaptureSurfaceCrestOceanExtentProbeAndExit",
            "h8_1920_surface_crest_ocean_extent_probe",
            "h8_1920 ocean extent probe",
        ),
        (
            "QuarantineSurfaceRejectsAndExit",
            "disabled_legacy_surface_quarantine",
            "legacy quarantine route",
        ),
    )
    forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "CaptureSurfaceAndExit",
        "CaptureSurfaceCrestProbeAndExit",
        "CaptureSurfaceAfterSceneWiringAndExit",
        "Renderer[]",
        "renderer.enabled",
        "File.WriteAllText",
        "RenderCamera",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabledDiagnosticRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_disabled_route_rejector_has_no_allow_exceptions(source: str) -> None:
    method_match = re.search(
        r"private\s+static\s+bool\s+RejectDisabledMutatingDiagnosticRoute\s*\(\s*string\s+captureName\s*\)\s*\{(?P<body>.*?)^\s*\}",
        source,
        re.DOTALL | re.MULTILINE,
    )
    if not method_match:
        return

    body = method_match.group("body")
    if "return false" in body or "h8_1920" in body or "h8_1921" in body or "h8_1929" in body:
        raise SystemExit("FAIL: disabled diagnostic route rejector must not contain allow exceptions")
    if "WriteDisabledDiagnosticRouteAndExit(captureName)" not in body or "return true" not in body:
        raise SystemExit("FAIL: disabled diagnostic route rejector must deny through WriteDisabledDiagnosticRouteAndExit")


def validate_public_execute_route_inventory(
    source: str,
    expected_routes: frozenset[str] = EXPECTED_PUBLIC_EXECUTE_ROUTES,
    label: str = "visual proof",
) -> None:
    routes = set(re.findall(r"public\s+static\s+void\s+(\w+)\s*\(", source))
    missing = sorted(expected_routes - routes)
    unexpected = sorted(routes - expected_routes)
    if missing:
        raise SystemExit(f"FAIL: missing known {label} public route(s): " + ", ".join(missing))
    if unexpected:
        raise SystemExit(f"FAIL: unexpected {label} public route(s): " + ", ".join(unexpected))


def validate_shared_capture_route_rejects_before_scene_open(source: str, method_signature: str, label: str) -> None:
    method_index = source.find(method_signature)
    if method_index == -1:
        raise SystemExit(f"FAIL: missing {label} diagnostic executeMethod route")

    reject_index = source.find("RejectDisabledMutatingDiagnosticRoute(captureName)", method_index)
    scene_open_index = source.find("EditorSceneManager.OpenScene", method_index)
    if reject_index == -1 or scene_open_index == -1 or reject_index > scene_open_index:
        raise SystemExit(f"FAIL: {label} diagnostic executeMethod route must reject before scene open")


def validate_optional_shared_capture_route_rejects_before_scene_open(source: str, method_signature: str, label: str) -> None:
    method_index = source.find(method_signature)
    if method_index == -1:
        return

    reject_index = source.find("RejectDisabledMutatingDiagnosticRoute(captureName)", method_index)
    scene_open_index = source.find("EditorSceneManager.OpenScene", method_index)
    if reject_index == -1 or scene_open_index == -1 or reject_index > scene_open_index:
        raise SystemExit(f"FAIL: {label} diagnostic executeMethod route must reject before scene open")


def validate_surface_crest_shared_route_disabled(source: str) -> None:
    validate_optional_shared_capture_route_rejects_before_scene_open(
        source,
        "private static void CaptureSurfaceCrestProbeAndExit(",
        "shared surface-crest",
    )


def validate_surface_route_persistent_polish_bypass_disabled(runner_source: str, fixer_source: str | None) -> None:
    validate_public_execute_route_inventory(
        runner_source,
        EXPECTED_SURFACE_POLISH_RUNNER_PUBLIC_ROUTES,
        "SurfaceRoutePersistentPolishRunner",
    )

    disabled_runner_routes = (
        (
            "DeferredApplyAndExit",
            "h8_1928_surface_lighting_material_polish_apply",
            "SurfaceRoutePersistentPolishRunner deferred apply",
        ),
        (
            "DeferredCaptureAndExit",
            "h8_1928_surface_owner_lighting_after_polish",
            "SurfaceRoutePersistentPolishRunner deferred capture",
        ),
        (
            "ApplyAndExit",
            "h8_1928_surface_lighting_material_polish_apply",
            "SurfaceRoutePersistentPolishRunner apply",
        ),
        (
            "CaptureAndExit",
            "h8_1928_surface_owner_lighting_after_polish",
            "SurfaceRoutePersistentPolishRunner capture",
        ),
    )
    runner_forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
        "ApplyHorizonHaze",
        "ApplyReadableMaterials",
        "ApplyReadableLighting",
        "RenderCamera",
        "WriteMetadata",
        "ApplyInternalAndExit",
        "CaptureInternalAndExit",
        "QueueDeferredMode",
        "EditorApplication.delayCall",
        "InitializeOnLoadMethod",
        "Environment.GetEnvironmentVariable",
        "Environment.SetEnvironmentVariable",
        "H8_SURFACE_ROUTE_POLISH_MODE",
    )

    runner_hidden_autorun_terms = (
        "InitializeOnLoadMethod",
        "EditorApplication.delayCall",
        "Environment.GetEnvironmentVariable",
        "Environment.SetEnvironmentVariable",
        "H8_SURFACE_ROUTE_POLISH_MODE",
        "QueueDeferredMode",
    )
    present_hidden_terms = [term for term in runner_hidden_autorun_terms if term in runner_source]
    if present_hidden_terms:
        details = ", ".join(present_hidden_terms)
        raise SystemExit(f"FAIL: SurfaceRoutePersistentPolishRunner hidden autorun term(s) present: {details}")

    for method_name, capture_name, label in disabled_runner_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            runner_source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabledPersistentPolishRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in runner_forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")

    disabled_runner_internal_routes = (
        (
            "ApplyAuthoringRoute1930AndExit",
            "h8_surface_route1930_authoring_apply",
            "SurfaceRoutePersistentPolishRunner 1930 authoring apply",
        ),
        (
            "CaptureAuthoringRoute1930AndExit",
            "h8_surface_route1930_owner_lighting_capture",
            "SurfaceRoutePersistentPolishRunner 1930 authoring capture",
        ),
        (
            "ApplyAuthoringRoute1931AndExit",
            "h8_surface_route1931_authoring_apply",
            "SurfaceRoutePersistentPolishRunner 1931 authoring apply",
        ),
        (
            "CaptureAuthoringRoute1931AndExit",
            "h8_surface_route1931_owner_lighting_capture",
            "SurfaceRoutePersistentPolishRunner 1931 authoring capture",
        ),
    )
    internal_forbidden_terms = runner_forbidden_terms + (
        "surface_route_1930_authoring_apply",
        "surface_route_1931_authoring_apply",
        "SurfaceRoute1930AuthoringBridge.ApplyAndExit",
        "SurfaceRoute1930AuthoringBridge.CaptureAndExit",
        "SurfaceSceneAuthoring1931Bridge.ApplyAndExit",
        "SurfaceSceneAuthoring1931Bridge.CaptureAndExit",
        "WriteRoute1930Metadata",
        "WriteRoute1931Metadata",
    )

    for method_name, capture_name, label in disabled_runner_internal_routes:
        method_match = re.search(
            rf"internal\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            runner_source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} internal route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabledPersistentPolishRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in internal_forbidden_terms):
            raise SystemExit(f"FAIL: {label} internal route must stay disabled")

    if fixer_source is None:
        return

    validate_public_execute_route_inventory(
        fixer_source,
        EXPECTED_SURFACE_CREST_FIXER_PUBLIC_ROUTES,
        "SurfaceCrestOceanMaterialAssignmentFixer",
    )

    disabled_fixer_routes = (
        (
            "AssignAndExit",
            "h8_1928_surface_crest_material_assign",
            "SurfaceCrestOceanMaterialAssignmentFixer assign",
        ),
        (
            "ForceTextReserializeWorldSceneAndExit",
            "h8_1928_surface_scene_force_text_reserialize",
            "SurfaceCrestOceanMaterialAssignmentFixer force text reserialize",
        ),
        (
            "ApplySurfaceRoutePersistentPolishAndExit",
            "h8_1928_surface_lighting_material_polish_apply",
            "SurfaceCrestOceanMaterialAssignmentFixer persistent polish wrapper",
        ),
        (
            "InvokeSurfaceRoutePrivatePolishAndExit",
            "h8_1928_surface_private_polish_invoke",
            "SurfaceCrestOceanMaterialAssignmentFixer private polish invoke",
        ),
    )
    fixer_forbidden_terms = (
        "SurfaceRoutePersistentPolishRunner.ApplyAndExit",
        "SurfaceRoutePersistentPolishRunner.CaptureAndExit",
        "SurfaceRoutePersistentPolishRunner.DeferredApplyAndExit",
        "SurfaceRoutePersistentPolishRunner.DeferredCaptureAndExit",
        "InvokeSurfaceRoutePrivatePolishAndExit();",
        "System.Reflection",
        "GetMethod",
        ".Invoke",
        "AssignPrefab",
        "AssignScene",
        "ForceReserializeAssets",
        "PrefabUtility",
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
    )

    for method_name, capture_name, label in disabled_fixer_routes:
        fixer_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            fixer_source,
            re.DOTALL | re.MULTILINE,
        )
        if not fixer_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        fixer_body = fixer_match.group("body")
        required_call = (
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("'
            f'{capture_name}")'
        )
        if required_call not in fixer_body or any(term in fixer_body for term in fixer_forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_h8_editor_bridge_1297_disabled(source: str) -> None:
    validate_public_execute_route_inventory(
        source,
        EXPECTED_H8_EDITOR_BRIDGE_1297_PUBLIC_ROUTES,
        "H8EditorBridge1297",
    )

    method_match = re.search(
        r"public\s+static\s+void\s+RunAndExit\s*\(\s*\)\s*\{(?P<body>.*?)^\s*\}",
        source,
        re.DOTALL | re.MULTILINE,
    )
    if not method_match:
        raise SystemExit("FAIL: missing H8EditorBridge1297.RunAndExit")

    body = method_match.group("body")
    required_call = (
        'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("'
        'h8_1928_surface_bridge1297_private_polish_invoke")'
    )
    forbidden_terms = (
        "System.Reflection",
        "BindingFlags",
        "GetMethod",
        ".Invoke",
        "ApplyInternalAndExit",
        "ResolveRunnerType",
        "AppDomain.CurrentDomain.GetAssemblies",
        "TargetInvocationException",
        "Debug.LogException",
        "EditorApplication.Exit",
    )
    if required_call not in body or any(term in body for term in forbidden_terms):
        raise SystemExit("FAIL: H8EditorBridge1297.RunAndExit must stay disabled")

    source_forbidden_terms = (
        "System.Reflection",
        "BindingFlags",
        "GetMethod",
        ".Invoke",
        "ApplyInternalAndExit",
        "ResolveRunnerType",
        "AppDomain.CurrentDomain.GetAssemblies",
    )
    present_terms = [term for term in source_forbidden_terms if term in source]
    if present_terms:
        details = ", ".join(present_terms)
        raise SystemExit(f"FAIL: H8EditorBridge1297 private invocation term(s) present: {details}")


def validate_surface_route_1929_polish_runner_disabled(source: str) -> None:
    validate_public_execute_route_inventory(
        source,
        EXPECTED_SURFACE_1929_POLISH_RUNNER_PUBLIC_ROUTES,
        "SurfaceRoute1929PolishProofRunner",
    )

    hidden_autorun_terms = (
        "InitializeOnLoad",
        "InitializeOnLoadMethod",
        "EditorApplication.delayCall",
        "EditorApplication.update",
        "Environment.GetEnvironmentVariable",
        "Environment.SetEnvironmentVariable",
        "H8_SURFACE_ROUTE_1929_POLISH_MODE",
    )
    present_hidden_terms = [term for term in hidden_autorun_terms if term in source]
    if present_hidden_terms:
        details = ", ".join(present_hidden_terms)
        raise SystemExit(f"FAIL: SurfaceRoute1929PolishProofRunner hidden autorun term(s) present: {details}")

    disabled_routes = (
        (
            "ApplyAndExit",
            "h8_1929_surface_lighting_material_polish_apply",
            "SurfaceRoute1929PolishProofRunner apply",
        ),
        (
            "CaptureAndExit",
            "h8_1929_surface_owner_lighting_after_polish",
            "SurfaceRoute1929PolishProofRunner capture",
        ),
    )
    forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
        "ResolveMainCamera",
        "ConfigureSurfaceProofCamera",
        "ApplyHorizonHaze",
        "DisableSkyRendererShadows",
        "ApplyReadableMaterials",
        "ApplyReadableLighting",
        "ResolveSurfaceCrestOceanRenderer",
        "InvokeCrestRunUpdate",
        "PumpEditorLoop",
        "RenderCamera",
        "WriteMetadata",
        "Debug.Log",
        "Debug.LogException",
        "EditorApplication.Exit",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabled1929PolishRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_surface_route_1930_authoring_bridge_disabled(source: str) -> None:
    validate_public_execute_route_inventory(
        source,
        EXPECTED_SURFACE_1930_AUTHORING_BRIDGE_PUBLIC_ROUTES,
        "SurfaceRoute1930AuthoringBridge",
    )

    disabled_routes = (
        (
            "ApplyAndExit",
            "h8_surface_route1930_authoring_apply",
            "SurfaceRoute1930AuthoringBridge apply",
        ),
        (
            "CaptureAndExit",
            "h8_surface_route1930_owner_lighting_capture",
            "SurfaceRoute1930AuthoringBridge capture",
        ),
    )
    forbidden_terms = (
        "ApplyAuthoringRoute1930AndExit",
        "CaptureAuthoringRoute1930AndExit",
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
        "RenderCamera",
        "WriteRoute1930Metadata",
        "Debug.Log",
        "EditorApplication.Exit",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = (
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("'
            f'{capture_name}")'
        )
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_surface_scene_1931_authoring_bridge_disabled(source: str) -> None:
    validate_public_execute_route_inventory(
        source,
        EXPECTED_SURFACE_1931_AUTHORING_BRIDGE_PUBLIC_ROUTES,
        "SurfaceSceneAuthoring1931Bridge",
    )

    disabled_routes = (
        (
            "ApplyAndExit",
            "h8_surface_route1931_authoring_apply",
            "SurfaceSceneAuthoring1931Bridge apply",
        ),
        (
            "CaptureAndExit",
            "h8_surface_route1931_owner_lighting_capture",
            "SurfaceSceneAuthoring1931Bridge capture",
        ),
    )
    forbidden_terms = (
        "ApplyAuthoringRoute1931AndExit",
        "CaptureAuthoringRoute1931AndExit",
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "EditorUtility.SetDirty",
        "RenderCamera",
        "WriteRoute1931Metadata",
        "Debug.Log",
        "EditorApplication.Exit",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = (
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("'
            f'{capture_name}")'
        )
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_surface_route_1932_authoring_runner_disabled(source: str) -> None:
    validate_public_execute_route_inventory(
        source,
        EXPECTED_SURFACE_1932_AUTHORING_RUNNER_PUBLIC_ROUTES,
        "SurfaceRoute1932AuthoringRunner",
    )

    hidden_autorun_terms = (
        "InitializeOnLoad",
        "InitializeOnLoadMethod",
        "EditorApplication.delayCall",
        "EditorApplication.update",
        "Environment.GetEnvironmentVariable",
        "Environment.SetEnvironmentVariable",
        "H8_SURFACE_ROUTE_1932",
    )
    present_hidden_terms = [term for term in hidden_autorun_terms if term in source]
    if present_hidden_terms:
        details = ", ".join(present_hidden_terms)
        raise SystemExit(f"FAIL: SurfaceRoute1932AuthoringRunner hidden autorun term(s) present: {details}")

    disabled_routes = (
        (
            "ApplyAndExit",
            "h8_surface_route1932_authoring_apply",
            "SurfaceRoute1932AuthoringRunner apply",
        ),
        (
            "CaptureAndExit",
            "h8_surface_route1932_reference_view",
            "SurfaceRoute1932AuthoringRunner capture",
        ),
    )
    forbidden_terms = (
        "EditorSceneManager.OpenScene",
        "EditorSceneManager.MarkSceneDirty",
        "EditorSceneManager.SaveScene",
        "AssetDatabase.SaveAssets",
        "AssetDatabase.LoadAssetAtPath",
        "EditorUtility.SetDirty",
        "ApplyModifiedPropertiesWithoutUndo",
        "Camera.main",
        "FindAnyObjectByType",
        "FindObjectsByType",
        "GetComponentsInChildren",
        "ConfigureSurfaceCamera",
        "ConfigureCrestOcean",
        "ConfigureHorizonHaze",
        "ConfigureShorelineFoam",
        "ConfigureReferenceMaterials",
        "ConfigureSkyAndCelestial",
        "ConfigureLighting",
        "InvokeCrestRunUpdate",
        "PumpEditorLoop",
        "RenderCamera",
        "WriteMetadata",
        "APPLIED_PENDING_VISUAL_PROOF",
        "System.Reflection",
        "BindingFlags",
        "GetMethod",
        ".Invoke",
        "QueuePlayerLoopUpdate",
        "Debug.LogException",
    )

    for method_name, capture_name, label in disabled_routes:
        method_match = re.search(
            rf"public\s+static\s+void\s+{method_name}\s*\(\s*\)\s*\{{(?P<body>.*?)^\s*\}}",
            source,
            re.DOTALL | re.MULTILINE,
        )
        if not method_match:
            raise SystemExit(f"FAIL: missing {label} direct executeMethod route")

        body = method_match.group("body")
        disabled_call = f'WriteDisabled1932AuthoringRouteAndExit("{capture_name}")'
        if disabled_call not in body or any(term in body for term in forbidden_terms):
            raise SystemExit(f"FAIL: {label} direct executeMethod route must stay disabled")


def validate_shared_capture_routes_disabled(source: str) -> None:
    validate_optional_shared_capture_route_rejects_before_scene_open(
        source,
        "private static void CaptureSurfaceAndExit(",
        "shared surface-main",
    )
    validate_optional_shared_capture_route_rejects_before_scene_open(
        source,
        "private static void CaptureSurfaceCrestProbeAndExit(",
        "shared surface-crest",
    )
    validate_optional_shared_capture_route_rejects_before_scene_open(
        source,
        "private static void CaptureWithPoseAndExit(",
        "shared pose",
    )


def validate_guardrails(
    source_path: Path = SOURCE_PATH,
    required_terms: dict[Path, tuple[str, ...]] = REQUIRED_DOC_TERMS,
) -> list[SourceRisk]:
    source = load_text(source_path)
    risks = find_source_risks(source)
    asset_references = find_asset_references(source)
    validate_asset_references(asset_references)
    validate_required_terms(required_terms)
    validate_no_stale_source_terms(source, required_terms)
    validate_no_hidden_autorun_source(source)
    validate_no_live_autorun_markers()
    validate_public_execute_route_inventory(source)
    validate_unsafe_public_routes_disabled(source)
    validate_disabled_route_rejector_has_no_allow_exceptions(source)
    validate_skycard_direct_execute_route_disabled(source)
    validate_flat_sky_direct_execute_route_disabled(source)
    validate_pure_ocean_direct_execute_routes_disabled(source)
    validate_persistent_scene_wiring_routes_disabled(source)
    surface_crest_fixer_source = (
        load_text(SURFACE_CREST_FIXER_PATH)
        if SURFACE_CREST_FIXER_PATH.exists()
        else None
    )
    validate_surface_route_persistent_polish_bypass_disabled(
        load_text(SURFACE_POLISH_RUNNER_PATH),
        surface_crest_fixer_source,
    )
    if H8_EDITOR_BRIDGE_1297_PATH.exists():
        validate_h8_editor_bridge_1297_disabled(load_text(H8_EDITOR_BRIDGE_1297_PATH))
    validate_surface_route_1929_polish_runner_disabled(load_text(SURFACE_1929_POLISH_RUNNER_PATH))
    validate_surface_route_1930_authoring_bridge_disabled(load_text(SURFACE_1930_AUTHORING_BRIDGE_PATH))
    validate_surface_scene_1931_authoring_bridge_disabled(load_text(SURFACE_1931_AUTHORING_BRIDGE_PATH))
    validate_surface_route_1932_authoring_runner_disabled(load_text(SURFACE_1932_AUTHORING_RUNNER_PATH))
    validate_shared_capture_routes_disabled(source)
    return risks


def print_harness_result(result: HarnessGateResult) -> None:
    print(
        f"{result.status} violations={len(result.violations)} "
        f"diagnostic_only={str(result.diagnostic_only).lower()}"
    )
    for violation in result.violations:
        print(
            f"{violation.line_number}: {violation.category}: "
            f"{violation.token}: {violation.line_excerpt}"
        )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--mode",
        choices=("risk-docs", "harness-candidate"),
        default="risk-docs",
        help="risk-docs keeps the legacy 1912 risk-routing check; harness-candidate gates canonical proof harness source.",
    )
    parser.add_argument("--source", default=str(SOURCE_PATH), help="Source file to inspect.")
    parser.add_argument(
        "--allow-diagnostic-rejection",
        action="store_true",
        help="Allow explicitly diagnostic/rejection-only source as non-canonical. Strict mode overrides this.",
    )
    parser.add_argument("--strict", action="store_true", help="Reject mutation tokens even in diagnostic-labeled source.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(sys.argv[1:] if argv is None else argv)
    source_path = Path(args.source)
    if not source_path.is_absolute():
        source_path = ROOT / source_path
    if args.mode == "harness-candidate":
        result = validate_harness_candidate_source(
            load_text(source_path),
            allow_diagnostic_rejection=args.allow_diagnostic_rejection,
            strict=args.strict,
        )
        print_harness_result(result)
        return 0 if result.status != HARNESS_REJECTED_STATUS else 1

    risks = validate_guardrails()
    asset_references = find_asset_references(load_text(SOURCE_PATH))
    categories = sorted({risk.category for risk in risks})
    print(
        "VISUAL_PROOF_CAPTURE_GUARDRAILS_OK "
        f"risks={len(risks)} asset_refs={len(asset_references)} categories={','.join(categories)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
