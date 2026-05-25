# Rationale_SHINOBU_253

Date: 2026-05-21
Status: POLISH PASS ACTIVE; ORIGIN FENCE + CONTROLLED COLD INIT + DISPATCHER FENCE PROOF + HOTSWAP DEFAULTS HYGIENE + ORIGIN SNAPSHOT VALIDITY FENCE + PENDING SELECTION APPLY FIX + CSV COLD WRITER FENCE + BLACKBOX STRIDE FIX + FLAG CONSTANT AUDIT + POST-JOB WRITER FENCE + AUP BLIT AUTHORITY + MONOLITH RUNTIME GATE + READER FENCE; FULL COMPILE NOT RE-LAUNCHED

## Decision 00 - Scope Ownership

Problem: Static GameObject spawn flows and managed runtime allocation would break the Echelon 3 fauna authority route and cause visible spawn pops.
Solution: Build an unmanaged, Burst-compatible spawn director surface with explicit DTO layout, deterministic RNG, AUP double precision spawn coordinates, and fixed telemetry.
Rejected Alternatives: Unity Transform spawn points, prefab instantiation, ScriptableObject reads in hot path, scene searches, and managed collections are rejected because they create GC, frame spikes, and static-world bias.
Scalability potential: Low uses tighter budget and fewer active entities; middle uses sparse mix; high expands candidate count; ultra spends saved CPU on richer hidden injection attempts without changing truth ownership.
Hardware Impact: MX350/i3 path avoids runtime Instantiate spikes and uses O(n) NativeArray passes; expected gain is removing multi-ms prefab/GC spikes from encounter starts.

## Decision 01 - New Director Instead Of Legacy Deletion

Problem: Legacy fauna compatibility files still expose spawn anchors and are likely consumed by other active agents.
Solution: Add a new Vault-owned `StressDrivenSpawnDirector` path that selects hidden AUP spawns and activates them through AICognition owner APIs after borrowed handle readiness, without deleting compatibility APIs.
Rejected Alternatives: Hard deleting `FaunaBrain.Compatibility.SetSpawnPoint` would break unknown branch owners and create integration churn; wrapping legacy spawners would preserve static-point bias.
Scalability potential: Low uses one apex slot and sparse candidates; middle keeps mixed predators; high expands candidate quality and probe count; ultra buys more hidden AUP probes and richer threat choice while truth ownership remains in DTOs.
Hardware Impact: Low-end i3/MX350 avoids prefab/Transform allocation spikes; expected encounter-start saving is 700-4,000 us depending on prefab complexity.

## Decision 02 - Explicit DTO Layout And Raw Burst Kernels

Problem: Spawn rules must survive ARM64 cache behavior and avoid CS1612/property mutation traps.
Solution: Use explicit-layout DTOs, direct fields, `NativeArrayOptions.UninitializedMemory`, generation handles, and pointer-based Burst `IJob`s inside dispatcher-owned completion.
Rejected Alternatives: Managed classes, ScriptableObject rule objects, properties over NativeArray elements, and LINQ scoring allocate or hide copies.
Scalability potential: Low trims candidate count and hidden probes; middle uses default capacities; high/ultra spend the same DTO route on more probes, not different schema.
Hardware Impact: Expected low-end gain is 20-80 us per cold evaluation plus stable cache-line behavior.

## Decision 03 - Dear Lie Spawn Placement

Problem: Static spawn points create predictable encounters and visible pop-in.
Solution: Compute hidden spawn AUP from player forward vector, frustum plane margins, turbidity-compressed fog radius, and cheap fake SDF clearance.
Rejected Alternatives: Navmesh spawn-point sampling, physics ray fan, and scene marker search are too slow and scene-dependent for a cold director.
Scalability potential: Low uses fewer probes and tighter fog radius; middle uses default SDF clearance; high increases max radius; ultra spends saved CPU on additional probes for better staging.
Hardware Impact: Probe loop stays roughly 30-120 us instead of scene query or physics fan spikes.

## Decision 04 - Continuous Budget Instead Of Quality Tiers

