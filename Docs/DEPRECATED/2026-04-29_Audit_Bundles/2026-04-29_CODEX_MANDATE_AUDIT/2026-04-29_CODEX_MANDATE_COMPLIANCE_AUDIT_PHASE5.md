# 2026-04-29 - CODEX Mandate Compliance Audit Phase 5
Date: 2026-04-29

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Persistent_Object_Registry.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## Method

- Audit focus: lifecycle ownership and teardown discipline.
- Checked first-party runtime scripts for pool creation/expansion, raw runtime instantiation, Addressables load/release ownership, and scene-persistent service patterns.
- Validated representative source files instead of relying only on grep counts.
- No Unity runtime validation was performed.

## What Is Actually Aligned

### 1. `PersistentWorldRegistry` contains a real unmanaged hydration core

Direct evidence:

- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
  - `NativeArray<PoolSlotData> _poolSlotData`
  - `NativeHashMap<ulong, int> _guidToPoolIndex`
  - `GameObject[] _hydratedInstancesBySlot`
  - `Transform[] _poolSlotTransforms`
  - `Rigidbody[] _poolSlotRigidbodies`

Assessment:

- This is not a fake registry.
- There is a serious attempt to keep GUID-to-slot ownership explicit and O(1).

### 2. Deferred Addressables release exists in one real owner path

Direct evidence:

- `Assets/_Project/Scripts/ItemCatalog.cs`
  - `_pendingWorldPrefabReleaseQueue`
  - `_pendingWorldPrefabReleaseSet`
  - `DrainDeferredWorldPrefabReleases(int maxReleaseCount)`
  - `Addressables.Release(runtimeRecord.Handle)`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:633`
  - `_resolvedItemCatalog?.DrainDeferredWorldPrefabReleases(4);`

Assessment:

- A real deferred-release path exists.
- The problem is not total absence.
- The problem is that the pattern is not systemic across the runtime.

### 3. Several pooled runtime actors do reset and unregister correctly

Representative aligned examples:

- `Assets/_Project/Scripts/Gameplay/MantaEmergencyWreck.cs`
  - `OnSpawn()` and `OnDespawn()` both reset state
  - pooled self-despawn prefers `ObjectPoolManager`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
  - `OnSpawn()` / `OnDespawn()` reset runtime state
  - `OnDisable()` / `OnDestroy()` unregister dispatcher and spatial handles
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs`
  - `OnSpawn()` / `OnDespawn()` reset rigidbody and tick registration
  - `OnDisable()` / `OnDestroy()` unregister floating-origin hooks
- `Assets/_Project/Scripts/BaseModule.cs`
  - `OnSpawn()` / `OnDespawn()` route through explicit register/unregister flow
- `Assets/_Project/Scripts/WorldProceduralProxyInstance.cs`
  - `OnSpawn()` / `OnDespawn()` restore and release optimization ownership

Assessment:

- Some teams inside the project are following the intended pooled lifecycle model.
- This makes the remaining gaps more obvious, not less.

## Confirmed Findings

### 1. `ObjectPoolManager` itself violates the fixed-capacity boot-time pooling mandate

Mandate conflict:

- `STRM_Persistent_Object_Registry.txt`: `Pool capacity fixed at boot. No runtime growth.`

Direct source evidence:

- `Assets/_Project/Scripts/ObjectPoolManager.cs`
  - `Awake()` enforces singleton + `DontDestroyOnLoad(gameObject)`
  - `Spawn(...)` creates a pool on demand when missing
  - `Spawn(...)` expands a pool when empty
  - `ExpandPool(...)` uses `fallbackExpandBatchSize`

Representative lines:

- `CreatePool(prefab, id);`
- `WarnExpand(prefab, "Pool created on-demand. Call Warmup() at load time!");`
- `if (pool.available.Count == 0) { ... ExpandPool(pool, prefab, id); }`
- `WarnExpand(prefab, $"Pool exhausted, expanding by {expandCount}.");`

Assessment:

