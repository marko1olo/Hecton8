# SHINOBU_253 Status - Stress Driven Spawn Director

Date: 2026-05-21
Status: POLISH PASS ACTIVE; ORIGIN FENCE + CONTROLLED COLD INIT + DISPATCHER FENCE PROOF + HOTSWAP DEFAULTS HYGIENE + ORIGIN SNAPSHOT VALIDITY FENCE + PENDING SELECTION APPLY FIX + CSV COLD WRITER FENCE + BLACKBOX STRIDE FIX + FLAG CONSTANT AUDIT + POST-JOB WRITER FENCE + AUP BLIT AUTHORITY + MONOLITH RUNTIME GATE + READER FENCE; FULL COMPILE NOT RE-LAUNCHED
Domain: ECHELON 3 FLORA/FAUNA/BIOTA, cross-read E5 Player Stress and E7 Weather.
Prompt task count: 20

## Prompt Proof

- [x] SHINOBU_253 block extracted with CLI regex from `Docs/Tasks/CURRENT_BATCH.md`.
  DOD: attribute-aware regex `<AGENT_PROMPT\s+id="SHINOBU_253"[^>]*>` returned 13,807 chars and exactly 20 `Task NN:` headers.
  Rejected: exact-tag regex because live tag contains `role` and `chat_name`.
  Estimate: 900 us audit only.
- [x] SHINOBU_253 block re-extracted during polish loop.
  DOD: same CLI regex returned 13,807 chars and exactly 20 task headers after remediation.
  Rejected: relying on compressed chat memory.
  Estimate: 900 us audit only.

## Mandates Selected Before Coding

- `AI_Director_Encounter_Manager.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`

## Current Polish Loop - Writer Fence / AUP Blit / Runtime Gate

- [x] Post-job commit remains inside the same Vault writer fence.
  DOD: `LateFrameTick` now keeps `_lockedVault` and unlocks job buffers only after telemetry microsecond patch, cull apply, selection apply, and black-box dump routing.
  Rejected: unlocking immediately after `TryComplete` and writing commit-side state through raw `TryResolve` views.
  Estimate: prevents stale cross-owner writes; runtime cost is 1 reference compare plus existing unlock path.
- [x] Hot-swap unlock owner is stable.
  DOD: `_lockedVault` records the exact Vault instance whose buffers were locked; a DataVault service replacement skips stale commit and still releases the original locks.
  Rejected: unlocking against the current registry Vault after service replacement.
  Estimate: prevents rare deadlock/corruption class; negligible steady-state cost.
- [x] Cold input and borrowed-readiness writes are fenced.
  DOD: `RefreshColdInputs`, `PublishDirectorInput`, `TrySetTuning`, and borrowed cognition readiness release write locks through `finally`, including invalid-length early returns.
  Rejected: compound `TryAcquireWriteLock && IsCreated && Length` checks because they leak locks when acquisition succeeds and validation fails.
  Estimate: removes lock-leak failure path; no extra hot allocation.
- [x] Counter init sentinel is no longer exposed to uninitialized memory.
  DOD: `ShinobuStressDirectorCounters` requests `NativeArrayOptions.ClearMemory`, and borrowed cognition readiness is published only after cold defaults/magic initialization.
  Rejected: reading random uninitialized counter memory as a magic sentinel.
  Estimate: prevents cold-start false-positive init; no frame cost after first handle creation.
- [x] Director AUP authority is now blit-stable.
  DOD: `DirectorInputDTO`, `DirectorSelectionDTO`, `DirectorOwnedSlotDTO`, `DirectorTelemetryEntry`, and `DirectorSpawnDebugDTO` store AUP facts as `AbsoluteUniversePositionBlit128`; raw `double3` is retained only as a cold cached origin snapshot and cognition bridge field.
  Rejected: native DTO authority fields as raw `double3`, because they are not the project AUP payload contract and invite absolute-float/local conversion mistakes.
  Estimate: +48 to +80 bytes on AUP-bearing rows; prevents wrong-origin replay and hidden spawn drift.
- [x] Data Monolith readiness now requires runtime-loaded payload proof.
  DOD: loot readiness is true only when `H8StaticDataArena.IsLoaded` exposes nonempty `LootCdf` records with a nonzero table hash.
  Rejected: file existence, H8DM header-only, and cold section-table proof as gameplay readiness.
  Estimate: cold gate only; prevents false-ready loot injection.
