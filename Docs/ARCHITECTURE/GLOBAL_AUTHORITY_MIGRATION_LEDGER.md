# Global Authority Migration Ledger

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

Evidence class: `STATIC_SOURCE` + `STATIC_DOC`. This ledger is not compile,
Unity Console, Play Mode, profiler, GC, Memory Profiler, player build, or scene
wiring proof.

Authority parent: `GLOBAL_AUTHORITY_BOUNDARIES.md`.
Operating model: `GLOBAL_AUTHORITY_OPERATING_MODEL.md`.
Setup playbook: `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`.
Route-card template: `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`.
Review checklist: `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.

## Purpose

This ledger turns the global authority warning into migration work. It exists to
prevent agents from improving H-Phi by adding more global references while real
ownership remains ambiguous.

The goal is not "remove all globals". The goal is bounded global authority:

- cold registry spine
- typed first-party signal lanes
- mod/API event isolation
- DataVault ownership only where shared native state needs it
- dispatcher-owned phase and job barriers

## Current Static Snapshot

Use these values only as the 2026-05-19 static snapshot from the HFI audit and
follow-up grep. Rerun before using them as gates.

| Surface | Snapshot |
|---|---:|
| Raw `GlobalRegistry.` source lines under `Assets/_Project/Scripts` | 5871 |
| Raw `HectonEventBus` / `GlobalSignals.Publish` / `SignalBus<T>.Push/TryPush` hits | 606 |
| Raw `GlobalSignals.cs` `NativeQueue<...>` references | 115 |
| Raw `GlobalSignals.cs` `SignalBus<T>.Configure/EnsureInitialized` hits | 267 |
| Raw native collection type references under `Assets/_Project/Scripts` | 12090 |
| Latest artifact `GlobalRegistrySurface` | 5552 |
| Latest artifact `SignalBusPush` | 495 |
| Latest artifact `EventPublish` | 25 |
| Latest artifact `DataVaultRefs` | 2359 |
| Latest artifact `NativeArrayRefs` | 9206 |
| Latest artifact `OwnerBlockedNativeArrayRefs` | 5108 |

R27 read-only grep recapture for orientation only: `GlobalRegistry.` line hits
`5871`, bus-publish line hits `606`, `GlobalSignals.cs` `NativeQueue<...>` line
hits `115`, `SignalBus<T>.Configure/EnsureInitialized` hits `267`, direct
`CreateQueue(...)` slots `73`, and typed `SignalBus<T>.EnsureInitialized()`
lanes `133`. These are not gates until the exact scan command is locked and
rerun.

Interpretation: static HFI evidence does not prove global terminal failure, but
global-authority growth is a controlled migration risk.

## Migration Streams

| Stream | Owner Shape | First Action | Stop Condition |
|---|---|---|---|
| Registry surface | bootstrap/interface owner | classify top hot-path registry readers | no hot-path live registry polling |
| First-party signal lanes | `SignalBus<T>` owner + lane metadata | classify hot `HectonEventBus` and `GlobalSignals.Publish` sites | no first-party hot managed bus traffic |
| Legacy direct queues | explicit bridge owner | inventory `GlobalSignals.cs` direct queues | every retained direct queue has owner/capacity/overflow/telemetry |
| DataVault sovereignty | BufferID/SystemID owner | migrate top owner-blocked native collection files by domain | owner-blocked refs decrease with no lifecycle regressions |
| Dispatcher barriers | dispatcher-owned phase | map `.Complete()` and queue drains to swap windows | no undocumented mid-frame completion |
| Proof gates | QA owner | attach compile/runtime/profiler artifacts | claims no longer exceed evidence class |

Every new migration slice needs a route card from
`GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` when it adds or changes any global
route.

## Top Static Targets

These are review queues, not automatic refactor orders.

### Registry Surface

Top raw `GlobalRegistry.` files from the 2026-05-19 grep:

| File | Raw Hits | Review Question |
|---|---:|---|
| `Bootstrap/GameBootstrapper.cs` | 161 | expected cold bootstrap owner; protect from becoming leaf-domain logic |
| `CrashTelemetryBuffer.cs` | 49 | ensure telemetry has cached services and no hot registry poll |
| `HectonFloatingOrigin.cs` | 44 | verify AUP/shift paths use cached service snapshots |
| `Fauna/FaunaBrain.cs` | 41 | verify AI solve does not poll registry every tick |
| `SaveManager.cs` | 38 | save cold path acceptable; verify no frame-lane registry query |
| `HectonPlayerMovement.cs` | 37 | high risk; movement must cache providers |
| `SpatialAudioManager.cs` | 37 | audio hot path must cache providers |
| `Core/SceneRuntimeService.cs` | 37 | likely transition/cold; verify scene activation gates |
| `GlobalPhysicsStateManager.cs` | 37 | physics hot path must cache providers |
| `HectonUnderwaterVisuals.cs` | 35 | render/presentation hot path must cache snapshots |
| `HectonFluidEngine.cs` | 35 | fluid/physics hot path must cache providers |

### DataVault / Native Ownership

Top raw native collection type-reference files from the 2026-05-19 grep:

| File | Raw Hits | Review Question |
|---|---:|---|
| `HectonVoxelEngine.cs` | 286 | which buffers cross domains/jobs and require Vault handles |
| `PlayerInventory.cs` | 199 | which inventory state is local versus shared snapshot |
| `World/DestructibleOrganicManager.cs` | 189 | persistent damage/organic state owner boundaries |
| `Power/LogisticsNetworkGraph.cs` | 180 | graph buffers, job handles, and persistence ownership |
| `World/HectonMapMagicVegetationBridge.cs` | 164 | third-party bridge quarantine and shared state boundaries |
| `SaveBinaryStorage.cs` | 162 | persistence buffers; avoid globalizing transient I/O scratch |
| `HectonFluidEngine.cs` | 155 | simulation buffers versus presentation outputs |
| `Audio/PlayerCriticalProceduralAudioRenderer.cs` | 146 | audio job buffers and completion windows |
| `Economy/TradeMarauderRuntime.cs` | 145 | local economy scratch versus persistent shared state |
| `Inventory/Shinobu19EconomyLedger.cs` | 144 | ledger ownership and binary layout proof |

## Decision Checklist

Before adding a global route, answer this in the owning rationale/log:

| Question | Required Answer |
|---|---|
| Is this one owner / one caller request-response? | use owner interface, not signal |
| Is this one owner / many listeners? | use typed `SignalBus<T>` |
| Is this mod-facing or external API? | use `HectonEventBus`, cold/mod boundary only |
| Does the data cross domains, jobs, scenes, save, or crash dump? | use `GlobalDataVault`/`IDataVault` with BufferID/SystemID |
| Is it local scratch or single-owner temporary data? | keep local, do not globalize |
| Is the dependency stable service access? | inject/cache from `GlobalRegistry` during bootstrap |
| Can the value change live? | publish ready/changed/shutdown signal and refresh cached field |
| What happens under overflow/failure? | drop/coalesce/fail-fast/fallback/telemetry |

## Required Audit Commands

Registry surface:

```powershell
rg -n "GlobalRegistry\\." Assets/_Project/Scripts -g "*.cs"
```

First-party/mod bus split:

```powershell
rg -n "HectonEventBus\\.(Publish|Subscribe)|GlobalSignals\\.Publish|SignalBus<[^>]+>\\.(Push|TryPush)" Assets/_Project/Scripts -g "*.cs"
```

Native collection ownership:

```powershell
rg -n "\\bNative(Array|List|HashMap|ParallelHashMap|Queue)<" Assets/_Project/Scripts -g "*.cs"
```

H-Phi static audit:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json
```

DataVault sovereignty gate:

```powershell
python Tools/DataVaultSovereigntyAudit.py --fail-on-regression
```

Do not run compile/profiler commands in a busy multi-agent machine without the
current compile-owner/CPU checks required by `AGENTS.md` and `QUALITY_GATES.md`.

## Completion Criteria

This migration is not complete until all are true:

- `GlobalRegistry` additions are flat or decreasing, and hot-path registry polls
  are gone or explicitly justified with cadence and profiler proof.
- New first-party hot traffic uses typed `SignalBus<T>`, not `HectonEventBus`.
- Legacy `GlobalSignals.Publish` growth is stopped; retained direct queues are
  owned bridge lanes with telemetry.
- Owner-blocked NativeArray/native collection debt decreases by domain without
  moving local scratch into a fake global heap.
- H-Phi static scores improve without violating `GLOBAL_AUTHORITY_BOUNDARIES.md`.
- Runtime proof exists: Unity Console, Play Mode, profiler, GC, Memory Profiler,
  signal overflow telemetry, DataVault stale-handle tests, and scene unload soak.

Until then, status remains `PENDING VERIFICATION`.
