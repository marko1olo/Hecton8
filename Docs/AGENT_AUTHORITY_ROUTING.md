# HECTON-8 Agent Authority Routing

Status: STATIC_POLICY
Evidence class: STATIC_DOC
Owner: DOCS_ACTUALIZATION

Purpose: make every agent load the right authority files without losing rules, skipping domain bibles, or turning ordinary work into bureaucracy.

## Prime Rule

No rule, constraint, rejection gate, product vision lock, proof requirement, or workflow exception may be deleted merely because it is noisy. If a rule file is split, shortened, mirrored, or regenerated, the removed text must first be preserved in a named live source, route bible, mandate file, generated snapshot source, or explicit archive with provenance.

Routing reduces context noise. It does not weaken authority.

## Start Sequence

Every non-trivial HECTON-8 task starts with this sequence:

1. Read root `AGENTS.md`.
2. Read `COMMON_SENSE.md` to load the 18 architectural AI cognitive constraints (Thread safety, Unity GC, physics, etc). You must obey these implicitly.
3. Read `Docs\HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` to verify the task aligns with the current V0 playable milestone and native ownership debt reduction.
4. Classify the task domain and risk class.
5. Read `PROJECT_BIBLES.md` for root route selection when the task is major, player-facing, design-facing, system-facing, or ambiguous.
6. Read `Docs\SYSTEMS_CONTRACTS.md` if the task involves non-asset runtime systems, architecture, signals, data vaults, or core memory.
7. Read `VISION_LOCKS.md` when the user asks for product direction, ambiguity resolution, route priority, taste conflict, or scope interpretation.
8. Read `TASTE.md` for player-visible work, plus the matching root route bible from `PROJECT_BIBLES.md`.
9. Read `.agents-skills/README.md`, then exactly the `2-8` task-relevant mandate files that match the task domain, before non-trivial code, architecture, rendering, gameplay, asset, data, or technical-report work. `AGENTS.md` `Mandate Intake Discipline` governs the read order for every agent: index first, matching mandates only, never bulk-read mandate bodies for orientation.
10. Read `Docs\QUALITY_GATES.md` before claiming a task is VERIFIED or COMPLETE to ensure all necessary proof artifacts (profiler, GC, visual parity, NativeMemory) are generated.
11. Read live source/assets/proof for the edited owner route before trusting reports, generated snapshots, task files, or stale logs.

Small typo fixes, narrow mechanical edits, and ordinary chat answers may skip the full intake, but they must not contradict the authority spine. **CRITICAL SUBAGENT RULE:** Subagents modifying any `.cs`, `.shader`, `.prefab`, or `.asset` files are strictly forbidden from using this "trivial task" bypass. They MUST read `COMMON_SENSE.md`.

Authority files, route bibles, mandate files, and important task documents must be read as complete documents before meaning is evaluated. Text search is a navigation and audit tool only.

Technical report means an audit, policy review, architecture review, proof review, route review, or durable technical artifact. It does not mean the ordinary final chat summary after a code, asset, content, or docs task.

After intake, every non-trivial task must name one primary deliverable class: `SOURCE_CHANGE`, `ASSET_CHANGE`, `CONTENT_ARTIFACT`, `FRESH_PROOF`, `BLOCKER`, or explicit `POLICY_DOC`. Routing, scanning, checklist filling, validator output, and report synthesis are support work only unless the user directly requested that class.

FIRST_20_MINUTES RULE: Until `Docs\ARCHITECTURE\FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` is proven, every non-trivial gameplay, runtime, player-visible visual, UI, audio, world, asset, system, and in-world content task must state which first-20-minutes route moment it improves or which route blocker it removes. Pure rule routing, tool-shim upkeep, generated snapshot sync, narrow typo fixes, and read-only governance checks may instead state `FIRST_20_NOT_APPLICABLE: <reason>`.

