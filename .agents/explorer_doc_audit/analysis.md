# Comprehensive Audit & Contradiction Survey: HECTON-8 Documentation

**Audit Date**: 2026-08-11  
**Auditor**: `explorer_doc_audit` (Teamwork Explorer)  
**Target Directory**: `C:\hades\Hecton8\Docs\` and Root Authority Spine  
**Status**: COMPLETE (READ-ONLY INVESTIGATION)  

---

## Executive Summary

A comprehensive structural and technical audit of all documentation across `C:\hades\Hecton8\` and `C:\hades\Hecton8\Docs\` was executed. The audit surveyed root authority documents (`AGENTS.md`, `GEMINI.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`), domain route bibles, technical specifications under `Docs/ARCHITECTURE/`, historical reports under `Docs/Reports/`, and task logs under `Docs/Tasks/`.

### Core Audit Deliverables:
1. **Contradiction Survey**: Discovered 7 critical technical contradictions across documentation regarding World Size (30km vs 50km vs 100km vs legacy 12km), Frame Rate Targets (60 FPS vs 90 FPS VR vs 30 FPS fallback), GC Allocation Budgets (Strict 0 B/frame vs legacy <1KB claims), Memory/VRAM Ceilings, Render Pipeline Rules (URP vs legacy HDRP/Built-in references), Architecture Drift (Burst/NativeJobs vs deprecated DOTS/ECS plans), and Multiplayer/Co-Op Positioning.
2. **Task Log Inventory**: Mapped all **86 completed task log files** (`Status_*.md`) in `Docs/Tasks/` to be moved to `Docs/Archive/Batch014_LegacyTasks/`.
3. **Stale/Duplicate/Obsolete File Audit**: Identified 5 major stale/duplicate file clusters (including redundant `Docs/PROCEDURAL_ASSET_PIPELINE.md`, temporary playtest logs in `Docs/V0_Playtest/`, 2026-06 static reports in `Docs/Reports/`, and deprecated DOTS/ECS adoption plans).
4. **Knowledge Graph Blueprint**: Designed a complete 7-domain structure for `C:\hades\Hecton8\Docs\HECTON8_KNOWLEDGE_GRAPH.md` to serve as the unified documentation navigation hub.

---

## 1. Technical Contradictions Across Documentation

### Contradiction 1: World Extent & Spatial Scale (30km vs ±50km vs 100km vs 12km)
- **Observation & Evidence**:
  - `Docs/Reports/HECTON8_GEOLOGY_MAPMAGIC_TECHNICAL_REPORT_20260811.md`: States: *"The world extent is explicitly bounded to 30km (±15000m)... Shelf lerp attribution requires ~32.7km to drop 2860m at a 5-degree angle. With a 30km world width, a 41.6km authored width cannot physically fit in the world."*
  - `Docs/Modding/SDK_Authoring_Interface_Plan.md`, `Mod_API_Sandbox_Quarantine.md`, and `Status_SHINOBU_102.md`: Assert a *"±50 km AUP sandbox bound"* (implying a 100km coordinate space).
  - `Docs/Marketing/BRAND_AND_POSITIONING_BIBLE.md` & `NO_COOP_PUBLIC_POSITIONING.md`: State: *"100km multiplayer ocean. Agent hallucination, banned from public marketing."*
  - Legacy task files & early terrain bibles: Refer to legacy 12km x 12km world bounds.
- **Logic Chain**:
  - `AGENTS.md` and `PROJECT_BIBLES.md` require single source of truth for runtime terrain math.
  - The geology engine (MapMagic 2) terrain generator is physically bounded to 30km (±15,000m).
  - The modding API and AUP (Absolute Universe Position) 64-bit float system supports precision up to ±50km (100km total coordinate window) to prevent floating-point origin jitter.
  - Marketing documents explicitly ban claiming a "100km ocean" to prevent misleading consumers about playable scale.
- **Resolution Standard**:
  - **Playable World Geometry Truth**: Exactly **30km (±15,000m)**.
  - **AUP Coordinate Math Precision Bound**: **±50km** (`double3` precision headroom).
  - **Deprecated/Banned Terms**: 12km terrain limit (stale) and "100km open ocean" public claims (banned marketing hallucination).

---

### Contradiction 2: Frame Rate Targets & Throttle Thresholds (60 FPS vs 90 FPS vs 30 FPS)
- **Observation & Evidence**:
  - `AGENTS.md` (lines 8-9): Enforces: *"Performance target: 60 FPS / 16.67 ms. Throttle threshold: 25 ms. Guardrails: main thread 12 ms, GC 0 B/frame..."*
  - `Docs/ARCHITECTURE/XR_*.md` and `xr.md`: Require **90 FPS / 11.11 ms** for PCVR and **72 FPS / 13.88 ms** for Standalone XR (Quest/PICO).
  - `Docs/Reports/` & older task logs: Reference a fallback 30 FPS / 33.3 ms baseline for low-tier hardware.
- **Logic Chain**:
  - `AGENTS.md` sets the global flat-screen benchmark for middle/high/ultra PC and handheld hardware.
  - XR hardware demands 90 FPS / 72 FPS to satisfy VR motion-to-photon latency standards and prevent motion sickness.
  - Continuous `GlobalQualityWeight` (0.0 to 1.0) must dynamically adjust shadow cascades, draw distance, and particle density across these hardware targets rather than hardcoding flat frame targets.
- **Resolution Standard**:
  - **Flat-Screen Baseline**: 60 FPS (16.67 ms target, 12 ms main thread max, 25 ms throttle ceiling).
  - **XR Target**: 90 FPS (11.11 ms) PCVR / 72 FPS (13.88 ms) Standalone XR.
  - **Scalability Mechanism**: Driven exclusively via continuous `GlobalQualityWeight`.

---

### Contradiction 3: Garbage Collection (GC) Allocation Budgets (Strict 0 B/frame vs <1KB/frame)
- **Observation & Evidence**:
  - `AGENTS.md` (lines 8, 259-262) & `COMMON_SENSE.md`: Explicitly forbid GC allocations in hot paths: *"GC 0 B/frame... LINQ, string concat/interpolation, ToString (including HUD text formatting in Update() like O2.ToString() + '%')... are strictly forbidden."*
  - Legacy task files (`Status_1428.md`, `Status_1801.md`): State *"GC budget < 1KB/frame allowed for UI events"*.
- **Logic Chain**:
  - `AGENTS.md` Product Law outranks historical status logs.
  - Unity 6000.4 GC spikes on handheld/compact hardware (e.g. Steam Deck, i3/MX350) cause stutter and frame drops if hot paths allocate memory.
  - Text formatting must use preallocated `char[]` buffers and `TMP_Text.SetCharArray()` or `ZString`.
- **Resolution Standard**:
  - Enforce **Strict 0 B/frame GC allocation** across all hot paths without exception.
  - Any initialization/cold allocations must carry explicit `// COLD ALLOC: Type[capacity] - reason - owner: ClassName` comments.

