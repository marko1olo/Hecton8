#!/usr/bin/env python3
"""Static gate for HECTON-8 agent rule routing.

Checks the rule surfaces that decide what future agents read first.
This is intentionally narrow: no Unity, no build, no import.
"""

from __future__ import annotations

import hashlib
import re
import sys
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
GLOBAL_CODEX = Path.home() / ".codex" / "AGENTS.md"
GLOBAL_GEMINI = Path.home() / ".gemini" / "GEMINI.md"
GEMINI_UNITY_MCP = Path.home() / ".gemini" / "antigravity-ide" / "mcp" / "unityMCP" / "instructions.md"
ACTIVE_PATH_PREFIXES = (
    "C:\\hades\\Hecton8\\",
    "Docs/",
    "Docs\\",
    "Assets/",
    "Assets\\",
    "Tools/",
    "Tools\\",
    ".agents-skills/",
    ".agents-skills\\",
    ".agent/",
    ".agent\\",
    ".codexrules/",
    ".codexrules\\",
    ".github/",
    ".github\\",
    ".cursor/",
    ".cursor\\",
    ".vscode/",
    ".vscode\\",
    "Packages",
)
PATH_REFERENCE_SKIP_TERMS = (
    "preferred",
    "when present",
    "legacy vendor folder",
    "example",
    "placeholder",
    "template",
    "pattern",
    "glob",
    "such as",
    "<",
    "*",
    # Added 2026-07-28 when path-existence checking was extended to route bibles and
    # mandates. Each term marks an idiom where a NON-existent path is the correct
    # documentation, verified case by case against the citing line:
    "dump target",   # black-box ring declares where it WOULD dump; `data.md` says
    "dumps",         # outright "no dump file was generated in this documentation pass"
    "on fault",      # `water.md` dump target "on fault"
    "migrated-away", # `water.md` records the path a file moved AWAY from
    "not the current",
    "artifact missing",   # `BUILD_PLAYTEST_ISSUES.md` cites a log to say it is GONE
    "does not exist",
)
AMBIGUOUS_AUTHORITY_TERMS = re.compile(
    r"\b(best effort|best-effort|when possible|if possible|nice to have|probably|hope|assume|stub|placeholder)\b",
    re.IGNORECASE,
)
AMBIGUOUS_AUTHORITY_ALLOWED_CONTEXT = re.compile(
    r"\b(forbid|forbidden|banned|no|not|never|do not|must not|avoid|reject|rejected|unless quoted|legacy|historical|diagnostic|template|quoted as|applies even)\b",
    re.IGNORECASE,
)
FALSE_READY_AUTHORITY_TERMS = re.compile(
    r"\b(PRODUCTION READY|SHIP READY|FINAL VERIFIED|STATUS:\s*(?:VERIFIED|COMPLETE)|good enough)\b",
    re.IGNORECASE,
)
FALSE_READY_AUTHORITY_ALLOWED_CONTEXT = re.compile(
    r"\b(do not|no|not|without|stale|false|claiming|claims?|must name|rejected|forbidden|pending|blocks?|status labels|not proof)\b",
    re.IGNORECASE,
)
DANGEROUS_AUTHORITY_TOKENS = (
    "Camera.main",
    "FindObjectOfType",
    "GameObject.Find",
    "DontDestroyOnLoad",
    "Resources.Load",
    "StartCoroutine",
    "BinaryFormatter",
    "JsonUtility.FromJson",
    "File.ReadAllText",
    "File.ReadAllBytes",
)
DANGEROUS_AUTHORITY_ALLOWED_CONTEXT = re.compile(
    r"\b(forbid|forbidden|banned|no|not|do not|must not|avoid|reject|rejected|legacy|historical|injected|cached|migration exception)\b",
    re.IGNORECASE,
)
ROUTE_BIBLE_AMBIGUOUS_TERMS = re.compile(
    r"\b(best effort|best-effort|when possible|if possible|nice to have|probably|hope|assume|stub|placeholder)\b",
    re.IGNORECASE,
)
ROUTE_BIBLE_AMBIGUOUS_ALLOWED_CONTEXT = re.compile(
    r"\b(forbid|forbidden|banned|no|not|never|do not|must|must not|avoid|reject|rejected|invalid|wrong|without|diagnostic|template|pending verification|hostile|cut it|not permission)\b",
    re.IGNORECASE,
)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def assert_contains(errors: list[str], path: Path, needle: str) -> None:
    text = read_text(path)
    if needle not in text:
        fail(errors, f"{path}: missing required text: {needle}")


def assert_not_contains(errors: list[str], path: Path, needle: str) -> None:
    text = read_text(path)
    if needle in text:
        fail(errors, f"{path}: forbidden inbound third-party shim reference remains: {needle}")


def assert_order(errors: list[str], path: Path, first: str, second: str) -> None:
    text = read_text(path)
    first_index = text.find(first)
    second_index = text.find(second)
    if first_index < 0:
        fail(errors, f"{path}: missing order marker: {first}")
        return
    if second_index < 0:
        fail(errors, f"{path}: missing order marker: {second}")
        return
    if first_index > second_index:
        fail(errors, f"{path}: wrong read order: {first} must appear before {second}")


def is_active_path_reference(value: str) -> bool:
    return value.startswith(ACTIVE_PATH_PREFIXES)


def resolve_reference(value: str) -> Path:
    if value.startswith("C:\\hades\\Hecton8\\"):
        return Path(value)
    return ROOT / value.replace("\\", "/")


