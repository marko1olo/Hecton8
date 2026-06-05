# Rationale 2021 - Multi-Orchestrator Git Protocol

Status: STATIC VERIFIED
Date: 2026-06-04

## Decisions

1. Protocol is docs-only.
   - Reason: user explicitly forbade Unity/build/import and active Unity owner work is ongoing.
   - Consequence: no runtime/Unity/profiler proof is claimed.

2. Unity slot is serialized through one named owner.
   - Reason: `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` and the current day log both identify Unity as the shared bottleneck.
   - Consequence: second orchestrators default to `STATIC_ONLY` until handoff.

3. Git strategy uses template branch/worktree names, not current remote assumptions.
   - Reason: current remotes/hashes were not needed for protocol content.
   - Consequence: protocol requires fetch/status/branch inspection before each operation.

4. Gemini budget is recorded as account-count capacity only.
   - Reason: repo files must not expose browser account names/emails.
   - Consequence: protocol uses `Account01`-style private labels only if scheduling is needed.

5. Static YAML cannot visually accept scenes/materials/water/sky/assets.
   - Reason: `quality.md` and `QA_Evidence_Text_Filter_Audit.txt` reject proof-label upgrades from static text.
   - Consequence: protocol separates static proof, Unity/editor proof, profiler proof, and acceptance.

6. No force-push or text-merge assumptions for Unity assets.
   - Reason: scenes, prefabs, materials, terrain layers, import settings, and `.meta` files are ownership-sensitive Unity artifacts.
   - Consequence: conflicts in those lanes require Unity-slot owner or integrator replay/proof.

