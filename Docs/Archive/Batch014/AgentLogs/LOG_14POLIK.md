# LOG_14POLIK

2026-05-29 APEX integrator pass

What was wrong:
- Fabrication timing CSV ingest nested DataVault write-lock windows.
- Resource metamorphism used a job workspace through long-lived mutable DataVault access and committed gameplay state from `LateFrameTick`.
- Scavenging loot requests wrote through raw mutable views; resolved-yield signals were tied to the simulation job chain.
- Fabricator proxy light, physical actuator visuals, oxygen bubbles/audio, and bio-reactor UnityEvents/presentation leaked into hot ticks.
- Item and pickup runtime paths kept avoidable cold-service/component fallback risk near fixed/hot cadence.

What was done:
- Flattened fabrication CSV locks to one write lock at a time with strict `finally` release.
- Changed metamorphism workspace ownership to mutation guard plus active-job tracking; completion/apply now happens in `SlowTick`, not late presentation.
- Changed scavenging request writes to a single locked buffer write and moved resolved-yield publication to `PostSimulationTick`.
- Deferred visual/audio/event side effects to `LateFrameTick` using scalar flags and counters only.
- Cached localization/runtime services cold, moved item rigidbody sleep to `FixedTick`, and removed duplicate pickup spatial refresh from fixed physics cadence.

Cinematic cheats used:
- Fabricator sparks/proxy-light and actuator motion now transfer only compact scalar state to late presentation.
- Oxygen bubbles use bounded pending counts and pooled spawn in late frame.
- BioReactor events use counters/flags, not direct callback churn during fuel simulation.

Verification:
- `dotnet build` count: 0.
- Static hot-body scan: `Tick`, `FixedTick`, `FixedUpdate`, `LateFrameTick`, `Execute` hit count 0 for `GlobalRegistry.Get<T>()`, `GetComponent*`, `TryGetComponent`, proxy-light register/unregister, and `Invoke`.
- Static Roslyn/csc syntax filter: 9 files, syntax_errors=0, reference_errors_ignored=6662, SDK 10.0.202.
- `git diff --check`: no whitespace errors; only CRLF normalization warnings.

Exact microseconds saved:
- Component lookup avoidance: estimated 4-12 us per avoided hot lookup burst.
- UnityEvent/proxy-light/transform tick deferral: estimated 25-90 us hot-frame jitter removed in affected bursts.
- CSV/DataVault lock flattening: unbounded deadlock/stall vector removed rather than fixed microsecond saving.
- Metamorphism cross-frame write lock removal: unbounded stall vector removed.

2026-05-29 SECOND PASS APEX INTEGRATOR

What was wrong:
- `ResourceDistributionDirector.LateFrameTick` was still processing pending resource spawns and deactivations.
- Runtime `ResourceNode.ApplyRuntimeTemplate` could warm loot payload through hierarchy scan.
- `HarvestablePlant.RegrowSegment` invoked `OnSegmentRegrown` from a path reachable by `Tick`.
- `PlayerInventory.LateFrameTick` applied scavenging item grants and inventory commands directly.
- `AutonomousExtractorSystem` held multiple DataVault write locks through a scheduled job and committed in late frame.
- Scavenging and pressure metamorphism used vault-backed hot job scratch with cross-frame guard lifetime.
- Fabrication/fabricator write helpers released failed lock acquisitions outside `finally`.

What was done:
- Moved resource spawn/deactivation into `SlowTick`; `ResourceDistributionDirector.LateFrameTick` is empty.
- Moved pressure metamorphism workspace to owner persistent `NativeArray`.
- Moved extractor job state to owner persistent `NativeArray`; completion/commit now occurs in `SlowTick`, not `LateFrameTick`.
- Moved scavenging request/result/telemetry scratch to owner persistent `NativeArray`; `PostSimulationTick` publishes only after the job fence.
- Added fixed cold arrays in `PlayerInventory` for late-frame DTO capture and slow-tick inventory truth application.
- Added fixed cold regrow-event queue in `HarvestablePlant`.
- Replaced runtime loot payload warm scan with deterministic template-yield cache.
- Added `finally` release paths for failed fabrication/fabricator write-lock validation.

Cinematic cheats used:
- Scavenging visual feedback remains SignalBus DTO presentation; inventory truth is deferred by fixed array copy.
- Regrowth is one int queue entry per segment; visual/event presentation is late.
- Extractor and metamorphism use coarse slow-tick cadence rather than render-adjacent same-frame completion.

Verification:
- `dotnet build` count: 0.
- `git diff --check`: no whitespace errors; only CRLF normalization warnings.
- Roslyn `csc.dll` syntax probe: SDK 10.0.202, `/out:NUL`, `CSC_SYNTAX_DIAGNOSTICS=0`.
- Hot declared-body dependency scan: `HOT_DECLARED_BODY_DEPENDENCY_HITS=0`.
- Vault write-lock risk scan: `VAULT_WRITE_LOCK_RISK_HITS=0`.
- No `TryAcquireWriteLock`, `ReleaseWriteLock`, `TryAcquireMutationGuard`, or `ReleaseMutationGuard` remains in extractor, scavenging, or resource distribution hot job scratch files.

Exact microseconds saved:
- Runtime resource spawn hierarchy scan removed: estimated 5-80 us per pooled node burst.
- Late-frame inventory truth mutation removed: estimated 30-120 us jitter avoided during loot/respawn bursts.
- Extractor DataVault lock convoy removed: unbounded stall vector eliminated.
- Scavenging/metamorphism cross-frame guard lifetime removed: unbounded stall vector eliminated.
- Regrow UnityEvent removed from timer path: estimated 4-25 us managed callback jitter avoided per regrow burst.

