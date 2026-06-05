# Source Routing Family Audits Synthesis - 2026-06-05

Date: 2026-06-05
Status: SYNTHESIZED_STATIC_GAP_QUEUE / RUNTIME_PROOF_PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC
Controller scope: synthesis of four source-routing family audit reports only

## Evidence Boundary

This synthesis used static report text, static source counts from the four family audits, and existing stable routing-doc targets.

It did not run Unity, importers, Play Mode, dotnet build, tests, player builds, profiler, GCMonitor, Memory Profiler, Frame Debugger, RenderDoc, shader import, scene validation, asset mutation, prefab mutation, scene mutation, or runtime verification.

Static source and static docs prove source/document presence only. They do not prove compile health, runtime wiring, 0 B/frame GC, frame time, visual quality, save/load continuity, platform readiness, Data Monolith readiness, signal safety, Unity import health, first-20 route readiness, or player acceptance.

First-20 route blocker removed by this synthesis: source-routing gaps are now grouped into one patch queue so the stable routing docs can name concrete owner families, proof classes, and runtime-pending boundaries before anyone claims route coverage from broad folder rows.

## Inputs Integrated

| Input report | Scope | Static count summary |
|---|---|---:|
| `SOURCE_ROUTING_PRESENTATION_FAMILY_AUDIT_20260605.md` | UI, Visor, Audio, PDA, VFX, Rendering, Graphics, Lighting | 351 folder scripts, 33 exact anchors, 318 missing exact anchors; loose-root presentation-adjacent 42 unique scripts, 2 exact anchors, 40 missing |
| `SOURCE_ROUTING_AUTHORING_DATA_FAMILY_AUDIT_20260605.md` | Editor, Authoring, Data, Tools, QA, Build, Dev, Meta | 486 folder scripts, 13 exact anchors in either shared doc, 473 missing exact anchors; `Editor` 408/7; `Tools` 31/0; loose-root authoring/data/tool families 131 unique scripts, 6 exact anchors |
| `SOURCE_ROUTING_WORLD_GAMEPLAY_FAMILY_AUDIT_20260605.md` | World, Gameplay, Physics, Survival-adjacent, Player, Vehicles, Construction, Interaction, Inventory, Tools, Power, Atmosphere | 815 folder scripts, 86 exact anchors, 729 missing exact anchors; loose-root family union 165 scripts |
| `SOURCE_ROUTING_CORE_SYSTEMS_FAMILY_AUDIT_20260605.md` | Core, Signals, Bootstrap, SaveSystem, Optimization, AI, Fauna, Ecosystem, Narrative, Networking, Modding, Plugins, Quest, Progression | 638 unique inspected scripts, 44 exact anchors, 594 missing exact anchors |

Counts overlap across scopes, especially `Tools`, loose root, DataVault-facing runtime, and mixed source folders. Do not sum these rows into a global project total.

## Shared Finding

`Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` and `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` are directionally honest: the audited reports did not find a new positive runtime/platform/release-ready claim inside those shared docs.

The failure mode is precision. Broad folder rows, short path mentions, and family/echelon labels are not enough for this project because the source tree contains large mixed bins and high-risk authority surfaces. Exact owner-path routing is sparse for core runtime, authoring/data, gameplay/world, presentation, and support systems.

## Patch Themes For Shared Routing Docs

Patch one shared-doc wave only. The two target docs are:

- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`

Required themes:

| Theme | Static routing problem | Minimum patch expectation |
|---|---|---|
| Core execution, registry, signals, DataVault, memory | Critical authority owners such as bootstrap, dispatcher, GlobalRegistry, SignalBus, GlobalSignals bridge, DataVault, H8Memory, telemetry spine have sparse exact anchors. | Add explicit grouped exact-anchor rows, owner bible, phase/cadence concern, and required proof class. Keep runtime proof pending. |
| SaveSystem and persistence | Save paging, Merkle/hash, entity/voxel deltas, migration and sidecar/storage surfaces are covered unevenly. | Add persistence subroute rows with save/load roundtrip, corruption/WAL, layout, and GC/profiler proof requirements. |
| Data Monolith authoring/runtime | Compiler, static arena, DTO/hash, reconstruction jobs, fuzzers, and audit tools are not exact-routed enough to support Data Monolith readiness claims. | Separate authoring compiler/bake route from runtime arena/DTO route. Require `static_data.h8bin`, bake/hash/schema, import/boot, and runtime owner proof. |
| Tools, CSV, upgrade, tool runtime | `Tools` has zero exact anchors in the audited shared docs despite DataVault/job/runtime and CSV parser files. | Add tool runtime and authoring parser groups with runtime-parser absence or bake isolation proof. |
| UI, PDA, Visor, Sonar, presentation | UI/Visor/Audio/PDA/VFX/Rendering/Lighting have many unanchored files and loose-root presentation files. | Add presentation-family source groups with UI zero-GC/TMP proof, visor/render proof, sonar/audio proof, and route-capture requirements. |
| Ocean, water, rendering, VFX, audio native | Graphics/rendering/VFX/audio/plugin bridge rows are incomplete at exact-path level. | Add rendering/water/audio bridge groups with Frame Debugger/profiler/import proof and third-party bridge boundaries. |
| World streaming, voxel, nav, biome, procedural wreckage, vegetation | World/gameplay audit found 729 missing exact anchors and many broad family rows. | Add grouped world-owner exact anchors with streaming/residency, deterministic seed, save/load, and profiler proof columns. |
| Player, physics, survival, airlock, buoyancy, vehicles | Player, Vehicles, Physics, Interaction, Construction, Power, Thermodynamics, Atmosphere have uneven exact routing. | Add player/vehicle/physics/survival route groups with dispatcher phase, SDF/nonalloc proof, black-box telemetry, and route-moment proof requirements. |
| AI, Fauna, Ecosystem, Narrative, Quest, Modding, Plugins | Core/systems audit found AI/Fauna/Ecosystem/Narrative/Quest/Modding/Plugins sparse or companion-file gaps. | Add exact owner groups and keep modding envelope-only unless runtime playbook proof exists. |

## Patch Policy

Use one shared-doc patch worker for both target docs. Do not run parallel patch workers against the same two files.

The patch must be static-only and conflict-minimized:

- preserve existing structure and existing static/runtime boundary wording;
- add grouped exact-anchor rows instead of thousands of per-file rows;
- do not claim runtime, import, compile, Play Mode, platform, visual, audio, save/load, Data Monolith, or first-20 readiness from text;
- do not edit source code, Unity assets, scenes, prefabs, project settings, root bibles, AGENTS.md, or dated worker reports;
- do not convert broad source gaps into pass/fail runtime verdicts;
- route proof requirements to artifacts, not optimism.

## Regression Model

CPU: no runtime code changed.

GC: no runtime code changed.

Memory: no runtime code changed.

Cadence: no runtime cadence changed.

Correctness: documentation correctness improves only if later shared-doc patch preserves static evidence boundaries and avoids overclaiming broad route coverage.

## Hot Path Impact

No hot path changed. This is a static documentation synthesis.

## Failure Modes

- Summing overlapping family counts would fabricate a global total.
- Adding thousands of exact rows would make the stable docs unreadable and raise conflict risk.
- Leaving only broad folder rows would keep source-owner routing too weak for high-risk systems.
- Claiming runtime readiness from exact anchors would violate the evidence ladder.
- Parallel workers on the same two shared docs would create avoidable merge conflicts.

## Why Kept

Kept because the four audits identify the same systemic routing gap from independent source families and provide enough static evidence to dispatch one scoped shared-doc patch.

## Controller Next Action

Dispatch one worker or one local task packet:

- Patch targets: `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`; `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Inputs: the four family audit reports and this synthesis.
- Evidence class to preserve: `STATIC_SOURCE / STATIC_DOC`.
- Required output: grouped exact-anchor route additions, proof-class columns, and runtime-pending wording.
- Forbidden output: runtime/platform/release readiness claims, Unity/build claims, source-code changes, asset changes, and broad refactors.
