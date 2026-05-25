# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md
Rule: historical snapshot only; not active doctrine.

# Global Authority Migration Ledger

Date: 2026-05-24
Status: PENDING VERIFICATION

Evidence class: `STATIC_SOURCE` + `STATIC_DOC`; CLI_COMPILE only inside dated
entries with artifact paths. This ledger is not Unity Console, Play Mode,
profiler, GC, Memory Profiler, player build, or scene wiring proof.

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

| Raw `GlobalRegistry.` source lines under `Assets/_Project` | 6179 |

| Raw `HectonEventBus` / `GlobalSignals.Publish` / `SignalBus<T>.Push/TryPush` plus direct publish/subscribe token hits under `Assets/_Project` | 890 |

| Raw `GlobalSignals.cs` `NativeQueue<...>` references | 115 |

| Raw `GlobalSignals.cs` `SignalBus<T>.Configure/EnsureInitialized` hits | 271 |

| Raw native collection line hits under `Assets/_Project` using `NativeArray|NativeList|NativeHashMap|NativeQueue` | 23375 |

| Historical HFI artifact `GlobalRegistrySurface` | 5552 |

| Historical HFI artifact `SignalBusPush` | 495 |

| Historical HFI artifact `EventPublish` | 25 |

| Historical HFI artifact `DataVaultRefs` | 2359 |

| Historical HFI artifact `NativeArrayRefs` | 9206 |

| Historical HFI artifact `OwnerBlockedNativeArrayRefs` | 5108 |

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

## 2026-05-24 EXTERNAL_CODEX Owner-Cache Burn-Down

Source-only cleanup loops55-73 removed more hot global tails without adding new routes. Current scope: binary scalability event/tier tails outside the Core bridge, beacon/construction action fanout, BeaconNetwork `GetOrCreate` registry fallback, SDF/Terrain probe `?? GlobalRegistry` fallbacks, `ConstructionManager` ObjectPool/PlayerInventory/DataVault action-path reads, callback/physics/audio fallback tails, structural integrity DataVault init fallback, selected organic/hull/voxel DataVault owner-cache tails, runtime `?? GlobalRegistry` pattern cleanup, `GameBootstrapper` warning cleanup, dead armor torture job removal, `ScannerDataMiningRouter` DataVault instance fallback removal with hot-swap rebind, `HectonFloatingOrigin` AUP tuner owner-cache fallback removal, combat DataVault fallback removal in ballistics/status/armor init, and `AsynchronousTelemetryExporter` analytics DataVault fallback removal with worker-safe rebind. Last zero-warning proof remains `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log`; newer builds are pass-with-warnings and guarded rebuild is blocked by CPU/compiler guard.

## 2026-05-20 ARCH_AUDIT Additions

These are governance targets from the global-systems audit. They are not
runtime proof and must be rechecked against source before code edits:

- Purge read-looking accessors that publish, sync, allocate/grow, complete jobs,

  mutate global state, or search the scene. Player/runtime-context getters are

  priority review targets.

- Replace domain runtime `GlobalDataVault.TryGetLatestCreated()` fallbacks with

  injected `IDataVault`, cached generation handles, or fail-closed behavior.

- Audit Burst/Jobs paths for tiny same-frame schedule/readback loops and

  completions outside dispatcher-owned swap/completion windows.

- Keep `GlobalSignals.Publish` growth frozen unless a route card proves retained

  bridge ownership and a migration stop condition.

