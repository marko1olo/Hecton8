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
    project_gemini = ROOT / "GEMINI.md"
    routing = ROOT / "Docs" / "AGENT_AUTHORITY_ROUTING.md"
    governance = ROOT / "Docs" / "DOC_GOVERNANCE.md"
    docs_readme = ROOT / "Docs" / "README.md"
    project_bibles = ROOT / "PROJECT_BIBLES.md"
    orchestrator = ROOT / "HECTON8_ORCHESTRATOR.md"
    skills_readme = ROOT / ".agents-skills" / "README.md"
    ledger = ROOT / "Docs" / "AGENTS_RULE_DETAIL_LEDGER.md"
    persona = ROOT / ".github" / "agents" / "unity-anime-dev.agent.md"
    historical_agent_rules_archive = ROOT / "Docs" / "DEPRECATED" / "AgentRulesHistorical_20260605"

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
        "Reference image folder before design/implementation/review/proof",
        "Docs/Lore/WriterScenarioAgentPrompt.md",
    ):
        assert_contains(errors, routing, needle)

    for needle in (
        "Technical report means",
        "Before player-visible visual creation",
        "## Delegation And Subagents",
        "Any HECTON-8 agent may spawn/use subagents",
        "Audio import defaults",
        "Revert over hack for proven regressions",
        "Prefab/scene consistency guard",
        "COLD ALLOC",
        "TryReserve",
        "MCP/Unity proof",
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
        "LOCAL SUBAGENT PROTOCOL",
        "Subagents inherit HECTON-8 law",
    ):
        assert_contains(errors, orchestrator, needle)

    assert_contains(errors, root_agents, "lane contracts")
    assert_contains(errors, routing, "AGENT LANE CONTRACTS")
    assert_contains(errors, routing, "LOCAL SUBAGENT PROTOCOL")
    assert_contains(errors, routing, "Subagent use by an ordinary implementation")
    assert_contains(errors, project_bibles, "lane contracts")
    assert_contains(errors, project_bibles, "subagent rules")
    assert_contains(errors, project_bibles, "C:\\Users\\danat\\.gemini\\GEMINI.md")
    assert_contains(errors, project_bibles, "project `GEMINI.md`")

    assert_contains(errors, agent_rules_agents, "Authority delegates to `C:\\hades\\Hecton8\\AGENTS.md`")
    assert_contains(errors, agent_rules_agents, "Docs\\AGENT_AUTHORITY_ROUTING.md")

    for rule_file in (ROOT / ".agent" / "rules").glob("*.md"):
        text = read_text(rule_file)
        if rule_file.name != "AGENTS.md":
            if "alwaysApply: true" in text:
                fail(errors, f"{rule_file}: historical rule must not alwaysApply")
            if re.search(r"(?m)^globs:", text):
                fail(errors, f"{rule_file}: historical rule must not keep active globs")
            if "HISTORICAL REFERENCE ONLY" not in text:
                fail(errors, f"{rule_file}: missing historical-reference marker")
            if "HECTON-8 Authority Override" not in text:
                fail(errors, f"{rule_file}: missing HECTON-8 override header")
            if "not active HECTON-8 law" not in text:
                fail(errors, f"{rule_file}: missing non-authority warning")
            archived = historical_agent_rules_archive / rule_file.name
            if not archived.exists():
                fail(errors, f"{archived}: missing archived body for historical agent rule")

    if GLOBAL_CODEX.exists():
        global_text = read_text(GLOBAL_CODEX)
        for needle in (
            "[GLOBAL CODEX ROUTER]",
            "C:\\hades\\Hecton8\\AGENTS.md",
            "C:\\hades\\Hecton8\\Docs\\AGENT_AUTHORITY_ROUTING.md",
            "complete document",
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
        "Hecton8\\AGENTS.md",
        "Docs\\AGENT_AUTHORITY_ROUTING.md",
        "complete documents",
        "Any HECTON-8 agent may spawn/use subagents",
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

    for path, needle in (
        (routing, "C:\\Users\\danat\\.gemini\\GEMINI.md"),
        (routing, "C:\\Users\\danat\\.gemini\\antigravity-ide\\mcp\\unityMCP\\instructions.md"),
        (governance, "C:\\Users\\danat\\.gemini\\GEMINI.md"),
        (governance, "project `GEMINI.md`"),
        (docs_readme, "Gemini/Antigravity project entrypoint"),
    ):
        assert_contains(errors, path, needle)

    assert_order(errors, governance, "../PROJECT_BIBLES.md", "../.agents-skills/README.md")
    assert_order(errors, docs_readme, "PROJECT_BIBLES.md", ".agents-skills/README.md")

    persona_text = read_text(persona)
    if "DEPRECATED HISTORICAL AGENT" not in persona_text or "non-binding" not in persona_text:
        fail(errors, f"{persona}: persona agent must remain deprecated/non-binding")

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