Subagent use by an ordinary implementation, content, QA, or docs agent is governed by root `AGENTS.md` `Delegation And Subagents`. It does not require `HECTON8_ORCHESTRATOR.md`, `C:\hades\.codex_ops\ORCHESTRATION_MEMORY.md`, `AgentGuiOps.ps1`, or `ProbeAgents.ps1`.

Read `HECTON8_ORCHESTRATOR.md` only when the agent is actually creating/judging standalone agent batches, writing `taskslocal` files, controlling external IDE/browser/GUI sessions, operating local external-agent processes, or acting as the explicit controller for a multi-agent wave. Internal subagent spawning for bounded review, evidence gathering, synthesis, or disjoint implementation remains ordinary delegation, not local orchestration.

Read `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` only when the user explicitly asks for local VS Code Codex GUI control, autonomous workstation control, or a night/day run that launches and monitors Codex GUI agents on this machine. It is process-control law, not ordinary implementation or subagent authority.

## Task Classes

| Task class | Mandatory authority beyond `AGENTS.md` |
|---|---|
| Ordinary runtime/gameplay implementation | `PROJECT_BIBLES.md`, `quality.md`, matching route bible, owner source/call sites, `.agents-skills/README.md`, exactly `2-8` matching mandates; `VISION_LOCKS.md` only when product ambiguity changes behavior |
| Player-visible water, terrain, sky, flora, UI, VFX, lighting, camera, materials, surface route, or hero biome | Reference image folder `Docs\mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)` before design/implementation/review/proof, Visual Reference Parity Gate in `Docs/QUALITY_GATES.md`, `PROJECT_BIBLES.md`, `TASTE.md`, matching route bibles, matching visual/performance mandates |
| Product vision, taste ambiguity, route priority, feature interpretation | `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, `TASTE.md`, matching route bible |
| Runtime architecture, bootstrap, global authority, signal/data ownership | `PROJECT_BIBLES.md`, `systems.md`, `data.md`, `performance.md`, global-authority architecture docs, matching `ARCH_*`, `DATA_*`, `OPT_*` mandates |
| Hot-path code, Burst, jobs, memory, GPU upload, DTOs | `performance.md`, `data.md` or `compute.md` as applicable, `.agents-skills/README.md`, matching `OPT_*`, `DATA_*`, `GPU_*`, `MATH_*` mandates |
| Physics, vehicle, collision, pressure, flooding, tethers | `physics.md`, related gameplay/vehicle/survival bible, matching `PHYS_*`, `MATH_*`, `OPT_*` mandates |
| UI, menus, HUD, terminals, localization, settings | `ui.md`, `UI_MENU_SCREEN_STANDARDS.md` or `UI_DIEGETIC_HUD_STANDARDS.md`, `settings.md` or `localization.md` if touched, matching `UI_*` mandates |
| Narrative, codex, diaries, in-world prose | `VISION_LOCKS.md`, `writing.md`, `narrative.md`, `localization.md` for all in-world content; specifically apply `writing.md` Anti-AI Prose Ban, LLM Style Suppression Law, Creative Freedom Envelope, Risk Word And Rhythm Firewall, AI Phrase Family Quarantine, Living Prose Floor, Zero-Shot Writer Contract, Few-Shot Rewrite Pattern Bank, Paragraph Evidence Firewall, Manual Redline Protocol, and Legacy Corpus Rewrite Law; `narrative.md` Evidence-First Prose Firewall; `localization.md` Multilingual AI-Style Localization Firewall; `Docs/Lore/WriterScenarioAgentPrompt.md` `AI_STYLE_FIREWALL`, `CREATIVE_FREEDOM_ENVELOPE`, `RISK_WORD_AND_RHYTHM_FIREWALL`, `AI_PHRASE_FAMILY_QUARANTINE`, `LIVING_PROSE_FLOOR`, `ZERO_SHOT_CONTRACT`, `FEW_SHOT_REWRITE LAW`, and Manual Multilingual Redline for dedicated writer/content agents; and `Docs/Lore/LoreCorpusManualRewriteAgentPrompt.md` for old corpus repair, release-set cleanup, full manual line review, and 15-locale rewrite waves. Canon source list and content owner are required. AppliedLore production tasks also use `Docs/QUALITY_GATES.md` AppliedLore Content Gate and must end in concrete `Docs/Lore/Grand_Library`, `Docs/Lore/AppliedContent`, DataMonolith import, page export, binding/route-card output, or a canon-source blocker |
| Public copy, store/social/creator text | `textes.md`, product proof gates for readiness claims |
| QA/proof/verification | `quality.md` for proof language, `Docs/QUALITY_GATES.md` for executable gates, matching route bible, `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`, current source/assets/proof artifacts; do not mine archives/logs unless named by the task or active ID |
| Batch-agent run with supplied ID | Matching prompt block only, `AGENTS.md`, task-relevant bibles/mandates, active Status/Rationale/LOG files for that ID. If a master batch path is not supplied, do not search neighboring prompts or `CURRENT_BATCH.md`; treat the user message as a batch assignment only when it directly asks for a batch-agent run |
| Orchestrator/controller work | `HECTON8_ORCHESTRATOR.md` including `AGENT LANE CONTRACTS`; add `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` only for explicit local VS Code Codex GUI control or autonomous workstation runs; use `C:\hades\.codex_ops\ORCHESTRATION_MEMORY.md` and GUI/process tools only when controlling external IDE/browser/GUI sessions or local external-agent processes; then task-relevant bibles/mandates |
| Documentation/rule routing work | `AGENTS.md`, this file, `PROJECT_BIBLES.md`, `.agents-skills/README.md`, `quality.md` for acceptance/proof language, `Docs/QUALITY_GATES.md` when executable gates or proof labels change, `Docs/DOC_GOVERNANCE.md`, `Docs/README.md`, live rule sources and generators. Run `Tools/Docs/TestMandateRegistry.py` after mandate edits. Use exactly `2-8` mandates only for technical policy/report work, not typo-only edits |

Reference image folder before design/implementation/review/proof means the mandatory player-visible reference folder listed in the player-visible task row above must be read before judging or changing water, terrain, sky, flora, UI, VFX, lighting, camera, materials, surface route, or hero biome work.

Visual Reference Parity Gate means the agent must compare current captures against the mandatory reference folder and the best-known internal baseline or current rejection matrix before accepting visual work. Raw diagnostic screenshots can prove rejection only. If current captures remain below April/previously-in-development visual baselines or the mandatory reference floor, the route is `VISUAL_ROUTE_INVALID` and must recover/replace owner stack before polish.

## Authority Receipt

For non-trivial tasks, final chat or explicit batch log must include a concise authority receipt:

`Authority used: AGENTS.md; PROJECT_BIBLES.md; <domain bible>; <mandate files>; <proof/source files>.`

Do not inflate this into a separate artifact for ordinary work. The receipt is a short proof that the agent routed correctly.

## Rule Surfaces

Canonical source:

- `AGENTS.md`

Routing sources:

- `Docs/AGENT_AUTHORITY_ROUTING.md`
- `PROJECT_BIBLES.md`
- `.agents-skills/README.md`
- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/AGENTS_RULE_DETAIL_LEDGER.md` for no-loss conflict resolution and migration provenance only; do not bulk-read it for ordinary work.