Problem: Binary quality switches would change gameplay truth and violate GlobalQualityWeight doctrine.
Solution: Threat budget is a continuous function of `GlobalQualityWeight` and thermal pressure; weak hardware substitutes swarm spam with a single high-threat species when budget is tight.
Rejected Alternatives: Low/High if-else spawn tables and fixed enemy counts mutate gameplay authority and CPU cost sharply.
Scalability potential: Low = one expensive but sparse threat, middle = mixed threats, high = more valid candidates, ultra = broader hidden staging and visual overkill from downstream presentation.
Hardware Impact: Reduces multi-entity cognition cost on i3/MX350 by roughly 150-600 us in loaded scenes.

## Decision 05 - Inventory Preload Ticket, Not Hot Loot Lookup

Problem: Spawn selection needs species loot readiness but DataMonolith readiness is absent in this checkout.
Solution: Write `InventoryPreloadTicketDTO` into a fixed Vault buffer and mark missing `static_data.h8bin` as a forensic/telemetry fault.
Rejected Alternatives: Synchronous loot table lookup, Resources load, or assuming loot exists would create hot-path IO or false success.
Scalability potential: Low issues one compact ticket; middle/high/ultra may preload richer loot metadata through the same ticket contract once DataMonolith exists.
Hardware Impact: Avoids 20-90 us hot lookup and prevents IO stalls; current blocker is missing file, not code route.

## Decision 06 - Sequential Buffer Lock Repair

Problem: Initial lock routine counted successful locks without stopping on middle failure, so a late buffer could remain locked if an earlier buffer failed.
Solution: Converted lock acquisition to strict ordered calls; first failure returns the exact prefix count and scheduling now requires all 12 locks.
Rejected Alternatives: Bitmask lock tracking would work but is unnecessary for this fixed small buffer set.
Scalability potential: Low through ultra all get deterministic lock/unlock behavior; capacity scaling does not change lock order.
Hardware Impact: Prevents rare deadlock/stall class; runtime cost is negligible.

## Decision 07 - Black Box Ring And Dump

Problem: AI director faults cannot be debugged from chat or transient logs.
Solution: Maintain 300 fixed telemetry entries and dump binary state on NaN/frustum fault or missing loot monolith.
Rejected Alternatives: `Debug.Log`, managed lists, or unbounded traces allocate and lose crash-local history.
Scalability potential: Same fixed footprint on all hardware; ultra does not expand diagnostic schema.
Hardware Impact: Fixed 38.4 KB telemetry payload plus 8-25 us write cost per cold tick.

## Decision 08 - Compile Wall Attribution

Problem: Generated Unity csproj references deleted Dynamic Decals and archived HectonWaterPhysics sources before compiler reaches reliable semantic validation.
Solution: Per fail-fast protocol, documented blocker and used static scanner plus syntax-class compiler scan for the new files; build servers were shut down.
Rejected Alternatives: Editing generated csproj, restoring unrelated third-party assets, or claiming clean compile.
Scalability potential: None; integration hygiene only.
Hardware Impact: No runtime impact; prevents wasting CPU on repeated failing builds.

## Decision 09 - Storm Runtime Dependency Removed

Problem: AIEcology/Core-side director read `StormPropagationDTO` directly from a sibling storm runtime lane, risking an asmdef cycle and compile-wall expansion.
Solution: Removed the storm DTO read. Weather stress now consumes the existing ocean weather Vault row and keeps deeper storm/fog integration to contract or scalar lanes when a first-party route is published.
Rejected Alternatives: Adding a Core-to-Storm runtime reference or duplicating the storm DTO locally; both would split authority or create a cycle.
Scalability potential: Low through ultra keep the same weather scalar route; richer storm visuals remain downstream shader/presentation work, not director truth.
Hardware Impact: 0 us direct runtime gain; prevents forced sibling recompiles and hidden managed dependency churn.

## Decision 10 - AICognition Borrowed Handles Only