---

### Contradiction 4: VRAM & RAM Memory Ceilings (1800 MB vs 2048 MB vs 4096 MB)
- **Observation & Evidence**:
  - `AGENTS.md` (line 8): Specifies compact VRAM hard ceiling = **1800 MB** (Texture budget: 900 MB; RT+depth: 320 MB; RAM: 4096 MB out of 8GB system RAM).
  - `Docs/ARCHITECTURE/ARM64_MEMORY_ALIGNMENT_SHINOBU_204.md`: Mentions **2048 MB VRAM** as mobile ceiling.
  - Older performance playbooks: Reference **4096 MB VRAM** without tier breakdown.
- **Logic Chain**:
  - Compact hardware class (2GB physical VRAM GPUs, e.g. NVIDIA MX350, integrated graphics) has ~1.8GB available after OS/driver overhead.
  - Standard/Middle tier has 4GB VRAM.
  - Higher VRAM allocations are permitted only when `GlobalQualityWeight` increases texture resolution and stream pool sizes.
- **Resolution Standard**:
  - **Compact Tier Ceiling**: 1800 MB VRAM (900MB textures, 320MB RT+depth, 4096MB System RAM).
  - **Middle/High Tier**: Continuous expansion governed by `HardwareTierDetector` and `GlobalQualityWeight`.

