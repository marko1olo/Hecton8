# Resource And Content Audit Matrix

Date: 2026-05-17
Status: STATIC_SOURCE_AUDIT / RUNTIME_PENDING

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

Owner prompt: MODDING_API_SCHEMA_BUILDER

## Source Files

- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs`

## Resource Boundary

| Contract | Source value | Rule |
|---|---:|---|
| Public resource resolve methods | `3` | Mods resolve hashes only. |
| Resource kinds | `3` | `Prefab`, `AudioClip`, `Texture`. |
| Resource registry capacity | `256` | More resources require explicit registry capacity review. |
| Internal asset loaders | `3` | `LoadPrefab`, `LoadAudioClip`, and `LoadTexture` are internal and throw from public facade. |
| Raw PNG max bytes | `8388608` | Raw texture fallback is capped at 8 MB. |
| Raw PNG max dimension | `2048` | Width or height above 2048 is rejected. |

## Public Resource Methods

| Method | Return | Rule |
|---|---|---|
| `TryResolvePrefab` | `uint hashId` | Hash-only. Engine owner resolves prefab internally. |
| `TryResolveAudioClip` | `uint hashId` | Hash-only. Engine owner resolves audio internally. |
| `TryResolveTexture` | `uint hashId` | Hash-only. Engine owner resolves texture internally. |

## Public Content Methods

| Surface | Methods | Rule |
|---|---|---|
| `HectonAPI.Items` | `RegisterCustomItem`, `TryFindItem` | Runtime catalog overlay; no authored asset mutation. |
| `HectonAPI.Crafting` | `RegisterRecipe`, `RegisterRecycleYield` | Cold recipe/recycle overlay only. |
| `HectonAPI.Recycling` | `ProcessRecycle` | Owner-arbitrated request; no direct inventory mutation. |
| `HectonAPI.Construction` | `RegisterBuildable`, `TryFindBuildable` | Runtime buildable overlay; no direct scene spawn. |
| `HectonAPI.Ecosystem` | `RegisterBiomeMutation` | Deterministic fauna mutation overlay. |
| `HectonAPI.Localization` | `InjectTable` | Cold localization injection only. |
| `HectonAPI.UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` bool, `RegisterSetting` float | Presentation/settings only; UI cannot claim command success without owner acceptance. |

Public content method count: `14`.

## Registry Capacities

| Registry | Capacity | Rule |
|---|---:|---|
| `ModItemRegistry` | `16` | Deferred item registrations until runtime catalog exists. |
| `ModRecipeRegistry` | `32` | Runtime recipe overlay. |
| `ModBuildableRegistry` | `16` | Deferred buildable registrations until module catalog exists. |
| `ModEcosystemRegistry` | `16` | Runtime biome mutation overlay. |
| `ModSettingsRegistry` | `32` | UI settings entries and lookup. |
| `ModLocalizationBridge` | `32` pending / `64` injected guards | Language table injection. |

## Forbidden

- No public Unity asset reference returned to mods.
- No `GameObject`, `AudioClip`, `Texture2D`, prefab, material, mesh, or ScriptableObject handle crosses the public facade.
- No direct scene spawn/despawn through content registration.
- No hot-path raw disk/AssetBundle loading through mod callbacks.
- No resource access outside active `ModExecutionScope`.

## Static Drift Gate

`Docs/Modding/Validate_Mod_API_Static.ps1` must fail if resource method count, content method count, resource kind count, registry capacity, internal asset loader count, or raw texture caps drift without this audit and schema being updated.