- [x] Public telemetry/debug reads fail closed while the writer job is scheduled.
  DOD: `TryGetTuning`, `TryGetLatestTelemetry`, `CopyTelemetrySnapshot`, and `TryGetLatestSpawnDebug` return no data during an active scheduled writer chain.
  Rejected: public debug readers racing a scheduled Burst writer over the same Vault rows.
  Estimate: avoids editor/debug race; 0 us when no external reader polls.
- [x] Mock stress jitter is integer-hash deterministic.
  DOD: `GenerateMockTensionJob` uses `Hash3(sector, worldSeed, frame)` bit slicing for jitter; `Unity.Mathematics.Random` and `NextFloat` are absent from touched files.
  Rejected: mutable RNG state in Burst for fallback mock data.
  Estimate: one hash path, no RNG state lane.

## Loop 1 - Tasks 1-5

- [x] Task 01 MONOBEHAVIOUR_SPAWNER_ERADICATION.
  DOD: new `StressDrivenSpawnDirector` injects DTOs through Vault and cognition arrays, no runtime GameObject spawner route.
  Rejected: deleting legacy `FaunaBrain.Compatibility` because other agents still depend on it.
  Estimate: removes 1,000-4,000 us prefab/scene spike when active.
- [x] Task 02 INSTANTIATE_SPIKE_PURGE.
  DOD: scanner reports `runtime_instantiate: 0` in scanned AI/Fauna/World/Environment scope; director path uses AICognition owner APIs after borrowed handle readiness.
  Rejected: prefab warmup pool because it still carries scene object churn.
  Estimate: 700-3,500 us saved per encounter start.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE.
  DOD: mutable DTO structs use direct fields; NativeArray elements are copied, mutated, then written back.
  Rejected: property-setter wrappers around NativeArray elements.
  Estimate: compile-safety and 20-60 us avoided branch/property churn per cold tick.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION.
  DOD: `SpawnRuleDTO` is explicit 32 bytes with required offsets 0/4/8/12/16; `ValidateLayout()` checks offsets.
  Rejected: implicit struct layout.
  Estimate: 5-15 us cache-line stability on low-end ARM/x86.
- [x] Task 05 EMERGENCY_MOCK_TENSION_GENERATOR.
  DOD: `GenerateMockTensionJob` produces deterministic triangle-wave tension/weather when no external stress input is valid.
  Rejected: `Random.Range` and managed RNG.
  Estimate: 3-8 us.

## Loop 2 - Tasks 6-10

- [x] Task 06 BURST_DIRECTOR_EVALUATION_KERNEL.
  DOD: evaluation, budget, hidden-AUP, cull, inventory ticket, telemetry are Burst `IJob`s over Native/Vault buffers; macro biomass fields are read from SHINOBU_116 contract rows or ecosystem service fallback.
  Rejected: managed LINQ/list scoring and same-frame scene queries.
  Estimate: 20-80 us.
- [x] Task 07 BUDGET_DRIVEN_SELECTION_MATH.
  DOD: continuous `GlobalQualityWeight` and thermal pressure scale CPU budget; low budget biases one high-threat apex over swarm spam.
  Rejected: binary quality tiers and fixed spawn counts.
  Estimate: 150-600 us under low-end thermal pressure.
- [x] Task 08 THE_DEAR_LIE_FRUSTUM_INJECTION.
  DOD: hidden spawn AUP probes use player forward, frustum plane margins, turbidity/fog radius, and cheap SDF clearance.
  Rejected: static Transform spawn points.
  Estimate: prevents visible spawn pop; 30-120 us for probe loop.
- [x] Task 09 DESPAWN_CULLING_ROUTINE.
  DOD: owned-slot DTO ring marks distant entries in Burst; managed apply phase unregisters cognition slots with swap-pop compaction.
  Rejected: per-entity MonoBehaviour distance checks.
  Estimate: 80-400 us saved across 64 slots.
- [x] Task 10 ASYNCHRONOUS_INVENTORY_PRELOAD - BLOCKED BY DEPENDENCY.
  DOD: fixed `InventoryPreloadTicketDTO` buffer is written by Burst job; runtime flags loot missing and dumps black box.
  Rejected: synchronous loot table lookup from hot spawn path.
  Estimate: 20-90 us saved when monolith exists.
  Blocker: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.

## Loop 3 - Tasks 11-15

- [x] Task 11 BIOME_TRANSITION_SUPPRESSION.
  DOD: `BiomeTransitionTicksRemaining` zeros candidate count and blocks selection during transition.
  Rejected: spawn during biome boundary uncertainty.
  Estimate: prevents wasted spawn/cull churn.
