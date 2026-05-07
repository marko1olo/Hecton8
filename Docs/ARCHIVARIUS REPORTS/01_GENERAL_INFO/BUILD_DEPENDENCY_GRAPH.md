# BUILD DEPENDENCY GRAPH â€” HECTON-8 Bootstrapper Bloat Check
Date: 2026-05-07
Status: PENDING VERIFICATION


**Date:** 2026-04-29
**Target:** `GameBootstrapper.cs` + `Assets/_Project/Prefabs` hard-references
**Authority:** CTO / Lead Architect
**Status:** ETA LEAK_MAPPED

---

## EXECUTIVE SUMMARY

`GameBootstrapper` is a **pure-code initialization orchestrator**. It does NOT directly reference any heavy assets (4K textures, audio beds, mesh prefabs, Addressables, or `Resources.Load` bundles).
**Verdict:** Zero bootstrap-forced bloat. The only asset touch is a built-in Unity font (~negligible).

> If the project is bleeding memory at boot, the leak is NOT in `GameBootstrapper`'s dependency graph. Investigate Crest `WaterRenderer` auto-initialization, `MapMagic` terrain chunk pre-warming, or BRG vegetation buffer pre-allocation instead.

---

## BOOTSTRAPPER PHASE MAP

```
00_BOOTSTRAP scene
â”‚
â”œâ”€â”€ GameBootstrapper (MonoBehaviour, DefaultExecutionOrder -29980)
â”‚   â”œâ”€â”€ InitializeCoreLayer()
â”‚   â”‚   â”œâ”€â”€ VRAMEnforcer.InitializeRuntimeBudget()          â† sets budgets (code only)
â”‚   â”‚   â”œâ”€â”€ SystemDispatcher.EnsureRuntimeInstance()        â† code only
â”‚   â”‚   â”œâ”€â”€ RenderDispatcher.EnsureRuntimeInstance()        â† code only
â”‚   â”‚   â”œâ”€â”€ SceneInstantiationGate.EnsureRuntimeInstance()  â† code only
â”‚   â”‚   â”œâ”€â”€ SceneRuntimeService.EnsureRuntimeInstance()     â† code only
â”‚   â”‚   â””â”€â”€ EquipmentInteractionHandler.EnsureRuntimeInstance() â† code only
â”‚   â”‚
â”‚   â”œâ”€â”€ InitializeEnvironmentLayer()
â”‚   â”‚   â”œâ”€â”€ GlobalPhysicsStateManager.EnsureRuntimeInstance()   â† code only
â”‚   â”‚   â”œâ”€â”€ PhysicsApplySystem.EnsureRuntimeInstance()          â† code only
â”‚   â”‚   â”œâ”€â”€ DebrisManager.EnsureRuntimeInstance()               â† code only
â”‚   â”‚   â”œâ”€â”€ EnvironmentRuntimeContextService.EnsureRuntimeInstance() â† code only
â”‚   â”‚   â””â”€â”€ OceanKinematicsRuntimeService.EnsureRuntimeInstance()    â† code only
â”‚   â”‚
â”‚   â”œâ”€â”€ InitializePlayerLayer()
â”‚   â”‚   â”œâ”€â”€ InputManager.Instance validation                  â† existing singleton
â”‚   â”‚   â”œâ”€â”€ InputDispatcher.EnsureRuntimeInstance()           â† code only
â”‚   â”‚   â”œâ”€â”€ PlayerRuntimeContextService.EnsureRuntimeInstance() â† code only
â”‚   â”‚   â”œâ”€â”€ PlayerInventoryManager.EnsureRuntimeInstance()    â† code only
â”‚   â”‚   â”œâ”€â”€ PlayerSensoryManager.EnsureRuntimeInstance()      â† code only
â”‚   â”‚   â””â”€â”€ ContextualPhysicalIkRuntime.EnsureRuntimeInstance() â† code only
â”‚   â”‚
â”‚   â””â”€â”€ InitializeUILayer()
â”‚       â””â”€â”€ (empty â€” no UI GlobalRegistry adapter yet)
â”‚
â””â”€â”€ BootstrapBiosErrorOverlay (emergency overlay, lazy-created on error only)
    â””â”€â”€ Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")  â† built-in asset
```

