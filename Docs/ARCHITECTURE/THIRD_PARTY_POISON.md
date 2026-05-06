# THIRD-PARTY POISON
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-04 Current-State Boundary

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
- Missing Crest bindings fail fast with explicit errors during runtime initialization instead of null-reference drift later.

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
