# SHINOBU_146 Rationale - Mesofauna Behavioral State Machine

Date: 2026-05-19
Status: HARDENED / EXTERNAL COMPILE WALL

## Decision 0 - Architecture Entry

Problem: Existing assignment targets mid-predator AI previously described as `NavMeshAgent` plus OOP state classes. That model creates managed heap pointer chasing, virtual dispatch, and invalid 2.5D navigation assumptions in underwater 3D space.

Solution: Implement a source-backed replacement inside the fauna cognition domain: explicit 64-byte unmanaged DTO, byte FSM state, Burst jobs, AUP-local steering, flat spatial hash target acquisition, SDF repulsion, continuous quality-weight time slicing, VAT/IK visual-sync data, and 300-frame telemetry.

Rejected Alternatives: Standard Unity `NavMeshAgent` is rejected because it is not volumetric and imposes bake/update costs. Classic `State_Wander` / `State_Attack` classes are rejected because polymorphic managed state is not Burst-compatible and breaks cache locality. `Physics.OverlapSphere` is rejected because target lookup must use spatial hash snapshots.

Scalability potential: Low uses small search radius, sparse target refresh, cheaper visual/scent sampling, and smooth velocity continuation. Middle increases acquisition cadence and SDF checks. High adds richer scoring and target prediction cadence. Ultra spends saved CPU on broader vision radius, more frequent scent gradient reads, and richer VAT/IK state output without bloating authoritative DTOs.

Hardware Impact: Expected low-end i3/MX350 gain comes from replacing managed state dispatch and NavMesh queries with linear 64-byte DTO iteration. Static estimate only until profiler proof: 50 predators avoid OOP/Unity navigation stalls and keep brain updates sliced to a controllable fraction of the frame.

Evidence: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; relevant mandates loaded from `.agents-skills`; runtime/profiler proof unavailable in shell.

## Decision 1 - Flat Vault Spatial Hash Instead Of NativeHashMap

Problem: Task 07 requests spatial hashing and Task 18 names a `NativeHashMap`, but Phase 3 Vault Law forbids persistent private Native containers and the user explicitly asks for flat byte/array state. A persistent `NativeParallelMultiHashMap` stored as a private field would fragment ownership and create disposal/order risks.

Solution: Allocate two Vault lanes, `MesofaunaTargetHashBucketHeads` and `MesofaunaTargetHashNext`, and build a fixed bucket linked-list hash in `BuildMesofaunaTargetSpatialHashJob`. Species CSV data uses a Vault-backed flat open-addressed `MesofaunaSpeciesProfileDTO[64]` table plus count lane.

Rejected Alternatives: `Physics.OverlapSphere` was rejected for O(n) broadphase and managed engine coupling. Private `NativeHashMap` was rejected for H-PHI ownership violation. Managed dictionary/list was rejected for GC and non-Burst access.

Scalability potential: Low quality keeps the same data shape but updates fewer brains via slice modulo; high/ultra can raise radius and search cadence without changing ABI or reallocating.

Hardware Impact: On i3/MX350, fixed bucket arrays avoid allocator pressure and hash-map metadata churn. Estimated spatial lookup drops from broadphase/O(n) style scans to bounded adjacent-bucket traversal, roughly 60-180 us/frame saved at 256 fauna slots depending density.

## Decision 2 - Deterministic Byte FSM With Explicit Switch

Problem: The first code pass risked behaving like a priority if-chain rather than the required state-owned FSM. That weakens auditability and makes later rollback diffing harder.