- [x] Task 12 AUP_PRECISION_SPAWN_MATH.
  DOD: hidden spawn authority is stored in `AbsoluteUniversePositionBlit128`; local float conversion happens only after subtracting the cached floating-origin AUP.
  Rejected: world-space float spawn coordinates.
  Estimate: removes large-world drift; no runtime spike.
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE.
  DOD: deterministic FNV state hashes and immutable DTO layout; `GlobalQualityWeight` does not change save identity or authority route.
  Rejected: quality-dependent DTO/schema mutation.
  Estimate: deterministic proof only.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS.
  DOD: Vault generation handles allocate with `NativeArrayOptions.UninitializedMemory`; initialization writes full controlled defaults.
  Rejected: hot ClearMemory allocation.
  Estimate: 10-70 us per buffer acquisition.
- [x] Task 15 TELEMETRY_DIRECTOR_RECORDER.
  DOD: 300-entry `DirectorTelemetryEntry` ring records tension, budget, spawn/cull counts, macro proof, state hash, AUPs, and chain microseconds; fault/loot absence dumps `Docs/AgentLogs/Dump_SHINOBU_253.bin`.
  Rejected: managed Debug.Log-only diagnostics.
  Estimate: 8-25 us.

## Loop 4 - Tasks 16-20

- [x] Task 16 SPAWN_DIRECTOR_TUNER_WINDOW.
  DOD: `Hecton8/AI/AI Director Tuner` exposes spawn rate, frustum margin, budgets, radii, and live graph.
  Rejected: runtime inspector-only tweaking.
  Estimate: editor-only.
- [x] Task 17 CSV_SPAWN_RULES_INGESTOR.
  DOD: `Data/AI/director_spawn_rules.csv` loads cold into Native scratch; parser uses `ReadOnlySpan<byte>` and no row objects.
  Rejected: `JsonUtility`/ScriptableObject hot read.
  Estimate: cold-load only; 10-40 us avoided per reload.
- [x] Task 18 LIVE_SPAWN_HEATMAP_GIZMO.
  DOD: `StressDrivenSpawnDirectorGizmo` draws latest hidden spawn AUP from debug DTO.
  Rejected: scene object markers.
  Estimate: editor-only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR.
  DOD: `Tools/Dynamic_Spawn_Scanner.py` generated `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`.
  Rejected: manual grep report.
  Estimate: audit only.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION.
  DOD: scanner rerun, syntax-like compiler scan found 0 syntax-class errors, lock sequence bug was found and fixed, compile blocker documented.
  Rejected: reporting clean compile when csproj still references missing sources.
  Estimate: audit only.

## Loop 5 - Verification

- [x] Static code scan.
  Result: `WORLD_OPTIMIZATION_REPORT.json` scanned 264 files; `runtime_instantiate: 0`, `mono_enemy_spawner: 0`, `scene_search: 0`; legacy findings remain in non-owned files.
- [x] Syntax-class compiler scan.
  Result: direct `csc.dll` parse/compile invocation on the three new C# files produced `syntax_like_errors=0`; semantic compile could not be isolated from Unity/project references.
- [ ] Full compile.
  Result: BLOCKED BY DEPENDENCY.
  Evidence: `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` fails before semantic validation on 38 missing source items under `Assets/Dynamic Decals/...` and `Assets/_Project/_Archive/HectonWaterPhysics*.cs`.
  Additional earlier evidence: project-graph build also fails on missing `Assets/_Project/Scripts/World/Contracts/GroundRadarContracts.cs` and `Assets/_Project/Scripts/IBuildPlacementRule.cs`.
- [x] CPU/build-server hygiene.
  Result: CPU checked below 50 before build attempts; `dotnet build-server shutdown` executed; no `dotnet`/`csc` processes remain.
- [x] Final log appended.
  Result: `Docs/AgentLogs/LOG_SHINOBU_253.md` updated after implementation.

## Loop 6 - Ultra-Think Polish Remediation

- [x] Cross-assembly storm dependency removed.
  DOD: `StormPropagationDTO` and `ShinobuStormPropagationState` direct reads are gone from `StressDrivenSpawnDirector`; weather consumption stays on `ShinobuOceanWeatherState` and contract/Vault-safe snapshots.
  Rejected: adding Core-to-Storm runtime asmdef reference.
  Estimate: prevents compile-wall coupling; runtime estimate 0 us direct, integration risk reduction only.
