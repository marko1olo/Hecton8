# Resource And Content Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC_SOURCE_AUDIT / RUNTIME_PENDING

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract

## 2026-05-19 Envelope-Only Override

The table below records the historical/source-audit resource facade. In current envelope-only runtime mode, direct mod resource/content ingress is quarantined:

- public resource proxy registration does not grant a live Unity object to mods;
- runtime `.bundle`, `lang_*.json`, raw PNG, prefab, material, mesh, texture, audio clip, or localization file discovery is not a mod right;
- UGC assets must be imported by SDK tooling, CRC-approved, byte-capped, and referenced by `FutureCommandEnvelope` asset opcodes;
- engine owners resolve approved hashes internally after the sandbox accepts a packet.

SDK authoring details are in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

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
| Resource owner proof | active `ModExecutionScope` id match | Registry rejects forged `modId` values before hashing. |
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
| `HectonAPI.Items` | none public | `ItemData` ScriptableObject handles are internal forbidden accessors only. |
| `HectonAPI.Crafting` | none public | Recycle yield overrides need content manifest ownership and unload revocation. |
| `HectonAPI.Recycling` | none public | Direct inventory mutation must use engine-owned command routes. |
| `HectonAPI.Construction` | none public | `BuildableData` ScriptableObject handles are internal forbidden accessors only. |
| `HectonAPI.Ecosystem` | none public | Biome mutation overlays need mod ownership, unload revocation, and runtime proof. |
| `HectonAPI.Localization` | `InjectBabelEnvelope` | Rejected binary Babel envelope seam; runtime dictionary injection is disabled. |
| `HectonAPI.UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` bool, `RegisterSetting` float | Presentation/settings only; UI cannot claim command success without owner acceptance. |

Public content method count: `6`.

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
- No resource registration under a `modId` different from the active `ModExecutionScope`.

## Static Drift Gate

`Docs/Modding/Validate_Mod_API_Static.ps1` must fail if resource method count, content method count, resource kind count, registry capacity, internal asset loader count, or raw texture caps drift without this audit and schema being updated.
