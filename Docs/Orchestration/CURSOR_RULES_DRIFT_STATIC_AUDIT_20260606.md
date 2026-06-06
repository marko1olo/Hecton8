# Cursor Rules Drift Static Audit - 2026-06-06

Status: `STATIC_REPAIRED / NO_RUNTIME_PROOF_NEEDED`
Evidence class: `STATIC_DOC + STATIC_FILESYSTEM + STATIC_TOOL`

No Unity, MCP, Play Mode, profiler, import/build, scene, prefab, material, ProjectSettings, raw YAML, deletion, restore, move, stage, checkout, copy, revert, or commit action was performed.

## Problem

Active Cursor `.mdc` rules contained stale generic Unity guidance that could steer future agents away from HECTON-8 law.

Rejected live patterns included:

- generic `GameManager.Instance`;
- generic `UnityEngine.Pool`;
- ScriptableObject `EventChannel` / `EventChannels`;
- `YourCompany.*` namespaces;
- hot Unity lifecycle defaults;
- `Resources.Load`;
- Netcode for GameObjects as default authority;
- UI Toolkit-only stance.

These conflict with HECTON-8 root law: `GlobalRegistry`, typed `SignalBus`, DataVault ownership, no hot lifecycle defaults, project-specific pooling/data routes, UI route bibles, and proof discipline.

## Action

- Preserved the old Cursor rule bodies under `Docs/DEPRECATED/CursorRulesHistorical_20260606/`.
- Replaced `.cursor/index.mdc` with a thin always-on router to root `AGENTS.md`, `Docs/AGENT_AUTHORITY_ROUTING.md`, `PROJECT_BIBLES.md`, and `.agents-skills/README.md`.
- Replaced `.cursor/rules/*.mdc` with historical stubs that are not project law and point to the archived old bodies.
- Kept `.cursor/rules/AGENTS.md` as the existing Cursor shim.
- Updated `Docs/AGENT_AUTHORITY_ROUTING.md` to classify Cursor rules and archive provenance.
- Extended `Tools/Docs/TestAgentRuleRouting.py` so future routing checks fail if active Cursor rules regain stale generic law or lose archive pointers.

## Verification

- `python -B Tools\Docs\TestAgentRuleRouting.py` -> `AGENT_RULE_ROUTING_CHECK=PASS`, `mandates=80`, `root_agents_lines=409`.
- Hidden scan for stale Cursor generic patterns returned no matches.
- Hidden scan for `alwaysApply: true` under `.cursor` returns only `.cursor/index.mdc`.
- `git diff --check` on touched routing files returned no whitespace errors; Git reported LF-to-CRLF warnings only.

## Current Disposition

- `.cursor/index.mdc`: active router only.
- `.cursor/rules/*.mdc`: historical stubs only.
- `Docs/DEPRECATED/CursorRulesHistorical_20260606/`: full old Cursor bodies preserved.
- Root `AGENTS.md` was not edited.

Final status: `CURSOR_GENERIC_RULE_DRIFT_REPAIRED_STATICALLY`.