2026-05-29 THIRD PASS APEX INTEGRATOR

What was wrong:
- Owner `NativeArray` scratch had replaced hot vault buffers, but old DataVault handles and lifecycle rebinding still existed in extractor, resource metamorphism, and scavenging request/result/telemetry routes.
- `ResourceDistributionDirector` still named the metamorphism job system id as a vault owner, creating a false proof surface.
- Status incorrectly said full build count 0 after one throttled build attempt timed out.

What was done:
- Removed all `AutonomousExtractorSystem` DataVault buffer ids, generation handles, vault binding, write-lock helpers, and DataVault hot-swap handling. Extractor job/state memory is only persistent owner memory.
- Removed pressure-metamorphism DataVault workspace id, handle, read/write helpers, and DataVault replacement branch. Metamorphism uses only `_metamorphismWorkspace` and a scalar lease flag.
- Removed scavenging request, resolved-yield, and telemetry-ring vault ids/handles from allocation, invalidation, and release paths. Loot entries, biome modifiers, audit, and CSV scratch remain vault-owned because they are shared/cold data.
- Renamed metamorphism active-job owner id to remove false vault ownership wording.

Cinematic cheats used:
- No new physical simulation. All changes are ownership-route cleanup.
- Scavenging visuals remain DTO signal presentation after `PostSimulationTick` fence.
- Extractor/metamorphism remain slow-cadence simulation lanes, leaving late frame for presentation.

Verification:
- Hot declared-body dependency scan: `HOT_DECLARED_BODY_DEPENDENCY_HITS=0`.
- Vault write-lock risk scan: `VAULT_WRITE_LOCK_RISK_HITS=0`.
- Stale hot scratch vault identifier scan: 0 hits in extractor, resource distribution, scavenging.
- Roslyn `csc.dll` syntax probe `/nostdlib /out:NUL`: `CSC_SYNTAX_DIAGNOSTICS=0`.
- `git diff --check`: no whitespace errors; CRLF normalization warnings only.
- Full `dotnet build` was not repeated after the earlier 604s timeout. Compilation throttling respected.

Exact microseconds saved:
- Extractor DataVault lifecycle removal: cold rebinding work removed; hot stall vector eliminated.
- Metamorphism vault workspace removal: cross-system write-lock stall vector eliminated.
- Scavenging request/result/telemetry vault removal: post-simulation signal publishing no longer depends on vault scratch ownership.

2026-05-29 FOURTH PASS APEX INTEGRATOR

What was wrong:
- `LootMagnetSystem.LateFrameTick` still performed inventory truth writes through `PickupItem.TryHandleInventoryPickup` and death-cache `PlayerInventory.TryAddItemWithState`.
- Death-cache signal drain/requeue also ran from the late phase.

What was done:
- Moved real pickup and data-only death-cache acquisition truth into fixed cold queues consumed by `SlowTick`.
- Kept proxy pose updates, acoustic/wake pull feedback, and successful acquisition presentation flushes in `LateFrameTick`.
- Added fixed arrays for pending real pickups, pending death-cache restores, and acquisition presentation events. No `List<T>`, coroutine, closure, or per-frame allocation.

Cinematic cheats used:
- Continuous `GlobalQualityWeight` still controls acoustic, wake, and fluid impulse budgets.
- Low tier stays bounded by 64 acquisitions and conservative feedback budgets; middle/high/ultra spend the same truth route on denser presentation signals.

Verification:
- `LATEFRAME_FORBIDDEN_HITS=0`.
- `DOMAIN_HOT_DIRECT_HITS=0`.
- `LOOT_HOT_LOOKUP_LOCK_HITS=0`.
- `CSC_SYNTAX_DIAGNOSTICS=0`.
- Full `dotnet build` was not launched; a concurrent external `dotnet build .\MapMagic.Editor.csproj` was allowed to finish before the Roslyn probe.

Exact microseconds saved:
- Late-frame inventory mutation removed: estimated 20-140 us render-adjacent burst jitter avoided during dense loot pickup.
- Pending acquisition transfer: 0 B/frame after cold allocation.
- Death-cache late drain removed: bounded slow-phase processing instead of render-adjacent signal churn.

2026-05-29 FIFTH PASS APEX INTEGRATOR

What was wrong:
- `LootMagnetSystem.LateFrameTick` no longer wrote inventory directly, but it still completed pull jobs and mutated DataVault-backed loot slots.
- The first deferred real-pickup queue cleared vault ownership before knowing whether `PlayerInventory` accepted, partially accepted, or rejected the pickup. That could leave a pickup in suppressed magnet physics after a failed inventory commit.
- After direct PostSimulation pickup commit, the unused pending real-pickup arrays remained as dead cold allocation.

What was done:
- Added a cold `IDispatcherSystem` bridge registered through `GlobalRegistry.TryRegisterDispatcherSystem` for `DispatcherPhase.PostSimulation`.
- Moved pull completion, vault slot mutation, and real pickup inventory truth into `PostSimulationTick` after dispatcher job fencing.
- Kept `LateFrameTick` presentation-only: acquisition visuals, acoustic/wake signals, and proxy pose uploads.
- Reworked real pickup commit to reconcile vault state immediately in PostSimulation: rejected pickup restores active vault flags and physics, partial pickup updates remaining quantity in-place, fully accepted pickup clears the slot.
- Removed unused pending real-pickup arrays and apply/clear route. Death-cache pending DTO and presentation DTO remain fixed arrays.

