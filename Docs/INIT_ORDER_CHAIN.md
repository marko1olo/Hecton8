# INIT ORDER CHAIN — is `GlobalDataVault` registered before `PlayerInventory.Awake`?

Scope: static source read only. No Unity run, no `dotnet`, no profiler. Every claim below carries
`file:line`. Where a claim is "the code says" rather than "at runtime this happens", it is labelled.

---

## VERDICT

**ORDER IS CORRECT. The vault registers first. This is NOT the cause of a dead inventory on the
production boot route.**

`GlobalRegistry.RegisterDataVault` runs in `BootstrapPhase.MemoryPreWarm`, which is phase **1** of 8
(`GameBootstrapper.cs:427`). The `PlayerInventory` MonoBehaviour is not authored in any scene — it exists
only on `Player.prefab`, and that prefab is cold-instantiated by `HectonPlayerSpawner`, which is authored
only in `02_HECTON_WORLD.unity`. That scene is not loaded until `BootstrapPhase.SceneActivate`, phase
**6** (`GameBootstrapper.cs:2445`). Five phases separate the two. On this route
`GlobalRegistry.DataVault` cannot be null when `PlayerInventory.Awake` runs.

**But the framing in the question is wrong in two places, and one of them is worth more than the verdict.**

---

## CORRECTION 1 — the line reference is stale, and `Awake` does not read the vault where you think

`PlayerInventory.cs:976` is not the null check. The `_cachedDataVault == null` early-out is at
**`PlayerInventory.cs:985-986`**, inside `BindPlayerInventoryVaultBuffers` (declared at
`PlayerInventory.cs:983`). Nine lines of drift; the file has moved since the reference was taken. Two
other diagnostics carry the same stale-line disease and will mislead the next reader:
`H8_HeadlessWorldDriver.cs:1333` cites `PlayerInventory.cs:1356-1372` and `:1385-1389` for the layout
guard and the bind bailout; the live locations are `PlayerInventory.cs:1394-1422` and `1435-1439`.

More important: **`_cachedDataVault` is never assigned in `Awake`'s own body.** It has exactly two
writers in the whole file — `PlayerInventory.cs:1515` (the hot-swap callback) and
`PlayerInventory.cs:1559` (inside `CacheRegistryServicesCold`, declared at `1551`). `CacheRegistryServicesCold`
is called from `OnEnable` at `PlayerInventory.cs:1447`, which runs *after* `Awake`.

That looked, on first read, like a guaranteed-null field and therefore a bind that can never succeed
regardless of registry state. It is not. `TryBindRuntimeStorageCold` calls
`CacheRegistryServicesCold()` at **`PlayerInventory.cs:2258`**, before it calls
`BindPlayerInventoryVaultBuffers(cellCount)` at **`PlayerInventory.cs:2266`**. So the field is refreshed
from the registry inside the Awake call path, and the null check at `:985` really is a live read of
`GlobalRegistry.DataVault` at Awake time. The ordering question is the right question. I record the
false lead because the ordering of `2258` before `2266` is the only thing that saves it, and an edit
that reorders those two lines would convert this into an unconditional failure with no other symptom.

## CORRECTION 2 — the recovery path is NOT uncalled. It has a live driver.

The question asks whether anything actually calls `TryRecoverRuntimeStorageCold`
(`PlayerInventory.cs:2220`). It does, and the chain is complete:

- `PlayerToolManager.cs:1461` — `if (!playerInventory.TryRecoverRuntimeStorageCold()) return;`
- inside `RetryRuntimeStartToolGrantIfPending()`, `PlayerToolManager.cs:1424`
- called first thing in `PlayerToolManager.Tick(float)` at `PlayerToolManager.cs:379`
- `Tick` is the `ITickable`/`IUpdatable` implementation — `PlayerToolManager.cs:45`
- registered on the dispatcher in `OnEnable` via `TryRegisterToTickManager()`
  (`PlayerToolManager.cs:293`, body at `327`), which calls
  `GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player)` at `PlayerToolManager.cs:337`
- `PlayerToolManager`'s script GUID `a2118fe6e07281c46a3e8e9de7073ed3` is authored on `Player.prefab`,
  the same prefab that carries `PlayerInventory` — so the driver ships with the thing it repairs.