---

### Contradiction 5: Render Pipeline & Material Runtime (URP vs Legacy HDRP/Built-In)
- **Observation & Evidence**:
  - `AGENTS.md` (line 6): Establishes HECTON-8 as a **Unity 6000.4 URP** (Universal Render Pipeline) project.
  - Older rendering docs (`rendering.md`, `lighting.md`): Contain lingering references to HDRP Volume Profiles and Built-in shader keywords (e.g. `_MAIN_LIGHT_SHADOWS_CASCADE`).
  - Particle/UI documentation: Mentions using `MaterialPropertyBlock` on SRP-batched geometry, contradicting `AGENTS.md` line 277 forbidding MPB on SRP-batched geometry (requires CBUFFER or GraphicsBuffer/BRG).
- **Logic Chain**:
  - URP 6000.4 RenderGraph is the sole render pipeline owner.
  - HDRP profiles and Built-in pipeline keywords break URP SRP Batching and cause shader compilation failures.
  - `MaterialPropertyBlock` breaks SRP Batcher instancing on standard geometry.
- **Resolution Standard**:
  - Standardize all rendering docs strictly around Unity 6000.4 URP RenderGraph.
  - Scrub all HDRP/Built-in terminology and enforce CBUFFER / BRG buffer uploads over MPB.

---

### Contradiction 6: Architectural Evolution (Burst + Unmanaged Jobs vs Deprecated DOTS/ECS)
- **Observation & Evidence**:
  - `Docs/ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md`: Proposes migrating game entities and terrain chunks to full Unity DOTS/ECS (`Unity.Entities`).
  - `AGENTS.md` & `systems.md` & `data.md`: Enforce unmanaged C# Structs, `SignalBus<T>`, `GlobalDataVault`, and Burst-compiled `IJobParallelFor` / `IJobEntityBatch`, explicitly banning DOTS Entity Component Managers in runtime hot paths due to serialization and modding constraints.
- **Logic Chain**:
  - Full DOTS/ECS requires complex entity world management, breaks raw binary `.h8bin` save serialization, and complicates modding SDK sandboxing.
  - Burst-compiled jobs operating on `NativeArray<T>` inside `GlobalDataVault` achieve identical CPU cache locality without ECS framework bloat.
- **Resolution Standard**:
  - Formally deprecate `ECS_DOTS_ADOPTION_PLAN.md`.
  - Reaffirm Unmanaged Structs + `GlobalDataVault` + Burst Jobs as the canonical architecture.

---

### Contradiction 7: Singleplayer Supremacy vs Multiplayer/Co-Op Positioning
- **Observation & Evidence**:
  - `VISION_LOCKS.md` (line 191) & `AGENTS.md`: Core game is **100% Singleplayer**, with architecture decoupled to support future optional 2-player co-op.
  - `Docs/Marketing/NO_COOP_PUBLIC_POSITIONING.md`: Strictly forbids public promises of multiplayer or 100km co-op.
  - Older task status logs (`Status_SHINOBU_81.md`): Discuss server tick rates, RPC bandwidth, and multiplayer state sync.
- **Logic Chain**:
  - Public marketing must position HECTON-8 as a singleplayer-first underwater survival game.
  - Internal data structures (DataVault, SignalBus DTOs) must avoid global static singletons to remain netcode-ready without advertising multiplayer features.
- **Resolution Standard**:
  - Public positioning = Singleplayer Underwater Survival.
  - Runtime architecture = Decoupled DTOs / SignalBus (co-op architecture foundation prepared cautiously, zero public promises).

---

## 2. Inventory of Completed Task Logs in `Docs/Tasks/`