- Treat Data Monolith as payload-blocked until
  `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists and has
  import/bake/boot evidence.

## 2026-05-23 EXTERNAL_CODEX Burn-Down Slice

Evidence class: STATIC_SOURCE / CLI_COMPILE.

- Replaced selected late/lifecycle registry reads and scene-search fallbacks with cached owner interfaces plus registry hot-swap cache refresh.
- Affected review surface included UI compass/PDA/terminal/audio feedback/loading/preview/particles, vocal-bank listener bootstrap, water/ocean/atmosphere/celestial/weather/voxel/player-tool/player-expression/pickup/drone/culling, spatial hash, wreck BRG, vegetation flow, resource distribution, ecosystem, and voxel streaming paths.
- Last local verifier PASS: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log`, `Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, 0 `: warning ` / 0 `: error ` text matches. Loops55-71 are source-only: scalability cleanup, registry fanout cleanup, owner-cache cleanup, warning cleanup, and Scanner/FloatingOrigin/Combat DataVault fallback cleanup applied; `diff --check` passes; guarded build blocked by CPU/compiler contention.
- Latest covered follow-up: `SaveThumbnailSystem` no longer uses `GlobalRegistry.ScalabilityTier` for low-quality capture skip; it maps continuous `GlobalQualityWeight` to a threshold without changing thumbnail path/cache layout.
- Current compiled surface also includes resource-scarcity service rebind, proxy-light continuous math, flora-genome/marauder outpost/soundscape continuous quality routing, `BiomeBoundarySdfRuntime` Player/Dispatcher hot-swap, `AbyssalThermalManager` continuous `GlobalQualityWeight * VRAM weight` thermal-grid gating, beacon/acoustic/pause UI service-cache cleanup, audio-log Save/Localization/AudioLogRuntime cache cleanup, BaseAirlock/charger/PDA shell/cultivation inventory service rebinding, flora/organic/trade/active-sonar/seismic/GPU scatter continuous quality cleanup, and current compile-wall repairs. Loops54-58 additionally edit somatic CCD/GI/memory/input graph, BaseModule/tether/voxel/drill/lockstep, player/submarine/scanner/gyro/interior-GI, and player movement scalability tails; not yet compile-verified.
- Remaining risk: generated project graph durability is not proven until Unity/project regeneration preserves the referenced local asmdef sources without ignored `.csproj` hand patches. Runtime GC and profiler proof remain absent.

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

## 2026-05-23 X_003 Compile-Wall Slice

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL. Runtime proof absent.

- Added `Tools/CompileWallX003Audit.py` and X_003-owned artifacts:
  `Docs/AgentLogs/CompileWall_X_003_Archaeology.json`,
  `Docs/AgentLogs/AssemblyDependencyAudit_X_003.json`,
  `Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json`.
- Static graph: 173 first-party asmdefs, 397 edges, 0 cycles.
- Gate status: `AssemblyDependencyAudit.py --fail-on-cycles` passed; `--fail-on-runtime-concrete-sibling-refs` failed with 114 runtime concrete sibling refs.
- Core wall: `Hecton8.Core.asmdef` still directly references 17 concrete sibling runtime assemblies. Current source has 2,194 using-boundary violations.
- Hot registry poll cleanup: `EndingSystem.SlowTick()` no longer reads `GlobalRegistry.AtlasSignal` or `GlobalRegistry.Quest`; it reads cached fields refreshed by `IGlobalRegistryHotSwapListener`.
- Blast-radius baseline: `CombatDamageRuntime.cs`, `HectonPlayerHealth.cs`, `HectonPlayerState.cs`, `HectonSubmarineOS.cs`, and `HectonPlayerMovement.cs` remain `Hecton8.Core` files with 94-assembly static reverse-closure blast radius.
- Stop condition unchanged: no broad asmdef reference deletion until call sites are replaced by pure contracts, typed signals, DataVault handles, or cached owner interfaces with compile proof.

Until then, status remains `PENDING VERIFICATION`.

## 2026-05-23 X_003 APEX Override Addendum

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL. Latest asmdef and seaglide edits
are pending CLI compile because active `dotnet` processes remain present; CPU
later dropped to 21%, but the "another dotnet is running" guard still blocks.

- Corrected X_003 scope to `Assets/_Project`; graph now covers generated input
  and editor-domain asmdefs.
- Removed five zero-hit `Hecton8.Core.asmdef` refs:
  `Hecton8.Bootstrap.Contracts`, `Hecton8.World.Contracts`,
  `Hecton8.Environment.Fluids.Contracts`,
  `Hecton8.Habitat.Deformation.Contracts`, `Hecton8.UI.Localization`.
- Current graph after removal: 178 first-party asmdefs, 418 edges, 0 cycles,
  0 unresolved first-party refs, 0 `autoReferenced=true` first-party asmdefs.
- Remaining wall: 115 runtime concrete sibling refs, with `Hecton8.Core.asmdef`
  still holding 16 concrete sibling runtime refs.
- Seaglide fix: `SeaglideHydrodynamicsRuntime` no longer casts
  `GlobalRegistry.Physics` to `PhysicsApplySystem`; force drain now accepts
  `IPhysicsService`.
- Source using audit: 131 cross-domain using edges, 2,275 cross-domain using
  directives, 0 critical AI<->Physics or AI/Physics/Physiology->UI/Audio using
  findings.
- Concrete cast audit: 1,014 runtime concrete `as/is/GetComponent` findings,
  concentrated in `Hecton8.Core`; AI/Physics/Physiology direct player concrete
  coupling count is 0.
- Key-file blast radius:
  `Physics/CablePhysicsSolver132.cs` remains `Hecton8.Core`, radius 98, reaches
  UI/audio; `Physiology/ShinobuMetabolismRuntime.cs` radius 2, reaches neither
  UI nor audio; `AI/Cognition/UtilityAICognitionVault.cs` radius 99 and still
  reaches UI/audio through Core's live AI dependency.

No claim is made that cable physics or AI cognition are decoupled. They are
measured blockers until their Core-owned callers move to contracts, typed
signals, or DataVault descriptors.

## 2026-05-23 X_003 Source-Domain DTO Addendum

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / PARTIAL_CLI_COMPILE. Latest
Core compile passed after the AGENTS.md CPU/compiler guard opened; broader
domain compile proof is unavailable because the current generated root contains
only `Hecton8.Core.csproj`.

- Promoted the 144-byte unmanaged `AcousticEchoTap` transit DTO from
  `Hecton8.Audio.Virtualization.Contracts` to `Hecton8.Core.Contracts`.
- Removed the audio-owned `AcousticEchoTap` copy to keep one DTO owner.
- Removed `using Hecton8.Audio.Virtualization` from
  `Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`.
- Changed `Tools/CompileWallX003Audit.py` source-domain scans to use folder
  ownership under `Assets/_Project/Scripts/<Domain>` instead of asmdef ownership
  only; root Core no longer masks `Scripts/AI/*` source coupling.
- Static result after rerun: AssemblyDependencyAudit using-boundary violations
  2208, X_003 source-domain edges 470, source-domain using directives 3374,
  critical AI/Physics/UI/Audio source imports 0.
- Compile result: `dotnet build Hecton8.Core.csproj --no-restore` passed with
  0 warnings and 0 errors in 00:01:18.12. Coverage check found that the project
  includes `HectonSignalLaneContract.cs` but not `AcousticEchoLocationRuntime.cs`,
  `AudioVirtualizationContracts.cs`, or the seaglide physics files.

This does not shrink the asmdef blast radius yet. `Hecton8.Core` still has live
audio virtualization service dependencies through `GlobalRegistry`, and selected
Core-owned files such as `Physics/CablePhysicsSolver132.cs` still remain at
98 affected assemblies.

## 2026-05-23 X_003 Critical Cast Gate Addendum

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / PARTIAL_CLI_COMPILE.

- Widened `Tools/CompileWallX003Audit.py` to include explicit C# `(Type)` casts
  and to report the AI/Physics/Physiology critical lane separately.
- Removed all 7 critical-lane findings by replacing value casts with existing
  `BufferID.ShinobuMetabolismStates` / `BufferID.Shinobu274RadiationStates`
  enum members and a physics vehicle command enum-cast with a byte-mask flag
  check.
- Static result: concrete cast pattern findings 1559->1552,
  AI/Physics/Physiology concrete cast findings 7->0, AI/Physics/Physiology
  direct player concrete coupling findings 0.
- Current graph remains 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.
- Latest covered compile: `dotnet build Hecton8.Core.csproj --no-restore`
  passed with 0 warnings and 0 errors in 00:01:05.08. This covers the changed
  Core-owned physics files, but not physiology because no generated
  `Hecton8.Physiology.csproj` exists.
- Blast-radius proof remains unchanged: cable physics radius 98 and reaches
  UI/audio; metabolism radius 2 and reaches neither; AI cognition radius 99 and
  reaches UI/audio through Core.

Cable solver isolation is explicitly blocked until `TetherManager` stops direct
static calls to `CablePhysicsSolver132`/`CableNodeFlags132`/`TetherTelemetryEntry`
and uses a contract, service, DataVault descriptor, or typed signal bridge.

## 2026-05-23 X_003 Fully-Qualified Source Boundary Gate

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL.

- Added a fully-qualified Hecton8 namespace reference scan to
  `Tools/CompileWallX003Audit.py`; it detects inline `Hecton8.Domain.Type`
  references that do not appear as `using` directives.
- Removed `FaunaDirector`'s direct AI->Physics listener dependency on
  `Hecton8.Physics.IAcousticPingEventListener` / `PhysicsEventBus`.
- `FaunaDirector` now consumes `SignalBus<AcousticPingSignal>` snapshots through
  the existing bounded acoustic panic ring.
- Removed stale `using Hecton8.AI` from `GlobalPhysicsStateManager.cs`; the
  scanner fauna contact interface is already contract-owned.
- Current source gate: critical AI/Physics/UI/Audio using findings 0; critical
  fully-qualified AI/Physics/UI/Audio findings 0.
- Current graph gate remains partial: 178 asmdefs, 418 edges, 0 cycles, 115
  runtime concrete sibling references, `autoReferencedFalse=178`.
- Compile proof for edited Core slice: `dotnet build Hecton8.Core.csproj
  --no-restore` passed with 0 warnings and 0 errors in 00:03:08.88 after the
  CPU/compiler guard opened.

Non-claim: cable solver and AI cognition blast radius did not shrink. Cable
remains `Hecton8.Core` radius 98 and reaches UI/audio; metabolism remains
`Hecton8.Physiology` radius 2 and reaches neither.

## 2026-05-23 X_003 Namespace-Domain Contract Pass

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL. Latest compile proof pending
because CPU was 100% with active `dotnet`/`csc`.

- `KinematicStateDTO` is now owned by `Hecton8.Core.Contracts.Physics`, not by
  `Hecton8.Physics.KCC`.
- `FaunaBrain` and `PredatorCognitionDomain_Steering` no longer import
  `Hecton8.Physics` or `Hecton8.Physics.KCC` for kinematic state reads.
- Predator procedural audio moved from the managed `ProceduralAudioEvents`
  static bridge to typed `SignalBus<AudioEvent>` payloads.
- Predator bite damage no longer falls back to concrete `HectonPlayerHealth`;
  it uses `CombatDamageRuntime` target registration.
- AI force routing no longer calls `PhysicsForceRouter`; it uses cached
  `IPhysicsService` refreshed by `GlobalRegistryServiceSlot.Physics`.
- `HectonBoidController` and `LeviathanTentacleVerletSolver` now read abyssal
  GPU flow through `IAbyssalFlowGpuReadModel`, not concrete
  `HectonFluidEngine`.
- X_003 source-domain scanner now uses declared namespace with path fallback.

Static result:
- Source-domain edges: 586.
- Source-domain using directives: 3619.
- Critical AI/Physics/UI/Audio source imports: 0.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Remaining AI/Physics/Physiology concrete cast findings: 49.
- Asmdef graph remains unchanged: 178 asmdefs, 418 edges, 0 cycles,
  115 runtime concrete sibling refs.

Non-claim: Cable physics and AI cognition blast radius did not shrink in this
pass. They remain blocked by Core live dependency routes.

## 2026-05-23 X_003 AI Physics FQN Eradication Pass

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / CLI_COMPILE_BLOCKED_BY_FOREIGN_ERRORS.

- `FaunaBrain` no longer contains direct or fully-qualified `Hecton8.Physics.*`
  calls for CCD math, impact material lookup, authored current sampling,
  wall-slide projection, or physics manager telemetry.
- Pure CCD math is contract-owned as
  `Hecton8.Core.Contracts.Physics.KinematicCcdContractMath`; the physics
  `KinematicCcdMath` type is now a compatibility facade over that contract.
- Impact material metadata is contract-owned as `IImpactMaterialProvider`; the
  legacy physics interface derives from it so existing physics implementers
  remain valid.
- Authored current sampling is routed through cold-cached
  `IAmbientCurrentReadModel`, resolved through `GlobalRegistry.TryGet<T>` to
  `FluidRuntime`; `CurrentVolume` remains physics/fluid-owned.
- AI wall-slide projection uses local pure math instead of
  `HectonContactJob`.
- Direct AI call to `GlobalPhysicsStateManager.ReportKinematicCcdIntervention`
  was removed; the cross-domain fact remains `HighSpeedImpactSignal`.

Static result:
- Source-domain edges: 587.
- Source-domain using directives: 3629.
- Critical AI/Physics/UI/Audio source imports: 0.
- Fully-qualified source edges: 120.
- Fully-qualified source references: 961.
- Critical fully-qualified AI/Physics/UI/Audio findings: 0.
- Concrete cast findings: 1313.
- AI/Physics/Physiology concrete cast findings: 49.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Asmdef graph: 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete sibling
  refs, `autoReferencedFalse=178`.

Compile status:
- Previous X_003-induced errors in `FaunaBrain`, `GlobalPhysicsStateManager`,
  `KinematicCcdMath`, `CurrentVolume` access, `HectonContactJob` access, and
  `AbyssalCavitationRuntime.TryLoadOrdnanceCsv` no longer appear in guarded
  Core build output.
- A missing `ResolvePriorityBitIndex` helper in `VocalWarningSystem` was
  restored because it was a local helper visibility break.
- Latest guarded `dotnet build Hecton8.Core.csproj --no-restore` is blocked by
  unrelated UI/Power compile stops:
  `PDADecryptionSpectrogramPanel._materialBufferBound` and multiple
  `ShinobuLogisticsRouter` Jacobi/delta-pass fields/types.

Non-claim: no current green CLI compile, no Unity import proof, no PlayMode
proof, no profiler proof. Cable physics remains `Hecton8.Core` radius 98 and
still reaches UI/audio; metabolism remains radius 2 and reaches neither.

### Generated Project Hygiene Addendum

- Guarded Core build later failed because `GlobalSignals.cs` referenced
  `SurvivalSignalRoute`, `AupSignalRoute`, `CraftingSignalRoute`, and
  `SimulationSignalRoute`.
- The route owner file already existed at
  `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs`.
- `Hecton8.Core.csproj` omitted that source file, so X_003 added the compile
  include beside the other Core signal files.
- Rebuild after the include is pending; active `dotnet/csc` processes kept the
  AGENTS guard closed for 5 minutes.

## 2026-05-23 X_003 AI/Physics Interface Facade Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

- Added narrow registry contracts for concrete owner routes still used by AI
  and physics consumers: `IObjectPoolService`, `IAtmosphereReadModel`, and
  `IMicroFaunaPresentationPulseSink`.
- Extended existing service/read models instead of adding new owner references:
  `IHazardZoneReadModel.TrySampleHazardAvoidance`,
  `IThermodynamicsService.TryResolveApexMigrationThermalAttractor`, and
  `IEcosystemDirectorService.RegisterApexPredatorKill`.
- Existing owners implement the contracts:
  `ObjectPoolManager`, `HectonAtmosphereManager`, `HazardZoneManager`,
  `SargassumMicroFaunaBoids`, `AbyssalThermalManager`, and
  `EcosystemDirector`.
- `GlobalRegistry` maps the new contracts through existing cold service slots.
- `FaunaBrain`, `FaunaDirector`, and `SubmarineFluidDynamics` consume the
  interfaces and no longer cast to those concrete owner classes.

Static result:
- Concrete cast findings: 1314 -> 1305 from the immediate pre-pass baseline.
- AI/Physics/Physiology concrete cast findings: 49 -> 40.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Critical AI/Physics/UI/Audio source using findings: 0.
- Critical AI/Physics/UI/Audio fully-qualified findings: 0.
- Asmdef graph unchanged: 178 asmdefs, 418 edges, 0 cycles, 115 runtime
  concrete sibling references, `autoReferencedFalse=178`.

Compile proof:
- Guard sample before build: CPU 13.2%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore`: passed, 0 warnings,
  0 errors, 00:00:46.15.

Non-claim: cable physics still lives in `Hecton8.Core`, radius 98, reaches
UI/audio; metabolism remains `Hecton8.Physiology`, radius 2, reaches neither.

## 2026-05-23 X_003 Read-Model Facade Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

- Added/extended narrow contract read models instead of exposing concrete
  domain owners: `IAnalyticalFlowReadModel`,
  `ICelestialSkyDirectionReadModel`,
  `IBrineFluidDensityReadModel.TrySampleBrineLayer`, and
  `ITerrainProvider.TryGetBiomeIndex`.
- Existing owners remain authoritative:
  `HectonFluidEngine`, `HectonCelestialEngine`,
  `ResourceDistributionDirector`, and `MapMagicBridge`.
- `GlobalRegistry` maps analytical flow and celestial sky direction through
  existing runtime slots.
- `FaunaDirector`, `SubmarineFluidDynamics`, `HectonFluidEngine`, and
  `GlobalPhysicsStateManager` consume interfaces for terrain/render scale,
  brine, analytical flow, celestial sky direction, and thermodynamics.

Static result:
- Concrete cast findings: 1305 -> 1297.
- AI/Physics/Physiology concrete cast findings: 40 -> 32.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Critical AI/Physics/UI/Audio source using findings: 0.
- Critical AI/Physics/UI/Audio fully-qualified findings: 0.
- Asmdef graph unchanged: 178 asmdefs, 418 edges, 0 cycles, 115 runtime
  concrete sibling references, `autoReferencedFalse=178`.

Compile proof:
- Initial guard sample blocked: CPU 53.1%, active `dotnet/csc` count 0.
- Second guard sample opened: CPU 21.2%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore`: passed, 1 `CS2002`
  duplicate-source warning, 0 errors, 00:01:11.13.

Non-claim: no asmdef sever was performed in this pass. Cable physics remains
`Hecton8.Core`, radius 98, reaches UI/audio; metabolism remains
`Hecton8.Physiology`, radius 2, reaches neither.

## 2026-05-23 X_003 Cable132 Service Bridge And Assembly Extraction

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

- New assembly: `Hecton8.Physics.Cable132`.
- Moved `CablePhysicsSolver132.cs` and `CablePhysicsDebugGizmo132.cs` into
  `Assets/_Project/Scripts/Physics/Cable132`.
- Added `ICablePhysics132Service` and `GlobalRegistryServiceSlot`
  `CablePhysics132Runtime`.
- `TetherManager` now consumes the cached `GlobalRegistry.CablePhysics132`
  interface route instead of direct `CablePhysicsSolver132` /
  `CableNodeFlags132` static access.
- `CablePhysics132Service` wraps existing solver/vault/dump operations; cable
  data ownership remains deterministic GlobalDataVault DTO rows.
- `Hecton8.Editor.asmdef` explicitly references the new cable assembly for the
  tuner window.

Static result:
- Assembly audit: 179 asmdefs, 421 DAG edges, 0 cycles,
  `autoReferencedFalse=179`, 116 runtime concrete sibling refs.
- Core debt remains: 15 concrete sibling refs.
- Cable selected blast radius: 98->3.
- Cable direct inbound: 92->1.
- Cable UI/audio reach: true->false / true->false.
- Critical source `using` findings: 0.
- Critical fully-qualified source reference findings: 0.
- AI/Physics/Physiology direct player concrete coupling: 0.

Compile proof:
- New build was not launched because the guard was closed:
  CPU 66.3%, 75.2%, 99.8%, 99.4%, 98.4%; no compiler process in those samples.
- Generated `.csproj` files do not yet include the new Unity asmdef project;
  Unity regeneration/import is required for cable assembly CLI coverage.

Non-claim: project-wide sibling refs are not solved. Runtime concrete sibling
refs increased 114->116 because the new cable assembly explicitly depends on
`Hecton8.Core` and `Hecton8.Core.Memory`. Runtime microseconds saved: 0
claimed.

## 2026-05-23 X_003 Alpha Leviathan Contract Extraction

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

- Moved pure Alpha Leviathan unmanaged DTO/flag contracts from
  `Hecton8.AI.Cognition` to `Hecton8.Core.Contracts.AI.Cognition`.
- Removed `Hecton8.AI.Cognition` from `Hecton8.Core.asmdef`.
- Updated AI cognition runtime, Fauna, and World consumers to import the
  contract namespace.

Static result:
- Asmdef edges: 418 -> 417.
- Core refs: 40 -> 39.
- Core first-party refs: 27 -> 26.
- Core concrete sibling refs: 16 -> 15.
- Runtime concrete sibling refs: 115 -> 114.
- `UtilityAICognitionVault.cs` and `ShinobuApexBrainVault.cs`: radius 99 -> 2,
  UI/audio reach true -> false.

Non-claim: cable physics is still not isolated. `CablePhysicsSolver132.cs`
remains `Hecton8.Core`, radius 98, UI=true, audio=true. The blocker is the
live tether/winch/player component object graph, not the Alpha Leviathan DTO
route.

## 2026-05-23 X_003 Fauna Contact/Sensory Interface Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

- Added narrow contact/sensory routes:
  `IFaunaSpatialContact`, `IFaunaBaitSource`,
  `IFaunaDistractorSignalSource`, `IPlayerBleedingReadModel`, and
  `IFaunaNoiseSignalReceiver`.
- Existing owners remain authoritative:
  `FaunaBrain`, `PickupItem`, `DeployableFlare`, and
  `HectonSurvivalSystem`.
- `FaunaBrain`, `FaunaSensorSuite`, and `NoiseSystem` now consume those
  interfaces instead of concrete owner checks for parental defense, cleaner
  hosts, apex rivals, prey panic, bait feeding, bleeding distractors, flare
  distractors, and player-noise dispatch.

Static result:
- Concrete cast findings: 1292 -> 1271.
- AI/Physics/Physiology concrete cast findings: 24 -> 2.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Critical AI/Physics/UI/Audio source using findings: 0.
- Critical AI/Physics/UI/Audio fully-qualified findings: 0.
- Asmdef graph unchanged: 178 asmdefs, 418 edges, 0 cycles, 115 runtime
  concrete sibling references, `autoReferencedFalse=178`.

Compile proof:
- Guard opened at CPU 22% with 0 compiler processes.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`:
  PASS, 2 `CS0168` unused-variable warnings, 0 errors, 00:01:17.46.

Non-claim: no asmdef sever was performed in this pass. Cable physics remains
`Hecton8.Core`, radius 98, reaches UI/audio; metabolism remains
`Hecton8.Physiology`, radius 2, reaches neither.

## 2026-05-23 X_003 Ecosystem/Terrain/Biome/Drag Facade Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE_BLOCKED.

- Added narrow owner facades in `GlobalRegistryContracts`:
  `ITerrainHeightSampleReadModel`, `IVegetationThreatReadModel`,
  `IVegetationThreatPulseSink`, `IBiomePhysicsInfluenceReadModel`,
  `ISargassumDragReadModel`, and `IDepthZoneReadModel`.
- Extended `IEcosystemDirectorService` with the exact ecology behavior calls
  already used by fauna code; ownership remains in `EcosystemDirector`.
- Existing owners implement the routes:
  `HectonMapMagicVegetationBridge`, `WorldProceduralFieldSampler`,
  `SargassumGlobalDragManager`, `DepthZoneDirector`, and `EcosystemDirector`.
- `GlobalRegistry` maps the new facades through existing slots.
- `FaunaBrain`, `FaunaDirector`, and `HectonFluidEngine` now consume interface
  routes instead of concrete owner casts for ecosystem behavior, terrain height
  payloads, vegetation threat pulse/weight, biome buoyancy influence, sargassum
  drag, and depth-zone readout.

Static result:
- Concrete cast findings: 1297 -> 1292.
- AI/Physics/Physiology concrete cast findings: 32 -> 24.
- AI/Physics/Physiology direct player concrete coupling: 0.
- Critical AI/Physics/UI/Audio source using findings: 0.
- Critical AI/Physics/UI/Audio fully-qualified findings: 0.
- Asmdef graph unchanged: 178 asmdefs, 418 edges, 0 cycles, 115 runtime
  concrete sibling references, `autoReferencedFalse=178`.

Compile proof:
- Guard sample before build: CPU 44.5%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore` stopped before X_003-edited
  files on unchanged signal split files:
  `SpscSignalRingBuffer.cs(120,2) CS1513`,
  `GlobalSignals.LegacyFacade.cs(1064,5) CS1519`,
  `GlobalSignals.RuntimeLifecycle.cs(1122,1) CS1022`.
- `git diff` shows no local diff in those three signal files; standalone syntax
  probe did not reproduce those parse errors.
- Follow-up guard stayed closed: CPU 62.2%, active `dotnet/csc` present.

Non-claim: no asmdef sever was performed in this pass. Cable physics remains
`Hecton8.Core`, radius 98, reaches UI/audio; metabolism remains
`Hecton8.Physiology`, radius 2, reaches neither.