Solution: `MesofaunaBehaviorJob.Execute` now uses `switch(state.CurrentState)` over Idle/Search/Hunt/Flee/TrackScent. State transitions are byte writes to `MesofaunaStateDTO.CurrentState`/`PreviousState`, with `StateTimerTicks` as deterministic tick counter. Jobs are `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.

Rejected Alternatives: OOP state objects and virtual methods were rejected for V-table/cache misses. Keeping only if/else priority logic was rejected because the XML explicitly mandates switch-governed transitions.

Scalability potential: Low keeps movement continuity every frame while only switching/evaluating expensive target logic for one modulo bucket. Ultra evaluates every frame and can feed richer visual sync without expanding authoritative state.

Hardware Impact: Expected reduction vs managed state path is 45-95 us/frame for 50 predators, static only until profiler proof.

## Decision 3 - AUP-Local Target Math

Problem: A 100 km world makes direct runtime-float target deltas unsafe at origin-shift edges. Direct target selection and interception must not compare absolute floats.

Solution: Direct target, spatial target, scent target, and intercept calculations subtract target AUP from predator AUP, then cast the local delta to `float3`. `ResolveInterceptDirection` predicts in local delta space (`delta + targetVelocity * leadSeconds`) instead of subtracting world floats after prediction.

Rejected Alternatives: Using `targetPosition - input.Position` as authoritative distance was rejected except where `targetPosition` has already been reconstructed from AUP-local delta. Double3 math for all steering was rejected after the local delta because it burns ALU without improving the final float kinematic solver.

Scalability potential: Same math path across Low/Middle/High/Ultra; quality only changes lead horizon/radius/cadence, not determinism.

Hardware Impact: CPU impact is neutral to slightly positive after caching self AUP in spatial lookup. Correctness gain is mandatory: prevents predator steering jitter/desync near sector boundaries.

## Decision 4 - Dear Lie Visual Sync

Problem: Per-predator Animator graph blending would spend CPU on a visual problem and is not rollback-authoritative.

Solution: Authoritative FSM emits `MesofaunaVisualSyncDTO`: desired velocity, state byte, previous byte, speed scalar, scent signal, obstacle pressure, target hash. VAT/IK/shader consumers can alter sine wave frequency/amplitude by state byte. The CPU does not run animation blend trees.

Rejected Alternatives: Animator parameters, state-machine behaviours, and procedural bone updates inside the AI job were rejected. CPU simulates intent only; GPU/IK fakes swim texture.

Scalability potential: Low uses the same state/speed scalar with sparse brain refresh. Middle/High/Ultra can spend GPU cycles on stronger caustic/silt/body-wave response from the same compact DTO.

Hardware Impact: Estimated 30-80 us/frame CPU saved for 50 predators. Big-O before: O(n * animator graph cost). After: O(n) scalar write, visual detail shifted to GPU.

## Decision 5 - Damage Signal Route

Problem: Damage-to-flee must be authoritative and pre-simulation, but per-creature managed callbacks would fan out allocations and race with scheduled jobs.

Solution: `BeginDispatcherFrame` consumes `SignalBus<CombatDamageSignal>.GetFrameSnapshot()` before scheduling AI. It matches `TargetHash` or short id, writes the Vault-backed mesofauna state to `StateFlee`, marks evaluation due, and stores override threat position through existing cognition control.

Rejected Alternatives: Unity messages, events on each `FaunaBrain`, or polling health from MonoBehaviours were rejected for GC and order ambiguity.

Scalability potential: Low/Middle/High/Ultra share the same route. Work scales only with active slots times damage signals on damage frames.

Hardware Impact: Hot frames without damage pay only snapshot length check. Damage frames avoid managed delegate fanout.

## Decision 6 - CSV Human Control Without Hot GC

Problem: Designers need species-specific speed/aggression/scents without C# recompiles. `string.Split` and managed CSV tables would violate the zero-GC hot path and could leak into play mode.

Solution: Cold file read copies bytes into Vault scratch (`MesofaunaCsvScratch`), parses `ReadOnlySpan<byte>`, hashes names with FNV-1a lowercase, and upserts fixed `MesofaunaSpeciesProfileDTO` records into Vault. Runtime jobs read the flat table by open addressing.

Rejected Alternatives: ScriptableObjects as authoritative runtime data were rejected for compile/reload coupling. Managed `Dictionary<string, Profile>` was rejected for GC and Burst incompatibility. `NativeHashMap` was rejected because the domain needs fixed Vault ownership and simple memcpy-friendly ABI.

Scalability potential: Low uses profile multipliers with smaller radius/cadence. Ultra uses same data to permit stronger species variation without new allocations.

Hardware Impact: Gameplay hot path: 0 managed allocations. Cold reload bounded by 4096-byte scratch and file IO only.

## Decision 7 - Blackbox Ring And Build Gate

Problem: NaN or over-budget AI frames need forensic data, but logging every frame would allocate and stall. Compilation also cannot be launched while the machine is already under load.

Solution: `MesofaunaTelemetryEntry[300]` Vault ring records high-level state hashes, counts, quality, slice, and microsecond estimates. Faults dump `.bin` and `.h8dump`. Build was gated until CPU/compiler pressure cleared, then `dotnet build Hecton8.Core.csproj --no-restore` was attempted once. It failed before SHINOBU_146 source analysis with CS2001 missing tracked files in World and Construction domains.

Rejected Alternatives: `Debug.Log`, managed history buffers, and forced build during another agent's compile were rejected.

Scalability potential: Telemetry is fixed-size and independent of quality. Low devices record same critical proof without expanding buffers; ultra can correlate higher update cadence through the same ring.

Hardware Impact: Fixed 19.2 KB telemetry lane plus post-evaluation write. Avoids compile wall contention on developer hardware.

External Build Wall: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` are referenced by `Hecton8.Core.csproj` and tracked by git, but absent on disk. I did not restore or synthesize those files because they are outside SHINOBU_146 ownership.