Problem: The director previously allocated/growed AICognition mesofauna buffers, violating one owner -> one route.
Solution: Added named BufferIDs in `H8Memory`, converted director constants away from local numeric casts, and changed AICognition rows to existing generation-handle observations only. Spawn activation fails closed if cognition has not already booted.
Rejected Alternatives: Continuing to call `GetGenerationHandle` with `SystemID.AICognition` from AIEcology; this creates cross-owner memory authority.
Scalability potential: Low avoids allocation spikes; middle/high/ultra can consume larger cognition capacity only when cognition owner publishes it.
Hardware Impact: Avoids 10-70 us cold allocation/growth spikes and removes a potential lock/race class on i3/MX350.

## Decision 11 - Macro Ecosystem Contract Snapshot

Problem: Task 06 required macro biomass influence, but direct macro runtime calls would cross domain authority.
Solution: `DirectorInputDTO` now carries sector hash, prey/predator/capacity/toxin/temperature, and macro state hash. Cold input reads SHINOBU_116 contract mirrors from Vault; cached `IEcosystemDirectorService` is only a fallback.
Rejected Alternatives: Static biomass constants or direct `EcosystemSectorDTO` runtime dependency.
Scalability potential: Low collapses scoring to cheap scalar multipliers; middle/high/ultra can spend saved CPU on richer downstream presentation while the selection route remains scalar and deterministic.
Hardware Impact: Adds roughly 5-20 us cold read cost, but prevents wasted spawn/cull/cognition work in depleted sectors.

## Decision 12 - Deterministic Cadence Gate

Problem: Selection requested a spawn whenever a candidate existed; mock tension used frame/time seeds.
Solution: Mock tension uses integer `Hash3(sector, worldSeed, frame)` bit slicing for jitter. Selection applies `BaseSpawnRatePerMinute`, quality, tension, and deterministic roll before setting `RequestSpawn`.
Rejected Alternatives: Unity time, `UnityEngine.Random`, and every-cold-tick spawn activation.
Scalability potential: Low = rare apex/substitution events; middle = measured mixed threats; high/ultra = higher probability and richer hidden placement without schema mutation.
Hardware Impact: Prevents runaway cognition growth and removes mutable RNG state; low-end scenes can avoid 150-600 us of repeated AI work under stress.

## Decision 13 - H8DM Readiness, Not File Existence

Problem: Raw `File.Exists(static_data.h8bin)` is not a loot-readiness proof.
Solution: Director now trusts only loaded `H8StaticDataArena` LootCdf rows with a nonzero table hash before marking loot ready.
Rejected Alternatives: File existence, header-only H8DM validation, section-table-only H8DM validation, synchronous loot lookup, or assuming fallback loot validity.
Scalability potential: Low through ultra share the same runtime arena proof gate; richer loot tables can scale inside Data Monolith without changing director DTOs.
Hardware Impact: Cold arena read only; prevents false activation that would trigger downstream fault churn.

## Decision 14 - Editor Facade And Debug Proof

Problem: The tuner used IMGUI and the gizmo drew only one latest sphere, missing required radius/history proof.
Solution: Tuner graph uses UI Toolkit `generateVisualContent`/`Painter2D`; gizmo reads fixed telemetry into a static editor scratch array and draws min radius, despawn radius, latest hidden placement, and red injected AUP history.
Rejected Alternatives: IMGUIContainer, GUILayout/Handles graph, or scene marker GameObjects.
Scalability potential: Editor-only; low/middle/high/ultra runtime paths do not inherit debug allocation.
Hardware Impact: 0 us player runtime; editor repaint cost is bounded and no runtime object markers are created.

## Decision 15 - Reloadable CSV And Loot Section Proof

Problem: A one-shot CSV load forces a restart for balancing, and H8DM header validation alone can falsely mark loot data ready.
Solution: Added `TryReloadRulesCold()` behind a Vault writer fence for counters and exposed it through the UI Toolkit tuner. The earlier Data Monolith file fallback has been superseded by the runtime `H8StaticDataArena` LootCdf gate.
Rejected Alternatives: Periodic file IO every cold tick, editor IMGUI reload, header-only validation, section-table-only validation, and raw file existence.
Scalability potential: Low through ultra receive the same deterministic rule table after reload; no runtime schema or authority route changes.
Hardware Impact: 0 us player runtime for the editor reload button; runtime arena proof avoids false-ready downstream fault churn.