All **86 completed task log files** (`Status_*.md`) in `C:\hades\Hecton8\Docs\Tasks\` were generated during previous development sprints (June 3–9, 2026). In accordance with `AGENTS.md` and intake regulations, active `Docs/Tasks/` must contain only active task specifications and non-log reference tables.

### Task Logs Slated for Archive (`Docs/Archive/Batch014_LegacyTasks/`):
1. `Status_1428.md` (19.7 KB, 2026-06-03)
2. `Status_1770.md` (2.1 KB, 2026-06-04)
3. `Status_1778.md` (3.2 KB, 2026-06-04)
4. `Status_1801.md` (3.5 KB, 2026-06-04)
5. `Status_1802.md` (4.8 KB, 2026-06-04)
6. `Status_1806.md` (2.7 KB, 2026-06-04)
7. `Status_1810.md` (1.7 KB, 2026-06-04)
8. `Status_1813.md` (1.8 KB, 2026-06-09)
9. `Status_1814.md` (1.1 KB, 2026-06-04)
10. `Status_1820.md` (1.0 KB, 2026-06-04)
11. `Status_1823.md` (1.4 KB, 2026-06-04)
12. `Status_1824.md` (2.3 KB, 2026-06-04)
13. `Status_1826.md` (1.6 KB, 2026-06-04)
14. `Status_1854.md` (2.0 KB, 2026-06-04)
15. `Status_1860.md` (1.6 KB, 2026-06-04)
16. `Status_1862.md` (0.7 KB, 2026-06-04)
17. `Status_1863.md` (0.9 KB, 2026-06-04)
18. `Status_1872.md` (1.9 KB, 2026-06-04)
19. `Status_1873.md` (1.2 KB, 2026-06-04)
20. `Status_1874.md` (1.2 KB, 2026-06-04)
21. `Status_1876.md` (1.2 KB, 2026-06-04)
22. `Status_1877.md` (2.2 KB, 2026-06-04)
23. `Status_1878.md` (1.3 KB, 2026-06-04)
24. `Status_1880.md` (1.2 KB, 2026-06-04)
25. `Status_1881.md` (1.6 KB, 2026-06-04)
26. `Status_1882.md` (1.6 KB, 2026-06-04)
27. `Status_1883.md` (1.3 KB, 2026-06-04)
28. `Status_1884.md` (1.7 KB, 2026-06-04)
29. `Status_1885.md` (0.8 KB, 2026-06-04)
30. `Status_1886.md` (1.5 KB, 2026-06-04)
31. `Status_1887.md` (2.2 KB, 2026-06-04)
32. `Status_1888.md` (1.0 KB, 2026-06-04)
33. `Status_1889.md` (1.6 KB, 2026-06-04)
34. `Status_1891.md` (1.0 KB, 2026-06-04)
35. `Status_1892.md` (1.7 KB, 2026-06-04)
36. `Status_1893.md` (1.0 KB, 2026-06-04)
37. `Status_1894.md` (1.6 KB, 2026-06-04)
38. `Status_1895.md` (1.5 KB, 2026-06-04)
39. `Status_1896.md` (2.0 KB, 2026-06-04)
40. `Status_1897.md` (1.2 KB, 2026-06-04)
41. `Status_1898.md` (2.0 KB, 2026-06-04)
42. `Status_1899.md` (1.1 KB, 2026-06-04)
43. `Status_1900.md` (1.1 KB, 2026-06-04)
44. `Status_1905.md` (4.2 KB, 2026-06-04)
45. `Status_1906.md` (4.1 KB, 2026-06-04)
46. `Status_1907.md` (4.6 KB, 2026-06-04)
47. `Status_1908.md` (4.2 KB, 2026-06-04)
48. `Status_1909.md` (5.2 KB, 2026-06-04)
49. `Status_2002.md` (0.9 KB, 2026-06-04)
50. `Status_2006.md` (0.7 KB, 2026-06-04)
51. `Status_2010.md` (2.2 KB, 2026-06-04)
52. `Status_2011.md` (2.2 KB, 2026-06-04)
53. `Status_2017.md` (1.1 KB, 2026-06-04)
54. `Status_2019.md` (0.9 KB, 2026-06-04)
55. `Status_2104.md` (3.2 KB, 2026-06-04)
56. `Status_3101.md` (2.2 KB, 2026-06-05)
57. `Status_3102.md` (1.9 KB, 2026-06-05)
58. `Status_3103.md` (3.1 KB, 2026-06-05)
59. `Status_3107.md` (1.6 KB, 2026-06-05)
60. `Status_3109.md` (7.5 KB, 2026-06-05)
61. `Status_3110.md` (2.5 KB, 2026-06-05)
62. `Status_3202.md` (1.3 KB, 2026-06-05)
63. `Status_3204.md` (1.9 KB, 2026-06-05)
64. `Status_3209.md` (1.4 KB, 2026-06-05)
65. `Status_3214.md` (1.8 KB, 2026-06-05)
66. `Status_3215.md` (1.1 KB, 2026-06-05)
67. `Status_3216.md` (1.0 KB, 2026-06-05)
68. `Status_3217.md` (0.7 KB, 2026-06-05)
69. `Status_3218.md` (1.4 KB, 2026-06-05)
70. `Status_3219.md` (1.3 KB, 2026-06-05)
71. `Status_3220.md` (0.7 KB, 2026-06-05)
72. `Status_3221.md` (0.8 KB, 2026-06-05)
73. `Status_3222.md` (1.0 KB, 2026-06-05)
74. `Status_3223.md` (0.5 KB, 2026-06-05)
75. `Status_3224.md` (1.5 KB, 2026-06-05)
76. `Status_3225.md` (2.0 KB, 2026-06-05)
77. `Status_3226.md` (1.3 KB, 2026-06-05)
78. `Status_3227.md` (1.2 KB, 2026-06-05)
79. `Status_3228.md` (1.5 KB, 2026-06-05)
80. `Status_3238.md` (1.3 KB, 2026-06-05)
81. `Status_3243.md` (1.4 KB, 2026-06-05)
82. `Status_3248.md` (2.0 KB, 2026-06-05)
83. `Status_3253.md` (0.5 KB, 2026-06-05)
84. `Status_3257.md` (0.5 KB, 2026-06-05)
85. `execution_priorities.csv` (Reference CSV, retain in `Docs/Tasks/`)
86. `shadow_culling_profiles.csv` (Reference CSV, retain in `Docs/Tasks/`)
87. `telemetry_flags.csv` (Reference CSV, retain in `Docs/Tasks/`)
88. `telemetry_hash_dictionary.csv` (Reference CSV, retain in `Docs/Tasks/`)
89. `validation_rules.csv` (Reference CSV, retain in `Docs/Tasks/`)
90. `README.md` (Retain in `Docs/Tasks/`)

---

## 3. Stale, Duplicate, and Obsolete File Audit

The audit identified 5 major categories of obsolete or redundant files requiring refactoring, archiving, or consolidation:

### 1. `Docs/PROCEDURAL_ASSET_PIPELINE.md` (Duplicate Authority)
- **Status**: Duplicate of root `PROCEDURAL_ASSET_PIPELINE.md`.
- **Finding**: As declared in `PROJECT_BIBLES.md` (line 38), root `PROCEDURAL_ASSET_PIPELINE.md` is canonical authority. Having a duplicate in `Docs/` introduces rule drift risk.
- **Action**: Slated for removal or archiving to `Docs/Archive/Duplicate_Docs/`.

### 2. `Docs/V0_Playtest/` Temporary Test Files
- **Status**: Intermediate test logs and temporary output dumps.
- **Files**:
  - `NEXT_CHAT_L12.md` through `NEXT_CHAT_L19.md`
  - `V0_L08_MEASURED.md` through `V0_L10_MEASURED.md`
  - `V0_L13_LIVE_RESULTS.md` through `V0_L18_LIVE_RESULTS.md`
  - Scratch files: `_tmp_l18_crash.txt`, `_tmp_path_out.txt`, `_tmp_reg_out.txt`
- **Action**: Move all historical playtest logs to `Docs/Archive/V0_Playtest_202607/` and delete temporary `_tmp_*.txt` files.

### 3. `Docs/Reports/DocumentationCompleteness_20260605/` & Dated Static Reports
- **Status**: 8 static scan logs from June 5, 2026 (`SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md`, `SOURCE_ROUTING_CORE_SYSTEMS_FAMILY_AUDIT_20260605.md`, etc.).
- **Action**: Archive to `Docs/Archive/Reports_20260605/`.

### 4. `Docs/ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md` (Obsolete Architecture)
- **Status**: Contradicts current `AGENTS.md` and `systems.md` unmanaged Burst/NativeJobs doctrine.
- **Action**: Add explicit `[DEPRECATED]` header routing developers to `systems.md` and `performance.md`.

### 5. `Docs/DEPRECATED/` & `Docs/_Archive/` Consolidation
- **Status**: Separate archive folders (`Docs/Archive/`, `Docs/_Archive/`, `Docs/DEPRECATED/`).
- **Action**: Consolidate all non-active historical files under a single clean hierarchy: `Docs/Archive/`.

---

## 4. Proposed Blueprint for `HECTON8_KNOWLEDGE_GRAPH.md`

`C:\hades\Hecton8\Docs\HECTON8_KNOWLEDGE_GRAPH.md` will serve as the single, clean, structured index mapping all active project bibles, authority routing files, subsystem contracts, and quality gates.

```markdown
# HECTON-8 Knowledge Graph & Master Navigation Hub

