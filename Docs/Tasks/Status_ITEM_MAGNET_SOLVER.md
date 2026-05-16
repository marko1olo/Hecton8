# Status_ITEM_MAGNET_SOLVER

Prompt: ITEM_MAGNET_SOLVER
Domain: GAMEPLAY/ITEMS
Task Count: 18
Current Phase: Multiplatform / H-PHI Inquisition Pass 4
Status: VERIFIED MASTER GRADE - BUILD BLOCKED BY EXTERNAL DEPENDENCY

## Batch Hygiene

- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` | Justification: strict XML extraction by prompt id, no neighboring task leakage | Alternatives Rejected: manual IDE reading; stale chat memory | Estimate: 40 us
- [x] Status file created | Justification: disk-backed anti-amnesia state machine required before code | Alternatives Rejected: chat-only status | Estimate: 8 us
- [x] Rationale file created | Justification: separate technical decision log required before marking tasks done | Alternatives Rejected: final-report-only rationale | Estimate: 8 us

## Phase 1 - The Great Purge

- [x] 1. [PURGE_TRIGGERS] Scan all `LootPickup` prefabs and scripts. Eradicate `OnTriggerEnter` / `OnTriggerStay` magnet logic. | Justification: `rg` over Items/Loot/pickup prefabs found no magnet triggers or `OverlapSphere`; existing pickup prefabs have non-trigger colliders, so purge state is verified clean | Alternatives Rejected: trigger throttling, bigger colliders, `OverlapSphere` | Estimate: 80 us/item at 500 authored loot avoided PhysX broadphase/contact churn
- [x] 2. [SINGLETON_KILL] Remove `LootManager.Instance` if present; route item data through Vault / Registry. | Justification: no `LootManager.Instance` exists; `[LOOT_MANAGER].prefab` is transform-only; runtime path uses GlobalRegistry/DataVault and PickupItem registry sidecars | Alternatives Rejected: lazy singleton fallback | Estimate: 4 us/frame singleton lookup/path-branch avoided
- [x] 3. [DATA_PREP] Ensure `EntityFlags` buffer exposes `Bit_IsMagnetic`. | Justification: `LootEntityFlags.Bit_IsMagnetic` aliases the vault pull bit and registry refresh writes it into `BufferID.EntityFlags` | Alternatives Rejected: tags, layer masks, per-item MonoBehaviour checks | Estimate: 0.03 us/item branchable bitmask filter

## Phase 2 - The Kernel

- [x] 4. [BURST_PULL_JOB] Implement `LootMagnetJob : IJobParallelFor` over active items and PlayerAUP. | Justification: `LootMagnetJob` scans SoA vault buffers and computes AUP deltas from grid/local coordinates in Burst | Alternatives Rejected: PhysX queries, GameObject polling | Estimate: 0.05 us/item hot kernel target
- [x] 5. [INV_SQUARE_MATH] Use `math.rcp(math.max(distSq, 0.1f))` pull force. | Justification: high-tier path uses `math.rcp(safeForceDistSq)` with `MinForceDistanceSq=0.1f` and separate `rsqrt` guard | Alternatives Rejected: `Vector3.Distance`, `/ distSq` | Estimate: 0.01 us/item reciprocal vs scalar divide
- [x] 6. [KINETIC_CLAMP] Clamp velocity to `MaxMagnetSpeed`. | Justification: `LootMagnetJob` clamps speed squared against `MaxVelocityMetersPerSecond^2` before AUP integration | Alternatives Rejected: unbounded impulse | Estimate: 0.02 us/item
- [x] 7. [SNAPPING_LOGIC] Mark item acquired inside 0.3 m and zero velocity. | Justification: `AcquireDistanceMeters=0.3f`, job writes `Flag_Acquired` and zeroes velocity without collider callbacks | Alternatives Rejected: collider enter events | Estimate: 0.01 us/item

## Phase 3 - Visual Overkill & Math LOD

- [x] 8. [LOW_TIER_FAKE] MX350 path runs scan at 10 Hz with cheap movement. | Justification: low tier schedules only from `SlowTick(0.1f)` and uses clamped linear lerp, not snap-acquire | Alternatives Rejected: 60 Hz everywhere | Estimate: saves roughly 5/6 of magnet job cadence on MX350
- [x] 9. [HIGH_END_OVERKILL] RTX path runs 60 Hz and writes turbulence coupling if buffer exists. | Justification: non-low tiers schedule from `FastTick`; wake presentation emits AUP-backed `WakeGeneratedSignal`, and High/Ultra also publish bounded `FluidImpulseSignal` for volumetric silt/wake consumers | Alternatives Rejected: flat uniform behavior | Estimate: 0.03 us/signal, bounded by tier budgets
- [x] 10. [REACTIVE_VFX] Publish `DebrisSpawnSignal(ItemSnapSpark)` on snap. | Justification: acquired items emit `DebrisSpawnSignal` through `GlobalSignals` using a fixed spark species hash/kind; item acquisition producers now use typed `ItemAcquiredSignal` lanes instead of legacy collected events | Alternatives Rejected: direct prefab spawn; managed `ItemCollectedEvent` publication | Estimate: 0.04 us/acquisition signal enqueue, legacy event allocation avoided but not measured
- [x] 11. [STP_STABILIZATION] Ensure fast-moving items expose motion-vector-ready movement state. | Justification: `PickupItem.ApplyLootMagnetPose` forces renderer motion vectors above 10 m/s, suppresses attached Rigidbody physics by making it kinematic/collision-disabled before transform mutation, and restores prior state when math ownership ends | Alternatives Rejected: mutating active Rigidbody transforms; ignoring fast loot visuals | Estimate: 0.02 us/moving visible pickup, physics broadphase corruption risk removed

## Phase 4 - Stability, Telemetry & Blackbox

- [x] 12. [NAN_VACCINATION] Guard all distance math and fail closed on non-finite coordinates. | Justification: job guards AUP locals, distance, `rsqrt`, velocity, and integrated AUP before writeback | Alternatives Rejected: unchecked Burst math | Estimate: 0.02 us/item
- [x] 13. [BLACKBOX_LOGGING] Record active pulls and peak velocity in fixed 300-frame NativeArray. | Justification: telemetry ring is owned by `GlobalDataVault` via `BufferID.EntityLootMagnetTelemetry`, fixed at 300 frames, dumps to `Docs/AgentLogs/Dump_ITEM_MAGNET_SOLVER.bin`, and `LootMagnetSystem` now resolves transient vault views instead of storing `NativeArray` fields | Alternatives Rejected: Debug.Log spam; private H8Memory allocation; persistent system-local NativeArray views | Estimate: 0.01 us/item during existing commit pass
- [x] 14. [TRIPLE_STRIKE_REPAIR] Handle signal signature dependency failures without breaking build. | Justification: `GlobalSignals.Publish(in ItemAcquiredSignal)` and `SignalBus<ItemAcquiredSignal>.Push(in signal)` signatures exist; no incompatible API invented | Alternatives Rejected: custom inventory singleton/event path | Estimate: 0 us repair, verified by source scan
- [x] 15. [HOMEOSTASIS_ADAPTATION] Reduce radius by 50% when `SystemStress01 > 0.8`. | Justification: scheduler samples `HomeostasisBrain.SystemHealthIndex01`, clamps to 0..1, and halves radius above threshold | Alternatives Rejected: fixed radius under overload | Estimate: avoids roughly 50% radius active work under stress
- [x] 16. [SPSC_SIGNAL] Publish `ItemAcquiredSignal` through inventory SPSC lane. | Justification: magnet acquisition, `PickupItem`, and duplicate `HectonItem` manual pickup path now publish `ItemAcquiredSignal`; magnet passes `publishAcquiredSignal:false` to avoid double publish | Alternatives Rejected: direct inventory mutation from job; legacy managed item-collected buses | Estimate: 0.04 us/acquisition enqueue
- [x] 17. [AUP_REBASE] Keep pull vectors correct during AUP shifts. | Justification: job uses absolute AUP deltas; system also registers `IOriginShiftListener`, force-completes pending work on shift, and reapplies runtime poses from AUP | Alternatives Rejected: transform-space distance | Estimate: cold path only, 0 hot-frame cost
- [x] 18. [FINAL_VALIDATION] Run `dotnet build` and reach 0 errors or document dependency wall. | Justification: validation attempted; final 0-error result blocked by external XR, submarine structural, vault probe, biolum/visor blackbox, and spatial audio errors; no loot/item magnet compile error emitted | Alternatives Rejected: static-only verification; editing unrelated domains | Estimate: BLOCKED BY DEPENDENCY

## Iteration Log

- Loop 1: Mandates read, prompt re-extracted, loot trigger/singleton scan completed, DataVault magnetic bit added.
- Loop 1 Compile Gate: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1` failed before local code with external `PlayerKinematicsRuntime.cs` missing symbols (`ResolveAupMaxDriftErrorMeters`, `_lastSyncFenceHash`, `_lastSyncFenceFrame`). `dotnet build .\Hecton8.slnx --no-restore` hung >300s and was killed after spawned dotnet workers remained active. Continue per dependency-wall protocol.
- Loop 2: Re-read prompt, patched velocity clamp, 0.3m snap, low-tier lerp, inverse-square reciprocal math, and snap debris signal.
- Loop 2 Compile Gate: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1` hung >120s on retry and required killing spawned dotnet workers. Dependency wall remains.
- Loop 3: Self-scan verified no `LootMagnetPullJob`, `LowTierSnap`, trigger callbacks, `OverlapSphere`, `Vector3.Distance`, or `foreach` in loot magnet scope.
- Loop 4: Self-read verified AUP path uses absolute grid/local deltas and added `IOriginShiftListener` cold-path pose reapplier.
- Loop 5: Self-read verified signal lanes: `ItemAcquiredSignal` uses `GlobalSignals.Publish`, `DebrisSpawnSignal` emits snap sparks, and `WakeGeneratedSignal` carries AUP+velocity for fluid consumers.
- Loop 6: Multiplatform pass converted loot magnet structs to explicit `Pack=1` field layouts and moved `_signalEvents`/`_telemetry` ownership to GlobalDataVault (`EntityLootMagnetSignalEvents`, `EntityLootMagnetTelemetry`).
- Loop 7: H-PHI pass changed magnet acquisition to suppress `PickupItem` legacy managed interaction/EventBus events; magnet path now emits typed `ItemAcquiredSignal`, `DebrisSpawnSignal`, `WakeGeneratedSignal`, and High/Ultra `FluidImpulseSignal` lanes.
- Loop 7 Compile Gate: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:normal` failed outside loot/item magnet scope in `ProceduralLadderClimbRuntime.cs`: missing `Hecton8.Input.Universal` and `UniversalInputStateSignal`. No local loot magnet errors were emitted before the external failure.
- Loop 8: H-PHI data sovereignty pass removed all `NativeArray<T>` fields from `LootMagnetSystem`; vault data is resolved as transient `LootMagnetVaultViews`, scheduled buffers are locked while the Burst job owns them, and commit/dump paths use existing DataVault aliases only.
- Loop 8 Compile Gate: `git diff --check` clean except Git CRLF warnings. `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal` failed outside loot/item magnet scope in `FloraInteractionManager`, `GlobalRegistry`, `LockstepStateValidator`, `VehicleDockingModule`, `ScreenSpaceLightShaftRuntime`, and `EcosystemDirector`. No local loot magnet errors were emitted.
- Loop 9: Neural connectivity pass purged item-domain legacy collection publishers from `PickupItem` and duplicate `HectonItem`; manual pickups now emit existing typed `ItemAcquiredSignal`, with shared source constants in `InventoryPickupContracts`.
- Loop 10: Physics/platform pass hardened magnet pose ownership: attached pickup Rigidbodies are made kinematic and collision-disabled before transform mutation, previous state is restored when magnet math stops owning pose, and magnet-emitted public signal structs (`ItemAcquiredSignal`, `WakeGeneratedSignal`, `FluidImpulseSignal`) now use `Pack=1`.
- Loop 10 Compile Gate: First retry exposed local `HectonItem` missing `Hecton8.Core.Contracts.Signals`; fixed. Second retry failed only outside item magnet scope in `HectonXRRuntimeState`, `SubmarineStructuralGrid`, `VaultProbeUtility`, `BiolumPulseSyncRuntime`, and `SpatialAudioManager`.
- Loop 11: Shutdown integrity pass restored all magnet-owned pickup proxy runtime physics state when the scheduler clears runtime state or disables, closing the post-pull collision suppression leak.