- [x] BufferID sovereignty repaired.
  DOD: `H8Memory.BufferID` now declares named `ShinobuMesofauna*` IDs `71180..71189` and `ShinobuStressDirector*` IDs `71190..71202`; director constants use names, not casts.
  Rejected: local numeric `(BufferID)7119x` casts.
  Estimate: 0 us runtime; prevents duplicate owner/range ambiguity.
- [x] AICognition ownership repaired.
  DOD: director no longer creates/grows mesofauna/cognition buffers. It observes existing generation handles and fails spawn activation closed if AICognition is not ready.
  Rejected: AIEcology allocating `SystemID.AICognition` buffers.
  Estimate: avoids cold allocation spikes and ownership races; 10-70 us avoided on bootstrap churn.
- [x] Macro-biomass input integrated.
  DOD: `DirectorInputDTO=208` now carries AUP blit origin/player rows plus sector hash, prey/predator/capacity/toxin/temperature and macro state hash; director reads SHINOBU_116 contract mirrors or cached ecosystem service fallback.
  Rejected: static spawn scoring divorced from ecosystem biomass.
  Estimate: 5-20 us cold read; prevents bad spawns into depleted sectors.
- [x] Deterministic seed and spawn-rate gating hardened.
  DOD: mock tension uses `Unity.Mathematics.Random` seeded by simulation tick/world/sector; selection uses deterministic roll plus `BaseSpawnRatePerMinute`, quality and tension.
  Rejected: `Time.frameCount`, `Time.time`, and every-cold-tick spawn requests.
  Estimate: prevents uncontrolled cognition growth; low-end saving 150-600 us in stressed scenes.
- [x] Black-box and gizmo expanded.
  DOD: `DirectorTelemetryEntry=192` records AUP blit player/spawn rows, macro proof, origin sequence, and spawn slot; gizmo draws min hidden radius, despawn radius, latest hidden sphere, and red injected AUP history from fixed telemetry snapshot.
  Rejected: single latest debug sphere only.
  Estimate: editor-only visualization; telemetry footprint becomes 38.4 KB fixed.
- [x] UI Toolkit facade repaired.
  DOD: tuner graph now uses `VisualElement.generateVisualContent`/`Painter2D`; IMGUI/GUILayout/Handles route removed.
  Rejected: IMGUIContainer graph.
  Estimate: editor-only; avoids IMGUI repaint churn.
- [x] Data Monolith gate strengthened.
  DOD: loot readiness now checks loaded `H8StaticDataArena` LootCdf rows and refuses file/header-only readiness, not raw file existence.
  Rejected: `File.Exists(static_data.h8bin)` as production proof.
  Estimate: cold-only; avoids false-ready loot injection.
- [x] Static verification rerun.
  DOD: `WORLD_OPTIMIZATION_REPORT.json` regenerated; `runtime_instantiate=0`, `mono_enemy_spawner=0`, `scene_search=0`. Targeted rg scans found no storm DTO, Time-based seed, local 7118/7119/7120 BufferID casts, or IMGUI graph usage in touched files.
  Rejected: dotnet rebuild during active CPU/build-wall constraints.
  Estimate: audit only.

## Loop 7 - Reload And H8DM Section Proof

- [x] CSV hot reload bridge added.
  DOD: `StressDrivenSpawnDirector.TryReloadRulesCold()` resets CSV counters under a Vault writer fence and the UI Toolkit tuner exposes a `Reload CSV Rules` command.
  Rejected: one-shot boot CSV load that requires C# reload or runtime restart for balancing.
  Estimate: editor-only; 0 us player runtime.
- [x] H8DM loot-section validation tightened - SUPERSEDED BY RUNTIME ARENA GATE.
  DOD: earlier cold section-table proof was removed from the director; gameplay readiness now requires loaded `H8StaticDataArena` LootCdf records.
  Rejected: header-only, section-table-only, and raw file existence proof.
  Estimate: cold-only; prevents false-ready spawn activation.
- [x] Build gate respected.
  DOD: checked `dotnet`/`csc` processes and CPU before any build; CPU sampled at 99.61%, so dotnet build was not launched.
  Rejected: violating the >50% CPU build rule.
  Estimate: hardware hygiene only.

## Loop 8 - Reload Authority Fence