## Decision 8 - Owner-Local Vault IDs Instead Of Core Enum Churn

Problem: Adding SHINOBU_146 buffer names to the shared `BufferID` enum in `H8Memory.cs` technically worked, but it violates the compile-wall pressure model under 20+ parallel agents. Core enum churn creates avoidable merge conflicts and makes an AI-domain change look like a memory-core ownership change.

Solution: Move mesofauna Vault lane IDs into `PredatorCognitionDomain` as owner-local constants cast to `BufferID`: 71180-71189. The numeric IDs remain stable for vault addressing and binary forensic reports, while the shared core enum no longer needs SHINOBU_146 symbols.

Rejected Alternatives: Keeping global enum symbols was rejected because the domain does not require a sibling contract or shared public API for these private AI lanes. Allocating private Native containers was rejected again because it breaks H-PHI Vault ownership.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is compile scalability, not frame math: fewer shared-file edits means less rebuild contention and fewer collisions with other agents.

Hardware Impact: Runtime 0 us. Developer hardware impact is reduced rebuild/merge pressure by avoiding unnecessary changes to a hot shared core file.

## Decision 9 - Mesofauna Vault Lifecycle Closure

Problem: Static review after the owner-local ID change found a lifecycle hole: the partial-allocation failure path released mesofauna state/mock/visual/telemetry/tuning but did not explicitly cover species profile, profile count, and CSV scratch lanes. The fast initialized-check also omitted those lanes.

Solution: Add species profile, profile count, and CSV scratch lanes to the initialized-check, failure-path release, and dispose default reset. The cold CSV/species data is now lifecycle-consistent with the hot FSM buffers.

Rejected Alternatives: Leaving it to implicit `default` cleanup was rejected because partial allocation failure is exactly where H-PHI ownership bugs hide. Moving these lanes to a private container was rejected because Vault remains the sole persistent memory owner.

Scalability potential: Low/Middle/High/Ultra runtime math is unchanged. Stability improves across all tiers because hot reload and partial boot failure cannot leave stale CSV/species handles.

Hardware Impact: Runtime hot path 0 us. Cold boot/failure-path only; prevents stale handle state and later undefined vault reads.

## Decision 10 - Chemical Breadcrumb Contract Correction

Problem: The mesofauna scent path initially referenced `ChemicalBreadcrumbWaypoint.AbsolutePositionDouble`. Static contract search showed no existing code uses that field; current consumers prove only `RuntimePosition`, `RadiusMeters`, `ExpiresAt`, and `Channels`.

Solution: Replace the unproven field with `RuntimePosition` converted through `RuntimeToAup(runtime, input.FloatingOriginOffset)`, then subtract predator AUP before casting to `float3`. The scent steering now accumulates a weighted gradient from valid, non-expired breadcrumbs while preserving the best target hash.

Rejected Alternatives: Keeping `AbsolutePositionDouble` was rejected as an avoidable compile risk. Sampling GameObject emitters or trigger volumes was rejected for GC/order coupling.