## Decision 16 - Reload Fence Covers Whole Rule Table

Problem: CSV reload reset counters under a writer fence, but the parser mutates rules, rule links, scratch bytes, and counters as one table update.
Solution: `TryReloadRulesCold()` now fails closed unless all reload handles exist and it locks `Rules -> RuleLinks -> Counters -> CsvScratch` before reset and parse, releasing the exact prefix in reverse order on failure.
Rejected Alternatives: Counters-only lock, periodic runtime file polling, or allowing editor reload during a scheduled director job.
Scalability potential: Low through ultra keep identical rule DTO layout and authority route; designers can rebalance spawn costs without C# reload, and runtime truth does not branch by quality tier.
Hardware Impact: 0 us player runtime; avoids partial-table editor reload corruption that would otherwise cascade into wasted spawn/cull/cognition work.

## Decision 17 - Atomic CSV Commit And Vault Fence

Problem: Subagent audit found that a failed reload could reset CSV state and clear rule/link tables while leaving the old rule count in counters.
Solution: `TryLoadRulesCsvCold` now accepts an explicit `forceReload` flag. Forced reload preserves prior counters until success. `ParseSpawnRulesCsv` runs a count/validation pass before clearing tables, so malformed CSV cannot zero active rules. Reload also rejects Vault allocation and compaction fences before locking.
Rejected Alternatives: Managed staging arrays, temporary NativeArrays outside Vault, or accepting counters-first failure state.
Scalability potential: Low through ultra keep the same table shape and deterministic selection math. Designers can iterate balance data without risking runtime rule collapse or quality-dependent schema mutation.
Hardware Impact: 0 us player runtime; avoids corrupt cold/editor reload causing 80-400 us of wasted spawn/cull work across owned slots.

## Decision 18 - Contract Boundary Residual

Problem: Static audit still sees root-assembly concrete references to ocean weather and AUP runtime conversion types.
Solution: No StormPropagation runtime DTO route is used; weather remains on the existing `ShinobuOceanWeatherState` row, macro biomass uses Core contract mirrors, and AUP conversion remains existing cognition boundary glue. Marked relocation of weather/AUP blit DTOs into Core.Contracts as a pending owner-contract task rather than duplicating layouts locally.
Rejected Alternatives: Duplicating `WeatherStateDTO` locally, reading the same Vault row through an incompatible mirror type, or deleting weather influence from the director.
Scalability potential: Low through ultra keep scalar weather/turbidity influence without changing DTO identity. Future contract relocation can reduce compile-wall risk without runtime behavior changes.
Hardware Impact: 0 us direct runtime gain; risk is integration/compile-wall hygiene, not frame cost.

## Decision 19 - AUP Context Polling Reduction

Problem: The runtime director still used legacy context getters for origin/runtime conversion after the Burst hidden-placement job had already produced `RuntimeSpawn` from the owner-phase `FloatingOriginOffset`.
Solution: Remove `GlobalSignals.CurrentRuntimeOriginAup()` and `HectonFloatingOrigin.ToRuntimePosition(...)` from `StressDrivenSpawnDirector`. Cold input consumes the cached origin snapshot maintained by `IOriginShiftListener`; late apply consumes `DirectorSelectionDTO.RuntimeSpawn` only when the origin sequence still matches, otherwise it recomputes the local delta from `SpawnAup - cachedCurrentOrigin`. Cognition input reconstructs `FloatingOriginOffset` as `SpawnAup - RuntimeSpawn` and packs `AbsoluteUniversePositionBlit128` locally using `HectonPhysicsContract.AupSectorSizeMetersDouble`.
Rejected Alternatives: Calling the current origin again during late apply, duplicating the World AUP struct locally, or moving AUP contracts from root/Core into Core.Contracts inside this domain patch.
Scalability potential: Low through ultra keep identical DTO layout and spawn truth. The change removes context drift risk across all devices; higher tiers still spend budget on hidden probes and downstream presentation, not extra owner lookups.
Hardware Impact: Direct saving is small, estimated 1-5 us in cold/late apply. Larger value is removing a legacy getter route and preventing origin mismatch between scheduled Burst selection and managed cognition activation.