- [x] CSV reload writer fence widened.
  DOD: `TryReloadRulesCold()` now requires all four reload buffers to be present and locks `Rules -> RuleLinks -> Counters -> CsvScratch` as one cold writer window before resetting counters and parsing CSV.
  Rejected: counters-only lock because `TryLoadRulesCsvCold()` mutates rules, links, scratch, and counters as one table update.
  Estimate: runtime 0 us; editor/cold reload prevents partial native table mutation.
- [x] Post-fence static verification.
  DOD: bracket balance returned braces=0/parens=0/brackets=0; `git diff --check` returned no errors; scanner regenerated `WORLD_OPTIMIZATION_REPORT.json` with `runtime_instantiate=0`, `mono_enemy_spawner=0`, `scene_search=0`.
  Rejected: dotnet build while CPU sampled at 100.00%.
  Estimate: audit only.

## Loop 9 - Subagent Audit Remediation

- [x] Atomic CSV reload repaired.
  DOD: reload no longer resets CSV counters before parse. `TryLoadRulesCsvCold(..., forceReload:true)` validates/counts the CSV without touching rule/link tables, then commits and flips counters only after parse success.
  Rejected: temporary managed staging arrays and counters-first reload state.
  Estimate: runtime 0 us; prevents corrupt editor/cold reload from wasting 80-400 us in downstream spawn/cull churn.
- [x] Reload fence matches Vault global states.
  DOD: `TryReloadRulesCold()` now rejects `IsAllocationLocked` and `IsCompactionFenceActive` before acquiring reload locks.
  Rejected: allowing designer reload during Vault allocation/compaction windows.
  Estimate: runtime 0 us; prevents partial native table mutation.
- [x] Read-accessor naming violation removed.
  DOD: mutating macro fallback names changed from `TryRead*` to `TryApplyMacroEcosystemContractsSnapshot` and `TryRefreshMacroEcosystemServiceCold`.
  Rejected: read-named method caching `GlobalRegistry.EcosystemDirector`.
  Estimate: audit/architecture only.
- [x] H8DM section-table bounds tightened - SUPERSEDED BY RUNTIME ARENA GATE.
  DOD: the old file section-table fallback was deleted from the director to avoid treating an unloaded binary file as gameplay-ready loot data.
  Rejected: accepting any H8DM file metadata without runtime arena hydration.
  Estimate: cold-only; prevents false-ready loot validation.
- [x] Direct Atmosphere/World DTO boundary.
  Result: ASMDEF SAFE / CONTRACT RELOCATION YELLOW.
  Evidence: `StressDrivenSpawnDirector.cs`, `WeatherStateDTO`, `AbsoluteUniversePositionBlit128`, and `HectonFloatingOrigin` all resolve under `Assets/_Project/Scripts/Hecton8.Core.asmdef`; no reference to `Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` or another sibling runtime asmdef was added. Runtime director no longer calls `GlobalSignals.CurrentRuntimeOriginAup()` or `HectonFloatingOrigin.ToRuntimePosition(...)`; AUP payloads use the existing Core blit contract. Future route-card cleanup should still move shared weather/AUP payload declarations into Core.Contracts for namespace hygiene.
- [x] Post-remediation static gate.
  DOD: targeted rg found no `TryReadMacroEcosystem*`, no counters-first CSV reload reset, and no loose H8DM section table guards; bracket balance remained zero; `git diff --check` returned no errors.
  Rejected: dotnet build because CPU sampled at 100.00%.
  Estimate: audit only.

## Loop 10 - AUP Context Polling Reduction

- [x] Legacy origin getter removed from runtime director.
  DOD: `StressDrivenSpawnDirector` no longer calls `GlobalSignals.CurrentRuntimeOriginAup()`; cold input consumes the cached floating-origin snapshot, hidden spawn apply uses `DirectorSelectionDTO.RuntimeSpawn` when the origin sequence still matches, and cognition input reconstructs the same local origin from `SpawnAup - RuntimeSpawn`.
  Rejected: duplicating `AbsoluteUniversePosition` or reading the scene/current origin again during apply.
  Estimate: 0-3 us direct; reduces context-owner drift risk and removes one legacy getter route.
- [x] Runtime conversion helper localized.
  DOD: local `ToLocalDeltaFloat3`, `ToRuntimeVector3`, and `PackAbsoluteAup` keep AUP math in double until the local float delta, then pack the cognition `AbsoluteUniversePositionBlit128` without `AbsoluteUniversePosition.FromAbsolutePosition`.
  Rejected: repeated `HectonFloatingOrigin.ToRuntimePosition(...)` calls in late apply/fallback.
  Estimate: 1-5 us direct in cold/late apply; architecture value is stronger owner-phase snapshot use.
