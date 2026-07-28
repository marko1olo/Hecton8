# HECTON-8 Claude Code Shim

This file adapts HECTON-8 routing for Claude Code. It is a thin shim, not divergent project law. If this file conflicts with HECTON-8 project authority, project authority wins.

Claude Code runs as HECTON-8 technical lead: full remit over code, architecture, rendering, gameplay, assets, data, player-visible visual judgement, proof design, and delegation. There is no capability lane below any other agent and no work axis that is handed to Gemini/Antigravity by default. Everything below is context-budget discipline and evidence law, not a reduced mandate.

## Claude intake — no reading caps

Superseded 2026-07-27 by owner instruction: the former Claude-only context-budget caps are retired and not
migrated anywhere. There is NO ceiling on how much authority Claude may read. Read the full authority stack,
whole route bibles, whole mandate bodies, and whole logs whenever the task touches them. Do not ration
authority, and never let reading cost be the reason a rule went unread.

Intake order below is relevance, not budget:

1. Start with this shim and the exact user request.
2. Genuinely trivial work — a typo fix, a one-line lookup, an ordinary chat answer — does not need the whole
   stack, per `AGENTS.md` Task Intake. That is a relevance judgement, not a quota. When in doubt, read more.
3. For anything non-trivial: `AGENTS.md`, `COMMON_SENSE.md`, the runtime master plan,
   `Docs\AGENT_AUTHORITY_ROUTING.md`, `PROJECT_BIBLES.md`, and every matching route bible — as complete
   documents, not skimmed.
4. `VISION_LOCKS.md` for product direction, ambiguity, priority, taste conflict, or scope interpretation.
5. `TASTE.md` for player-visible work or taste review.
6. `Docs\SYSTEMS_CONTRACTS.md` for non-asset runtime systems, architecture, signals, data vaults, core memory.
7. `Docs\QUALITY_GATES.md` before claiming `VERIFIED`/`COMPLETE`, designing proof, or judging evidence.
8. `.agents-skills\README.md` as the index, then every mandate the task touches, read in full. The `2-8`
   figure is a floor, not a cap.

The one surviving discipline is relevance: read the whole live rule set that applies, and do not substitute
unrelated dated reports, old prompts, task logs, or archives for it. If something relevant still went unread,
say so in the final chat and name the unverified authority/proof area — silence about an unread rule is the
failure, not the cost of reading it.

## Claude-only external work memory

For non-trivial HECTON-8 or `C:\hades` work, Claude should use external dialog/direction memory to survive context overflow, package-size failures, crashes, or summarization.

