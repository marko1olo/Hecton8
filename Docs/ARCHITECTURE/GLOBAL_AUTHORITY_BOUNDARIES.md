# Global Authority Boundaries

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R28 Root/Architecture Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`. R27 remains the latest source-counter/index snapshot only until a newer counter pass reruns it.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

This document defines the boundary between `GlobalRegistry`, `SignalBus<T>`,
`GlobalSignals`, `HectonEventBus`, and `GlobalDataVault`.

Evidence class: `STATIC_SOURCE` + `STATIC_DOC`. This is not compile proof, Unity
Console proof, profiler proof, GC proof, Play Mode proof, player-build proof, or
scene wiring proof.

Operating model: `GLOBAL_AUTHORITY_OPERATING_MODEL.md`.
Setup playbook: `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`.
Route-card template: `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`.
Review checklist: `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.
Migration ledger: `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.

## Verdict

The global authority direction is correct for HECTON-8, but the current surface is
already in the danger zone.

It is not a terminal architecture failure yet. It becomes one if new systems treat
these globals as a universal control plane, universal event pipe, or universal
mutable heap.

## Authority Matrix

| Surface | Correct Use | Forbidden Use |
|---|---|---|
| `GlobalRegistry` | Cold bootstrap, stable service discovery, dependency injection cache, dense lifecycle buckets | Live query bus, event transport, mutable state store, concrete leaf-domain catalog, per-frame settings polling |
| `SignalBus<T>` | First-party hot/cross-domain broadcasts with unmanaged payloads, bounded snapshots, deterministic overflow, telemetry | Request/response queries, one-private-caller events, unbounded global traffic, Unity object payloads |
| `GlobalSignals` direct queues | Legacy bridge and owned low-level queue infrastructure during migration | New gameplay traffic target, mixed catch-all lane, undocumented direct queue expansion |
| `HectonEventBus` | Mod/API boundary, cold managed isolation, external extension events, watchdog-protected callbacks | First-party hot gameplay bus, Burst/job traffic, per-frame UI/audio/physics/gameplay state propagation |
| `GlobalDataVault` / `IDataVault` | Cross-domain persistent native buffers, generation-checked handles, shared snapshots, relocation/defrag ownership, crash-dump state | Global mutable heap, local scratch replacement, unmanaged dumping ground without owner/SystemID/BufferID/generation/dispose proof |

## Current Static Reality

These counters are static text/source evidence only:

| Surface | Current Observation |
|---|---:|
| Raw `GlobalRegistry.` source lines under `Assets/_Project/Scripts` | 5871 |
| Top raw `GlobalRegistry.` files | `GameBootstrapper.cs` 161, `CrashTelemetryBuffer.cs` 49, `HectonFloatingOrigin.cs` 44, `FaunaBrain.cs` 41, `SaveManager.cs` 38, `HectonPlayerMovement.cs` 37 |
| Raw bus publish/subscribe hits for `HectonEventBus`, `GlobalSignals.Publish`, `SignalBus<T>.Push/TryPush` | 606 |
| `GlobalSignals.cs` raw `NativeQueue<...>` references | 115 |
| `GlobalSignals.cs` raw `SignalBus<T>.Configure/EnsureInitialized` hits | 267 |
| Raw native collection type references under `Assets/_Project/Scripts` | 12090 |
| Latest H-Phi artifact counters from `Docs/Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md` | `GlobalRegistrySurface=5552`, `SignalBusPush=495`, `EventPublish=25`, `DataVaultRefs=2359`, `NativeArrayRefs=9206`, `OwnerBlockedNativeArrayRefs=5108` |

R27 read-only grep recapture for orientation only: `GlobalRegistry.` line hits `5871`, bus-publish line hits `606`, `GlobalSignals.cs` `NativeQueue<...>` line hits `115`, `SignalBus<T>.Configure/EnsureInitialized` hits `267`, direct `CreateQueue(...)` slots `73`, and typed `SignalBus<T>.EnsureInitialized()` lanes `133`. These are not gates until the exact scan command is locked and rerun.

Interpretation:

- `FindObjectCalls=0` in the latest H-Phi artifact is a positive static-source
  signal only. It is not runtime integration or scene-wiring proof.
- The immediate threat is global centralization: `GlobalRegistry` breadth,
  mixed bus models, and incomplete DataVault sovereignty.
- H-Phi can improve while architecture still gets worse if teams add references
  to global systems without reducing real ownership ambiguity.

## Are We Already Globally Failing?

Static HFI evidence does not show terminal global-authority failure. Core pieces
have the intended contract shape: typed service slots, `RegistryPhase`, typed
`SignalBus<T>`, unmanaged payload validation, snapshot reads, `IDataVault`
handles, generation checks, and DataVault release/defrag contracts. This remains
pending Unity/profiler/runtime proof.

Yes: the project is already drifting toward global-object failure:

- `GlobalRegistry` is broad enough to become a concrete domain catalog.
- `GlobalSignals` has both generic `SignalBus<T>` lanes and legacy direct queue
  surfaces.
- `HectonEventBus` exists next to first-party buses and can be misused by future
  agents as a convenient gameplay bus.
- DataVault migration is visibly incomplete; raw `NativeArray`/native collection
  surface still dominates the data-sovereignty picture.

The current state is a controlled warning, not a collapse. Treat it as a freeze
line: no new global surface without deleting or migrating old debt.

## No-Failure Rules

1. Define the communication shape before coding: immediate query, broadcast,
   persistent shared memory, mod event, telemetry, or debug.
2. Use `GlobalRegistry` only for service ownership and cold dependency injection.
   Hot paths consume cached fields or cached snapshots.
3. Use `SignalBus<T>` for first-party broadcasts only when there are multiple
   consumers, phase crossing, job/Burst producers, or dirty-state fan-out.
4. Use direct owner interfaces for private request/response. Do not invent a
   signal lane for one caller.
5. Use `HectonEventBus` only at the mod/API/cold boundary. New first-party
   gameplay traffic through `HectonEventBus` is rejected.
6. Use `GlobalDataVault` only for cross-domain, persistent, job-visible, or
   relocation-relevant native state. Local scratch remains local unless a real
   ownership boundary exists.
7. Every new signal lane needs owner, producer phase, consumer phase, max events,
   overflow policy, retention policy, telemetry, payload layout, and duplicate
   name scan.
8. Every new DataVault buffer needs `BufferID`, `SystemID`, length/capacity,
   generation handling, disposal/release behavior, and stale-handle behavior.
9. Every new global route needs a failure mode: drop, coalesce, fail-fast,
   fallback snapshot, or disable-with-telemetry.
10. H-Phi improvement is not acceptance. Runtime acceptance still requires Unity
    Console, Play Mode, profiler, GC, Memory Profiler, player build, and scene
    proof where applicable.
11. New global authority routes need a route card from
    `GLOBAL_AUTHORITY_OPERATING_MODEL.md` /
    `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md` /
    `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` /
    `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` before implementation or review.

## Merge Blockers

Block new work when any of these appear:

- New `GlobalRegistry` slot for an absent service.
- New hot-path `GlobalRegistry.Get<T>()` or `GlobalRegistry.*` poll.
- New first-party `HectonEventBus.Publish` / managed subscribe path for gameplay.
- New `GlobalSignals.Publish` call when a typed `SignalBus<T>` lane should own
  the traffic.
- New direct `NativeArray<T>` ownership outside approved memory owners without
  DataVault/H8Memory migration rationale.
- New catch-all signal such as `RuntimeSignal`, `GameplaySignal`, or a large
  enum/switch payload that hides unrelated traffic in one lane.
- Signal payload containing Unity objects, strings, managed arrays, delegates, or
  undocumented layout-sensitive fields.
- DataVault buffer without owner and generation proof.

## Migration Order

Do not refactor everything at once. Use this order:

1. Freeze net growth of `GlobalRegistry` surface. Additions require a deletion,
   migration, or written exception.
2. Classify current `HectonEventBus.Publish` calls as `MOD_API_COLD`,
   `FIRST_PARTY_COLD`, or `FIRST_PARTY_HOT`. Migrate hot first-party calls to
   `SignalBus<T>`.
3. Stop adding direct `GlobalSignals.Publish` call sites. New work uses typed
   `SignalBus<T>` unless it is explicitly legacy-bridge work.
4. Inventory legacy direct `NativeQueue` surfaces in `GlobalSignals.cs`; keep
   only owned bridge lanes until a typed lane replaces each one.
5. Attack DataVault migration by top native-collection owners first:
   `HectonVoxelEngine.cs`, `PlayerInventory.cs`,
   `World/DestructibleOrganicManager.cs`, `Power/LogisticsNetworkGraph.cs`,
   `World/HectonMapMagicVegetationBridge.cs`.
6. Convert live registry polls in player, physics, UI, AI, logistics, and render
   loops to dependency-injected cached interfaces or dirty snapshot signals.
7. Only after static debt decreases, rerun H-Phi and DataVault sovereignty gates.
   Do not claim runtime readiness from those gates.

## Runtime Proof Required

Before reporting this global architecture as healthy:

- Unity Console clean import for the current workspace.
- Play Mode smoke through bootstrap, world scene, and shutdown.
- Profiler capture showing hot-path global surfaces do not create frame spikes.
- GCMonitor or Profiler proof of `0 B/frame` on hot bus/registry/vault paths.
- Signal overflow/drop/coalesce telemetry under stress.
- DataVault stale-handle, generation mismatch, relocation, release, and scene
  unload tests.
- NativeMemorySentinel flat persistent memory over idle soak.

Until those artifacts exist, status remains `PENDING VERIFICATION`.