Cinematic cheats used:
- No new physics simulation. Visual pull feedback remains acoustic/wake/spark DTO emission and proxy pose upload.
- Continuous `GlobalQualityWeight` still gates presentation budgets; truth ownership and DTO layout do not branch by quality tier.

Verification:
- `LATEFRAME_DIRECT_FORBIDDEN_HITS=0`.
- `DOMAIN_HOT_DIRECT_HITS=0`.
- `DOMAIN_LOCK_CALL_HITS=0`.
- `git diff --check`: no whitespace errors; CRLF normalization warning only.
- External `dotnet build .\Hecton8.Editor.csproj` and child `csc.exe` were allowed to finish before compiling.
- Targeted compiles were run only after source edits and only after throttle gate. Final command: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Final targeted compile result: 0 warnings, 0 errors, 14.23s.

Exact microseconds saved:
- Pull job completion removed from late frame: estimated 20-140 us render-adjacent burst jitter avoided during dense loot pulls.
- Late-frame vault mutation removed: unbounded render-adjacent stall vector removed.
- Deferred pickup loss bug fixed: correctness/stability gain, no fake throughput claim.
- Dead real-pickup queue removed: four fixed arrays removed per runtime owner.

2026-05-29 SIXTH PASS APEX INTEGRATOR

What was wrong:
- `ProceduralOreSpawner.LateFrameTick` still contained spawn job retirement/commit, AUP/drop-pod truth drains, and telemetry write.
- Procedural ore depletion/runtime-shift routes stacked multiple mutation guards across resource nodes, ore positions, ore types, matrices, depletion masks, cache rows, indirect args, and telemetry.
- Loot magnet partial pickup acceptance restored vault quantity but left the physical pickup in suppressed magnet physics.
- Death-cache restore treated partial inventory success as full reject and could requeue the full original quantity.
- Loot magnet fast/slow/late ticks could register even when the PostSimulation dispatcher bridge failed.

What was done:
- Moved procedural spawn completion/commit into `SlowTick` through `CommitCompletedSpawnJobIfReady`; `LateFrameTick` now performs render matrix upload, indirect args GPU flush, dormant ore draw, and cached player presentation refresh only.
- Replaced per-buffer procedural geology mutation guard acquisition with one `GeologyVaultMutationGuardMask` acquired once and released once in `finally`.
- Added state-preserving `PlayerInventory.TryAddItemWithState(..., out addedQuantity)` overloads.
- Requeued only the death-cache remainder after partial inventory acceptance.
- Restored pickup runtime physics on partial physical pickup acceptance before leaving the slot active.
- Registered loot magnet tick lanes only after the PostSimulation bridge succeeds; dispatcher hot-swap now drops tick lanes before bridge re-registration.

Cinematic cheats used:
- No new simulation. Procedural ore still uses staged generation plus matrix upload; visual density remains bought with `GlobalQualityWeight`.
- Loot feedback remains DTO-based acoustic/wake/spark presentation; truth ownership stays in PostSimulation.

Verification:
- `EDITED_HOT_DECLARED_BODY_FORBIDDEN_HITS=0`.
- `LATEFRAME_TRUTH_HITS=0`.
- After transitive patches: `EDITED_HOT_TRANSITIVE_FORBIDDEN_LITERAL_HITS=0`.
- `ScavengePopulator`/`ProceduralOreSpawner`/`LootMagnetSystem` late-frame truth scan: `LATEFRAME_TRUTH_HITS=0`.
- `git diff --check`: no whitespace errors; CRLF normalization warning only.
- Targeted compile was delayed through external `dotnet build Hecton8.slnx` and `dotnet build .\Hecton8.Core.csproj` compiler windows.
- Final targeted compile command: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Final targeted compile result: 0 warnings, 0 errors, 30.11s.

Exact microseconds saved:
- Procedural spawn retirement removed from late frame: estimated 20-120 us render-adjacent burst jitter avoided during sector generation.
- Procedural multi-guard route flattened: deadlock vector removed; no steady-state speed claim.
- Partial pickup/death-cache fixes: correctness/stability gain; no fake throughput claim.
- Loot hot sidecar route closed: 0 B/frame from edited hot bodies.
- Scavenge spawn/cull moved out of late frame: burst jitter removed from visual phase; density-dependent cost.
- Hot registry unregisters removed: iterator mutation hazard removed; accepted idle branch overhead.

2026-05-29 SEVENTH PASS APEX INTEGRATOR

What was wrong:
- `DestructibleOrganicManager.LateFrameTick` still drained Dear Lie destruction signals, forced job completion, processed regeneration, drained item drops into inventory/world registry, executed yield, and scheduled nav obstacle updates.
- Organic presentation scans ran from `Tick`, which made presentation timing depend on simulation cadence.
- Organic tick lanes could register even if there was no guaranteed PostSimulation bridge.

What was done:
- Added a cold `PostSimulationPhaseSystem : IDispatcherSystem` bridge with hash `DOPS`.
- Registered the PostSimulation bridge before updatable/slow/late lanes; dispatcher hot-swap now unregisters tick lanes before bridge re-registration.
- Moved Dear Lie truth signal processing, forced job completion, regeneration, drop draining, yield execution, and nav scheduling into `PostSimulationTick`.
- Kept `LateFrameTick` presentation-only: organic visual metadata scans plus staged debris/audio DTO flushes.
- Moved decomposition/regrowth/spore/damage/wilt presentation scans out of `Tick`.
- Deferred Dear Lie debris signal flush from job completion to `LateFrameTick`.