Status: CANONICAL NAVIGATION HUB
Owner: DOCS_GOVERNANCE

---

## 1. Authority & Routing Spine
- [AGENTS.md](../AGENTS.md) — Canonical HECTON-8 Agent Law & Task Intake Protocols
- [GEMINI.md](../GEMINI.md) — Gemini / Antigravity Agent Shim & Routing Adapter
- [PROJECT_BIBLES.md](../PROJECT_BIBLES.md) — Root Domain Bibles Index & Product Bar Standards
- [VISION_LOCKS.md](../VISION_LOCKS.md) — User Product Vision Locks & Ambiguity Resolution
- [TASTE.md](../TASTE.md) — Player-Facing Visual, Audio & Design Quality Floor
- [COMMON_SENSE.md](../COMMON_SENSE.md) — 18 Architectural AI Cognitive Constraints
- [AGENT_AUTHORITY_ROUTING.md](AGENT_AUTHORITY_ROUTING.md) — Mandatory Intake Sequence & Task Classification
- [QUALITY_GATES.md](QUALITY_GATES.md) — Mandatory Verification Gates & Proof Requirements
- [.agents-skills Index](../.agents-skills/README.md) — Technical Mandates Inventory

---

## 2. Core Systems & Architecture Specifications
- [bootstrap.md](../bootstrap.md) — Boot Sequence, Scene Transitions, Cold Registry Setup
- [systems.md](../systems.md) — System Ownership, SignalBus Lanes, Tick Cadence
- [performance.md](../performance.md) — Zero-GC Hot Paths, Frame Budgets (16.67ms), GlobalQualityWeight
- [data.md](../data.md) — DTO Layouts, NativeArray Memory, ARM64 Alignment
- [math.md](../math.md) — Deterministic Math, AUP Floating-Origin double3 Precision
- [compute.md](../compute.md) — GPU Compute Kernels, Dispatch Sizing, Buffer Barriers
- [persistence.md](../persistence.md) — Binary Delta Save/Load, Checksums, Slot Topology
- [telemetry.md](../telemetry.md) — Post-Mortem Crash Dumps, Black-Box Ring Buffers
- [SYSTEMS_CONTRACTS.md](SYSTEMS_CONTRACTS.md) — Core Systems Contracts & Data Vault Handles