- [x] Registry fallback polling removed from macro service refresh.
  DOD: `TryRefreshMacroEcosystemServiceCold` uses the cached `IEcosystemDirectorService` supplied at bootstrap or `OnGlobalRegistryServiceReplaced`; it no longer polls `GlobalRegistry.EcosystemDirector` when null.
  Rejected: repeated cold getter lookup inside the refresh path.
  Estimate: 0-2 us direct; compile/authority hygiene only.
- [x] Build gate respected after code patch.
  DOD: `dotnet`/`csc` process check returned no rows, but CPU sampled at `100`; no rebuild launched.
  Rejected: violating the >50% CPU rule for a known externally blocked project graph.
  Estimate: hardware hygiene only.
- [x] Static gate after AUP patch.
  DOD: targeted rg returned no matches for legacy origin getter, runtime conversion helper, `using Hecton8.World`, `AbsoluteUniversePosition.FromAbsolutePosition`, `TryGetLatestCreated`, `.Complete()`, Unity random/time, or LINQ selectors in `StressDrivenSpawnDirector.cs`; bracket balance remained zero. `git diff --check` on tracked docs returned only CRLF normalization warning, and `git diff --no-index --check` on the untracked director source returned only CRLF normalization warning.
  Rejected: claiming a full compile or scanner rerun while CPU was pegged at 100%.
  Estimate: audit only.

## Loop 11 - Origin Shift Apply Fence

- [x] Runtime spawn stale-origin bug fenced.
  DOD: `DirectorInputDTO.OriginShiftSequence` captures the owner-published origin generation, `DirectorSelectionDTO.OriginShiftSequence` carries it across Burst selection, and `ApplyCompletedSelection` recomputes local `runtimeSpawn` from `SpawnAup - cachedCurrentOrigin` when the sequence changed before LateFrame apply.
  Rejected: trusting scheduled `RuntimeSpawn` across an arbitrary origin rebase; querying scene transforms during apply.
  Estimate: 1-4 us only on origin-shift mismatch; prevents wrong-local-position activation after rebase.
- [x] Hot origin polling reduced to cold/bootstrap routes.
  DOD: runtime cold tick reads `_cachedFloatingOriginOffset`; `IOriginShiftListener.OnOriginShift` updates offset/sequence from `OriginShiftEventData.NewTotalOffsetDouble`; `HectonFloatingOrigin.CurrentTotalOffsetDouble` remains only in cold snapshot refresh and editor gizmo local conversion.
  Rejected: polling `GlobalRegistry.FloatingOrigin` through `CurrentTotalOffsetDouble` every director cold tick.
  Estimate: 1-5 us saved per director cold input refresh plus lower owner-route drift risk.
- [x] Selection DTO layout revalidated.
  DOD: `DirectorSelectionDTO` expanded from 128 to 144 bytes with `OriginShiftSequence@128`, `_pad0@132`, `_pad1@136`; `ValidateLayout()` now asserts size 144 and the sequence offset.
  Rejected: overloading `Flags`, `Frame`, or `SectorHash` with origin generation bits.
  Estimate: +16 bytes per selection row; no hot allocation.
- [x] Black box origin generation recorded.
  DOD: `DirectorTelemetryEntry.OriginShiftSequence@124` records the same owner-published origin generation in the 300-frame ring without increasing telemetry size.
  Rejected: leaving the origin generation only in transient selection state.
  Estimate: 0 extra bytes; one uint store per telemetry record.
- [x] Editor gizmo legacy conversion removed.
  DOD: `StressDrivenSpawnDirectorGizmo` now uses local AUP double-subtraction helper instead of `HectonFloatingOrigin.ToRuntimePosition(...)`.
  Rejected: leaving adjacent SHINOBU_253 surface on the old conversion helper.
  Estimate: editor-only.
- [x] Build gate respected after origin fence.
  DOD: checked `dotnet`/`csc` processes and sampled CPU; CPU returned `100.00`, so full dotnet rebuild was not launched.
  Rejected: violating the explicit >50% CPU build rule.
  Estimate: hardware hygiene only.

## Loop 12 - Controlled Cold Initialization