Scalability potential: Low still narrows effective scent follow range by quality and multiplier; Ultra keeps broader range and richer gradient accumulation. No binary tier switch.

Hardware Impact: Runtime cost remains O(active breadcrumbs on evaluated slices). Compile risk is reduced because the code now matches known contract fields.

## Decision 11 - Runtime Use Of Designer State Timeout

Problem: The editor facade wrote `MesofaunaTuningDTO.StateTimeoutSeconds`, but the first FSM pass used only fixed tick fields for Search/Flee transitions. That made the slider a cold facade value with weak gameplay authority.

Solution: Add `ResolveStateTimeoutTicks()` inside the Burst job. It maps `StateTimeoutSeconds` through a continuous quality-weight cadence (`5..60` logical ticks per second) and caps Search/Flee thresholds without using `Time.deltaTime`.

Rejected Alternatives: Using Unity time was rejected for rollback determinism. Keeping the slider as editor-only metadata was rejected because the prompt requires human-readable tuning that actually controls the system.

Scalability potential: Low quality maps seconds to fewer logical ticks, making stale search/flee states resolve faster while target acquisition is time-sliced. Ultra keeps near-frame cadence for richer behavior.

Hardware Impact: A few scalar ops per evaluated brain; no allocations. Low tier sheds stale target pursuit sooner, reducing wasted hash/scent work on later slices.

## Decision 12 - Origin-Shift-Stable Target Identity Inputs

Problem: The polished FSM used AUP-local movement math, but direct target hashes and scent breadcrumb hashes still used runtime float positions. Runtime floats can shift when the floating origin moves, so the steering path was safe while the target identity hash could still jitter across origin shifts.

Solution: Direct prey/player target hashes now derive from the AUP-local `toTarget` vector after the predator AUP is subtracted. Scent target hashes derive from the AUP-local breadcrumb delta after converting `RuntimePosition` through the current floating-origin offset. The authoritative movement and the stored hash now share the same localized coordinate basis.

Rejected Alternatives: Hashing absolute `double3` bit patterns was rejected because the current cross-domain target packets do not expose a stable source entity id for every fallback path and bit-level double hashing would not improve current identity authority. Keeping runtime float hashes was rejected because it contradicts the 100 km AUP precision rule.

Scalability potential: Low/Middle/High/Ultra all use the same hash basis. Quality changes range/cadence only, not coordinate truth.

Hardware Impact: CPU neutral. The gain is determinism: target identity no longer changes solely because the floating origin moved.

## Decision 13 - SDF Reciprocal Pressure And Output Flag Retention

Problem: Task 09 explicitly requires `SDF_Normal * (1.0 / max(0.1, SDF_Distance))`. The previous obstacle pressure was occupancy-shaped, not distance-reciprocal. Also, mesofauna output rewrites could preserve `RetinalBlind` but drop `EcoHeadless`, allowing a behavior job to accidentally wake a headless ecology lane.

Solution: Obstacle pressure now computes a guarded approximate SDF distance from the signed byte payload and mean voxel cell size, then applies `math.rcp(math.max(0.1f, sdfDistance))`. The output bitmask now retains `RetinalBlind | EcoHeadless` before writing behavior attack/threat flags.

Rejected Alternatives: Raycasts, path nodes, and exact terrain collision were rejected because the prompt demands SDF math and Dear Lie avoidance. Preserving all old flags was rejected because stale attack/pack flags from a previous cognition path would leak into mesofauna output.

Scalability potential: Low quality probes closer and evaluates fewer brains; higher quality expands probe distance and frequency. The formula remains continuous and avoids a low/high branch.

Hardware Impact: Constant extra scalar ALU on evaluated slices only; no memory allocation. Prevents headless-lane false motion and aligns the avoidance math with the explicit task contract.

## Decision 14 - Compile-Wall Honesty Boundary

Problem: The compile-wall mandate requires no direct sibling Runtime assembly route. A fresh asmdef scan shows `MesofaunaBehavioralStateMachine.cs` does not introduce a new asmdef or assembly reference, but the existing root `Assets/_Project/Scripts/Hecton8.Core.asmdef` already references sibling runtime assemblies such as `Hecton8.AI.Cognition`, `Hecton8.Logistics`, and `Hecton8.Cartography`. Claiming the domain assembly is clean would be false.