## Decision 20 - Compile Boundary Proof

Problem: The prior status labeled direct Atmosphere/World DTO use as pending without distinguishing namespace coupling from asmdef coupling.
Solution: CLI asmdef walk shows `StressDrivenSpawnDirector.cs`, `ShinobuOceanSurfaceAtmosphereContracts.cs`, `PersistentWorldRegistry.cs`, and `HectonFloatingOrigin.cs` all resolve to `Assets/_Project/Scripts/Hecton8.Core.asmdef`; `H8Memory.cs` is in `Hecton8.Core.Memory.asmdef`, already referenced by Core. The remaining `WeatherStateDTO` read is the existing root/Core weather Vault row, not a StormPropagation runtime dependency.
Rejected Alternatives: Adding a sibling atmosphere runtime reference, creating a local mirror DTO for the same Vault row, or claiming that namespace alone proves a compile-wall breach.
Scalability potential: Low through ultra unaffected; this is assembly routing proof, not gameplay behavior.
Hardware Impact: 0 us frame saving. It protects iteration time by avoiding unnecessary asmdef edits and by keeping the actual future work scoped to a route-card relocation of shared weather/AUP contract declarations.

## Decision 21 - Origin Shift Apply Fence

Problem: Subagent audit found that `RuntimeSpawn` was computed in the Burst hidden-placement job from the origin snapshot active at schedule time, then consumed later in `LateFrameTick`. A floating-origin rebase between those phases would keep `SpawnAup` correct but pass stale local runtime coordinates to AICognition.
Solution: Add an owner-published origin generation field. `DirectorInputDTO.OriginShiftSequence` captures the cached `OriginShiftEventData.Sequence`; `DirectorSelectionDTO.OriginShiftSequence` carries it through selection; `DirectorTelemetryEntry.OriginShiftSequence` records it in the 300-frame ring; `ApplyCompletedSelection` recomputes `runtimeSpawn = SpawnAup - _cachedFloatingOriginOffset` when the generation changed before apply. The director now implements `IOriginShiftListener` and consumes `_cachedFloatingOriginOffset` in cold tick instead of polling `CurrentTotalOffsetDouble` every refresh.
Rejected Alternatives: Trusting `RuntimeSpawn` across origin shifts, overloading `Flags`/`Frame`/`SectorHash` to hide generation bits, querying scene transforms during apply, or expanding this patch into core floating-origin internals.
Scalability potential: Low through ultra keep the same AUP truth and same continuous quality math. Low hardware pays only a uint compare in the normal path; high/ultra still spend budget on hidden probe quality, not extra owner lookup.
Hardware Impact: Normal path cost is a single sequence compare. Mismatch path costs one double3 subtraction and float cast, estimated 1-4 us only on rebase frames. Prevents wrong-local-position activation that would waste cognition work and create visible spawn errors after origin shifts.

## Decision 22 - Controlled Cold Initialization

Problem: The director allocates Vault lanes with `NativeArrayOptions.UninitializedMemory`, but the boot sentinel accepted `CounterInitialized == 1`. A fresh counter lane could contain arbitrary nonzero bytes and skip cold defaults, leaving `DirectorSelectionDTO.RequestSpawn` or counters undefined before the first scheduled job.
Solution: Replace the weak sentinel with `CounterInitializedMagic=0x253D1A0F`, and initialize all owner lanes on mismatch: counters, tuning, frustum planes, rules, links, candidates, selection, telemetry, owned slots, inventory tickets, spawn debug, and the first input row. The input row now starts from the cached floating origin and origin sequence.
Rejected Alternatives: Switching every Vault lane to `ClearMemory`, trusting raw native memory, or clearing only counters/rules while leaving selection and telemetry undefined.
Scalability potential: Low through ultra share identical boot state and DTO layout. No gameplay truth depends on quality; `GlobalQualityWeight` only seeds tuning/input scalars after deterministic initialization.
Hardware Impact: Cold boot writes roughly 400 small DTO rows. Player hot path cost is 0 us; failure avoided is an undefined spawn/cull/telemetry state that could waste hundreds of microseconds or activate a wrong cognition slot.