Retry budget is finite but generous: 600 attempts (`PlayerToolManager.cs:155`), the first 16 on
consecutive ticks (`:159`), then a stride of 60 (`:162`).

Two real limits on that recovery, both static-provable:

1. `TryRegisterToTickManager` early-returns when `GlobalRegistry.Dispatcher == null`
   (`PlayerToolManager.cs:331-332`). `Dispatcher` is a bare field read — `GlobalRegistry.cs:2304`,
   `public static SystemDispatcher Dispatcher => _dispatcher;` — so if the tool manager's `OnEnable`
   runs before the dispatcher is registered, no tick lane exists and the recovery never runs.
2. `TryRecoverRuntimeStorageCold` returns false immediately when `_grid == null`
   (`PlayerInventory.cs:2236-2237`), i.e. before `Awake` has built the grid at
   `PlayerInventory.cs:1424`.

---

## STEP 3, THE CRITICAL ONE — is the accessor a null-object?

**No. `GlobalRegistry.DataVault` is a bare field read and returns real `null` when unset.**

```
GlobalRegistry.cs:673   private static IDataVault _dataVault;
GlobalRegistry.cs:807   public static IDataVault DataVault => _dataVault;
```

No fallback, no null-object, no warning publication. So `_cachedDataVault == null` at
`PlayerInventory.cs:985` is a *meaningful* test: it is true exactly when no vault is registered. The
inventory's failure mode is therefore the loud one (fail-closed plus a logged
`STORAGE UNAVAILABLE` line at `PlayerInventory.cs:2317-2328`), not the invisible one.

Contrast with the Input precedent, which is exactly the shape the question warned about, and which is
real:

```
GlobalRegistry.cs:313-314  // COLD ALLOC: NoOpInputService[1] - null-object fallback for premature
                           // GlobalRegistry.Input reads
                           private static readonly IInputService _noOpInputService = new NoOpInputService();
GlobalRegistry.cs:920-938  public static IInputService Input { get { ... return _noOpInputService; } }
GlobalRegistry.cs:8428-8430 private sealed class NoOpInputService : IInputService
                            { public bool IsInitialized => false; ... }
```

So the two slots are genuinely different: `Input` hands out a plausible non-null liar, `DataVault`
hands out `null`. **The bug shape does not transfer to the vault via the accessor.**

### What DOES transfer: the "first registration notifies nobody" mechanic

This half of the precedent is intact and applies to `DataVault` unchanged:

```
GlobalRegistry.cs:7406-7408   MarkServiceRegistered(serviceSlot);
                              if (previousService != null)
                                  QueueServiceRebound(serviceSlot, previousService, instance);
```

First registration into an empty slot has `previousService == null`, so no rebound is queued and no
`IGlobalRegistryHotSwapListener` is notified. `PlayerInventory` implements that interface
(`PlayerInventory.cs:34`) and handles `GlobalRegistryServiceSlot.DataVault` at
`PlayerInventory.cs:1514-1519` — but that handler can only ever fire on a *replacement*, never on the
first publish. And it would not be enough anyway: it calls
`RebindPlayerInventoryVaultReferences` (`:1516`), which re-points existing lanes at a new vault; it does
not allocate lanes that were never bound. The component's own doc comment says this at
`PlayerInventory.cs:2204-2206`.

Compounding it: `Awake` sets `enabled = false` on bind failure (`PlayerInventory.cs:1437`), so `OnEnable`
never runs, so `TryRegisterHotSwapListener` (`:1448`) never runs, so the component is not even on the
notification list. Self-recovery is structurally impossible; that is precisely why the
`PlayerToolManager` driver in Correction 2 exists.

---

## THE FULL CHAIN, IN ORDER

Session start, before any scene:

1. `GameBootstrapper.ResetStaticState()` — `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`,
   `GameBootstrapper.cs:878-879`. Calls `ShutdownGlobalDataVaultForBootstrapTeardown()` at
   `GameBootstrapper.cs:906`, which unregisters and disposes the vault
   (`GameBootstrapper.cs:1849-1861`). This exists because domain reload is disabled on this project
   (`HectonPlayerSpawner.cs:104` cites `m_EnterPlayModeOptions: 1`), so statics carry across play
   sessions. **Net effect: the vault slot is deliberately empty at t=0.**
