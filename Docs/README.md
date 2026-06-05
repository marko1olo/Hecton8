# HECTON-8 Documentation Index

Date: 2026-06-02
Status: STATIC INDEX
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: stable read map for active documentation. This is not a current-work digest.

## Product Compass

HECTON-8 documentation should push agents toward one result: a playable, beautiful, optimized, believable underwater sci-fi survival game. Surface, sky, Aegir, coastline, water, flora, terrain, UI, tools, and hero routes must look intentional and premium while preserving performance and gameplay decision value.

Use performance as a budget for immersion, not as a reason to make the product flat. Use documentation as a routing map, not as bureaucracy. Generated snapshots, reports, logs, and task files can provide evidence, but live source, root authority, route bibles, and fresh proof decide current truth.

For player-visible work, inspect the reference images in:

`C:\hades\Hecton8\Docs\mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)`

## Read Order

This index is a read map, not an alternate authority hierarchy. The standing authority spine is defined in root `AGENTS.md`.

Use this order when gathering documentation context:

1. `AGENTS.md`
2. `Docs/AGENT_AUTHORITY_ROUTING.md` for non-trivial task intake and no-loss rule routing
3. `.agents-skills/README.md`
4. task-relevant files under `.agents-skills/`
5. `PROJECT_BIBLES.md`
6. `VISION_LOCKS.md` when product vision or ambiguity is involved
7. `TASTE.md` when player-facing, plus the matching root route bible for the current domain
8. this index plus the active stable files listed below
9. current C# source under `Assets/_Project` for source reality checks
10. fresh proof artifacts under `Docs/Reports`
11. archives under `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive`

`PROJECT_BIBLES.md`, `VISION_LOCKS.md`, and the standing root route bibles listed by `PROJECT_BIBLES.md` are explicit root authorities. Other root reports, prompts, status/log files, generated evidence, and task-progress prose are not.

Current source can disprove stale documentation, but source presence alone does not prove Unity import, Play Mode, profiler, GC, visual, save/load, or player-build readiness.

Dated reports and archived files are evidence snapshots. They are not active system contracts.

## Start Here

| Need | Read |
|---|---|
| Agent task intake, no-loss rule routing, and tool-surface delegation | `Docs/AGENT_AUTHORITY_ROUTING.md` |
| Root bible routing and domain bible selection | `PROJECT_BIBLES.md` |
| User product vision locks and ambiguity resolution | `VISION_LOCKS.md` |
| Procedural asset package pipeline binding route bible | `PROCEDURAL_ASSET_PIPELINE.md` |
| Project baseline and documentation boundaries | `Docs/PROJECT_BASELINE.md` |
| Source-backed runtime reality, scene spine, and source owner map | `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` |
| Real-script systems map: source owners, implemented surfaces, missing proof | `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` |
| Domain-to-architecture coverage matrix | `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` |
| Root file policy | `Docs/ROOT_DOCS_REFERENCE.md` |
| Documentation update rules | `Docs/DOC_GOVERNANCE.md` |
| Public copy voice | `textes.md` |
| Evidence and acceptance gates | `Docs/QUALITY_GATES.md` |
| Cross-system contracts | `Docs/SYSTEMS_CONTRACTS.md` |
| Architecture entry point | `Docs/ARCHITECTURE/README.md` |
| Distilled source constants and current proof snapshots | `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` |

## Active Contract Map

Core:

- `Docs/PROJECT_BASELINE.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/ROOT_DOCS_REFERENCE.md`

Generated/tool-entry stubs:

- `Docs/PROJECT_ATLAS.md`
- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/ARCHITECT_HANDBOOK.md`
- `Docs/Generated/README.md`
- `Docs/Data/Profiles/README.md`

Architecture spine:

- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`
- `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`
- `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`
- `Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
- `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`
- `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`

Domain contracts:

- `PROCEDURAL_ASSET_PIPELINE.md` - binding procedural asset package pipeline route bible. `Docs/PROCEDURAL_ASSET_PIPELINE.md` is a non-binding supporting/historical duplicate and must not be treated as equal authority.
- `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md`
- `Docs/ARCHITECTURE/MESH_STATE_SWAP_DESTRUCTION_PIPELINE.md`
- `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`

Content and support corpora:

| Path | Classification | Active boundary |
|---|---|---|
| `Docs/Lore` | narrative, encyclopedia, localization, and applied content corpus | Content authority only. Does not prove implementation, route availability, or runtime wiring. |
| `Docs/Marketing` | public-copy, launch, outreach, Steam, creator, press, and gate-tracking corpus | Public-facing work must also read root `textes.md`; no public send/readiness claim without proof gates. |
| `Docs/Modding` | mod/API specification, sandbox, schema, command/event audit matrices, starter-kit planning | API/product plan only unless current source and runtime artifacts prove loader behavior. |
| `Docs/Design` | design/spec support: binary specs, LUTs, UI scaler, mission notes, VR comfort/haptics | Lower authority than `Docs/ARCHITECTURE` and current source; promote durable engineering facts before treating as contract. |
| `Docs/Data` | CSV authoring and tuning profiles | Authoring data only. Runtime readiness belongs to Data Monolith contracts and proof artifacts. |
| `Docs/Audio` | dialogue, stem, and synth CSV authoring data | Audio content/profile data only; DSP/runtime proof remains separate. |
| `Docs/Atmosphere` | gas/atmosphere CSV profile data | Authoring data only; simulation/runtime proof remains separate. |
| `Docs/AI_Texturing_Templates` | image/texturing prompt template support | Asset-generation support only; not runtime or art QA proof. |

Evidence and archive boundaries:

- `Docs/Reports/README.md`
- `Docs/DEPRECATED/README.md`
- `Docs/_Archive/README.md`
- `Docs/Archive/README.md`

## Verification Language

Use `PENDING VERIFICATION` unless the document links the current proof artifact.

Required proof classes:

- compile: build log path with command, timestamp, and exit code
- Unity import or Console: Unity log path
- runtime: Play Mode or player capture path
- profiler: Profiler or frame-time capture path
- memory: GCMonitor or Memory Profiler capture path
- rendering: Frame Debugger, renderdoc, screenshot, or GPU timing artifact

Static source reads do not prove runtime behavior.