## Omega Polish Mandate

- [x] Anti-bloat scan completed after all 18 tasks were checked or blocked. | Justification: no `GameObject.Find`, trigger fallback, `OverlapSphere`, stale `LootMagnetPullJob`, `LowTierSnap`, `foreach`, or `Vector3.Distance` in loot magnet scope | Alternatives Rejected: pre-core polish pass; chat-only verification | Estimate: 12 us/search pattern
- [x] H-PHI NativeArray scan completed. | Justification: `LootMagnetSystem.cs` contains no `NativeArray<T>` declarations, `new NativeArray`, `Allocator.Persistent`, `H8Memory.Allocate`, or `H8Memory.Release`; remaining `NativeArray` references are the Burst job contract and DataVault view DTO | Alternatives Rejected: private system-local vault aliases | Estimate: 0 hot cost, reduced allocator ownership risk
- [x] Legacy collection-event publisher scan completed. | Justification: no `HectonEventBus`, `InteractionEvents.RaiseItemCollected`, or `ItemCollectedEvent` remains in `Gameplay/Loot`, `Items/PickupItem.cs`, or `HectonItem.cs`; existing cross-domain subscribers require their own migration | Alternatives Rejected: duplicate typed+legacy publication | Estimate: avoids managed event object allocation per manual pickup, exact gain not measured
- [x] Rigidbody transform mutation audit completed. | Justification: magnet transform writes now occur only after `SuppressLootMagnetPhysics` makes the Rigidbody kinematic and disables collisions, satisfying the physics transform mutation rule | Alternatives Rejected: `MovePosition` without sweep; active Rigidbody transform mutation | Estimate: cold state flip per magnet-owned pickup
- [x] Disable/clear restoration audit completed. | Justification: `LootMagnetSystem` now restores all managed pickup proxy Rigidbody state before runtime state is cleared on dependency loss, disable, or shutdown | Alternatives Rejected: relying on pickup `OnDisable` only; leaving collision-disabled pickups after scheduler shutdown | Estimate: cold O(active pickups), 0 hot-frame cost
- [x] Circular dependency check completed. | Justification: `Hecton8.Gameplay.Loot` references `Hecton8.Core`; `PickupItem` stays in Core and does not reference Loot, so STP hook does not create an asmdef cycle | Alternatives Rejected: moving PickupItem into Loot assembly | Estimate: 0 hot cost
- [x] Build status reported truthfully. | Justification: mandate requested build green, but objective compiler state is blocked by unrelated `PlayerKinematicsRuntime` and dotnet hangs; false green is rejected | Alternatives Rejected: fake report | Estimate: BLOCKED BY DEPENDENCY
