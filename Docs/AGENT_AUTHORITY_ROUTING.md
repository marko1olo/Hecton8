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
2. Classify the task domain and risk class.
3. Read `PROJECT_BIBLES.md` for root route selection when the task is major, player-facing, design-facing, system-facing, or ambiguous.
4. Read `VISION_LOCKS.md` when the user asks for product direction, ambiguity resolution, route priority, taste conflict, or scope interpretation.
5. Read `TASTE.md` for player-visible work, plus the matching root route bible from `PROJECT_BIBLES.md`.
6. Read `.agents-skills/README.md` and exactly `2-8` task-relevant mandate files before non-trivial code, architecture, rendering, gameplay, asset, data, or technical-report work.
7. Read live source/assets/proof for the edited owner route before trusting reports, generated snapshots, task files, or stale logs.

Small typo fixes, narrow mechanical edits, and ordinary chat answers may skip the full intake, but they must not contradict the authority spine.

Authority files, route bibles, mandate files, and important task documents must be read as complete documents before meaning is evaluated. Text search is a navigation and audit tool only.

## Task Classes

| Task class | Mandatory authority beyond `AGENTS.md` |
|---|---|
| Player-visible water, terrain, sky, flora, UI, VFX, lighting, camera, materials, surface route, or hero biome | Reference image folder, `PROJECT_BIBLES.md`, `TASTE.md`, matching route bibles, matching visual/performance mandates |
| Product vision, taste ambiguity, route priority, feature interpretation | `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, `TASTE.md`, matching route bible |
| Runtime architecture, bootstrap, global authority, signal/data ownership | `PROJECT_BIBLES.md`, `systems.md`, `data.md`, `performance.md`, global-authority architecture docs, matching `ARCH_*`, `DATA_*`, `OPT_*` mandates |
| Hot-path code, Burst, jobs, memory, GPU upload, DTOs | `performance.md`, `data.md` or `compute.md` as applicable, `.agents-skills/README.md`, matching `OPT_*`, `DATA_*`, `GPU_*`, `MATH_*` mandates |
| Physics, vehicle, collision, pressure, flooding, tethers | `physics.md`, related gameplay/vehicle/survival bible, matching `PHYS_*`, `MATH_*`, `OPT_*` mandates |
| UI, menus, HUD, terminals, localization, settings | `ui.md`, `UI_MENU_SCREEN_STANDARDS.md` or `UI_DIEGETIC_HUD_STANDARDS.md`, `settings.md` or `localization.md` if touched, matching `UI_*` mandates |
| Narrative, codex, diaries, in-world prose | `writing.md`, `narrative.md`, `localization.md` when localized |
| Public copy, store/social/creator text | `textes.md`, product proof gates for readiness claims |
| Batch-agent run with supplied ID | Matching prompt block only, `AGENTS.md`, task-relevant bibles/mandates, active Status/Rationale/LOG files for that ID |
| Orchestrator/controller work | `HECTON8_ORCHESTRATOR.md` including `LOCAL SUBAGENT PROTOCOL` and `AGENT LANE CONTRACTS`, `C:\hades\.codex_ops\ORCHESTRATION_MEMORY.md`, active orchestration evidence, then task-relevant bibles/mandates |
| Documentation/rule routing work | `AGENTS.md`, this file, `PROJECT_BIBLES.md`, `.agents-skills/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/README.md`, live rule sources and generators |

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

- `C:\Users\danat\.codex\AGENTS.md` is a global router only. It must route HECTON-8 work to root `AGENTS.md` and this file, and must not carry a divergent HECTON-8 law copy. If shortened, preserve the full previous text in an explicit recovery/provenance file.
- `.codexrules/AGENTS.md` must delegate to or stay byte-intent synced with root `AGENTS.md`.
- `.github/agents/AGENTS.md` must delegate to or stay byte-intent synced with root `AGENTS.md`.
- `.agent/rules/AGENTS.md` delegates to root `AGENTS.md`.
- `Docs/PROJECT_ROOT_BIBLES_COMBINED.md` is generated by `Tools/Docs/BuildProjectRootBiblesCombined.py`; update live source files, then regenerate.

Historical or generic rule files under `.agent/rules/*.md` are not HECTON-8 authority unless root `AGENTS.md`, this document, or a current route bible explicitly imports them. If they conflict with HECTON-8 law, HECTON-8 law wins.

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