Solution: Do not edit the shared root asmdef from a fauna task. Record the distinction precisely: SHINOBU_146 added no new assembly route, while the root Core assembly has pre-existing compile-wall debt outside this task's safe ownership. The mesofauna code uses existing Core/Vault/SignalBus routes and owner-local Vault IDs.

Rejected Alternatives: Removing root Core asmdef references was rejected as a high-blast-radius shared-assembly refactor outside SHINOBU_146 scope. Hiding namespace imports with fully qualified names was rejected because it would not remove assembly dependency and would make the audit less honest.

Scalability potential: Runtime behavior unchanged. Compile scalability is protected by avoiding new asmdef churn and by documenting the existing debt for an integrator-level pass.

Hardware Impact: Runtime 0 us. Developer hardware impact is risk containment: no new root asmdef mutation from this fauna pass.

## Decision 15 - Admission-Failure Scheduling Order

Problem: Mesofauna hash/mock helper jobs were scheduled before the swarm job admission gate. If the swarm job was rejected by the job-admission scheduler, the code had to call `Complete()` on the already-scheduled mesofauna helper jobs inside the frame lane. That is a hidden main-thread stall exactly when the scheduler is trying to shed work.

Solution: Schedule mesofauna hash/mock helper jobs only after the swarm job has been admitted. On admission failure, no mesofauna helper work exists, so the frame returns without a cleanup completion. On success, mesofauna still depends on predator evaluation plus hash/mock helper handles through `JobHandle.CombineDependencies`.

Rejected Alternatives: Keeping the early helper schedule was rejected because `Complete()` on a rejected frame violates the dependency-chain mandate. Scheduling mesofauna independently when swarm admission fails was rejected because it would bypass the common AI lane pressure signal.

Scalability potential: Low pressure frames avoid extra helper work when AI lane admission rejects the frame. High/Ultra frames still schedule hash/mock in parallel and feed mesofauna evaluation normally.

Hardware Impact: Prevents a worst-case admission-failure stall equal to one target-hash clear/build plus mock target generation pass. Normal admitted-frame cost is unchanged.

## Decision 16 - Direct Target Route And CSV Fail-Closed Repair

Problem: `TryResolveDirectTarget()` selected prey first, but later used broad `hasPlayer` to choose player AUP/hash. When both prey and player were visible, the selected prey runtime position could be paired with player AUP identity. Separately, CSV parsing cleared the profile table before returning `0` profiles, but did not reset `SpeciesProfileCount` first, leaving stale count over empty rows.

Solution: Introduce `selectedPlayer` as the only authority for player AUP and player hash salt. Reset `_mesofaunaSpeciesProfileCount[0]` before parsing mutates profile rows, so malformed/empty CSV fails closed to default species profiles.

Rejected Alternatives: Broad `hasPlayer` after selection was rejected because it violates one fact -> one route. Preserving stale CSV counts was rejected because it makes the editor facade lie after a bad reload.

Scalability potential: Same across all tiers. Correct target identity prevents wasted hunt pursuit; CSV fail-closed keeps low/high tuning deterministic after malformed authoring data.

Hardware Impact: One bool in the direct target path; hot cost is negligible. CSV repair is cold only.

## Decision 17 - Target AUP Blackbox Evidence Lane

Problem: The 300-frame mesofauna blackbox recorded `TargetHash`, but `ProbeAup` was still populated from `state.AUP_Position`. That made the dump prove where the predator was, not which AUP target drove hunt/flee/scent behavior. The state DTO cannot grow without breaking the 64-byte rollback snapshot.

Solution: Keep `MesofaunaStateDTO` unchanged at 64 bytes and expand only `MesofaunaVisualSyncDTO` from 32 to 64 bytes: offsets 32-55 hold `double3 TargetAup`, offset 56 holds `TargetDistanceMeters`, and offset 60 holds `TargetFlags`. Hunt, flee, and scent states write a validated target AUP. Direct, spatial-hash, and scent acquisition return `double3 targetAup` directly to interception math and the writer, so steering and blackbox proof are not reconstructed from localized `float3 targetPosition`. Continuity frames preserve the previous target AUP so time-slicing does not erase forensic evidence. Telemetry now writes `ProbeAup` from `visual.TargetAup` when `TargetFlags & 1` is set.