- This is a convenience pool manager, not a mandate-compliant fixed-capacity hydration pool.
- Runtime growth is explicitly supported.
- On-demand pool creation means ownership is discovered during play, not established at boot.

Impact:

- CPU hitch risk during emergency expand.
- Memory predictability is weakened.
- Pool sizing errors are hidden instead of being surfaced as hard failures.

### 2. Persistent world hydration is architecturally closer to the mandate than most systems, but still falls back to runtime warmup

Mandate conflict:

- `STRM_Persistent_Object_Registry.txt`: `Pool capacity fixed at boot. No runtime growth.`

Direct source evidence:

- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
  - if a prefab pool does not exist, hydration calls `pool.Warmup(prefab, 1);`
  - hydrated spawn uses `allowExpand: false`

Representative lines:

- `if (!pool.HasPool(prefab)) pool.Warmup(prefab, 1);`
- `pool.Spawn(prefab, ..., allowExpand: false);`

Assessment:

- `allowExpand: false` is the correct local discipline.
- But the system still tolerates missing pool ownership by creating capacity at hydration time.
- That is still runtime pool growth.

What is objectively missing:

- Pool slot ownership fully established before gameplay.
- Hydration that binds only to predeclared capacity.
- Hard failure or preload-time rejection when required pool capacity is absent.

### 3. Addressables lifecycle discipline is not global; it is effectively concentrated in `ItemCatalog`

Mandate conflict:

- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`: `Every handle issued = entry in global registry. No orphaned handles. Ever.`

Repository evidence:

- First-party `Addressables` load/release match count in `Assets/_Project/Scripts`: `1`
- That one confirmed release is:
  - `Assets/_Project/Scripts/ItemCatalog.cs:649`

Additional source evidence:

- `Assets/_Project/Scripts/ItemCatalog.cs`
  - `LoadAssetAsync<GameObject>()`
  - runtime handle record storage
  - deferred release queue
- `Assets/_Project/Scripts/AsyncLoadHelper.cs`
  - legacy async helper is disabled
  - runtime Resources/Addressables path intentionally fails immediately

Assessment:

- There is no visible project-wide Addressables registry matching the mandate.
- There is one localized handle owner for world item prefabs.
- The rest of the runtime is mostly not on the approved async asset lifecycle at all.

What is objectively missing:

- One global handle registry with ref-count ownership.
- Tiered load/release policy.
- Leak auditing for handles outside item-prefab residency.
- A unified asset lifecycle instead of one compliant island.

### 4. Runtime instantiation still bypasses the pool/hydration model in several live systems

Repository evidence:

- `Instantiate(` match count under `Assets/_Project/Scripts`: `20`
- Several hits are editor-only, but runtime first-party examples remain.

Confirmed runtime examples:

- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:723`
  - `Instantiate(sceneConfig.RuntimeDirectorPrefab);`
- `Assets/_Project/Scripts/UI/UIParticleEffect.cs:103`
  - `Instantiate(particlePrefab, transform);`
- `Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs:116`
  - `Instantiate(markerIconPrefab, iconContainer);`
- `Assets/_Project/Scripts/UI/BeaconHUDElement.cs:94`
  - `Instantiate(beaconIconPrefab, iconContainer);`

Nuance:

- `PDAMarkerHUDElement` and `BeaconHUDElement` look like scene-lifetime UI prebuilds, not hot-path spam.
- `UIParticleEffect` instantiates only on initialization.
- `HectonMusicDirector` is more serious because it is part of persistent runtime service bootstrap.

Assessment:

- Not every raw instantiate is equally harmful.
- The project still does not have one hard ownership rule for runtime object birth.
- Pool/hydration discipline is partial, not systemic.

### 5. Non-pooled escape paths still exist inside pooled or pool-aware gameplay objects

Direct source evidence:

- `Assets/_Project/Scripts/Gameplay/FloraProjectile.cs`
  - preferred path: `pool.Despawn(gameObject);`
  - fallback path: `Destroy(gameObject);`
- `Assets/_Project/Scripts/BaseModule.cs`
  - fallback path destroys object if pool manager is unavailable

Assessment:

- These fallbacks improve resilience in broken states.
- They also mean lifecycle ownership is not absolute.
- A mandate-compliant persistent/runtime pool model should fail at ownership setup, not silently devolve to destroy paths in gameplay.

### 6. Scene-persistent ownership is still hybrid `GlobalRegistry` plus singleton plus `DontDestroyOnLoad`

Mandate conflict:

- `AGENTS.md`: `GlobalRegistry (Service Locator Pattern)`
- `AGENTS.md`: `[FORBID] Classic Singletons and Awake() self-registration.`
- `AGENTS.md`: `[FORBID] DontDestroyOnLoad without instruction.`

Repository evidence:

- `DontDestroyOnLoad` match count in shipping first-party scripts: `82`

Confirmed examples:

- `Assets/_Project/Scripts/SaveManager.cs`
  - singleton `_instance`
  - `DontDestroyOnLoad(gameObject)`
  - also registers into `GlobalRegistry`
- `Assets/_Project/Scripts/ObjectPoolManager.cs`
  - singleton `_instance`
  - `DontDestroyOnLoad(gameObject)`
- `Assets/_Project/Scripts/GameTickManager.cs`
  - `GlobalRegistry.RegisterTickManager(this);`
  - `DontDestroyOnLoad(gameObject)`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - dispatcher singleton/registry hybrid
  - `DontDestroyOnLoad(gameObject)`

Assessment:

- The project is not purely on the declared GlobalRegistry init architecture.
- It is running a hybrid persistence model:
  - singleton guards
  - `DontDestroyOnLoad`
  - registry registration

Impact:

- Ownership is harder to reason about during scene transitions.
- Duplicate-instance protection is being handled reactively with destroy guards.
- Lifecycle bugs become bootstrap-order problems instead of explicit composition problems.

## System-Level Assessment

Pooling:

- Good pooled object discipline exists in multiple gameplay/world classes.
- The pool infrastructure itself still permits runtime growth and late ownership creation.

Persistent hydration:

- `PersistentWorldRegistry` is one of the stronger architectural pieces in the project.
- It still stops short of fixed-capacity boot-time hydration because it can warm pools at runtime.

Addressables:

- There is one real deferred-release owner path.
- There is no evidence of mandate-level global asset lifecycle ownership.

Scene persistence:

- The runtime still leans heavily on singleton plus `DontDestroyOnLoad`.
- That directly conflicts with the declared architecture direction.

## What The Project Objectively Missed In This Phase

- A truly fixed-capacity pool model established before gameplay starts.
- One global Addressables ownership registry with ref-count and deferred-release discipline.
- Removal of runtime warmup as a normal persistent-world hydration fallback.
- Removal of singleton and `DontDestroyOnLoad` persistence drift from core managers.
- One hard rule for runtime object birth instead of mixed pool, hydration, cold instantiate, and destroy fallback behavior.

## Regression Model

CPU:

- Runtime pool creation and pool expansion create stall risk exactly where the mandate tries to avoid it.

GC:

- Mixed lifecycle ownership increases the chance that non-pooled fallback paths and ad hoc bootstrap objects keep re-entering the runtime.

Memory:

- Weak global handle ownership increases leak risk.
- `DontDestroyOnLoad` spread increases retained-runtime surface across scenes.

Cadence:

- Hybrid lifecycle rules slow down debugging because ownership is distributed across bootstrap, singleton guards, pools, and scene persistence.

Correctness:

- Destroy fallbacks and duplicate-instance destroy guards hide invalid ownership states instead of failing at composition boundaries.

## Verification Status

Static verification only.

Not performed:

- Unity Play Mode validation
- pool exhaustion runtime test
- Addressables handle leak audit in profiler
- scene transition teardown validation
- long-session memory retention capture

Final status: PENDING VERIFICATION