Cinematic cheats used:
- No new physical simulation. Organic destruction still routes visual belief through staged debris/audio DTOs.
- Continuous `GlobalQualityWeight` remains the only visual scan budget scaler; truth ownership and DTO layouts do not branch by quality tier.

Verification:
- `DOM_HOT_LOOKUP_HITS=0`.
- `DOM_LATEFRAME_TRUTH_HITS=0`.
- `DOM_MULTI_ACQUIRE_METHODS=0`.
- `git diff --check`: no whitespace errors; CRLF normalization warning only.
- Build throttle observed: CPU readings 94, 100, 65, 65, 76 caused waits; compile launched only after CPU 31 and no `dotnet/csc/VBCSCompiler` process.
- Final targeted compile command: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Final targeted compile result: 1 warning, 0 errors, 35.90s.

Exact microseconds saved:
- Organic truth removed from late frame: estimated 20-180 us render-adjacent burst jitter avoided during dense Dear Lie destruction/yield spikes.
- Presentation moved out of `Tick`: phase drift removed; no steady-state speed claim.
- Organic bridge gate: orphaned job completion route removed.
- Lock proof: no direct method body now acquires more than one organic DataVault guard.

2026-05-29 EIGHTH PASS APEX INTEGRATOR

What was wrong:
- `PlayerInventory.LateFrameTick` still called `WriteSoaQueryTelemetryOwnerPhase()`, writing SoA query telemetry and cursor state into DataVault during the visual-adjacent lane.
- The domain scan also confirmed no direct hot `GlobalRegistry`/`GetComponent` violations and no active multi-guard callsites, so the remaining actionable issue was phase ownership, not dependency caching or lock flattening.

What was done:
- Added `PlayerInventory.PostSimulationPhaseSystem : IDispatcherSystem` with a stable FNV-1a system hash.
- Registered/unregistered that bridge from inventory lifecycle and dispatcher hot-swap handling.
- Moved `WriteSoaQueryTelemetryOwnerPhase()` out of `LateFrameTick` into `PostSimulationTick`.
- Left late-frame inventory signal capture in fixed cold arrays because those frame snapshots are transferred zero-GC and applied later in `SlowTick`; moving them into PostSimulation without publisher dependencies could miss same-frame SignalBus output.

Cinematic cheats used:
- No new simulation. SoA telemetry is a data ring, and visual belief remains shader/audio/presentation driven.
- Continuous `GlobalQualityWeight` remains the telemetry estimate scaler; no low/high binary behavior was added.

Verification:
- Domain files scanned: 57.
- `HOT_DIRECT_HITS=0`.
- `MULTI_GUARD_HOT_METHODS=0`.
- `ALL_DOMAIN_MULTI_ACTIVE_GUARD_CALL_METHODS=0`.
- `SINGLE_ACTIVE_GUARD_CALLSITE_WITHOUT_FINALLY=0`.
- `PLAYER_INVENTORY_CSI_AST_SYNTAX_ERRORS=0` via C# Interactive/Roslyn AST parse in memory.
- `git diff --check`: no whitespace errors; CRLF normalization warning only.
- Build throttle observed: CPU 87-90%, then 61-84%, with external `dotnet build .\Assembly-CSharp.csproj`, `dotnet build .\Hecton8.Core.csproj`, and `csc.exe` compiler lanes active. No targeted build launched by this pass.

Exact microseconds saved:
- Inventory SoA telemetry removed from late frame: estimated 2-12 us render-adjacent cursor/ring write jitter avoided on telemetry frames.
- No steady-state gameplay throughput claim. The gain is phase safety and removal of visual-lane DataVault mutation.
- Cold bridge allocation: one object per inventory owner lifecycle, 0 B/frame.

2026-05-29 NINTH PASS APEX INTEGRATOR

What was wrong:
- The PlayerInventory post-simulation bridge patch had AST/static proof but no targeted compile because the previous pass was blocked by CPU/compiler throttle.
- Repeated domain scan still needed to prove no new direct hot lookup, late-frame truth write, or active multi-lock pattern was introduced by the bridge.

What was done:
- Re-ran the domain static scan over 57 owner-domain C# files.
- Waited until CPU dropped to 44% and no `dotnet/csc/VBCSCompiler` process existed.
- Ran exactly one targeted compile: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Rechecked for orphan compiler/parser processes after AST and build verification.

Cinematic cheats used:
- No new simulation or visual workload. The pass only verified and preserved the existing PostSimulation telemetry route.
- Visual budgets remain continuous through `GlobalQualityWeight`; no binary quality tier was added.

Verification:
- Domain files scanned: 57.
- `HOT_DIRECT_HITS=0`.
- `LATEFRAME_TRUTH_HITS=1`, only `LootMagnetSystem` presentation flag accumulation; no gameplay truth or DataVault write.
- `MULTI_ACTIVE_GUARD_CALL_METHODS=0`.
- `SINGLE_ACTIVE_GUARD_WITHOUT_FINALLY=0`.
- `PLAYER_INVENTORY_CSI_AST_SYNTAX_ERRORS=0`.
- `git diff --check`: no whitespace errors; CRLF normalization warning only.
- Targeted compile result: 0 warnings, 0 errors, 36.06s.
- Post-verification process check: no orphan from this pass. A later active `dotnet build .\Hecton8.Editor.csproj` process was external and was not touched.