2. `GameBootstrapper.GuardEntryVectorBeforeSceneLoad()` —
   `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, `GameBootstrapper.cs:1279-1283`. Body is
   `return;`. **Nothing registers the vault before the first scene's Awake batch.**

First scene load (`00_BOOTSTRAP.unity`, text-serialized):

3. `GameBootstrapper.Awake()` — `GameBootstrapper.cs:1518` — calls `MarkProjectPersistentRoot()` at
   `:1540`, which is `DontDestroyOnLoad(gameObject)` (`GameBootstrapper.cs:7044-7047`). This is load-bearing
   for the verdict: see "the one way this could still break" below.
4. `GameBootstrapper.GuardInitialSceneEntry()` — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`,
   `GameBootstrapper.cs:1264-1277` — calls `EnsureRuntimeInstance()?.BeginBootstrap()` at `:1276`, but
   only `if (TryRecoverEntryVector(activeScene, true) && IsBootstrapScene(activeScene))`.

Phase machine — `RunBootstrapStateMachineAsync`, `GameBootstrapper.cs:2399`, phases awaited strictly
sequentially:

| Phase | Enum | Await site |
|---|---|---|
| HardwareCheck | `:426` | `:2409` |
| **MemoryPreWarm** | `:427` | **`:2411`** |
| CoreServices | `:428` | `:2413` |
| Environment | `:429` | `:2415` |
| Player | `:430` | `:2418` |
| UI | `:431` | `:2424` |
| **SceneActivate** | `:432` | **`:2445`** |

5. `InitializeMemoryPreWarmPhaseAsync` (`GameBootstrapper.cs:2614`) → `InitializeBootstrapAllocators()`
   at `:2619` (declared `:2704`) → `EnsureGlobalDataVaultRegistered(...)` at `:2716` (declared `:2771`):
   - `_globalDataVault = GlobalDataVault.Create(...)` — `GameBootstrapper.cs:2778`
   - **`GlobalRegistry.RegisterDataVault(_globalDataVault)` — `GameBootstrapper.cs:2781`**
   - accepted, because `RegisterService` only refuses once the registry phase is `Ready`
     (`GlobalRegistry.cs:7203-7205`), and `LockReady()` is not called until after SceneActivate
     (`GameBootstrapper.cs:2448`, `GlobalRegistry.cs:2547-2551`).
   - `GlobalDataVault` is a plain `sealed unsafe class : IDataVault`
     (`Core/Memory/GlobalDataVault.cs:442`), not a MonoBehaviour, held in a static. It survives scene
     loads by construction.

6. `BootstrapPhase.Player` (`:2418`) does **not** create `PlayerInventory`. Its node
   `BootstrapDependencyNode.PlayerInventoryManager` (`GameBootstrapper.cs:470`) resolves to
   `PlayerInventoryManager.EnsureRuntimeInstance()` at `GameBootstrapper.cs:5817` — a *different* type,
   `Core/PlayerInventoryManager.cs:14`, which is a service facade that later *finds* the concrete
   component (`PlayerInventoryManager.cs:321`, `_playerObject.TryGetComponent(out _inventory)`). Do not
   read the Player phase as the inventory's construction site; it is not.

7. `BootstrapPhase.SceneActivate` (`:2445`) → gameplay scene load,
   `LoadGameplaySceneFromBootstrapHandoffAsync` (`GameBootstrapper.cs:3306`),
   `"Step 0: Loading {sceneName}"` at `:3317`, `LoadProductionSceneAsync(sceneLoadPath, LoadSceneMode.Single)`
   at `:3324`. Default target is `02_HECTON_WORLD` (`GameBootstrapper.cs:120`, path `:124`).

8. World-scene Awake batch: `HectonPlayerSpawner.Awake()` — `HectonPlayerSpawner.cs:361` — falls through
   its four resolution routes and, at `HectonPlayerSpawner.cs:463-464`, calls
   `TryInstantiateProductionPlayerPrefab(...)` (declared `:1220`), which is a cold
   `Instantiate(productionPlayerPrefab, ...)` at **`HectonPlayerSpawner.cs:1229`**. `Instantiate` on an
   active prefab runs the new object's `Awake` synchronously before returning.

