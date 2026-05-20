# Global Authority Migration Ledger

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary
This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
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
| Raw `GlobalRegistry.` source lines under `Assets/_Project` | 6199 |
| Raw `HectonEventBus` / `GlobalSignals.Publish` / `SignalBus<T>.Push/TryPush` plus direct publish/subscribe token hits under `Assets/_Project` | 575 |
| Raw `GlobalSignals.cs` `NativeQueue<...>` references | 116 |
| Raw `GlobalSignals.cs` `SignalBus<T>.Configure/EnsureInitialized` hits | 271 |
| Raw native collection line hits under `Assets/_Project` using `NativeArray|NativeList|NativeHashMap|NativeQueue` | 18045 |
| Historical HFI artifact `GlobalRegistrySurface` | 5552 |
| Historical HFI artifact `SignalBusPush` | 495 |
| Historical HFI artifact `EventPublish` | 25 |
| Historical HFI artifact `DataVaultRefs` | 2359 |
| Historical HFI artifact `NativeArrayRefs` | 9206 |
| Historical HFI artifact `OwnerBlockedNativeArrayRefs` | 5108 |

2026-05-20 DOC_GLOBAL R45/R46 source-scale orientation supersedes the R43/R42 static grep tuples for current planning: `GlobalRegistry.` line hits `6199`, bus-publish/subscribe line hits `575`, `GlobalSignals.cs` `NativeQueue<...>` refs `116`, native-collection line hits `18045`, `SignalBus<T>.Configure/EnsureInitialized` hits `271`, direct `CreateQueue(...)` slots `73`, typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs` `135`, and broader script-level typed-lane matches `1345`. These are static-source orientation values, not gates, until the exact scan command is locked and rerun before acceptance. R43/R42 tuples are historical orientation only.

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

Unified read-only global authority gate:

```powershell
python Tools/GlobalAuthorityGate.py
```

Prioritized architecture hotlist:

```powershell
python Tools/ArchitectureRiskHotlistAudit.py
```

Current domain burn-down plan:

```text
Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BURN_DOWN_PLAN.md
```

HFI R26 hotlist schema `hecton8.architecture_risk_hotlist.v2` adds domain
pressure. Current first burn-down slices are `Root`, `World`, `Core`,
`Gameplay`, `Construction`, `UI`, `Audio`, `Atmosphere`, and `Power`, in that
order unless a route blocker for the first-20-minutes slice demands otherwise.
Domain pressure is a review-order input only; it does not authorize broad file
moves, baseline resets, asmdef rewrites, or runtime readiness claims.

R26 note: generic `GlobalRegistry.Get/TryGet<T>` hard-gate hits were reduced
back to `0` by replacing cold Core bridge lookups with typed registry slots.
DataVault candidate no-regression still fails on forbidden field declaration
growth (`5125 -> 5130`), so the Vault baseline remains unapproved.

H-Phi static audit:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json
```

DataVault sovereignty gate:

```powershell
python Tools/DataVaultSovereigntyAudit.py --fail-on-regression
```

Current HFI candidate baseline, not official approval:

```powershell
python Tools/DataVaultSovereigntyAudit.py --baseline Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json --report Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md --write-baseline
```

The candidate proves current counters only. Do not replace the official active
baseline unless the integrator explicitly accepts the debt or schedules a
burn-down.

BufferID sovereignty gate:

```powershell
python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates
```

Use `--fail-on-local-casts` only after the current migration debt is burned down
or an explicit owner/range ledger exists for every retained local numeric cast.

Assembly dependency / compile-wall gate:

```powershell
python Tools/AssemblyDependencyAudit.py
python Tools/AssemblyDependencyAudit.py --fail-on-cycles
```

Use `--fail-on-core-concrete-sibling-refs` for new Core sibling runtime refs and
for planned burn-down slices. Do not remove existing asmdef references without
source call-site classification, `.Contracts` or owner-interface route, and
Unity import proof.

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
- Central `BufferID` values have no numeric duplicates, and retained local
  numeric `(BufferID)N` casts have owner/range/lifetime proof.
- `Tools/GlobalAuthorityGate.py` passes its hard checks, and any warnings that
  touch changed files are either reduced or linked to route-carded migration
  work.
- `Tools/ArchitectureRiskHotlistAudit.py` is used to order broad owner-domain
  burn-down work; high-score files are reviewed, not mass-refactored blindly.
  Domain pressure is tracked through
  `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BURN_DOWN_PLAN.md`.
- `Tools/AssemblyDependencyAudit.py --fail-on-cycles` passes, and Core concrete
  sibling runtime references do not grow.
- H-Phi static scores improve without violating `GLOBAL_AUTHORITY_BOUNDARIES.md`.
- Runtime proof exists: Unity Console, Play Mode, profiler, GC, Memory Profiler,
  signal overflow telemetry, DataVault stale-handle tests, and scene unload soak.

Until then, status remains `PENDING VERIFICATION`.