- [x] Uninitialized selection/counter bootstrap hardened.
  DOD: counter initialization now uses `CounterInitializedMagic=0x253D1A0F` instead of accepting any nonzero garbage value from `UninitializedMemory`.
  Rejected: trusting `CounterInitialized == 1` in a raw native lane.
  Estimate: cold boot only; prevents undefined selection/counter state from reaching LateFrame.
- [x] Full owned lanes receive deterministic cold defaults.
  DOD: cold initialization clears candidates, selection, telemetry, owned slots, inventory tickets, and spawn debug, then writes a controlled input row with cached AUP origin, forward vector, turbidity, quality, world seed, and origin sequence.
  Rejected: partial rule/counter-only initialization.
  Estimate: cold boot writes roughly 400 DTO rows; 0 us player hot-path cost.

## Loop 13 - Dispatcher Fence Proof

- [x] LateFrame nonblocking completion route audited.
  DOD: `SystemDispatcher.CompleteDispatcherLateFrame()` wraps all `ILateFrameTickable.LateFrameTick()` calls with `DispatcherJobFence.BeginLateFrameSwapWindow()` / `EndLateFrameSwapWindow()`. `StressDrivenSpawnDirector.LateFrameTick()` uses `TryComplete(..., forceComplete:false)`, which returns false when `handle.IsCompleted` is false and only finalizes an already-complete handle inside that dispatcher-owned swap window.
  Rejected: arbitrary main-thread `JobHandle.Complete()` and same-frame schedule/readback loops.
  Estimate: no added runtime cost; blocks a hidden-stall regression class.
- [x] Forced completion scope remains teardown-only.
  DOD: the only forced director completion is in `Dispose()`, after `_jobScheduled` is true and before unlocking Vault writer lanes. Runtime late-frame apply remains non-forced.
  Rejected: forcing completion in the normal `LateFrameTick()` path.
  Estimate: 0 us normal path; teardown safety only.

## Loop 14 - Hot-Swap Defaults Hygiene

- [x] Cold defaults consume the resolved Vault parameter.
  DOD: `InitializeColdDefaults` and `InitializeInputDefaults` now receive the `IDataVault vault` parameter already validated by `EnsureVaultState`, and quality defaults resolve through that object instead of the mutable `_vault` field.
  Rejected: reading `_vault` during cold initialization after a DataVault hot-swap callback.
  Estimate: 0 us hot path; removes a stale-field edge in bootstrap/hot-swap paths.
- [x] Prompt recall gate rerun.
  DOD: CLI regex re-extracted the SHINOBU_253 block from `CURRENT_BATCH.md` as 13,807 chars with exactly 20 `Task NN:` headers.
  Rejected: relying on chat compression state.
  Estimate: 900 us audit only.

## Loop 15 - Origin Snapshot Validity Fence

- [x] Sub-agent static audit executed locally under no-edit/no-build constraints, then primary agent integrated fixes.
  DOD: scoped scans covered `StressDrivenSpawnDirector.cs`, `StressDrivenSpawnDirectorGizmo.cs`, `IOriginShiftListener`, `OriginShiftEventData`, `HectonFloatingOrigin`, and asmdef ownership. `IOriginShiftListener.OnOriginShift(in OriginShiftEventData)` matches the director, `OriginShiftEventData.NewTotalOffsetDouble` and `Sequence` are present, and scoped forbidden scans returned no direct `.Complete()`, LINQ, `foreach`, Unity random/time, scene query, `TryGetLatestCreated`, or sibling runtime asmdef edge.
  Rejected: running `dotnet build` under the active CPU gate.
  Estimate: 1.4 ms static audit only.
- [x] Invalid origin snapshot now fails closed before candidate generation.
  DOD: `_floatingOriginSnapshotValid` tracks finite owner-published origin state. `InitializeInputDefaults` and `RefreshColdInputs` set `InputFlagOriginInvalid` when the origin snapshot is invalid, and `EvaluateSpawnConditionsJob` writes zero candidates and returns when that flag is present.
  Rejected: overloading cooldown/biome counters as a suppression side-channel, because that would create stale gameplay state after the origin recovers.
  Estimate: one bit-test per cold evaluation; prevents wrong-origin spawn activation.
- [x] Late apply now refuses nonfinite/currently invalid origin snapshots.
  DOD: `ApplyCompletedSelection` returns before cognition activation if `_floatingOriginSnapshotValid` is false or the cached origin is nonfinite; sequence mismatch still recomputes local runtime spawn from `SpawnAup - cachedCurrentOrigin`.
  Rejected: using last-known local `RuntimeSpawn` after an invalid origin update.
  Estimate: one validity branch plus finite check on apply frames.