---

## 3. World, Terrain & Ecosystem Bibles
- [world.md](../world.md) — World Extent (Canonical 30km / ±15,000m), Macro Biomes & Wrecks
- [terrain.md](../terrain.md) — MapMagic 2 Bridge, Heightmap Math, Slope & Thermal Erosion
- [voxels.md](../voxels.md) — HectonVoxelEngine, SDF Cave Sampling, Marching Cubes
- [water.md](../water.md) — Crest Ocean Integration, Photic Shallows & Abyssal Water Presentation
- [atmosphere.md](../atmosphere.md) — Pressure Fields, Gas Dynamics, Vents & Macro Weather
- [celestial.md](../celestial.md) — Aegir Orbital Mechanics, Day/Night Relay, Seismic Timing
- [ecosystem.md](../ecosystem.md) — Biome Simulation, Biomass Migration, Ecological Placement
- [PROCEDURAL_ASSET_PIPELINE.md](../PROCEDURAL_ASSET_PIPELINE.md) — Procedural Asset Package Pipeline

---

## 4. Gameplay, Survival & Mechanics Bibles
- [gameplay.md](../gameplay.md) — Core Gameplay Loop, Survival Verbs, Progression, Salvage
- [survival.md](../survival.md) — Oxygen Reserves, Pressure Hazards, Decompression & Trauma
- [player.md](../player.md) — Kinematic Character Controller, Swimming, EVA Handoff
- [physics.md](../physics.md) — PhysX Integration, Buoyancy, Tethers, Structural Flooding
- [combat.md](../combat.md) — Threat Contact, Penetration Math, Damage Routing
- [sonar.md](../sonar.md) — Hydroacoustic Radar, 3D Cartography, Scanner Hardware
- [vehicles.md](../vehicles.md) — Submarines, Cockpit Instruments, Hull Pressure Integrity
- [tools.md](../tools.md) — Plasma Welder, Cutter, Repair & Scanner Tools
- [construction.md](../construction.md) — Base Building, Socket CSR Solvers, Module Catalog
- [logistics.md](../logistics.md) — Power, Oxygen, Coolant & Data Graph Networks
- [inventory.md](../inventory.md) — Grid Inventory, Resource Balance, Salvage Economy