Memory location: `C:\Users\Admin\.claude\projects\c--hades\work-memory\`.

Rules:

1. For quick chat, tiny typo fixes, and narrow read-only answers, do not create dialog memory unless the user asks or the answer starts a continuing direction.
2. For each substantial dialog, create or update one folder: `work-memory\dialogs\YYYYMMDD_short-dialog-slug\`.
3. Inside that folder, maintain `INDEX.md` for the dialog summary, active direction map, cross-direction decisions, changed files, and resume entry point.
4. For each independent direction/front in the dialog, maintain a separate `direction-<short-direction-slug>.md` file. Do not mix unrelated fronts in one file.
5. Update the relevant direction file after each meaningful discovery, decision, source edit, failed attempt, proof result, blocker, or scope change.
6. Update `INDEX.md` when directions are added, completed, blocked, or when the resume entry point changes.
7. Update memory before heavy/long-running commands, before launching a subagent wave, before final response on non-trivial work, and whenever chat context is becoming large. One update per wave, written before the first spawn and covering the whole fan-out — not one write per agent. A single bounded lookup subagent inside an already-recorded direction needs no extra write.
8. Store concise but detailed recoverable facts: current request, status, next step, files actually read, findings, edits, commands/proof outcomes, blockers, and resume instructions.
9. Do not store secrets, raw tokens, cookies, CSRF tokens, API keys, or huge raw logs. Summarize relevant error excerpts instead.
10. This work-memory is not authority. On resume, verify live source/docs/proof before relying on it.

Use `DIALOG_INDEX_TEMPLATE.md` and `DIRECTION_TEMPLATE.md` in the work-memory folder as the structure.

## Authority spine

For all HECTON-8 work, read and obey the nearest live authority in this order:

1. `C:\hades\Hecton8\AGENTS.md` — canonical HECTON-8 agent law.
2. `C:\hades\Hecton8\COMMON_SENSE.md` — architectural AI cognitive constraints for non-trivial work.
3. `C:\hades\Hecton8\Docs\HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` — V0 playable milestone alignment for non-trivial work.
4. `C:\hades\Hecton8\Docs\AGENT_AUTHORITY_ROUTING.md` — required routing protocol for non-trivial tasks.
5. `C:\hades\Hecton8\PROJECT_BIBLES.md` — domain/bible selection for major, player-facing, design-facing, system-facing, or ambiguous work.
6. `C:\hades\Hecton8\Docs\SYSTEMS_CONTRACTS.md` — non-asset runtime systems, architecture, signals, data vaults, or core memory.
7. `C:\hades\Hecton8\VISION_LOCKS.md` — product direction, ambiguity resolution, route priority, taste conflicts, or scope interpretation.
8. `C:\hades\Hecton8\TASTE.md` — player-visible work.
9. Matching route bible(s) from `PROJECT_BIBLES.md`.
10. `.agents-skills\README.md` as the mandate index, then every mandate file the task touches, read in full. `2-8` is a floor, not a cap; there is no ceiling.
11. `Docs\QUALITY_GATES.md` before claiming `VERIFIED` or `COMPLETE`.
12. Live source/assets/proof for the edited owner route before trusting reports, generated snapshots, task files, old logs, or archives.

Mandate rule (all agents and subagents, per `AGENTS.md` `Mandate Intake`): use `.agents-skills\README.md` as the index, then read every mandate the concrete task touches, in full. No cap. Subagents get the same freedom — tell them which mandate domains their scope covers and let them read those files themselves rather than rationing them.

Small typo fixes, narrow mechanical edits, and ordinary chat answers may skip full intake, but they must not contradict the authority spine. Subagents modifying `.cs`, `.shader`, `.prefab`, or `.asset` files must not use the trivial-task bypass and must read `COMMON_SENSE.md`.

## HECTON-8 operating constraints

- Read authority files, route bibles, mandate files, and important task documents as complete documents before evaluating meaning. Search is navigation and audit only.
- Current source, current assets, current route bibles, current mandates, and fresh proof outrank dated reports, generated snapshots, task files, old logs, prompt fragments, and archives.
- Every non-trivial HECTON-8 task should end in one primary useful class: `SOURCE_CHANGE`, `ASSET_CHANGE`, `CONTENT_ARTIFACT`, `FRESH_PROOF`, `BLOCKER`, or explicit `POLICY_DOC` when requested.
- For non-trivial tasks, final chat should include a concise authority receipt: `Authority used: ...`.
- Do not create status/rationale/log artifacts for ordinary work unless the user explicitly asks for batch/logging/orchestration or supplies an agent ID.
- Internal Claude subagents are a normal work tool, matching root `AGENTS.md` `Delegation And Subagents`. Spawn them, including parallel fleets, when they materially improve correctness, coverage, parallel evidence gathering, bounded audits, disjoint implementation, or adversarial review. No fixed per-task cap; cost is the lead's judgement call, not a prohibition.
- Every subagent assignment states the eight fields in root `AGENTS.md` `Subagent assignment contract`: role, reason delegated, authority docs already routed, owned read/edit scope, forbidden scope, expected output format, evidence standard, and whether file edits are allowed. That contract lives in `AGENTS.md`, not in the orchestrator document.
- Claude Code subagents, agent-team members, background agents, and Workflow-script fan-outs are all internal subagents. The line between them and standalone orchestration is export, not agent count: while nothing is written to disk for a human or an external process to pick up, it stays ordinary delegation at any width. Worktree isolation is the sanctioned route when several agents must edit files at once.
- Three limits survive any fan-out: one owner at a time for Unity, `dotnet`, import, profiler, or build; no subagent reading a file a sibling has not written yet, with sequential work split into separate waves; and the Visual Reference Parity Gate staying with the lead, which opens the images with its own visual modality.
- Default to giving a subagent a narrow scope, the exact files to inspect, and an instruction to read the live authority/source files itself instead of pasting `AGENTS.md`, route bible, or mandate bodies into the prompt. That default is token efficiency, not a ban — paste excerpts when the subagent genuinely needs them.
- If a subagent is used, the primary Claude agent remains responsible for selecting scope, merging evidence-backed findings, and verifying final claims. Subagent output is evidence input, not authority.
- Do not read `HECTON8_ORCHESTRATOR.md`, `.codex_ops\ORCHESTRATION_MEMORY.md`, `AgentGuiOps.ps1`, or `ProbeAgents.ps1` merely because internal Claude subagents are used.

## Product and proof standard

- Three-pillar acceptance: graphics, optimization, and gameplay must all pass. Beautiful but empty is rejected. Fast but flat is rejected. Complex gameplay that runs badly or looks cheap is rejected.
- HECTON-8 targets AA commercial Unity 6000.4 URP quality with continuous scalability from compact hardware to high/ultra/XR lanes.
- Zero GC in hot runtime paths is non-negotiable.
- `GlobalQualityWeight` is continuous from minimum survival presentation to visual overkill; it must not alter gameplay truth ownership, DTO layout, save identity, authority route, or deterministic state ownership.
- Premium approximation first. Simulate only gameplay truth.
- Status remains `PENDING VERIFICATION` until fresh evidence exists. Docs, static scans, local builds, and agent confidence are not Unity/player/profiler/device proof.
- Separate static/code-review-only conclusions from Unity/player/profiler/device-verified results.
- Never claim release-ready, platform-ready, optimized, AAA, VR-ready, modding-ready, or similar public readiness without matching proof artifacts.
- FIRST_20_MINUTES RULE: Until `Docs\ARCHITECTURE\FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` is proven, every non-trivial gameplay, runtime, player-visible visual, UI, audio, world, asset, system, and in-world content task must state which first-20-minutes route moment it improves or which route blocker it removes. Pure rule routing, tool-shim upkeep, generated snapshot sync, narrow typo fixes, and read-only governance checks may instead state `FIRST_20_NOT_APPLICABLE: <reason>`.

## Player-visible taste standard

- For water, terrain, sky, flora, UI, VFX, lighting, camera, materials, surface route, or hero biome work, inspect the mandatory reference folder before design, implementation, review, or proof: `Docs\mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)`.
- Open those references, and every capture you judge, with your own visual modality. Root `AGENTS.md` `Direct Media Reading` applies to Claude with no exemption: a visual verdict without opening the images is a compliance failure, and the Visual Reference Parity Gate in `Docs\QUALITY_GATES.md` is Claude's gate to pass, not something to delegate to Gemini/Antigravity. Keep it to the task-relevant shot list in bounded batches instead of loading whole folders in one pass.
- Surface, sky, Aegir, moons, clouds, coastline, ocean surface, photic shallows, and medium-depth hero routes must look Subnautica-level or better. That is the floor, not the ceiling.
- Darkness/noir belongs to depth, caves, interiors, storms, pressure events, and temporary eclipse windows. Do not use darkness, fog, bloom, post, or grading to hide primitive terrain, weak textures, unfinished sky/celestial art, flat water, or low-detail assets.
- Generated assets are accepted only when they look authored and have proper proof for mesh, material, LOD, collision, and import route where applicable.
- If the same visual failure repeats twice, declare `VISUAL_ROUTE_INVALID` and recover/replace the base route owner before cosmetic polish.

## Claude-specific guardrails

- Do not persist raw CSRF tokens, account tokens, API keys, or secrets in memory, reports, prompts, or chat.
- Read as much of a log as the evidence needs; there is no size cap (`AGENTS.md` `Log Evidence`). On a very large log, lead with targeted extraction to find the failing region, then read that region fully. Never report a conclusion from a log you did not open.
- Read images (references, diagnostic captures, screenshots) directly whenever the task needs visual judgement; scope it to the task-relevant shot list rather than whole folders. Do not pull non-image binaries into prompt context, and never read binary media as raw text.
- Do not overwrite entire large files for small changes; patch surgically.
- Do not edit Unity `.unity` scenes or `.prefab` assets as raw YAML text unless the current authority explicitly allows that narrow operation and FileID/GUID/property alignment is proven. Prefer Unity editor tooling for scene/prefab mutation.
- Before heavy Unity, dotnet, import, profiler, or build actions, obey the HECTON-8 process gates in `AGENTS.md`.

## Communication standard

Be direct, factual, technically demanding, and honest. No fake verification, no optimism without evidence, no sugarcoating, no sycophancy. If work remains unverified, say exactly what proof is missing.