Delegated or generated surfaces:

- `C:\Users\Admin\.codex\AGENTS.md` is a global router only. It must route HECTON-8 work to root `AGENTS.md` and this file, and must not carry a divergent HECTON-8 law copy. If shortened, preserve the full previous text in an explicit recovery/provenance file.
- `.codexrules/AGENTS.md` and `.github/agents/AGENTS.md` are one-line `[DELEGATE]: C:\hades\Hecton8\AGENTS.md` stubs as of 2026-07-27. They were previously byte-identical 57 KB copies of root law; the copies were retired because a forgotten re-copy silently shipped stale law to the Codex and GitHub surfaces. Do not restore full copies. `Tools/Docs/TestAgentRuleRouting.py` accepts either form, but the stub is the intended steady state and removes the re-sync trap.
- `.agent/rules/AGENTS.md` delegates to root `AGENTS.md`. `.agent/skills/*` is a local helper surface, not authority; it may not add, relax, or reinterpret root law.
- `.vscode/AGENTS.md` is a thin VS Code router that delegates to root `AGENTS.md`. It is enforced by `Tools/Docs/TestAgentRuleRouting.py`.
- `.github/agents/unity-anime-dev.agent.md` is a deprecated, non-user-invocable persona stub that delegates to root `AGENTS.md`; its full historical body is preserved under `Docs/DEPRECATED/AgentShimsHistorical_20260606/`.
- `.codexrules/agent_memory.txt` is an operational scratch memo, not authority and not a rule surface. Verify every command in it against live source before running.
- `.cursor/index.mdc` is a thin Cursor router only. It may be always-on only to route to root `AGENTS.md` and this document.
- `.cursor/rules/AGENTS.md` delegates to root `AGENTS.md`.
- Historical or generic Cursor `.mdc` rules under `.cursor/rules/*.mdc` are not HECTON-8 authority unless root `AGENTS.md`, this document, or a current route bible explicitly imports them. Their former full bodies are preserved under `Docs/DEPRECATED/CursorRulesHistorical_20260606/`.
- `C:\hades\.claude\rules\*.md` are Claude-side path-scoped routing rules with `paths:` frontmatter, added 2026-07-27. Each loads only when Claude opens a matching file. They are routing pointers plus high-rework non-negotiables, never law; root `AGENTS.md` outranks them and a disagreement is a defect in the rule. They are registered in `C:\hades\CLAUDE.md`.
- `Docs/PROJECT_ROOT_BIBLES_COMBINED.md` is generated by `Tools/Docs/BuildProjectRootBiblesCombined.py`; update live source files, then regenerate.