9. `PlayerInventory.Awake()` — `PlayerInventory.cs:1392` → `TryBindRuntimeStorageCold()` at `:1435`
   (declared `:2253`) → `CacheRegistryServicesCold()` at `:2258` → `_cachedDataVault = GlobalRegistry.DataVault`
   at `:1559` → **non-null** → `BindPlayerInventoryVaultBuffers(cellCount)` at `:2266` passes the guard at
   `:985`.

### Why the instantiation site is the decisive fact

`PlayerInventory`'s script GUID is `26d3e796d3be1184cacd974da491e310`
(`Assets/_Project/Scripts/PlayerInventory.cs.meta`). Byte-aware reachability — `Tools/SceneGuidReachability.py`,
which handles the four binary scenes where a plain `rg` for the hex string cannot match — reports it
**PRESENT in exactly one live file: `Assets/_Project/Prefabs/Player.prefab`** (text), and absent from
every scene. There is no `AddComponent<PlayerInventory>` anywhere in runtime code; the only hits are four
editor test fixtures (`Tests/Editor/PlayerToolManagerTickTests.cs:39`,
`PlayerToolManagerInventoryErrorEditTests.cs:27`, `ToolLoadoutProvisionerTests.cs:47` and `:66`).

`HectonPlayerSpawner`'s GUID `560e83b763132d2418e071332d17b172` is **PRESENT only in
`02_HECTON_WORLD.unity`** (binary — a text search returns nothing here, which is the trap noted in
`Tools/SceneGuidReachability.py:14-16`), plus three `Assets/_Recovery/` copies that are not reachability.
`HectonPlayerSpawner.cs:126-131` independently states a format-agnostic object-model census found exactly
one authored instance, on GameObject `PlayerSpawner`, and no code path that creates another.

So: **the inventory cannot exist before the world scene exists, and the world scene cannot load before
phase 6.** The vault lands in phase 1. That is the verdict.

### The one way this could still break, and why it does not

`LoadSceneMode.Single` at `GameBootstrapper.cs:3324` destroys `00_BOOTSTRAP`. If `GameBootstrapper` died
with it, its `OnDestroy` (`GameBootstrapper.cs:1598`) would call `DisposeSessionNativeStateForShutdown()`
at `:1615`, which calls `ShutdownGlobalDataVaultForBootstrapTeardown()` at `:1803` — unregistering and
disposing the vault *during the very scene load that spawns the player*. That would invert the order and
produce exactly the symptom under investigation.

It does not happen: the bootstrapper's GameObject is `DontDestroyOnLoad` from its own `Awake`
(`GameBootstrapper.cs:1540` → `7046`), so it is not destroyed by the single-mode load and `OnDestroy` does
not fire. The only other caller of the vault teardown is `ResetStaticState` at session start
(`:906`) and the editor assembly-reload hook (`:922-924`). **This is the tightest coupling in the chain
and the thing to re-check first if the verdict is ever contradicted by a runtime log.**

---

## THE ONE ROUTE WHERE THE ORDER *IS* INVERTED (and why it is harmless)

Press Play with `02_HECTON_WORLD` as the active scene, or otherwise enter a non-bootstrap scene first:

- the world scene's Awake batch runs immediately, so `HectonPlayerSpawner.Awake`
  (`HectonPlayerSpawner.cs:361`) cold-instantiates `Player.prefab` at `:1229`;
- nothing has registered the vault — the `BeforeSceneLoad` hook is a no-op
  (`GameBootstrapper.cs:1279-1283`) and `ResetStaticState` actively cleared the slot (`:906`);
- so `PlayerInventory.Awake` sees `GlobalRegistry.DataVault == null`, `BindPlayerInventoryVaultBuffers`
  returns false at `PlayerInventory.cs:985-986`, `TryBindRuntimeStorageCold` announces
  `STORAGE UNAVAILABLE - GlobalDataVault lane binding failed` (`:2268` → `:2311`), and `Awake` sets
  `enabled = false` (`:1437`);
- recovery cannot start either, because `PlayerToolManager.TryRegisterToTickManager` early-returns on
  `GlobalRegistry.Dispatcher == null` (`PlayerToolManager.cs:331-332`) and the dispatcher is a
  CoreServices-phase node that has not run.