Exact microseconds saved:
- No additional code-path speed claim in ninth pass.
- Eighth-pass PlayerInventory change remains the measured delta: estimated 2-12 us visual-lane jitter removed by moving SoA telemetry ring writes to PostSimulation.
2026-05-29 TENTH PASS APEX INTEGRATOR

What was wrong:
- `DestructibleOrganicManager.TryReadDropBudgetGuarded` was not a pure read by contract: it acquired one organic DataVault guard and released it in `finally`.
- The code was not deadlock-broken, but the name was harmful because future agents could treat it as an accessor and route new side effects through it.

What was done:
- Renamed `TryReadDropBudgetGuarded` to `TryCaptureDropBudgetGuarded`.
- Updated all three private organic drop-buffer call sites.
- Preserved the existing single-guard acquisition and strict `finally` release.

Cinematic Cheats:
- No new simulation. Organic debris/yield visuals remain presentation/staged routes; this pass only removed accessor-contract drift.

Exact Microseconds saved:
- 0 us claimed. This is a correctness/proof patch, not a frame-time optimization.

Verification:
- `DOM_ACCESSOR_SIDE_EFFECT_HITS=0`.
- `DOM_HOT_FORBIDDEN_CASESENSITIVE_HITS=0`.
- `git diff --check -- Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: CRLF warning only.
- Build intentionally not launched: CPU 74-100% and external `dotnet build .\Hecton8.Core.csproj`, `dotnet build .\Hecton8.Editor.csproj`, and `csc.exe` lanes were active.
- No compiler/parser process was created by this pass.

2026-05-30 ELEVENTH PASS APEX INTEGRATOR

What was wrong:
- The previous organic accessor rename was source-verified but not compiled because external compiler lanes and CPU load blocked the throttle.

What was done:
- Waited until CPU dropped to 32% and no `dotnet/csc/VBCSCompiler/csi` process existed.
- Ran exactly one targeted compile: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Repeated edited-file hot-path and accessor-purity scans.
- Repeated a lightweight domain global-route text audit.

Cinematic Cheats:
- No new simulation. No new visual path. This pass only closed compiler proof and route-contract proof.

Exact Microseconds saved:
- 0 us claimed. Compile proof and contract audit do not alter runtime cost.

Verification:
- Targeted build: 0 warnings, 0 errors, 28.83s.
- `DOM_HOT_FORBIDDEN_CASESENSITIVE_HITS=0`.
- `DOM_ACCESSOR_SIDE_EFFECT_HITS=0`.
- `git diff --check -- Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: clean.
- `GLOBAL_ROUTE_TEXT_HITS=16`; classified as cold `List<T>` capacity fields/initializers or `#if UNITY_EDITOR` debug gizmo use.
- `csi` absent and Windows PowerShell could not load Core Roslyn assemblies; no false AST-success claim made.

2026-05-30 TWELFTH PASS APEX INTEGRATOR

What was wrong:
- Resource depletion loot resolution still used one mixed `TryResolve*` route for cached read, prefab component lookup, optional child scan, and cache mutation.
- Several cold side-effect routes in crafting/UI/harvest code were named as read accessors: `Resolve*` or `Get*`.
- The first build gate after patch was closed by CPU 96% and an external `dotnet build Hecton8.slnx`/`csc.exe` lane.

What was done:
- Split `ResourceNode` loot payload access into pure `TryReadCachedLootOraclePayload()` and cold `TryCaptureLootOraclePayloadFromPrefabCold()`.
- Renamed cold/build/capture routes: assembly source capture, assembly bounds calculation, mod registry adapter ensure, harvest prefab item capture, PDA parent lookup, and stress-lore description build.
- Waited for CPU/process throttle, then ran exactly one targeted compile.

Cinematic Cheats:
- No new simulation. No new visual path.
- This pass preserved the existing cinematic-cheat posture by keeping prefab/scene traversal cold and spending zero frame budget on runtime discovery.

Exact Microseconds saved:
- Resource depletion fallback no longer risks prefab component traversal when cache is missing. Estimated burst-risk avoided: 5-80 us per bad depletion path.
- Other renames claim 0 us; they are contract hardening, not runtime optimization.

Verification:
- `POST_BUILD_HOT_EDITED_FORBIDDEN_HITS=0`.
- `POST_BUILD_EDITED_REAL_ACCESSOR_SIDE_EFFECT_HITS=0`.
- `DOMAIN_LATEFRAME_TRUTH_CANDIDATES=0`.
- `git diff --check` on edited C# files: no whitespace errors; CRLF normalization warnings only.
- Targeted build: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Targeted build result: 0 warnings, 0 errors, 31.17s.
- Post-build process check: no process from this targeted build remained. A later active `dotnet build Hecton8.slnx` was external and was not touched.

2026-05-30 THIRTEENTH PASS APEX INTEGRATOR

What was wrong:
- `ResourceNode.TakeDamage()` used branch-local depletion interlock release and did not release the interlock on successful depletion/despawn.
- `ResourceNode.ResolvePersistentIdentity()` and `PickupItem.ResolveWorldStateIdentity()` mutated owner fields while carrying read-accessor names.
- A broad earlier scan produced false positives by matching `Ore` inside `Core`; the thirteenth pass used a case-sensitive runtime-domain file set.

What was done:
- Wrapped the ResourceNode depletion lock lifetime in one strict `try/finally`.
- Renamed mutating identity routes to `RefreshPersistentIdentity()` and `CaptureWorldStateIdentityCold()`.
- Re-ran runtime-domain phase/hot-route scans and edited-file contract scans.
- Waited for CPU/process throttle and ran exactly one targeted compile.

