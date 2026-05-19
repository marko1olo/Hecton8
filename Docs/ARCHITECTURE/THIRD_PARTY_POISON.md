# THIRD-PARTY POISON
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current R31 static/tool boundary: R31 is the latest DOC_GLOBAL root/architecture current-boundary propagation layer; R30 remains the prior internal-currentness layer; AtlasCheck fails `57` RealtimeCSG refs; Mod API static validation now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this boundary as current runtime truth.
- This document is an anti-corruption contract, not proof that every Crest/MapMagic binding, material path, renderer feature, or scene object currently complies.
- Re-open adapters, scene bindings, and current console evidence before touching third-party integration.

## Scope

This document defines the runtime boundary between first-party HECTON-8 systems and third-party assets.

Third-party owners in scope:

- Crest
- MapMagic

Core engine owners in scope:

- `Hecton8.Core`
- gameplay/runtime systems consuming `GlobalRegistry`
- rendering systems that publish first-party buffers and draw contracts

## Rule Set

1. Core gameplay code does not import Crest or MapMagic namespaces directly.
2. Core gameplay code does not resolve third-party singletons directly.
3. Scene-wide fallback searches are forbidden for third-party runtime owners.
4. Third-party bindings must be injected explicitly at bootstrap or serialized on the owning adapter.
5. Runtime material instantiation for Crest or MapMagic integration is forbidden.
6. Shared authored materials remain shared. Per-instance data travels through `GraphicsBuffer` / `ComputeBuffer` on BRG or indirect draw paths.
7. `MaterialPropertyBlock` is restricted to approved legacy procedural draws, particles, and UI. It is not a standard-geometry SRP Batcher path.
8. `MaterialPropertyBlockRegistry` is keyed by stable owner entity IDs for those approved legacy paths. It is not a loophole for renderer-local `material` mutation.
9. Procedural renderers must treat `renderer.material`, `renderer.materials`, and runtime `new Material()` as violations unless the path is explicitly documented as UI-only or test-only.

## Anti-Corruption Layers

### Crest

Primary isolation points:

- `IHectonOceanKinematics`
- `Crest4KinematicsAdapter`
- `OceanKinematicsRuntimeService`

Contract:

- Gameplay samples ocean height, displacement, normals, and flow through `IHectonOceanKinematics`.
- Gameplay resolves the service from `GlobalRegistry.OceanKinematics`.
- Crest-specific `OceanRenderer` and collision-provider ownership remains inside the adapter layer.

### MapMagic

Primary isolation points:

- `MapMagicBridge`
- `HectonMapMagicVegetationBridge`
- explicit terrain/height/flow payload handoff into first-party render and simulation owners

Contract:

- Runtime systems consume first-party payloads exported by the bridge layer.
- Runtime systems do not call MapMagic APIs directly.
- If a required MapMagic owner is missing, the bridge logs a hard failure. It does not fall back to `Resources.FindObjectsOfTypeAll`, `FindAnyObjectByType`, or other scene-wide scans.

## Rendering Boundary

Approved ownership:

- `HectonHLODRenderer` publishes HLOD instance matrices and fade data through `GraphicsBuffer`.
- `GPUScatterDirector` publishes scatter payloads through `GraphicsBuffer` and indirect draw arguments.
- Buffer-backed renderers keep the authored material shared and bind data through buffer contracts.
- HLOD and scatter shaders consume instance payloads through `SV_InstanceID` plus `StructuredBuffer` reads. They do not use MPBs for per-instance world-geometry data.
- `MaterialPropertyBlockRegistry` exists only for approved legacy procedural draws, particle paths, and UI paths where SRP-batcher-sensitive standard geometry is not involved.

Forbidden ownership:

- Runtime `new Material()` clones to bind per-renderer data onto shared world geometry.
- `renderer.material.Set*`, `renderer.materials`, or any implicit material instantiation path on world geometry.
- MPB use as a substitute for buffer-backed instance data on BRG, indirect, HLOD, scatter, or other GPU-resident geometry lanes.
- Crest or MapMagic adapters mutating global rendering state outside their approved bridge layer.
- Material fallback creation in hot or warm runtime paths when a serialized asset is required.

## Documentation Prune

Outdated claims were removed from the root-level third-party note. This architecture document is now the canonical source.
No active architecture document should describe runtime `new Material()` as an acceptable rendering practice. Audit logs may mention historical violations as evidence only, not as approved guidance.

## Current Known Debt

The anti-corruption layer is not project-wide yet. First-party runtime code still contains Crest-aware owners outside the core gameplay boundary. Those references must remain confined to explicit integration layers until they are migrated.

Known examples to audit separately:

- `HectonUnderwaterVisuals`
- `HectonSurfaceWeatherDirector`
- Crest bootstrap and validation owners under `_Project/Scripts/Core` and `_Project/Scripts/World`

## Mandates Followed

- `PROJECT_LTS_Compatibility_Layer`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`