## Decision 23 - Dispatcher-Owned Completion Window Proof

Problem: The director has to unlock Vault writer lanes and apply the selected cognition request after its chained Burst jobs finish, but a hidden main-thread wait would violate the dispatcher dependency law.
Solution: Audited `DispatcherJobFence.TryComplete` and the caller path. `SystemDispatcher.CompleteDispatcherLateFrame()` opens the late-frame swap window before invoking every `ILateFrameTickable.LateFrameTick()`. The director calls `TryComplete(ref _activeHandle, forceComplete:false)` only there; that helper returns false while `handle.IsCompleted` is false and calls `Complete()` only to finalize an already-complete handle. The only forced call remains in `Dispose()` for teardown before unlocking Vault lanes.
Rejected Alternatives: Calling `JobHandle.Complete()` directly in `LateFrameTick`, forcing completion after scheduling, or leaving writer lanes locked until a later uncontrolled phase.
Scalability potential: Low through ultra retain the same nonblocking schedule/apply cadence. Low devices can let the job spill a frame instead of stalling; high/ultra can consume richer candidate probes when complete without changing authority route.
Hardware Impact: Normal path adds no work beyond the existing `IsCompleted` poll. It prevents a hidden stall class that could consume 100+ us under thermal pressure.

## Decision 24 - Cold Defaults Use Resolved Vault

Problem: Controlled initialization accepted a validated `IDataVault vault`, but the default tuning/input rows still resolved `GlobalQualityWeight` through the mutable `_vault` field.
Solution: Thread the validated Vault parameter through `InitializeColdDefaults` and `InitializeInputDefaults`; quality defaults now resolve against the same Vault object whose handles were just allocated/resolved.
Rejected Alternatives: Keeping the field read because it is cold path, or adding another GlobalRegistry lookup during initialization.
Scalability potential: Low through ultra keep identical quality math and DTO layout. The change only removes a stale-reference edge during DataVault hot-swap/bootstrap.
Hardware Impact: 0 us hot path. Cold path cost is unchanged; risk reduction is preventing defaults from reading a stale/null field during Vault replacement.

## Decision 25 - Origin Snapshot Validity Fence

Problem: The origin-sequence patch still had two sharp edges: a nonfinite `OriginShiftEventData.NewTotalOffsetDouble` could be converted to a zero local origin, and cold boot fallback could read a current offset while leaving origin validity implicit. That could let a scheduled selection reach cognition with an invalid or stale local-space basis.
Solution: Add `_floatingOriginSnapshotValid` as a cold owner-snapshot guard, refresh the cached sequence through `HectonFloatingOrigin.CurrentShiftSequence` when the latest event is missing/stale, set `InputFlagOriginInvalid` in cold input rows when the snapshot is invalid, suppress `EvaluateSpawnConditionsJob` candidate generation on that flag, and make `ApplyCompletedSelection` fail closed before cognition activation when the current snapshot is invalid/nonfinite. `ValidateLayout()` now proves the input, selection, and telemetry origin-sequence offsets.
Rejected Alternatives: Mutating spawn cooldown or biome transition counters to block invalid-origin spawns, because that would store a temporary infrastructure fault as gameplay state; polling scene/global origin state during late apply; expanding the patch into core floating-origin internals.
Scalability potential: Low devices pay one bit-test in the cold evaluation job and avoid wasted cognition activation. Middle/high/ultra keep the same continuous `GlobalQualityWeight` math and can spend saved error-recovery budget on richer hidden placement/visual follow-through. DTO layout, save identity, and authority route do not vary by quality.
Hardware Impact: Normal path cost is one validity branch and one sequence compare. Invalid-origin frames skip candidate evaluation early, avoiding downstream spawn/cull/cognition work estimated at 40-250 us on i3/MX350-class hardware when an origin rebase fault occurs.

## Decision 26 - Pending Selection Apply Is Not Consumed Prematurely