- [x] Layout validator drift closed.
  DOD: `ValidateLayout()` now verifies `DirectorInputDTO.OriginShiftSequence@156`, `DirectorTelemetryEntry.OriginShiftSequence@124`, `DirectorSelectionDTO` size `144`, and `DirectorSelectionDTO.OriginShiftSequence@128`.
  Rejected: relying on the final size check alone.
  Estimate: editor/diagnostic only.
- [x] Build gate respected after validity patch.
  DOD: checked `dotnet`/`csc` processes and sampled CPU; no compiler processes existed but CPU returned `100`, so full dotnet rebuild was not launched.
  Rejected: violating the explicit >50% CPU build rule.
  Estimate: hardware hygiene only.

## Loop 16 - Pending Selection Apply Fix

- [x] Selection consume frame moved after cognition readiness and slot acquisition.
  DOD: `_lastAppliedFrame` is now written only for explicit fault consumption or after `PredatorCognitionDomain.Register()` succeeds. A valid selected spawn remains pending if borrowed cognition lanes are not ready, owned-slot capacity is full, origin snapshot is temporarily invalid, or the cognition pool returns no slot.
  Rejected: marking the frame consumed before readiness checks, which silently dropped valid selected spawns.
  Estimate: 0 us normal path; prevents a lost encounter after delayed cognition boot.
- [x] Loot-missing dump deferred to actual selection consume.
  DOD: `SelectionFlagLootMissing` now sets `_dumpFaultPending=2` only after a cognition slot is acquired for the selected frame, avoiding repeated dumps while the same selection waits pending.
  Rejected: dumping every LateFrame while a pending selection waits for borrowed lanes.
  Estimate: avoids repeated black-box IO on blocked apply frames.
- [x] Scoped scanner gate rerun after apply fix.
  DOD: `Dynamic_Spawn_Scanner.py` regenerated `WORLD_OPTIMIZATION_REPORT.json`; repo-wide legacy hits remain 174, but filtered hits for SHINOBU_253 touched files are 0, with `runtime_instantiate=0`, `mono_enemy_spawner=0`, and `scene_search=0`.
  Rejected: interpreting unrelated legacy `FaunaBrain`/World findings as this director's regression.
  Estimate: audit only.

## Loop 17 - Forensic Audit Append

- [x] Current self-audit appended to `Docs/AgentLogs/LOG_SHINOBU_253.md`.
  DOD: bottom log entry includes 20-task reconciliation, `DirectorSelectionDTO` byte math, continuous quality curve, Vault handle list, NoAlias/dependency graph, compile guard, Dear Lie complexity, and no-build verification.
  Rejected: chat-only audit text that would be lost after context compaction.
  Estimate: documentation only.

## Loop 18 - CSV Cold Writer Fence

- [x] Initial cold CSV ingest now uses the same Vault writer fence as editor reload.
  DOD: `TryLoadRulesCsvCold(vault, forceReload:false, locksHeld:false)` acquires `Rules -> RuleLinks -> Counters -> CsvScratch` before mutating scratch/rule/link/counter lanes. `TryReloadRulesCold` passes `locksHeld:true` after acquiring the same lock prefix.
  Rejected: mutating cold CSV lanes with plain resolved arrays just because no director job was scheduled yet.
  Estimate: cold/editor only; removes a partial-table race/authority violation.

## Loop 19 - Black Box Dump Stride Fix

- [x] Dump payload now matches `DirectorTelemetryEntry` stride after AUP blit conversion.
  DOD: `DumpBlackBoxCold()` writes both `AbsoluteUniversePositionBlit128` rows and both padding ulongs, so each telemetry record emits 192 bytes matching `UnsafeUtility.SizeOf<DirectorTelemetryEntry>()`.
  Rejected: header stride `192` with only 176 payload bytes, which corrupts forensic replay after the AUP blit conversion.
  Estimate: +16 bytes per telemetry row in dump only after AUP blit conversion; 0 us normal runtime.

## Loop 20 - Flag Constant Audit

- [x] Burst job flag writes/readbacks now use named constants.
  DOD: input, selection, owned-slot, and telemetry flag constants are `internal const`; Burst jobs reference `StressDrivenSpawnDirector.*Flag*` instead of `1u/2u/4u/8u/16u` literals.
  Rejected: magic numeric flags in deterministic state and telemetry paths.
  Estimate: 0 us; compile-time constants only.