---

## HARD-REFERENCE AUDIT

### GameBootstrapper.cs

| Reference Type | Asset / Class | Weight | Notes |
|----------------|-------------|--------|-------|
| `Resources.GetBuiltinResource<Font>` | `LegacyRuntime.ttf` | ~20 KB | Built-in Unity font; loaded ONLY if bootstrap error overlay is shown. |
| `DontDestroyOnLoad(inputManager.gameObject)` | InputManager prefab | Varies | InputManager is a scene singleton; `DontDestroyOnLoad` preserves it across scenes. Not a bootstrap load. |
| `StringBuilder` (1024 chars) | Managed string buffer | ~2 KB | Cold-alloc for fatal crash log formatting. |
| `NativeArray<byte>` (24576 bytes) | Crash log scratch | ~24 KB | Temp allocator; disposed in `finally` block. |

**Total forced RAM at bootstrap:** < 50 KB of managed/code memory.
**Zero textures, zero audio, zero meshes, zero Addressables.**

---

### Prefab Folder Scan (Hard-Reference Check)

A search for `m_SourcePrefab: {fileID:` and direct `AssetReference` fields inside `Assets/_Project/Prefabs` that are pulled by `GameBootstrapper` yielded **no matches**. The bootstrapper never calls:
- `Object.Instantiate()`
- `Resources.Load()`
- `Addressables.LoadAssetAsync()`
- `AssetReference.LoadAssetAsync()`

All `EnsureRuntimeInstance()` calls either:
1. Find an existing singleton in the bootstrap scene, OR
2. `AddComponent<T>()` to a shell GameObject.

---

## ASSET_DEPENDENCY_MAP.md STATUS

`ASSET_DEPENDENCY_MAP.md` was referenced in `MASTER_INDEX.md` but **does not exist** on disk.
**Action:** Created as a placeholder stub in `01_GENERAL_INFO/ASSET_DEPENDENCY_MAP.md` mapping known hard-references. See file below.

---

## MASSIVE ASSET INVENTORY (NOT BOOTSTRAPPED)

These assets live in the project but are **NOT** forced into RAM by `GameBootstrapper`. They are loaded later via Addressables or scene streaming:

| Asset Category | Typical Path | Load Trigger | Owner |
|----------------|-------------|--------------|-------|
| Ocean data / Crest settings | `Assets/_ThirdParty/Crest/...` | Scene `02_HECTON_WORLD` init | Crest (third-party) |
| Terrain / MapMagic graphs | `Assets/_Project/Data/Terrain/...` | Chunk streaming | MapMagicBridge |
| Vegetation meshes | `Assets/_Project/Art/Vegetation/...` | BRG indirect draw | HectonIndirectVegetationRenderer |
| 4K Textures (hero props) | `Assets/_Project/Art/Textures/...` | Addressables async | Asset loading system |
| Audio beds (ambient/music) | `Assets/_Project/Audio/...` | Addressables async | AudioManager |
| Player prefab | `Assets/_Project/Prefabs/Player.prefab` | Scene `02_HECTON_WORLD` spawn | SceneRuntimeService |

---

## REGRESSION MODEL

| Dimension | Before | After | Delta |
|-----------|--------|-------|-------|
| Bootstrapper-forced heavy assets | Unknown | 0 confirmed | â€” |
| `Resources.Load` calls in bootstrap | Unknown | 1 (built-in font only) | â€” |
| Addressables loads in bootstrap | Unknown | 0 | â€” |
| Prefab instantiations in bootstrap | Unknown | 0 | â€” |

---

**MANDATES FOLLOWED:** AGENTS.md Â§Addressables (async only, no hard refs), Â§13 (Memory Lifetime), Â§Scene Flow (00_BOOTSTRAP â†’ 01_MAIN_MENU â†’ 02_HECTON_WORLD).

**STATUS:** ETA LEAK_MAPPED â€” Build Dependency slice complete.