Problem: `ApplyCompletedSelection` wrote `_lastAppliedFrame` before checking borrowed cognition readiness, owned-slot capacity, current origin validity, and cognition slot availability. If AICognition booted one frame late, a valid `DirectorSelectionDTO` stayed in Vault but was ignored forever because its frame matched `_lastAppliedFrame`.
Solution: Move `_lastAppliedFrame` to actual consume points: explicit fault consumption, or after `PredatorCognitionDomain.Register()` returns a slot. Valid selected spawns now remain pending across delayed cognition readiness or temporary origin invalidity. `SelectionFlagLootMissing` dump scheduling also moved to the successful consume point so the same pending selection does not dump every LateFrame while blocked.
Rejected Alternatives: Clearing the selection on readiness failure, forcing AICognition allocation from the director, or registering a dummy cognition slot to consume the request.
Scalability potential: Low through ultra keep the same DTO route and quality math. Low hardware can defer activation without losing the encounter; high/ultra keep richer hidden staging once downstream capacity becomes available.
Hardware Impact: Normal path cost is unchanged. Prevents lost-spawn behavior and avoids repeated black-box dump IO on blocked apply frames.

## Decision 27 - Disk-Resident Self Audit

Problem: The final architectural proof must survive chat compaction and cannot live only in the conversation.
Solution: Append a fresh `<SELF_AUDIT>` block to `Docs/AgentLogs/LOG_SHINOBU_253.md` with task reconciliation, DTO offsets, continuous quality curve, Vault ownership, dependency graph, compile guard, Dear Lie proof, and verification results.
Rejected Alternatives: Chat-only report or relying on the older audit entry that predates the origin-validity and pending-selection fixes.
Scalability potential: No runtime effect; audit records how low/middle/high/ultra behavior remains continuous and schema-stable.
Hardware Impact: 0 us runtime; improves integrator traceability and crash forensics.

## Decision 28 - CSV Cold Ingest Uses Vault Writer Fence

Problem: The editor reload path locked `Rules`, `RuleLinks`, `Counters`, and `CsvScratch`, but the initial cold CSV load called the same parser through plain resolved arrays. That left a cold/bootstrap mutation route outside the first-party Vault writer proof.
Solution: Add a `locksHeld` parameter. Normal cold ingest now acquires the exact same `Rules -> RuleLinks -> Counters -> CsvScratch` lock prefix before reading the file into Vault scratch and committing parsed rule/link rows. The reload entry point passes `locksHeld:true` only after it already owns that prefix.
Rejected Alternatives: Assuming cold tick is single-writer by convention, or allocating a managed staging array to avoid mutating rules before lock.
Scalability potential: Low through ultra unchanged. The lock exists only for cold/editor CSV ingest; gameplay truth and quality scaling are identical after the table is committed.
Hardware Impact: 0 us hot path. Cold ingest adds bounded lock calls and removes a partial-table authority race that could cascade into wasted spawn/cull work.

## Decision 29 - Black Box Dump Stride Matches Telemetry DTO

Problem: `DirectorTelemetryEntry` now stores two `AbsoluteUniversePositionBlit128` rows and has `UnsafeUtility.SizeOf<DirectorTelemetryEntry>() == 192`, but `DumpBlackBoxCold()` could still emit a shorter row if padding was not written. Any forensic reader trusting the header stride would desynchronize after the first row.
Solution: Emit both 48-byte AUP blit rows and both tail padding ulongs, restoring the emitted payload to 192 bytes per entry.
Rejected Alternatives: Reducing the dump header stride or omitting padding, because the DTO is the authoritative binary payload and origin generation/AUP rows are required for rebase forensics.
Scalability potential: No gameplay effect. Low through ultra share the same fixed 300-row black-box schema.
Hardware Impact: 0 us normal path. Fault dump writes 4,800 extra padding/alignment bytes across 300 rows after the AUP blit conversion and preserves replay alignment.

## Decision 30 - Flag Constants Are Named Across Burst Jobs

