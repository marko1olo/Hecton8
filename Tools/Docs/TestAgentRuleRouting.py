#!/usr/bin/env python3
"""Static gate for HECTON-8 agent rule routing.

Checks the rule surfaces that decide what future agents read first.
This is intentionally narrow: no Unity, no build, no import.
"""

from __future__ import annotations

import hashlib
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
GLOBAL_CODEX = Path.home() / ".codex" / "AGENTS.md"
GLOBAL_GEMINI = Path.home() / ".gemini" / "GEMINI.md"
GEMINI_UNITY_MCP = Path.home() / ".gemini" / "antigravity-ide" / "mcp" / "unityMCP" / "instructions.md"


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
        if mirror.read_bytes() != root_text:
            fail(errors, f"{mirror}: must byte-match root AGENTS.md or become an explicit shim")

    for path in (root_agents, routing, project_bibles):
        assert_contains(errors, path, "complete document")

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
        "COLD ALLOC",
        "TryReserve",
        "MCP/Unity proof",
        "Do not read `HECTON8_ORCHESTRATOR.md`",
        "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md",
        "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict",
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
    assert_contains(errors, routing, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, project_bibles, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, root_docs_reference, "HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md")
    assert_contains(errors, root_docs_reference, "GEMINI.md")
    assert_contains(errors, project_bibles, "lane contracts")
    assert_contains(errors, project_bibles, "subagent rules")
    assert_contains(errors, project_bibles, "ordinary subagent rules")
    assert_contains(errors, project_bibles, "without pulling GUI/process orchestration")
    assert_contains(errors, project_bibles, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, governance, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, quality_gates, "Tasklocal Lane Contract Gate")
    assert_contains(errors, quality_gates, "TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict")
    assert_contains(errors, lane_contract_gate, "same-wave/sibling dependency guard")
    assert_contains(errors, lane_contract_gate, "DEPENDENCY_GUARD_TERMS")
    assert_contains(errors, lane_contract_gate, "DEPENDENCY_OUTPUT_TERMS")
    assert_contains(errors, lane_contract_gate, "weak dependency wording passed")

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
        if f'"{route_file}"' not in combined_builder_text:
            fail(errors, f"{combined_bibles_builder}: SOURCE_FILES missing route bible {route_file}")

    for path, forbidden in (
        (ROOT / ".agents-skills" / "AUDIO_Hrtf_Binaural_Spatialization.txt", "Good enough"),
        (ROOT / "streaming.md", "World streaming must consider"),
    ):
        assert_not_contains(errors, path, forbidden)
    assert_contains(errors, ROOT / "streaming.md", "World streaming decisions must evaluate")

    allowed_root_docs = {
        "AGENTS.md",
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


if __name__ == "__main__":
    sys.exit(main())