Historical or generic rule files under `.agent/rules/*.md` are not HECTON-8 authority unless root `AGENTS.md`, this document, or a current route bible explicitly imports them. Their former full bodies are preserved under `Docs/DEPRECATED/AgentRulesHistorical_20260605/`. If they conflict with HECTON-8 law, HECTON-8 law wins.

If any historical Cursor `.mdc` rule conflicts with HECTON-8 law, HECTON-8 law wins.

## No-Loss Split Protocol

When shortening or splitting a monolithic rule document:

1. Identify the exact source file and section.
2. Choose the destination: route bible, mandate file, architecture doc, generated source list, or archive.
3. Move or copy the full rule text before removing it from the source.
4. Add a short provenance line in the destination when the moved text is not self-evident.
5. Keep a canonical route from `AGENTS.md`, `PROJECT_BIBLES.md`, `.agents-skills/README.md`, or this file to the destination.
6. Regenerate generated snapshots after source edits.
7. Run a scoped grep for the moved phrase and for old conflicting language.
8. Report what moved, where it moved, and what command checked the route.

Forbidden split behavior:

- deleting a rule because it is duplicated without confirming the surviving copy;
- replacing a detailed rule with a vague summary when no detailed copy remains;
- hand-editing generated snapshots as if they were live source;
- leaving tool-specific copies with stale or conflicting law;
- treating old reports, task files, or archives as authority because the live source was shortened.

## Anti-Bureaucracy Guard

Routing is a read discipline, not a paperwork mandate. Ordinary agents should improve code, assets, data, scenes, or concise docs for the current request. Status files, rationale logs, route cards, broad audits, and historical cleanup require explicit batch/logging/orchestration scope or a direct owner-route need.

Verification loops are rejected. Once static review identifies a blocker or `PENDING VERIFICATION`, the next action is proof execution, source/asset/root-route repair, or a concrete blocker report. Do not keep reading, scanning, or writing reports over unchanged state to simulate progress.

Same-failure loops are rejected across domains. After two matching failures, route recovery must change the owner path: restore/replace/revert/fix the real source route, write the requested content artifact or source blocker, run the missing proof, or stop with the exact external gate. Do not add another wrapper, validator, packet, or summary over the same failed path.