Rejected Alternatives: Adding target AUP to `MesofaunaStateDTO` was rejected because it would break the fixed 64-byte rollback layout. Adding another Vault lane was rejected because visual sync already owns presentation/forensics target intent and widening it is simpler than introducing another owner-local ID. Keeping only `TargetHash` was rejected because it is insufficient for AUP autopsy.

Scalability potential: Low-tier continuity frames retain the last target without rerunning acquisition. Middle/high/ultra tiers update target AUP at higher cadence through the same visual sync lane. No binary quality branch was introduced.

Hardware Impact: +32 bytes per mesofauna visual sync slot and one same-slot read on continuity frames. No new persistent allocation owner, no GC, no extra job dependency, and telemetry proof is now exact for resolved hunt/flee/scent targets.

## Decision 18 - Over-Budget Telemetry Dump Fence

Problem: Task 16 requires a blackbox dump when mesofauna FSM time exceeds 1.0 ms. The telemetry ring recorded `_mesofaunaLastChainMicroseconds`, but dump emission was only tied to non-finite state/visual faults.

Solution: Add `DumpReasonOverBudgetHash` and an `overBudget = _mesofaunaLastChainMicroseconds > 1000f` branch in `UpdateMesofaunaPostEvaluationTelemetry()`. Telemetry flag bit 1 remains NaN/fault; bit 2 marks budget breach. Faults still reset bad slots. Budget breaches dump the same fixed ring without mutating predator state.

Rejected Alternatives: Treating over-budget as a NaN fault was rejected because it would corrupt the `NonFiniteFallbackCount` signal. Adding managed profiler markers or Debug.Log was rejected because the blackbox must remain bounded and allocation-free.

Scalability potential: Low-tier pressure now leaves a forensic trace when time slicing fails to keep the chain below 1.0 ms. High/Ultra can prove when richer cognition exceeds the same fixed budget.

Hardware Impact: One post-evaluation float comparison and bit write per frame; hot Burst jobs unchanged.

## Decision 19 - Named Forensic Flags And Hash Cell Route

Problem: Visual target validity and telemetry fault/budget flags used literal `1u`, `1`, and `2` in multiple files. The values were correct, but the route was not self-documenting and could be misread during later forensic dump parsing. The mesofauna spatial hash builder received `CellSizeMeters`, while the search path still used a literal `8f`; that was an avoidable drift trap if the shared bucket size changes.

Solution: Add `VisualFlagHunt`, `VisualTargetFlagValid`, `TelemetryFlagFault`, and `TelemetryFlagOverBudget` to `MesofaunaBehaviorConstants`, then route visual sync, debug gizmo target vectors, and blackbox telemetry through those constants. Add `TargetHashCellSizeMeters` to `MesofaunaBehaviorJob` and schedule it from `SwarmBucketCellSize`, matching the builder route.

Rejected Alternatives: Leaving magic bits in place was rejected because the blackbox binary dump needs unambiguous field semantics. Duplicating `8f` in the searcher was rejected because builder/query mismatches create false-negative target acquisition that only appears after tuning changes.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged, but the spatial hash cell route now scales with the single bucket-size authority. Constants keep forensic flags stable without adding runtime branches.

Hardware Impact: Runtime impact is 0 us after constant propagation. The cell-size field is copied into the job struct once per scheduled frame and replaces a literal in bucket math.

## Decision 20 - Target DTO Owns Spatial Hash Position

Problem: `BuildMesofaunaTargetSpatialHashJob` inserted buckets from `input.Position`, while `TryAcquireSpatialHashTarget()` scored and returned `MockTargets[candidate]`. If a mock/prey/player target was offset from the owning slot, bucket membership and target DTO position could describe different facts. That violates one fact -> one owner -> one route and can create silent false negatives in adjacent-bucket queries.