That doomed instance is then thrown away. `HandleSceneLoadedGuard` → `TryRecoverEntryVector`
(`GameBootstrapper.cs:7250`, declared `:7253`) sees a non-bootstrap scene and schedules a
single-mode load of `00_BOOTSTRAP` (`:7297-7299`) — and the comment at `GameBootstrapper.cs:7271-7272`
states plainly that "every reload destroys the scene it arrived in, along with whatever that scene had
already spawned". Bootstrap then reruns from phase 0 and the surviving `PlayerInventory` is the one
created in phase 6 with the vault present.

Consequence for log reading, and this matters: **on the editor direct-entry route one
`STORAGE UNAVAILABLE` error is expected and refers to a corpse.** The announce latch
`_runtimeStorageFailureAnnounced` is per component and never reset (`PlayerInventory.cs:2296`,
documented `:2306-2309`), so it cannot be used to distinguish "the live inventory is dead" from "a
discarded pre-bootstrap instance was dead". Anyone diagnosing an empty inventory from that line alone
will mis-attribute it on this route.

---

## WHAT I COULD NOT DETERMINE BY READING

- **Runtime order is not proven.** I cannot run Unity (single editor lock held elsewhere). Everything
  above is the static call graph. Unity's Awake ordering *within* one scene load batch is unspecified
  without explicit script execution order, and the verdict deliberately does not depend on it — it
  depends on the phase-to-scene-load separation, which is sequential `await` code.
- **Whether `productionPlayerPrefab` is actually assigned on the scene's spawner instance.**
  `Player.prefab`'s GUID `1c4db7a430141e5408e01b6ce4ed19d7` is present in `02_HECTON_WORLD.unity`'s
  `FileIdentifier` external-reference table (byte-aware; corroborated in
  `Docs/AgentLogs/2026-07-29_REACHABILITY_CLAIM_RETEST.md:103-113`). That proves the scene file references
  the prefab. It does **not** prove which serialized field holds it, and
  `Tools/SceneGuidReachability.py:278-282` says exactly that. If the field is empty, the spawner logs
  "All four routes failed" (`HectonPlayerSpawner.cs:477-481`) and there is no inventory at all — a
  different bug with a different log line. Settling this needs a Unity readback of the scene object model.
- **Whether the world scene is loaded twice per session.** `HectonPlayerSpawner.cs:114-136` records a
  *measured* headless run in which "Production Player.prefab cold-instantiated" appeared twice against a
  single "Step 0: Loading 02_HECTON_WORLD", and concludes the scene is being loaded twice. Both loads
  would still be after MemoryPreWarm, so it does not change this verdict — but it does mean a surviving
  `PlayerInventory` may not be the first one constructed, and `SpawnPlayerAsync` may be holding a
  destroyed reference. That is a live defect on an adjacent axis.
- **Whether the vault's lane binds succeed once the vault is non-null.** A non-null vault is necessary,
  not sufficient. `BindPlayerInventoryVaultBuffers` chains ~54 `BindVaultLane` calls
  (`PlayerInventory.cs:989-1038`) and any single false collapses the whole conjunction; the arena, key
  table, or compaction fence can each refuse. Distinguishing "no vault" from "vault refused a lane"
  requires reading the `dataVault=` token that `AnnounceRuntimeStorageFailureOnce` prints at
  `PlayerInventory.cs:2324` — it reports `present` or `NULL`. **That single token in a real log answers
  the original question empirically, and no amount of further static reading can substitute for it.**

## Where to look instead

The vault-ordering hypothesis is eliminated for the production route. The surviving candidates for an
empty inventory, in the order they are cheapest to falsify:

1. Read the `dataVault=` token on the `STORAGE UNAVAILABLE` line (`PlayerInventory.cs:2324`). If it says
   `present`, the failure is a lane refusal, not ordering, and the arena/key-table budget is the suspect.
2. If no `STORAGE UNAVAILABLE` line exists at all, storage bound fine and the empty inventory is
   downstream — the starter grant, the loadout authoring, or `PlayerToolManager.IsToolAvailableInSlot`.
3. The duplicate-world-load defect at `HectonPlayerSpawner.cs:114-136`: a live, bound inventory on a
   player object that the bootstrap no longer points at presents identically to a dead inventory.