---

## 5. Audio, Visuals & Presentation Bibles
- [rendering.md](../rendering.md) — URP 6000.4 RenderGraph, Fog, Volumetrics, GPU Budgets
- [shaders.md](../shaders.md) — SRP Batcher Compatibility, Material Keywords, HLSL Runtime
- [lighting.md](../lighting.md) — Motivated Lighting, Bioluminescence, Darkness Readability
- [vfx.md](../vfx.md) — Particle Pooling, Silt Suspension, Tool Effects
- [ui.md](../ui.md) — Visor Glass HUD vs Wrist PDA (КПКТ-планшет)
- [UI_MENU_SCREEN_STANDARDS.md](../UI_MENU_SCREEN_STANDARDS.md) — Frontend & Menu Screen Standards
- [UI_DIEGETIC_HUD_STANDARDS.md](../UI_DIEGETIC_HUD_STANDARDS.md) — Diegetic World-Space UI Standards
- [audio.md](../audio.md) — Spatial Audio DSP Pipeline, Soundscape Mix States, ADPCM/Vorbis

---

## 6. Narrative, Lore & Localization Bibles
- [narrative.md](../narrative.md) — Deep Reach / Marauder Backstory, Quest State, Evidence
- [writing.md](../writing.md) — In-World Articles, Encyclopedia Entries, Anti-AI Prose Law
- [localization.md](../localization.md) — 15 Supported Locales, Zero-GC Text Rendering, Font Atlases
- [textes.md](../textes.md) — Public Store Copy, Marketing Captions, Creator Outreach

---

## 7. Platforms, SDK & Verification Bibles
- [platform.md](../platform.md) — Hardware Proof Ladder (Compact i3/MX350 -> High/Ultra)
- [xr.md](../xr.md) — PCVR (90 FPS) & Standalone XR (72 FPS) Comfort & Foveation
- [modding.md](../modding.md) — Mod SDK Sandbox Quarantine, AUP ±50km Bounds
- [release.md](../release.md) — Master Release Work Plan, Build Proof Gates
- [testing.md](../testing.md) — CI Automation, Verification Evidence Classes
```

---

## Verification & Implementation Plan

To implement the recommendations of this audit safely in follow-up tasks:
1. Run `python -B Tools/Docs/TestMandateRegistry.py --strict` to verify mandate registry health.
2. Execute file relocations via Git (`git mv`) to move `Docs/Tasks/Status_*.md` to `Docs/Archive/Batch014_LegacyTasks/`.
3. Create `Docs/HECTON8_KNOWLEDGE_GRAPH.md` with the verified blueprint.
4. Run `python -B Tools/Docs/BuildProjectRootBiblesCombined.py` and `python -B Tools/Docs/TestAgentRuleRouting.py` to confirm zero broken links or authority drift.