Solution: Make `MesofaunaTargetDTO` the spatial hash authority for mesofauna target acquisition. `GenerateMesofaunaMockTargetsJob` writes the target DTO first. `BuildMesofaunaTargetSpatialHashJob` now depends on that handle, reads `MockTargets[slot].AUP_Position`, converts AUP back to runtime-local coordinates using the candidate slot's floating-origin offset, and hashes the target DTO position. The FSM depends on predator evaluation plus the hash handle; the hash handle already fences mock generation.

Rejected Alternatives: Keeping builder/searcher split authority was rejected because the search could miss valid targets after target offsets. Hashing only creature input positions was rejected because Task 05 introduced explicit secondary target DTOs. Running mock and hash in parallel was rejected once the hash started reading target DTOs.

Scalability potential: Low/Middle/High/Ultra keep the same consumer time-slicing. The helper graph does one additional target DTO read per active slot during admitted frames; correctness improves across all tiers because target spatial locality is now tied to the target record itself.

Hardware Impact: Expected cost is one 64-byte target DTO read per active slot during hash build and one dependency edge from mock generation to hash build. This is cheaper than missed target acquisition causing repeated search/scent fallback over multiple frames.

## Decision 21 - Damage Flee Route Must Own The Threat Vector

Problem: `ProcessMesofaunaDamageSignals()` correctly wrote `StateFlee` and `TargetHashID` from `CombatDamageSignal.SourceHash`, and stored `Control.OverrideThreatPosition`. The mesofauna Burst FSM did not read `CognitionControl`, so the state byte could flee while the vector still came from stale `ThreatPosition`, player position, or fallback forward. Direct prey/mock target AUP also rebuilt from runtime floats even though `CognitionInput` carries `PackTargetAup` and `PlayerTargetAup`.

Solution: Add read-only, no-alias `NativeArray<CognitionControl>` to `MesofaunaBehaviorJob` and schedule it from `_controls`. `ResolveThreatPosition()` now honors `HasOverrideThreatPosition` until `OverrideUntilTime`, making PRE_SIMULATION damage source position feed the actual flee vector. Ongoing `StateFlee` preserves nonzero `state.TargetHashID`, so a damage source hash survives the evaluated flee frame. Direct prey and mock prey target AUP now use `PackTargetAup` with runtime fallback; player target AUP already uses `PlayerTargetAup` and mock player now does the same.

Rejected Alternatives: Expanding `MesofaunaStateDTO` with threat position was rejected because the authoritative rollback DTO must remain exactly 64 bytes. Ignoring `CognitionControl` was rejected because it made damage routing half-authoritative. Keeping runtime+origin as the primary prey AUP route was rejected because the AUP packet is already present.

Scalability potential: Low-tier time-sliced brains still keep flee state and source hash through continuity frames. Middle/high/ultra evaluate the override vector at higher cadence through the same control lane. No binary quality branch was introduced.

Hardware Impact: One extra read-only control lane reference in the job struct and one control read only when resolving a flee threat. No persistent allocation, no DTO growth, no main-thread completion.

## Decision 22 - Explicit Deterministic RNG In Mock Targets

Problem: Fallback mock targets were deterministic through hash plus trigonometric phase, but the mandate requires deterministic RNG seeded from sector/frame truth. Relying on implied hash variation is weak forensic proof and easy to misread as noncompliance.

Solution: Add local `CreateDeterministicRandom()` inside `GenerateMesofaunaMockTargetsJob`, seeded by an AUP-derived 256m sector hash, `FrameId`, and stable slot/species salt. The seed is forced nonzero before constructing `Unity.Mathematics.Random`. The RNG produces only small bounded jitter layered on the existing orbit phase, so target movement remains smooth enough for profiling.

Rejected Alternatives: Importing rollback/network RNG helpers was rejected because it would create a direct sibling-domain dependency from fauna code. Full frame-random target placement was rejected because it would produce visible target pops and break movement smoothness.

Scalability potential: Low-tier mock profiling still gets stable, smooth fallback targets while only a fraction of brains evaluate per frame. Ultra can evaluate every frame with deterministic but richer target variation.

Hardware Impact: +3 `NextFloat` calls only on fallback mock target generation, not on direct prey/player targets. No GC and no new Vault lane.