Problem: Burst job code still used numeric flag literals for external-input, selection, owned-slot, and telemetry state. The values matched today, but forensic reasoning and future layout changes would be brittle.
Solution: Widen those constants to `internal const` and replace job literals with named `StressDrivenSpawnDirector.*Flag*` references. The compiler still folds them to constants; no runtime lookup is introduced.
Rejected Alternatives: Leaving magic numbers in deterministic state code or duplicating local constants inside each job.
Scalability potential: No behavior change. Low through ultra keep the same bit layout; auditability improves.
Hardware Impact: 0 us. Compile-time constants only.

## Decision 31 - Post-Job Commit Stays Under Writer Fence

Problem: `LateFrameTick` completed the scheduled handle and could unlock Vault writer lanes before telemetry microsecond patching, cull apply, selection apply, and black-box dump routing touched the same rows.
Solution: Store the exact `_lockedVault` used for scheduling and release locks only after all post-job commit work finishes. If DataVault is hot-swapped, stale commit is skipped and the original lock owner is still released in `finally`.
Rejected Alternatives: Unlocking immediately after completion, reacquiring ad hoc write locks after completion, or unlocking against the current registry Vault after a service replacement.
Scalability potential: Low through ultra keep the same job chain and DTO route; higher tiers can spend more budget on probes without widening the write window semantics.
Hardware Impact: One reference compare plus existing unlock path; prevents stale writes/deadlock class rather than saving frame time.

## Decision 32 - AUP Blit Authority In Director DTOs

Problem: Raw `double3` AUP fields inside director Native DTOs were not the project AUP payload contract and made replay/origin math depend on implicit absolute-coordinate interpretation.
Solution: Convert `DirectorInputDTO`, `DirectorSelectionDTO`, `DirectorOwnedSlotDTO`, `DirectorTelemetryEntry`, and `DirectorSpawnDebugDTO` AUP authority fields to `AbsoluteUniversePositionBlit128`. All local float math subtracts a packed origin first; raw `double3` remains only for the cold cached origin snapshot and the existing cognition bridge field.
Rejected Alternatives: Keeping raw `double3` in Native DTOs, casting absolute doubles to float runtime positions, or relocating the global AUP contract in this domain patch.
Scalability potential: Low through ultra keep identical authority payloads and quality math. Low devices avoid wrong-local activation; ultra can spend hidden-probe budget without changing save identity.
Hardware Impact: Increases AUP-bearing row sizes (`DirectorInputDTO=208`, `DirectorSelectionDTO=192`, `DirectorOwnedSlotDTO=80`, `DirectorTelemetryEntry=192`, `DirectorSpawnDebugDTO=128`) but removes a high-cost correctness failure at 100 km scale.

## Decision 33 - Data Monolith Runtime Arena Gate

Problem: Header or section-table validation proves only that bytes exist on disk, not that the runtime Data Monolith importer hydrated gameplay loot data.
Solution: `ResolveDataMonolithReadyCold()` now returns true only when `H8StaticDataArena.IsLoaded` exposes nonempty `LootCdf` records and the first table hash is nonzero. The dead file/header validator was deleted from the director.
Rejected Alternatives: `File.Exists`, H8DM header scan, H8DM section-table scan, or synchronous file parsing inside the director.
Scalability potential: Low through ultra use one runtime data proof; table size can scale in the monolith without changing director DTO layout.
Hardware Impact: Cold arena check only; prevents false-ready loot activation and downstream fault churn.

## Decision 34 - Public Reader Fence And Hash Mock Jitter

Problem: Editor/debug readers could observe Vault rows while the scheduled writer chain was in flight, and the fallback mock path still carried mutable RNG state.
Solution: Public readers return no data when `_jobScheduled` is true. Mock stress jitter uses integer hash bit slicing from sector/world/frame, leaving no `Unity.Mathematics.Random` or `NextFloat` in touched files.
Rejected Alternatives: Completing the job from a reader, cloning debug arrays, or holding RNG state in a Native lane.
Scalability potential: Low through ultra keep identical gameplay; debug surfaces fail closed instead of forcing synchronization.
Hardware Impact: Reader fence prevents hidden main-thread stalls; hash jitter is a few integer ops with no mutable RNG state.