def path_part(value: str) -> str:
    if "[" in value or "]" in value:
        return ""
    if re.search(r"\.(py|ps1|exe|bat|cmd)\s+", value, re.IGNORECASE):
        return value.split(maxsplit=1)[0]
    # Strip a trailing line or line-range citation: `file.cs:123` and `file.py:499-500`.
    #
    # WHY: file:line is this repository's house citation style - Docs/INIT_ORDER_CHAIN.md alone carries 76
    # of them and BUILD_PLAYTEST_ISSUES.md 75 - but the existence check received the whole backtick body,
    # so any scanned doc citing a line inside a Tools/ or Assets/ path failed with
    # "active local path reference is missing" even when the file was present and long enough.
    # Observed on 3DMODEL_TEXTURES_MATERIALS.md:43 citing Tools/Blender/h8forge/law.py:499-500, where
    # law.py exists and has 667 lines.
    #
    # This strictly improves precision rather than relaxing the gate: the FILE is still existence-checked,
    # only the line suffix is removed. Anchored to the end and requires digits after the colon, so a
    # Windows drive prefix (C:\hades\...) and a namespace-style colon are untouched.
    value = re.sub(r":\d+(?:-\d+)?$", "", value)
    return value


def assert_referenced_paths_exist(errors: list[str], path: Path) -> None:
    for line_number, line in enumerate(read_text(path).splitlines(), start=1):
        lowered = line.lower()
        if any(term in lowered for term in PATH_REFERENCE_SKIP_TERMS):
            continue
        for match in re.finditer(r"`([^`]+)`", line):
            value = path_part(match.group(1))
            if not value:
                continue
            if not is_active_path_reference(value):
                continue
            target = resolve_reference(value)
            if not target.exists():
                fail(errors, f"{path}:{line_number}: active local path reference is missing: {value}")


def assert_no_unguarded_authority_language(errors: list[str], path: Path) -> None:
    for line_number, line in enumerate(read_text(path).splitlines(), start=1):
        if AMBIGUOUS_AUTHORITY_TERMS.search(line) and not AMBIGUOUS_AUTHORITY_ALLOWED_CONTEXT.search(line):
            fail(errors, f"{path}:{line_number}: unguarded ambiguous authority language: {line.strip()}")
        if FALSE_READY_AUTHORITY_TERMS.search(line) and not FALSE_READY_AUTHORITY_ALLOWED_CONTEXT.search(line):
            fail(errors, f"{path}:{line_number}: unguarded false-ready authority language: {line.strip()}")
        for token in DANGEROUS_AUTHORITY_TOKENS:
            if token in line and not DANGEROUS_AUTHORITY_ALLOWED_CONTEXT.search(line):
                fail(errors, f"{path}:{line_number}: dangerous runtime token appears as active authority route: {line.strip()}")


def assert_no_unguarded_route_bible_language(errors: list[str], path: Path) -> None:
    for line_number, line in enumerate(read_text(path).splitlines(), start=1):
        if ROUTE_BIBLE_AMBIGUOUS_TERMS.search(line) and not ROUTE_BIBLE_AMBIGUOUS_ALLOWED_CONTEXT.search(line):
            fail(errors, f"{path}:{line_number}: unguarded ambiguous route-bible language: {line.strip()}")


def mirror_matches_or_delegates(mirror: Path, primary: Path, primary_bytes: bytes) -> bool:
    mirror_bytes = mirror.read_bytes()
    if mirror_bytes == primary_bytes:
        return True

    text = mirror_bytes.decode("utf-8-sig", errors="replace")
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    if len(lines) != 1:
        return False

    delegate_match = re.match(r"^\[DELEGATE\]:\s*(.+?)\s*$", lines[0])
    if not delegate_match:
        return False

    raw_target = delegate_match.group(1).strip().strip("`").strip('"')
    if not raw_target:
        return False

    target = resolve_reference(raw_target).resolve()
    return target == primary.resolve()


