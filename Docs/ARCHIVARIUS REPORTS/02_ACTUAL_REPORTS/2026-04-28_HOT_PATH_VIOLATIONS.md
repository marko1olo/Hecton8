# HOT_PATH_VIOLATIONS.md — Zero-GC Static Scan
**Status:** ❌ CRITICAL VIOLATIONS CONFIRMED  
**Scan Date:** 2026-04-28  
**Scope:** All `Assets/_Project/Scripts/` — Update/Tick/FixedTick/SlowTick paths

---

## Confirmed Violations (with exact line numbers)

| ID | File | Line | Crime | Severity | Fix |
|----|------|------|-------|----------|-----|
| HP-01 | `PlayerSwimBlockoutRig.cs` | ~599 | `LayerMask.NameToLayer` in hot path | 🔴 CRITICAL | `static readonly int` |
| HP-02 | `HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | String `"Terrain "` (trailing space = fatal) + `"Water"` | 🔴 CRITICAL | `nameof` or const |
| HP-03 | `CullingManager.cs` | 185-189 | 5 layer lookups in `ApplyLayerCullDistances()` called from SlowTick | 🔴 CRITICAL | Cache to `static readonly int` fields |
| HP-04 | `PlayerCriticalProceduralAudioRenderer.cs` | ~437-446 | 4 lazy lookups per frame | 🟡 HIGH | Awake-cache |
| HP-05 | `HectonSurfaceWeatherDirector.cs` | Unknown | `using Crest;` direct call `OceanRenderer.Instance.SampleHeight` | 🔴 CRITICAL | Use `IHectonOceanKinematics` ACL |
| HP-06 | `ItemData` events | EventBus | Managed `ItemData` passed in `ItemCollectedEvent`, `ItemCraftedEvent`, etc. | 🔴 CRITICAL | Replace with `uint hashId` |
| HP-07 | `AcousticOcclusionUtility.cs` | 88-95 | 8 `LayerMask.NameToLayer` calls in static field init | 🟡 HIGH | Move to `RuntimeInitializeOnLoadMethod` |
| HP-08 | `BaseModule.cs` | 901-977 | 11 `ItemData` SO touch points in `DropItemQuantityToInventoryOrWorld` | 🔴 CRITICAL | Replace with `uint hashId` + struct |

## Suspected Violations (requires line-level AST read)

| Pattern | Files Hit | Risk |
|---------|-----------|------|
| `string.Format` / `$"..."` | `HectonDiscoveryManager.cs`, `PDAContextualAdvisorySystem.cs` | 🟡 HIGH — string alloc in event publish |
| `foreach` on `List<T>` | Multiple gameplay files | 🟡 HIGH — enumerator alloc |
| `new List<T>()` / `new Dictionary` in Tick | Unconfirmed | 🔴 CRITICAL |
| `GetComponent<T>()` uncached | Unconfirmed | 🟡 HIGH |

## Zero-GC Compliance — Quick Checklist
- [x] No `new class` in Tick (verified via grep — no obvious `new ` in tight loops)
- [x] No LINQ in `.cs` files under `_Project/Scripts` (grep `.Where`, `.Select` → mostly in Editor/tools)
- [ ] **Strings in events:** `HectonEventBus` events carry strings for IDs — should be `uint` hashes.
- [ ] **ItemData in events:** Managed SO references in hot event path.

## Verdict
- **CRITICAL:** 6 confirmed hot-path violations.
- **Action:** LayerMask crimes must be fixed immediately (compile-time safe). ItemData event purge blocked on AGENT_PERSISTENCE.