## Decision 23 - Executable Layout Proof And Scheduler Field Hygiene

Problem: Static re-audit found the mesofauna scheduler still assigning old chemical-grid fields into `MesofaunaBehaviorJob`, while the job struct had already moved to the breadcrumb-only chemical contract. The same review exposed that layout proof was strongest for `MesofaunaStateDTO` but weaker for the widened visual/telemetry DTOs. A patch attempt also showed why field hygiene matters: `Controls` belongs to the behavior job for flee override routing, not to the target-hash builder.

Solution: Remove stale chemical-grid field assignments from the mesofauna job initializer and keep only `ChemicalBreadcrumbs`, count, and follow-step. Keep `[ReadOnly, NoAlias] NativeArray<CognitionControl> Controls` on `MesofaunaBehaviorJob` where `ResolveThreatPosition()` reads it, and keep the builder free of unused control fields. Expand `ValidateLayout()` to assert all relevant target, visual sync, telemetry, tuning, and species-profile offsets, including public explicit padding endpoints.

Rejected Alternatives: Re-adding unused chemical grid fields to `MesofaunaBehaviorJob` was rejected because it widens the job ABI and reintroduces stale coupling. Leaving layout proof only in Markdown was rejected because executable cold validation is cheap and catches future drift. Removing the control lane entirely was rejected because damage-source flee would lose its override vector.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The gain is structural: fewer job fields under low-end scheduling pressure, stricter validation before any tier executes, and no unnecessary chemical-grid payload in the mesofauna FSM job.

Hardware Impact: Runtime hot path is neutral except the hash builder no longer carries an unused `Controls` field. Cold validation adds no frame cost. The avoided cost is a compile break and future cache/job-ABI drift.

## Decision 24 - Species CSV Reload Must Fail Closed

Problem: The cold mesofauna species CSV parser reset the profile count only after a file was found and read. If a designer loaded valid species multipliers once, then removed or corrupted the CSV, the reload call could return false while the old Vault profile table stayed active. That violates human-control predictability.

Solution: Resolve the species profile Vault array first, set `_mesofaunaSpeciesProfileCount[0] = 0`, and clear the fixed profile table before path lookup or file read. Any missing, empty, oversized, unauthorized, or malformed file now leaves the runtime with zero loaded profiles and the Burst job falls back to default species multipliers.

Rejected Alternatives: Keeping stale profiles was rejected because editor status would say the CSV failed while behavior still used old data. Using a managed CSV library was rejected for zero-GC/parser-contract reasons. Adding another validity bit lane was rejected because the count lane already owns profile table authority.

Scalability potential: Low/Middle/High/Ultra all get deterministic default behavior after reload failure. Designer iteration becomes predictable without changing runtime hot math.

Hardware Impact: Cold reload clears at most 64 `MesofaunaSpeciesProfileDTO` records. Gameplay cost remains 0 us and 0 B.

## Decision 25 - Damage Override Vectors Cannot Survive Missing Payloads

Problem: Damage routing cleared the state into `StateFlee`, but if a later `CombatDamageSignal` did not carry a valid runtime point, the previous `CognitionControl.HasOverrideThreatPosition` flag and vector could survive while `OverrideUntilTime` was extended. That can make a predator flee from a stale source while the target hash points to the new damage source.

Solution: Before decoding each damage point, clear `HasOverrideThreatPosition` and reset `OverrideThreatPosition`. Only a fresh, finite `CombatDamageSignalCodec.TryToRuntimePoint()` result re-enables the override. Without a valid point, the Burst FSM uses its existing deterministic fallback chain: input threat target, player target, then reverse forward.

Rejected Alternatives: Keeping the old override was rejected because it violates one fact -> one route. Writing default zero as a valid threat was rejected because it would produce origin-biased flee vectors. Adding threat position to `MesofaunaStateDTO` was rejected again because the DTO must remain 64 bytes.

Scalability potential: Low-tier sliced brains cannot inherit an old high-impact flee vector for ten frames. High/Ultra get the same source-correct flee vector at higher cadence.

Hardware Impact: Cost exists only on damage signal frames: one bit clear and one float3 reset per matched predator. Steady-state hot path is unchanged.