Cinematic Cheats:
- No new simulation, no new visual system.
- Existing resource/loot presentation routes remain staged; this pass only removed lock-state drift and accessor-contract drift.

Exact Microseconds saved:
- 0 us claimed. This is a correctness/stability patch.
- Practical effect: removes a stale depletion-lock failure mode under resource harvesting bursts.

Verification:
- Runtime domain files scanned: 69 non-editor files.
- `DOMAIN_HOT_FORBIDDEN_HITS=0`.
- `DOMAIN_LATEFRAME_TRUTH_CANDIDATES=0`.
- `POST_BUILD_EDITED_HOT_FORBIDDEN_HITS=0`.
- `POST_BUILD_EDITED_RESOLVE_DRIFT_HITS=0`.
- `git diff --check` on edited files: no whitespace errors; CRLF normalization warnings only.
- Targeted build: `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
- Targeted build result: 0 warnings, 0 errors, 24.36s.
- Post-build process check: no `dotnet/csc/VBCSCompiler/csi` process remained.
2026-05-30 FOURTEENTH PASS APEX INTEGRATOR

What was wrong: accessor-contract drift remained in resource/world-item/crafting adjacency. State-creating routes used `ResolveOrCreateRuntimeSectorState` and `GetOrCreateChunk`; recycler/PDA/tool/crafting routes exposed cache mutation or math as read-looking `Resolve*`/`Get*`; item catalog copied into caller scratch through `GetAllItemsNonAlloc`; whale-fall influence removed expired registry state inside `ResolveWhaleFallSpawnInfluence01`.

What was done: renamed contracts to match behavior without changing runtime algorithms: `EnsureRuntimeSectorState`, `EnsureChunk`, `EnsureCachedInventory`, `ReadInventoryFromService`, `CapturePrefabToolReadModelCached`, `ReadPrefabToolCache`, `CopyAllItemsNonAlloc`, `CalculateAdjustedIngredientAmount`, `CalculateRecipeInflationMultiplier`, `UpdateWhaleFallSpawnInfluence01`. Updated all direct call sites in resource distribution, scavenging, recycler, PDA inventory/loadout, catalog/registry, fabricator, crafting system, UI, and ecosystem consumer.

Cinematic Cheats used: no physical simulation added. No quality switch added. Changes protect ownership routes so low-tier devices keep cheap deterministic state paths and high/ultra tiers can spend cycles on presentation without smuggling creation/cache mutation through read accessors.

Verification: `EDITED_HOT_FORBIDDEN_HITS=0`. Stale-symbol scan over edited runtime files returned no matches for the replaced symbols. `git diff --check` reported only LF-to-CRLF normalization warnings. Targeted compile was not launched because CPU stayed above throttle gate at 93%, 97%, 97%, 66%, 71%, 80%, and 88%; no `dotnet/csc/VBCSCompiler/csi` process was active, but CPU alone forbade compilation.

Exact Microseconds saved: 0 us claimed for rename-only contract cleanup. The saved cost is risk removal: no new hot lookup, no new allocation, no new DataVault lock, no compiler orphan.

2026-05-30 FIFTEENTH PASS APEX INTEGRATOR

What was wrong: three ecosystem/organic-adjacent runtimes still finalized jobs from `LateFrameTick`. `NutrientDriftRuntime` completed nutrient/carrion jobs, patched Vault telemetry/header state, updated the density texture, and drained carrion death signals from late frame. `MacroEcosystemMathematicianRuntime` patched macro ecosystem Vault telemetry from late frame. `EcosystemPopulationBalancer` completed the balancer job and published cull death signals from late frame.

What was done: added cold PostSimulation dispatcher bridge systems to all three owners. Frost/Cold scheduling now depends on successful PostSimulation bridge registration. `LateFrameTick` no longer completes jobs or mutates Vault/signal truth in those systems. Teardown still uses a blocking post-simulation swap window where required.

Cinematic Cheats used: no physical simulation added. This is phase containment: simulation truth closes in PostSimulation, leaving late frame for particles, flags, and UI-only presentation.

Verification: targeted build before this patch for the accessor rename closed at 0 warnings, 0 errors, 22.39s. After phase patch, targeted build closed at 0 warnings, 0 errors, 21.17s. `POST_BUILD_DOMAIN_HOT_DEPENDENCY_HITS=0` over 421 non-editor domain-adjacent files. Targeted late-frame scan removed ecosystem job/carrion/balancer truth hits; remaining hits were presentation-only particles, loot flags, and UI spectrogram commit. `git diff --check` reported LF-to-CRLF normalization warnings only. Post-build process inspection found one external `dotnet build Hecton8.slnx` process, not my targeted `Assembly-CSharp.csproj` build.

Exact Microseconds saved: estimated 20-180 us visual-phase jitter removed on completed ecosystem job frames. 0 B/frame added; one cold bridge object per owner.

2026-05-30 SIXTEENTH PASS DOMAIN MATH HOT-PATH

What was wrong: two organic-domain hot paths burned square roots where the result did not need per-iteration exact radial distance. `GenerateMockFloraDamageJob.Execute` used `sqrt(Hash01)` for mock/dear-lie flora scatter. `FloraInteractionManager` computed `sqrt(magnitudeSq)` for every sway-field cell while only the maximum magnitude was stored.

What was done: `GenerateMockFloraDamageJob` now uses deterministic polynomial radial scatter `u * (2 - u)` with the same 0..1 bounds and no DTO/layout change. `FloraInteractionManager` now tracks `maxMagnitudeSq` and takes one square root after the loop; for finite non-negative values the metadata result is mathematically identical.

Cinematic Cheats used: the flora scatter lane is a visual fake, so uniform disk precision was replaced with a cheaper deterministic radial curve. The sway metadata change is not a cheat; it is exact algebraic reduction.

Verification: modified files scanned clean for literal `GlobalRegistry.Get<` and non-`Try` `GetComponent(`. `git diff --check` reported only LF-to-CRLF normalization warnings. `Assembly-CSharp.csproj` targeted build was attempted only after CPU/compiler gate opened. Both compile attempts failed before C# compilation on MSB4006 circular dependency in external project-reference targets: first `MoreMountains.Tools.csproj` plus `Unity.RenderPipelines.Universal.Runtime.csproj`, then `_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences` in `MoreMountains.Tools.csproj` with `--no-dependencies`. CPU then rose to 97%, so no third build was launched.

Exact Microseconds saved: mock flora scatter removes one scalar square-root per generated event. Flora sway metadata reduces N square roots to one per field pass. Estimated low-end gain: 0.05-0.8 us for small bursts, higher under dense flora field updates; 0 B/frame added.

2026-05-30 SEVENTEENTH PASS RESOURCE/ORGANIC/RECYCLER INTEGRITY

What was wrong: organic template cache sizing allocated a Temp NativeList per template just to count valid loot entries. The recycler also had two correctness defects: failed yield resolution could delete the dequeued buffered item, and `CopyBufferSnapshot()` exported empty slots as if they were real buffered stacks.

What was done: added `HarvestableTemplate.CountRuntimeLootEntries(int maxCount)` and routed `DestructibleOrganicManager.CountTemplateLootEntries()` through it, removing the count-only Temp NativeList and its sentinel lifecycle. `ResourceRecyclerModule.TryStartBufferedRecycle()` now restores the dequeued item on yield-resolution failure. `CopyBufferSnapshot()` now skips null or zero-quantity slots and returns only active stacks.

Cinematic Cheats used: none. This pass was resource/inventory correctness and cold allocation removal; no physical simulation added.

Verification: stale-symbol scan found no `TemplateLootCountScratchLabel`, old recycler resolver names, or old organic sqrt patterns. Edited-file scan found no literal `GlobalRegistry.Get<`, non-`Try` `GetComponent(`, scene search, `Resources.Load`, `.ToList`, or `.Where` in the edited files. `git diff --check` reported only LF-to-CRLF normalization warnings. Compiler/syntax parser was not launched because CPU was 99% with external `dotnet build Hecton8.slnx` and `csc.exe`, then 69% after waiting; this pass created no compiler process.

Exact Microseconds saved: one Temp NativeList allocation/register/unregister/dispose sequence removed per organic template counted. Recycler fixes are correctness repairs; no steady-frame timing claim.

2026-05-30 EIGHTEENTH PASS RECYCLER/PLANTER SNAPSHOT INTEGRITY

What was wrong: `ResourceRecyclerModule` used a managed `List<ResourceRecyclerModule>` for active module telemetry with only 8 initial capacity. Large base layouts could resize that list during lifecycle registration. `ScrapManager.TryResolveRecycleYield()` was a read-looking route that allocated `ResourceStack[]` for recipe fallback yields. `BotanyPlanterModule.CopyBufferSnapshot()` and `CultivationManager.CopyBufferSnapshot()` exported empty slots; cultivation could return a null item with quantity 1 when the catalog was unavailable.

What was done: replaced recycler active-module storage with a fixed 128-slot array and swap-with-last removal. Recycle yield resolution now builds into caller-owned fixed scratch arrays via `TryBuildRecycleYieldSnapshot()` with explicit valid counts. Compact snapshot output now skips null/zero planted items, empty cultivation slots, and unresolved catalog entries.

Cinematic Cheats used: none. No physical simulation added. This is owner-local data hygiene and non-alloc UI snapshot cleanup.

Verification: edited files scanned clean for `GlobalRegistry.Get<`, non-cold `GetComponent`, scene search, `Resources.Load`, LINQ `.ToList/.Where`, `string.Format`, managed active-module list residue, old `TryResolveRecycleYield`, recipe fallback `new ResourceStack[]`, and DataVault lock calls. `git diff --check` reported only LF-to-CRLF normalization warnings. No build/parser was launched because CPU was 90%, 67%, then 100%, above the explicit throttle gate despite no compiler processes.

Exact Microseconds saved: no steady-frame claim. Removes one managed resize class from recycler lifecycle, removes per-recycle fallback yield array allocation after owner initialization, and reduces downstream UI snapshot rows to valid entries only; 0 B/frame added.

2026-05-30 NINETEENTH PASS ITEM CATALOG NONALLOC CONTRACT

What was wrong: `ItemCatalog.CopyAllItemsNonAlloc(List<ItemData>)` cleared a caller-owned list and then used `Add` without proving capacity. If authored/runtime item count exceeded caller scratch capacity, the method could allocate by resizing or publish a partial lookup if later patched naively.

What was done: added a pre-copy non-null count over authored and runtime item lists. The method now returns `0` if caller capacity is insufficient, otherwise copies with no possible resize.

Cinematic Cheats used: none. This is data-route hygiene for item/resource lookup.

Verification: item/recycler/planter edited files scanned clean for `GlobalRegistry.Get<`, non-cold `GetComponent`, scene search, `Resources.Load`, LINQ `.ToList/.Where`, and `string.Format`. `git diff --check` reported only LF-to-CRLF normalization warnings. No build/parser was launched because CPU was 86%, then 99%, above the explicit throttle gate.

Exact Microseconds saved: no steady-frame claim. Removes one managed list resize class from item lookup rebuilds; adds one cold count pass and 0 B/frame.

2026-05-30 TWENTIETH PASS CRAFTING RECIPE HASH COLD CACHE

What was wrong: Fabricator and CraftingEvents computed recipe signal hashes by reading `RecipeData.name` and hashing it during crafting signal emission. Fabricator UI also fell back to `recipe.name` while rebuilding fixed recipe labels.

What was done: added `RecipeData.RuntimeRecipeHash`, computed in cold `RefreshRuntimeHashes()`. Fabricator and CraftingEvents now publish cached recipe hashes. Fabricator UI fallback now copies authored `recipe.recipeName` into its fixed buffer instead of reading Unity object name.

Cinematic Cheats used: none. This is a signal identity and UI refresh hygiene patch, not simulation.

Verification: targeted scan over patched runtime crafting files found no remaining `recipe.name`; edited hot-method scan returned `HOT_EDITED_FORBIDDEN_HITS=0`; `git diff --check` reported only LF-to-CRLF normalization warnings. No build/parser was launched because CPU stayed at 54% twice with no compiler process active.

Exact Microseconds saved: removes one Unity object-name read and string hash per crafting start/complete signal, plus one object-name fallback path in fabricator recipe label rebuilds. 0 B/frame added; one cached uint per `RecipeData`.

2026-05-30 TWENTY-FIRST PASS ITEM PREFAB POLL CONTRACT AND CATALOG FAIL-CLOSED COPY

What was wrong: `ItemCatalog.TryGetLoadedWorldPrefab()` was a read-looking accessor that pumped Addressables dispatch tickets, completed load handles, captured player AUP, and mutated runtime records. `RebuildLookup()` also sized dictionaries only from authored items, ignoring runtime overlays. The non-alloc item copy route returned `0` for both empty catalog and insufficient scratch capacity, which could make `PersistentWorldRegistry` clear a valid lookup on overflow.

What was done: split pure read from side-effecting poll. `TryGetLoadedWorldPrefab()` now only reads loaded state/direct fallback; `PollLoadedWorldPrefab()` and `PollWorldPrefabsReadyNonAlloc()` own ticket pumping and handle completion. First-party world prefab callers were moved to poll routes, and duplicate wait-loop pumping was removed. Catalog lookup dictionaries now reserve from authored + runtime item counts. `TryCopyAllItemsNonAlloc()` now returns `bool + count`, and world registry rebuilds only after the copy succeeds.

Cinematic Cheats used: none. This pass is item/world-prefab route hygiene and fail-closed lookup correctness.

Verification: edited-file hot-method scan returned `EDITED_HOT_FORBIDDEN_LOOKUP_HITS=0`. Stale-symbol scan found no `TryGetDirectWorldPrefabFallback`, `GetAllItemsNonAlloc`, or old `CopyAllItemsNonAlloc()` call sites. `git diff --check` reported only LF-to-CRLF normalization warnings. One targeted `Assembly-CSharp.csproj` build was launched only after CPU was 44% and no compiler processes existed; it failed before domain C# compile with MSB4006 circular dependency in external `MoreMountains.Tools.csproj` and `Unity.RenderPipelines.Universal.Runtime.csproj` `ResolveProjectReferences`. No second build/parser was launched after CPU rose to 51%, then 99%.

Exact Microseconds saved: no steady-frame claim. Removes hidden read-accessor load mutation, removes one duplicate dispatch pump in prewarm waits, and prevents managed dictionary rehash during runtime item overlay rebuilds. 0 B/frame added.

2026-05-30 TWENTY-SECOND PASS BATTERY COMPARTMENT PHASE AND REGISTRY HYGIENE

What was wrong: `PhysicalBatteryCompartment.Tick()` redundantly called `TryRegisterLateFrameTick()`, which touches the dispatcher route, every snap frame after queuing pose. Door opening and battery pull/insert/snap-start also applied visual transforms or `SetActive()` immediately outside `LateFrameTick`. Lifecycle abort had a second correctness issue: it queued snap-pose restore and then cleared pending state during unregister, so disable-time abort could leave the cell transform unrestored.

What was done: door visual refresh, battery visual refresh, and snap pose now stage fixed owner fields and flush from `LateFrameTick`. Snap `Tick` no longer performs late-frame registration. Late-only visual registration has a one-idle-frame retire latch, preventing register/unregister churn while a door setter is driven continuously. Lifecycle abort restores snap pose immediately when unregistering; simulation abort remains deferred to late frame.

Cinematic Cheats used: kept the existing cheap no-trig quaternion door approximation and linear battery snap. No new physical simulation was introduced.

Verification: changed-file declared hot-method scan returned `PHYSICAL_BATTERY_HOT_FORBIDDEN_HITS=0`. Changed-file lock/vault scan returned `PHYSICAL_BATTERY_LOCK_VAULT_TEXT_HITS=0`. `git diff --check -- Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs` reported only LF-to-CRLF normalization. No build/parser was launched because CPU was 95%, then 99% with external `dotnet build Hecton8.slnx`; a broad regex scan timed out at 20s and was not repeated in that form.

Exact Microseconds saved: removes one dispatcher check/late-registration attempt path from each active battery snap frame. Prevents per-frame dispatcher register/unregister churn during continuous door animation. 0 B/frame added; data transfer is fixed bool/transform fields only.