def main() -> int:
    errors: list[str] = []

    root_agents = ROOT / "AGENTS.md"
    codex_agents = ROOT / ".codexrules" / "AGENTS.md"
    github_agents = ROOT / ".github" / "agents" / "AGENTS.md"
    agent_rules_agents = ROOT / ".agent" / "rules" / "AGENTS.md"
    vscode_agents = ROOT / ".vscode" / "AGENTS.md"
    cursor_agents = ROOT / ".cursor" / "rules" / "AGENTS.md"
    cursor_index = ROOT / ".cursor" / "index.mdc"
    project_gemini = ROOT / "GEMINI.md"
    routing = ROOT / "Docs" / "AGENT_AUTHORITY_ROUTING.md"
    governance = ROOT / "Docs" / "DOC_GOVERNANCE.md"
    quality_gates = ROOT / "Docs" / "QUALITY_GATES.md"
    docs_readme = ROOT / "Docs" / "README.md"
    root_docs_reference = ROOT / "Docs" / "ROOT_DOCS_REFERENCE.md"
    project_bibles = ROOT / "PROJECT_BIBLES.md"
    combined_bibles_builder = ROOT / "Tools" / "Docs" / "BuildProjectRootBiblesCombined.py"
    lane_contract_gate = ROOT / "Tools" / "Docs" / "TestTaskLocalLaneContracts.py"
    mandate_registry_gate = ROOT / "Tools" / "Docs" / "TestMandateRegistry.py"
    applied_lore_source_guard = ROOT / "Tools" / "AppliedLoreProductionSourceGuard.py"
    orchestrator = ROOT / "HECTON8_ORCHESTRATOR.md"
    autonomous_orchestrator = ROOT / "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md"
    skills_readme = ROOT / ".agents-skills" / "README.md"
    global_registry_mandate = ROOT / ".agents-skills" / "ARCH_Global_Registry_ServiceLocator_DI_Init.txt"
    ledger = ROOT / "Docs" / "AGENTS_RULE_DETAIL_LEDGER.md"
    data_bible = ROOT / "data.md"
    physics_bible = ROOT / "physics.md"
    voxels_bible = ROOT / "voxels.md"
    streaming_bible = ROOT / "streaming.md"
    performance_bible = ROOT / "performance.md"
    bootstrap_bible = ROOT / "bootstrap.md"
    persistence_bible = ROOT / "persistence.md"
    rendering_bible = ROOT / "rendering.md"
    settings_bible = ROOT / "settings.md"
    save_paging_protocol = ROOT / "Docs" / "ARCHITECTURE" / "SAVE_PAGING_PROTOCOL.md"
    route_card_template = ROOT / "Docs" / "ARCHITECTURE" / "GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md"
    route_review_checklist = ROOT / "Docs" / "ARCHITECTURE" / "GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md"
    persona = ROOT / ".github" / "agents" / "unity-anime-dev.agent.md"
    historical_agent_rules_archive = ROOT / "Docs" / "DEPRECATED" / "AgentRulesHistorical_20260605"
    visual_reference_folder = ROOT / "Docs" / "mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)"
    historical_agent_shims_archive = ROOT / "Docs" / "DEPRECATED" / "AgentShimsHistorical_20260606"
    historical_cursor_rules_archive = ROOT / "Docs" / "DEPRECATED" / "CursorRulesHistorical_20260606"

    root_text = root_agents.read_bytes()
    for mirror in (codex_agents, github_agents):
        if not mirror_matches_or_delegates(mirror, root_agents, root_text):
            fail(errors, f"{mirror}: must byte-match root AGENTS.md or contain an explicit [DELEGATE]: path to root AGENTS.md")

    for path in (root_agents, routing, project_bibles):
        assert_contains(errors, path, "complete document")
    for path in (root_agents, routing, project_bibles, governance, quality_gates, docs_readme, skills_readme):
        assert_referenced_paths_exist(errors, path)
    for path in (root_agents, routing, project_bibles, ROOT / "VISION_LOCKS.md", ROOT / "TASTE.md", governance, quality_gates, docs_readme, skills_readme):
        assert_no_unguarded_authority_language(errors, path)

    for needle in (
        "Ordinary runtime/gameplay implementation",
        "QA/proof/verification",
        "Technical report means",
        "before design/implementation/review/proof",
        "Docs\\mandatory if you work on systems that user sees",
        "Docs/Lore/WriterScenarioAgentPrompt.md",
    ):
        assert_contains(errors, routing, needle)

    for needle in (
        "Technical report means",
        "Before player-visible visual creation",
        "## Delegation And Subagents",
        "Work as much as possible means",
        "Do not conserve effort by simplifying the user's meaning",
        "Subagents are a primary HECTON-8 work tool",
        "Audio import defaults",
        "Revert over hack for proven regressions",
        "Prefab/scene consistency guard",
        "Lore/content production artifact means concrete files",
        "Visual benchmark parity",
        "COLD ALLOC",
        "TryReserve",
        "MCP/Unity proof",
        "Do not read `HECTON8_ORCHESTRATOR.md`",
        "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md",
        "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict",
        "TestAgentRuleRouting.py",
    ):
        assert_contains(errors, root_agents, needle)

    for needle in (
        "AGENT LANE CONTRACTS",
        "GAME_VISUAL",
        "RUNTIME_SYSTEM",
        "ASSET_PIPELINE",
        "LORE_CONTENT",
        "DOCS_RULES",
        "QA_PROOF",
        "ORCHESTRATION",
        "TOOLING_AUTOMATION",
        "LANE_CLASS",
        "VALID_COMPLETION",
        "INVALID_COMPLETION",
        "KILL_SWITCH",
        "EVIDENCE_BUDGET",
        "CONTROLLER SIDE-DELEGATION NOTE",
        "Ordinary internal subagent spawning is governed by root `AGENTS.md`",
        "9-10 top-level agents are allowed",
        "Internal side-delegation does not count against the top-level batch size",
        "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict",
    ):
        assert_contains(errors, orchestrator, needle)

    assert_contains(errors, root_agents, "lane contracts")
    assert_contains(errors, routing, "AGENT LANE CONTRACTS")
    assert_contains(errors, routing, "Subagent use by an ordinary implementation")
    assert_contains(errors, routing, "Internal subagent spawning")
    assert_contains(errors, routing, "does not require `HECTON8_ORCHESTRATOR.md`")
    assert_contains(errors, routing, "AppliedLore Content Gate")
    assert_contains(errors, routing, "DataMonolith import")
    assert_contains(errors, routing, "Visual Reference Parity Gate")
    assert_contains(errors, routing, "Raw diagnostic screenshots can prove rejection only")
    assert_contains(errors, routing, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, project_bibles, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, root_docs_reference, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, root_docs_reference, "GEMINI.md")
    assert_contains(errors, project_bibles, "lane contracts")
    assert_contains(errors, project_bibles, "subagent rules")
    assert_contains(errors, project_bibles, "ordinary subagent rules")
    assert_contains(errors, project_bibles, "without pulling GUI/process orchestration")
    assert_contains(errors, project_bibles, "Visual Reference Parity Gate")
    assert_contains(errors, project_bibles, "raw diagnostic captures remain reject-only")
    assert_contains(errors, project_bibles, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, project_bibles, "TestMandateRegistry.py")
    assert_contains(errors, governance, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, governance, "TestMandateRegistry.py")
    assert_contains(errors, quality_gates, "Tasklocal Lane Contract Gate")
    assert_contains(errors, quality_gates, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, quality_gates, "Mandate Registry Gate")
    assert_contains(errors, quality_gates, "TestMandateRegistry.py")
    assert_contains(errors, quality_gates, "TestMandateRegistry.py --self-test")
    assert_contains(errors, quality_gates, "ambiguous escape clauses")
    assert_contains(errors, quality_gates, "visual parity inheritance for player-visible mandates")
    assert_contains(errors, quality_gates, "dangerous active runtime API examples")
    assert_contains(errors, quality_gates, "AppliedLore Content Gate")
    assert_contains(errors, quality_gates, "ValidateGrandLibraryLoreQuality.py")
    assert_contains(errors, quality_gates, "ValidateGrandLibraryLoreQuality.py --article-glob <glob> --require-status-comment")
    assert_contains(errors, quality_gates, "AppliedLoreProductionSourceGuard.py")
    assert_contains(errors, quality_gates, "AppliedLoreProductionSourceGuard.py --self-test")
    assert_contains(errors, quality_gates, "AppliedLoreImporter.py --check")
    assert_contains(errors, quality_gates, "AppliedLoreRouteCardExporter.py --check")
    assert_contains(errors, quality_gates, "AppliedLorePageExporter.py --packet-glob <P*> --check")
    assert_contains(errors, quality_gates, "AppliedLorePacketCoverageAudit.py --packet-id <P*>")
    assert_contains(errors, quality_gates, "AppliedLorePacketCoverageAudit.py --inventory")
    assert_contains(errors, quality_gates, "AppliedLorePacketCoverageAudit.py --all --sample-limit 3")
    assert_contains(errors, quality_gates, "Runtime route-card export may only contain baked packet IDs")
    assert_contains(errors, quality_gates, "Visual Reference Parity Gate")
    assert_contains(errors, quality_gates, "ValidateVisualReferenceOwnerMatrix.py")
    assert_contains(errors, quality_gates, "ValidateVisualReferenceCurrentRejectionMatrix.py")
    assert_contains(errors, quality_gates, "test_validate_visual_reference_owner_matrix.py")
    assert_contains(errors, quality_gates, "VISUAL_ROUTE_INVALID")
    assert_contains(errors, quality_gates, "Agent Rule Routing Gate")
    assert_contains(errors, quality_gates, "TestAgentRuleRouting.py")
    assert_contains(errors, quality_gates, "current live-path references")
    assert_contains(errors, quality_gates, "unguarded upper-authority/route-bible ambiguity")
    assert_contains(errors, skills_readme, "TestMandateRegistry.py")
    assert_contains(errors, skills_readme, "TestMandateRegistry.py --self-test")
    assert_contains(errors, skills_readme, "Legacy or illustrative mandate snippets")
    assert_contains(errors, skills_readme, "Camera.main")
    assert_contains(errors, skills_readme, "FindObjectOfType")
    assert_contains(errors, root_agents, "TestMandateRegistry.py")
    assert_contains(errors, docs_readme, "TestAgentRuleRouting.py")
    assert_contains(errors, docs_readme, "TestMandateRegistry.py")
    assert_contains(errors, lane_contract_gate, "same-wave/sibling dependency guard")
    assert_contains(errors, lane_contract_gate, "DEPENDENCY_GUARD_TERMS")
    assert_contains(errors, lane_contract_gate, "DEPENDENCY_OUTPUT_TERMS")
    assert_contains(errors, lane_contract_gate, "VALID_DELIVERABLE_CLASSES")
    assert_contains(errors, lane_contract_gate, "LANE_DELIVERABLE_CLASSES")
    assert_contains(errors, lane_contract_gate, "LANE_PROOF_TERMS")
    assert_contains(errors, lane_contract_gate, "LORE_APPLIED_CONTENT_TERMS")
    assert_contains(errors, lane_contract_gate, "PROOF_ACTION_TERMS")
    assert_contains(errors, lane_contract_gate, "DELIVERABLE_CLASS")
    assert_contains(errors, lane_contract_gate, "PROOF_ROUTE")
    assert_contains(errors, lane_contract_gate, "invalid deliverable class passed")
    assert_contains(errors, lane_contract_gate, "incompatible deliverable class passed")
    assert_contains(errors, lane_contract_gate, "report-only proof route passed")
    assert_contains(errors, lane_contract_gate, "weak dependency wording passed")
    assert_contains(errors, lane_contract_gate, "generic lore proof route passed")
    assert_contains(errors, mandate_registry_gate, "BANNED_WEAK_LANGUAGE")
    assert_contains(errors, mandate_registry_gate, "AMBIGUOUS_PRODUCTION_LANGUAGE")
    assert_contains(errors, mandate_registry_gate, "FALSE_READY_LABELS")
    assert_contains(errors, mandate_registry_gate, "OLD_UNITY_VERSION")
    assert_contains(errors, mandate_registry_gate, "LEGACY_HECTON_ASSEMBLY")
    assert_contains(errors, mandate_registry_gate, "RAW_DMI_TOKEN")
    assert_contains(errors, mandate_registry_gate, "MPB_TOKEN")
    assert_contains(errors, mandate_registry_gate, "DANGEROUS_RUNTIME_TOKENS")
    assert_contains(errors, mandate_registry_gate, "LOCAL_PATH_REFERENCE")
    assert_contains(errors, mandate_registry_gate, "STALE_SOURCE_CLAIM")
    assert_contains(errors, mandate_registry_gate, "VISUAL_PARITY_MANDATES")
    assert_contains(errors, mandate_registry_gate, "VISUAL_PARITY_TERMS")
    assert_contains(errors, mandate_registry_gate, "CORE_Weather_Abyssal_FlowField_Currents.txt")
    assert_contains(errors, mandate_registry_gate, "PHYS_Fluid_Incursion_Interior.txt")
    assert_contains(errors, mandate_registry_gate, "REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt")
    assert_contains(errors, mandate_registry_gate, "STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt")
    assert_contains(errors, mandate_registry_gate, "UI_Diegetic_Physical_Interfaces.txt")
    assert_contains(errors, mandate_registry_gate, "VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt")
    assert_contains(errors, mandate_registry_gate, "visual parity mandate fixture passed")
    assert_contains(errors, mandate_registry_gate, "MIN_MANDATE_BYTES")
    assert_contains(errors, mandate_registry_gate, "old Unity version fixture passed")
    assert_contains(errors, mandate_registry_gate, "raw DrawMeshInstancedIndirect active-route fixture passed")
    assert_contains(errors, mandate_registry_gate, "active MaterialPropertyBlock world-geometry fixture passed")
    assert_contains(errors, mandate_registry_gate, "dead local path fixture passed")
    assert_contains(errors, mandate_registry_gate, "stale hard source claim fixture passed")
    assert_contains(errors, mandate_registry_gate, "active dangerous runtime token fixture passed")
    assert_contains(errors, mandate_registry_gate, "ambiguous production language fixture passed")
    assert_contains(errors, mandate_registry_gate, "MANDATE_REGISTRY_SELFTEST=PASS")
    assert_contains(errors, applied_lore_source_guard, "APPLIED_LORE_PRODUCTION_SOURCE_GUARD_SELFTEST=PASS")
    assert_contains(errors, applied_lore_source_guard, "packet-JSON release without .production.md source failed")
    assert_contains(errors, applied_lore_source_guard, "production source ready-claim fixture passed")
    assert_contains(errors, applied_lore_source_guard, "valid production source fixture failed")
    assert_contains(errors, ROOT / "TASTE.md", "best-known internal baseline")
    assert_contains(errors, ROOT / "TASTE.md", "April/previously-in-development")

    if not visual_reference_folder.exists():
        fail(errors, f"{visual_reference_folder}: missing mandatory visual reference folder")
    else:
        image_count = sum(1 for p in visual_reference_folder.iterdir() if p.is_file() and p.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"})
        if image_count < 10:
            fail(errors, f"{visual_reference_folder}: expected at least 10 image references, found {image_count}")

    for path, needle in (
        (data_bible, "DataVault Write-Lock And Fencing Law"),
        (data_bible, "try/finally ReleaseWriteLock"),
        (route_card_template, "DataVault write-lock scope"),
        (route_review_checklist, "same-phase `try/finally ReleaseWriteLock`"),
        (quality_gates, "DataVault write-lock/fence proof"),
        (physics_bible, "Character and vehicle environment collision route"),
        (physics_bible, "must not run synchronous `SphereCast`, `CapsuleCast`, or `Raycast` chains"),
        (voxels_bible, "SDF Collision Read Model"),
        (voxels_bible, "baked voxel/terrain colliders or an approved SDF read model"),
        (streaming_bible, "`used/total > 0.90`"),
        (performance_bible, "`used/total > 0.90`"),
        (bootstrap_bible, "Texture Memory"),
        (bootstrap_bible, "Total Reserved Memory"),
        (quality_gates, "Required scene-load memory proof"),
        (persistence_bible, "Load application bands"),
        (persistence_bible, "0-10`: Core"),
        (save_paging_protocol, "Save Operation Bands And Cadence"),
        (save_paging_protocol, "Autosave minimum cadence: 30 seconds"),
        (rendering_bible, "Current URP quality defaults"),
        (rendering_bible, "URP_Medium (PC_RPAsset).asset"),
        (settings_bible, "Current graphics quality defaults"),
        (settings_bible, "URP_Low (PC_RPAsset).asset"),
    ):
        assert_contains(errors, path, needle)

    assert_contains(errors, agent_rules_agents, "Authority delegates to `C:\\hades\\Hecton8\\AGENTS.md`")
    assert_contains(errors, agent_rules_agents, "Docs\\AGENT_AUTHORITY_ROUTING.md")

    for path, archive_name in (
        (vscode_agents, "vscode_AGENTS.md"),
        (cursor_agents, "cursor_rules_AGENTS.md"),
    ):
        for needle in (
            "THIN_ROUTER / NOT_PROJECT_LAW",
            "C:\\hades\\Hecton8\\AGENTS.md",
            "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
            "complete document",
            f"Docs/DEPRECATED/AgentShimsHistorical_20260606/{archive_name}",
        ):
            assert_contains(errors, path, needle)
        text = read_text(path)
        for forbidden in (
            "## PROJECT ARCHITECTURE",
            "### Key Interfaces",
            "GlobalRegistry (Service Locator Pattern)",
            "Renderer: PC_Renderer",
            "NOT a creative director",
        ):
            if forbidden in text:
                fail(errors, f"{path}: stale full AGENTS body remains: {forbidden}")
        archived = historical_agent_shims_archive / archive_name
        if not archived.exists():
            fail(errors, f"{archived}: missing archived shim body")

    for rule_file in (ROOT / ".agent" / "rules").glob("*.md"):
        text = read_text(rule_file)
        if rule_file.name != "AGENTS.md":
            if "alwaysApply: true" in text:
                fail(errors, f"{rule_file}: historical rule must not alwaysApply")
            if re.search(r"(?m)^globs:", text):
                fail(errors, f"{rule_file}: historical rule must not keep active globs")
            if "HISTORICAL REFERENCE ONLY" not in text:
                fail(errors, f"{rule_file}: missing historical-reference marker")
            if "not active HECTON-8 authority" not in text:
                fail(errors, f"{rule_file}: missing non-authority warning")
            if "Docs/DEPRECATED/AgentRulesHistorical_20260605" not in text:
                fail(errors, f"{rule_file}: missing archive pointer for old body")
            if re.search(r"\b(Update|FixedUpdate|LateUpdate)\s*\(", text):
                fail(errors, f"{rule_file}: active historical stub must not carry Unity lifecycle examples")
            if re.search(r"\b(GetComponent|UnityEngine\.Pool|Instantiate|Debug\.Log)\b", text):
                fail(errors, f"{rule_file}: active historical stub must not carry generic Unity examples")
            archived = historical_agent_rules_archive / rule_file.name
            if not archived.exists():
                fail(errors, f"{archived}: missing archived body for historical agent rule")

    cursor_index_text = read_text(cursor_index)
    for needle in (
        "THIN_ROUTER / NOT_PROJECT_LAW",
        "C:\\hades\\Hecton8\\AGENTS.md",
        "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
        "complete document",
        "Docs/DEPRECATED/CursorRulesHistorical_20260606/index.mdc",
    ):
        if needle not in cursor_index_text:
            fail(errors, f"{cursor_index}: Cursor router missing {needle}")
    if "alwaysApply: true" not in cursor_index_text:
        fail(errors, f"{cursor_index}: Cursor root router must remain alwaysApply")
    if not (historical_cursor_rules_archive / "index.mdc").exists():
        fail(errors, f"{historical_cursor_rules_archive / 'index.mdc'}: missing archived Cursor index body")

    generic_cursor_patterns = (
        "GameManager.Instance",
        "UnityEngine.Pool",
        "EventChannel",
        "EventChannels",
        "YourCompany",
        "Update(",
        "FixedUpdate(",
        "LateUpdate(",
        "OnUpdate(",
        "Resources.Load",
        "Netcode for GameObjects",
        "UI Toolkit (not UGUI)",
    )
    for cursor_rule in (ROOT / ".cursor" / "rules").glob("*.mdc"):
        text = read_text(cursor_rule)
        if "alwaysApply: true" in text:
            fail(errors, f"{cursor_rule}: historical Cursor rule must not alwaysApply")
        if re.search(r"(?m)^globs:", text):
            fail(errors, f"{cursor_rule}: historical Cursor rule must not keep active globs")
        if "HISTORICAL REFERENCE ONLY" not in text:
            fail(errors, f"{cursor_rule}: missing historical-reference marker")
        if "NOT_PROJECT_LAW" not in text:
            fail(errors, f"{cursor_rule}: missing non-authority marker")
        if "Docs/DEPRECATED/CursorRulesHistorical_20260606" not in text:
            fail(errors, f"{cursor_rule}: missing Cursor archive pointer")
        for forbidden in generic_cursor_patterns:
            if forbidden in text:
                fail(errors, f"{cursor_rule}: active Cursor stub carries stale generic rule text: {forbidden}")
        archived = historical_cursor_rules_archive / "rules" / cursor_rule.name
        if not archived.exists():
            fail(errors, f"{archived}: missing archived body for Cursor historical rule")

    if GLOBAL_CODEX.exists():
        global_text = read_text(GLOBAL_CODEX)
        for needle in (
            "[GLOBAL CODEX ROUTER]",
            "C:\\hades\\Hecton8\\AGENTS.md",
            "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
            "complete document",
            "[HECTON-8 SUBAGENTS]",
            "not subagent rules",
        ):
            if needle not in global_text:
                fail(errors, f"{GLOBAL_CODEX}: global router missing {needle}")
        if "35 distilled" in global_text:
            fail(errors, f"{GLOBAL_CODEX}: stale mandate count 35 remains")

    if GLOBAL_GEMINI.exists():
        global_gemini_text = read_text(GLOBAL_GEMINI)
        for needle in (
            "[GLOBAL GEMINI / ANTIGRAVITY ROUTER]",
            "C:\\hades\\Hecton8\\GEMINI.md",
            "C:\\hades\\Hecton8\\AGENTS.md",
            "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
            "complete document",
            "nested Antigravity workspaces",
            "Subagents inherit HECTON-8 law",
        ):
            if needle not in global_gemini_text:
                fail(errors, f"{GLOBAL_GEMINI}: global Gemini router missing {needle}")
        if "35 distilled" in global_gemini_text:
            fail(errors, f"{GLOBAL_GEMINI}: stale mandate count 35 remains")

    if GEMINI_UNITY_MCP.exists():
        gemini_mcp_text = read_text(GEMINI_UNITY_MCP)
        for needle in (
            "HECTON-8 guard",
            "C:\\hades\\Hecton8\\GEMINI.md",
            "C:\\hades\\Hecton8\\AGENTS.md",
            "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
            "root law overrides",
            "current process gates",
        ):
            if needle not in gemini_mcp_text:
                fail(errors, f"{GEMINI_UNITY_MCP}: Unity MCP instructions missing {needle}")

    for needle in (
        "THIRD-PARTY TOOL SHIM / NOT PROJECT LAW",
        "one-way adapter",
        "If this file conflicts with project authority",
        "Hecton8\\AGENTS.md",
        "Docs\\AGENT_AUTHORITY_ROUTING.md",
        "exactly `2-8`",
        "LANE_CLASS",
        "Do not launch or trigger Unity",
        "Antigravity brain",
    ):
        assert_contains(errors, project_gemini, needle)

    actual_mandates = len(list((ROOT / ".agents-skills").glob("*.txt")))
    skills_text = read_text(skills_readme)
    match = re.search(r"Current inventory:\s*`?(\d+)`?\s+`?\.txt`?\s+mandates", skills_text)
    if not match:
        fail(errors, f"{skills_readme}: cannot find mandate inventory line")
    elif int(match.group(1)) != actual_mandates:
        fail(errors, f"{skills_readme}: inventory {match.group(1)} != actual {actual_mandates}")

    ledger_bytes = ledger.read_bytes()
    marker = b"\xef\xbb\xbf[CORE IDENTITY]"
    idx = ledger_bytes.find(marker)
    if idx < 0:
        fail(errors, f"{ledger}: cannot find preserved old AGENTS.md body marker")
    else:
        tail_hash = hashlib.sha256(ledger_bytes[idx:]).hexdigest().upper()
        ledger_text = read_text(ledger)
        stored = re.search(r"Source body SHA256:\s*`([A-Fa-f0-9]{64})`", ledger_text)
        if not stored:
            fail(errors, f"{ledger}: missing Source body SHA256")
        elif tail_hash != stored.group(1).upper():
            fail(errors, f"{ledger}: preserved body hash mismatch {tail_hash} != {stored.group(1).upper()}")

    for path, needle in (
        (root_agents, "Docs\\AGENTS_RULE_DETAIL_LEDGER.md"),
        (routing, "Docs/AGENTS_RULE_DETAIL_LEDGER.md"),
        (governance, "Docs/AGENTS_RULE_DETAIL_LEDGER.md"),
        (docs_readme, "Docs/AGENTS_RULE_DETAIL_LEDGER.md"),
        (project_bibles, "Docs/AGENTS_RULE_DETAIL_LEDGER.md"),
    ):
        assert_contains(errors, path, needle)

    for authority_doc in (routing, docs_readme, project_bibles):
        for needle in ("GEMINI.md", ".gemini", "Gemini/Antigravity"):
            assert_not_contains(errors, authority_doc, needle)

    route_files = set(re.findall(r": `([^`]+\.md)`", read_text(project_bibles)))
    route_bibles_exempt_from_first20 = {"TASTE.md", "VISION_LOCKS.md", "quality.md"}
    route_completeness_patterns = {
        "GlobalQualityWeight": r"GlobalQualityWeight",
        "proof/evidence/artifacts": r"Proof|Evidence|Artifacts",
        "rejection language": r"Reject|Rejection|Forbidden|Forbid|Banned|Ban",
        "acceptance language": r"Acceptance Sentence|Acceptance Statement|accepted only",
        "runtime/truth/boundary section": r"(?im)^## .*?(Runtime|Hot|Performance|Truth|Ownership|Boundary)",
    }
    combined_builder_text = read_text(combined_bibles_builder)
    for route_file in sorted(route_files):
        route_path = ROOT / route_file
        if not route_path.exists():
            fail(errors, f"{project_bibles}: route bible missing on disk: {route_file}")
            continue
        route_text = read_text(route_path)
        if route_file not in route_bibles_exempt_from_first20 and "First-20 Route Hook" not in route_text:
            fail(errors, f"{route_path}: missing First-20 Route Hook")
        for label, pattern in route_completeness_patterns.items():
            if not re.search(pattern, route_text, re.IGNORECASE | re.MULTILINE):
                fail(errors, f"{route_path}: missing route completeness marker for {label}")
        assert_no_unguarded_route_bible_language(errors, route_path)
        # Extended 2026-07-28. Until then assert_referenced_paths_exist ran on only
        # nine routing files, so a route bible could name an owner `.cs` that had
        # moved and nothing failed. Measured at the time: three bibles cited moved
        # owner files (terrain.md twice, world.md once). Route bibles are owner-file
        # contracts, so a wrong path there sends the next agent to a file that is
        # not the owner.
        assert_referenced_paths_exist(errors, route_path)
        if f'"{route_file}"' not in combined_builder_text:
            fail(errors, f"{combined_bibles_builder}: SOURCE_FILES missing route bible {route_file}")

    # Mandates are owner-contract documents too, and they were the other half of the
    # uncovered surface. Same rationale as the route-bible line above: a mandate that
    # cites a moved `Assets/`, `Docs/`, `Tools/` or `.agents-skills/` path sends the
    # next agent to the wrong owner, and nothing failed before 2026-07-28.
    # The deliberate limit: ACTIVE_PATH_PREFIXES does not include bare basenames or
    # `Runtime/` / `Tests/`-style fragments, so prose citing a symbol or a partial path
    # is unchecked. Widening it was MEASURED on 2026-07-28 and rejected, so do not try
    # again without reading this:
    #
    # A wider rule was probed — treat any backticked token ending in a source or doc
    # extension as dead when its BASENAME exists nowhere in the repo. Over 46998 indexed
    # basenames and 25334 path-shaped citations it produced 1055 findings, of which
    # essentially none were defects. Two reasons, both structural:
    #   * The bulk sit in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`,
    #     `Docs/_Archive/**` and dated `Docs/Reports/**`. Those documents cite evidence
    #     artifacts that were deliberately cleaned up — citing a deleted artifact is
    #     what a historical ledger IS, so flagging it is wrong by construction.
    #   * Restricted to the live authority surface (root bibles, mandates, routing and
    #     governance docs — 167 files, 1057 citations) the count drops to 53, and 52 of
    #     those are bare EXTENSIONS: rule prose says "any agent touching `.cs`/`.shader`"
    #     and a basename rule reads `` `.cs` `` as a filename. The 53rd is
    #     `.codex_ops\ORCHESTRATION_MEMORY.md`, which lives outside this repository and
    #     is correctly absent from it.
    #
    # So: zero real dead citations on the live surface, and the wider rule buys noise.
    # The prefix list is the right shape. If you want more coverage, add a specific
    # prefix for a directory that actually exists, never a bare-basename rule.
    for mandate_path in sorted((ROOT / ".agents-skills").glob("*.txt")):
        assert_referenced_paths_exist(errors, mandate_path)

    for path, forbidden in (
        (ROOT / ".agents-skills" / "AUDIO_Hrtf_Binaural_Spatialization.txt", "Good enough"),
        (ROOT / "streaming.md", "World streaming must consider"),
    ):
        assert_not_contains(errors, path, forbidden)
    assert_contains(errors, ROOT / "streaming.md", "World streaming decisions must evaluate")

    allowed_root_docs = {
        # Standard repository / community-health files. Policy: Docs/ROOT_DOCS_REFERENCE.md Root Policy.
        "README.md",
        "CONTRIBUTING.md",
        "LICENSE.md",
        "CHANGELOG.md",
        "SECURITY.md",
        "CODE_OF_CONDUCT.md",
        "AGENTS.md",
        "CLAUDE.md",
        "COMMON_SENSE.md",
        "PROJECT_BIBLES.md",
        "VISION_LOCKS.md",
        "TASTE.md",
        "textes.md",
        "MASTER_RELEASE_WORK_PLAN.md",
        "BUILD_PLAYTEST_ISSUES.md",
        "GEMINI.md",
        "HECTON8_ORCHESTRATOR.md",
        "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md",
        *route_files,
    }
    for root_md in ROOT.glob("*.md"):
        if root_md.name not in allowed_root_docs:
            fail(errors, f"{root_md}: root markdown is not allowed by PROJECT_BIBLES.md / ROOT_DOCS_REFERENCE.md")

    for required_route in (
        "3DMODEL_HARD_SURFACE_MODULES.md",
        "3DMODEL_EQUIPMENT_PROPS.md",
        "3DMODEL_FLORA_CORAL.md",
        "3DMODEL_FAUNA.md",
        "3DMODEL_GEOLOGY_ROCKS.md",
        "3DMODEL_TEXTURES_MATERIALS.md",
    ):
        if required_route not in route_files:
            fail(errors, f"{project_bibles}: missing generated-asset family route {required_route}")

    for path, needle in (
        (autonomous_orchestrator, "LOCAL GUI ORCHESTRATION LAW"),
        (autonomous_orchestrator, "current VS Code Codex"),
        (autonomous_orchestrator, "HECTON8_ORCHESTRATOR.md"),
        (autonomous_orchestrator, "Historical Mission Capture Boundary"),
        (global_registry_mandate, "Current HECTON-8 Source Override"),
        (global_registry_mandate, "Do not implement legacy examples"),
        (global_registry_mandate, "GlobalRegistry.TryRegisterUpdatable"),
    ):
        assert_contains(errors, path, needle)
    assert_not_contains(errors, autonomous_orchestrator, "Current User Mission, Captured")

    assert_order(errors, governance, "../PROJECT_BIBLES.md", "../.agents-skills/README.md")
    assert_order(errors, docs_readme, "PROJECT_BIBLES.md", ".agents-skills/README.md")

    persona_text = read_text(persona)
    for needle in (
        "DEPRECATED HISTORICAL AGENT",
        "user-invocable: false",
        "DEPRECATED_STUB / NOT_USER_INVOCABLE / NOT_PROJECT_LAW",
        "C:\\hades\\Hecton8\\AGENTS.md",
        "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
        "Docs/DEPRECATED/AgentShimsHistorical_20260606/github_unity-anime-dev.agent.md",
    ):
        if needle not in persona_text:
            fail(errors, f"{persona}: persona deprecated stub missing {needle}")
    for forbidden in (
        "You are Chad",
        "alpha male",
        "victory statement",
        "user-invocable: true",
    ):
        if forbidden in persona_text:
            fail(errors, f"{persona}: deprecated persona body remains: {forbidden}")
    if not (historical_agent_shims_archive / "github_unity-anime-dev.agent.md").exists():
        fail(errors, f"{historical_agent_shims_archive / 'github_unity-anime-dev.agent.md'}: missing archived persona body")

    if errors:
        print("AGENT_RULE_ROUTING_CHECK=FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print("AGENT_RULE_ROUTING_CHECK=PASS")
    print(f"mandates={actual_mandates}")
    print(f"root_agents_lines={len(read_text(root_agents).splitlines())}")
    return 0


def self_test() -> int:
    """Reject-case proof for assert_referenced_paths_exist.

    Root law forbids adding a check without a reproducible reject case
    (`AGENTS.md` `[FORBID] Self-check cascade`). Path-existence coverage was
    extended to route bibles and mandates on 2026-07-28, so this proves both
    directions: a moved citation fails, and each legitimate not-on-disk idiom in
    PATH_REFERENCE_SKIP_TERMS stays silent. Runs on temp files, never the repo.
    """
    cases = (
        ("moved owner file",
         "| `Assets/_Project/Scripts/World/GoneAway_Owner.cs` | owner contract |", True),
        ("missing Docs artifact",
         "See `Docs/Reports/NO_SUCH_BUILD_LOG_20260101.log` for the pass.", True),
        ("missing tool",
         "Run `Tools/Docs/NoSuchTool.py` after edits.", True),
        ("real existing file",
         "Canonical law is `Docs/AGENT_AUTHORITY_ROUTING.md`.", False),
        ("dump-target idiom",
         "300-entry ring with dump target `Docs/AgentLogs/Dump_FAKE_ID.bin`.", False),
        ("migrated-away idiom",
         "migrated-away path: `Assets/_Project/Scripts/World/Old.cs` is not the current location.", False),
        ("artifact-missing idiom",
         "Cited artifact `Docs/Reports/GONE.log` — artifact missing, so this is not proof.", False),
    )

    wrong = 0
    with tempfile.TemporaryDirectory() as tmp:
        probe = Path(tmp) / "probe.md"
        for name, line, should_fail in cases:
            probe.write_text(line + "\n", encoding="utf-8")
            errors: list[str] = []
            assert_referenced_paths_exist(errors, probe)
            fired = bool(errors)
            if fired != should_fail:
                wrong += 1
                print(f"- SELFTEST WRONG: {name}: expected "
                      f"{'FAIL' if should_fail else 'PASS'}, got "
                      f"{'FAIL' if fired else 'PASS'}")

    if wrong:
        print("AGENT_RULE_ROUTING_SELFTEST=FAIL")
        return 1
    print("AGENT_RULE_ROUTING_SELFTEST=PASS")
    print(f"cases={len(cases)}")
    return 0


if __name__ == "__main__":
    if "--self-test" in sys.argv[1:]:
        sys.exit(self_test())
    sys.exit(main())
