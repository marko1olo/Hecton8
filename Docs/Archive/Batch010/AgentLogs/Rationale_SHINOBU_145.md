# Rationale_SHINOBU_145

Date: 2026-05-20
Agent: SHINOBU_145
Status: PENDING VERIFICATION

## Decision 001: Owner-Local Metabolism With Vault-Backed Tables

Problem: Hunger, hydration, core temperature, and toxicity need to apply to thousands of living entities without per-object Update loops or managed collections.

Solution: Store authoritative state in `NativeArray<MetabolicStateDTO>` acquired from `GlobalDataVault`, execute Burst `IJobParallelFor` from `ISlowTickable`, and complete in the late-frame swap window.

Rejected Alternatives: Standard Unity MonoBehaviour-per-creature logic was rejected because slow biology does not justify per-frame GameObject dispatch, transform access, or heap-scattered state. Managed `List<SurvivalStats>` was rejected because the task requires contiguous unmanaged DTOs.

Scalability potential: Low uses the same authoritative math at a longer cadence. Middle tightens cadence. High and Ultra keep cadence near 0.5s and spend saved cost on thermal/toxic sampling and presentation scalar fidelity.

Hardware Impact: Estimated low-end i3/MX350 gain is removal of thousands of Update dispatches and managed object dereferences. Expected hot cost target remains under 10 us per SlowTick cycle for 5000 entities pending profiler proof.

## Decision 002: Existing Global Lanes, No New Signal Lane

Problem: Starvation/dehydration and toxicity damage must notify other systems without direct dependency on combat, player health, or UI.

Solution: Use existing `SignalBus<PhysiologyStateSignal>` and `SignalBus<CombatDamageSignal>` without modifying `GlobalSignals.cs`. For starvation/dehydration, `PhysiologyStateSignal.SourceHash` carries the `MetabolicStateDTO.EntityHashID` and `EntityIndex` carries the vault row. For toxin damage, `CombatDamageSignal.TargetHash` carries the same entity hash. Metabolism-owned Vault routes use local numeric `BufferID` casts inside the Physiology assembly instead of expanding the Core memory enum during this batch.

Rejected Alternatives: New global SignalBus lane was rejected because existing physiology and combat lanes already own this fact class. Editing `GlobalSignals.cs` or `H8Memory.BufferID` was rejected after the ULTRA mandate because Core headers are shared compile-wall surfaces. Direct calls into combat/player health were rejected as cross-domain coupling.

Scalability potential: Low devices emit only authoritative state signals. High/Ultra consumers can add richer presentation downstream without changing survival truth.

Hardware Impact: NativeQueue push is O(1). Avoids managed event fanout and avoids per-entity direct component calls.

## Decision 006: Compile-Wall Route Correction

Problem: Direct sibling assembly references and Core enum edits would turn a metabolism feature into a project-wide recompile risk.

Solution: Keep `Hecton8.Physiology.asmdef` referencing only Core/Core.Contracts/Core.Memory plus Burst/Collections/Jobs/Mathematics. Thermodynamics is queried only through `IThermodynamicsService` cached from `GlobalRegistry`; the runtime sees `NativeArray<float>` plus dimensions and AUP origin, never `AbyssalThermalManager` concrete types in the Physiology assembly.

Rejected Alternatives: Adding `Hecton8.Thermodynamics` to the Physiology asmdef was rejected as a direct sibling dependency. Adding new memory enum members was rejected because local `BufferID` numeric IDs are enough for owner-local Vault buffers.

Scalability potential: Low/Middle/High/Ultra all consume the same neutral thermal grid route; only the thermal interpolation weight and cadence change with `GlobalQualityWeight`.

Hardware Impact: Avoids unnecessary C# compile fanout and keeps runtime data flow pointer-only once cold services are cached.

## Decision 003: AUP Thermal Sampling By Relative Delta

Problem: Absolute double3 AUP values lose precision if cast to float before grid mapping.

Solution: Use entity double3 AUP minus thermal grid root double3 AUP, then cast only the relative delta to float3 before cell division. Reuse the existing thermodynamics mapping contract where assembly boundaries allow.

Rejected Alternatives: `Transform.position`, world-space `Vector3`, or absolute AUP float cast were rejected because they break at map edges and violate the prompt.

Scalability potential: Low can use last thermal grid snapshot or fallback ambient. Middle/High/Ultra can sample every cadence without changing authoritative formulas.

Hardware Impact: Constant-time integer/float math per entity, no physics queries, no scene search.

## Decision 004: Dear Lie Presentation

Problem: Cold stress needs feedback without CPU particles, per-entity UI, or post-process volume churn.

Solution: Publish one global frost scalar derived from aggregated core temperature. The scalar is presentation-only and does not own gameplay truth.

Rejected Alternatives: Particle systems, screen overlay prefab spawning, and per-status post-process volume manipulation were rejected for CPU overhead and GC risk.

Scalability potential: Low reads one scalar. Middle/High/Ultra can bind the same scalar into richer visor shaders or a constant buffer without extra physiology simulation cost.

Hardware Impact: One global shader value after job completion; no per-frame GameObject work.

## Decision 005: Black Box Ring First

Problem: NaN/infinite state in an authoritative survival system must be reconstructable.

Solution: Allocate a 300-entry telemetry ring in the Vault and dump it to `Docs/AgentLogs/Dump_METABOLISM_SURGEON.bin` on NaN detection.

Rejected Alternatives: Debug.Log and editor-only console output were rejected because they allocate strings and do not survive player crashes.

Scalability potential: Ring size is fixed across tiers. Higher tiers can expose more visualization downstream without expanding the black box payload.

Hardware Impact: One fixed-size write per completed SlowTick, expected below 1 us.

## Decision 007: Deterministic Mock Ecosystem Seed

Problem: Throughput verification cannot wait for AI/mesofauna ownership, but random fallback state must not desync rollback clients.

Solution: `InitMockMetabolismJob` hydrates 5000 rows with `Unity.Mathematics.Random` seeded from a fixed sector hash, simulation frame, and entity row. Every generated field lands directly in Vault-backed native rows.

Rejected Alternatives: `UnityEngine.Random`, managed test fixture lists, and editor-only ScriptableObject fixtures were rejected because they either desync or bypass the real memory path.

Scalability potential: Low/Middle/High/Ultra all test the same solver. Higher tiers do not change authoritative mock truth; they only exercise richer thermal interpolation and presentation output.

Hardware Impact: Cold-only Burst initialization replaces thousands of GameObject test spawns. Expected gameplay frame cost is 0 us after hydration.

## Decision 008: CSV Profile Hydration Without Managed Tokenization

Problem: Designers need metabolism constants without a C# recompile, but `string.Split`, LINQ, or dictionaries would violate the allocation-free ingest requirement.

Solution: Read `biological_metabolism_profiles.csv` into a Vault byte scratch buffer, slice it with `ReadOnlySpan<byte>`, hash species names with FNV-1a, parse ASCII floats manually, and mutate `MetabolicSpeciesRuleDTO` rows in place.

Rejected Alternatives: `TextAsset`, `File.ReadAllText`, `string.Split`, `List<SpeciesProfile>`, and JSON were rejected because they allocate and hide layout errors behind managed objects.

Scalability potential: Low devices use the same prehydrated rules. High/Ultra designers can add more species rows without changing runtime code or widening the hot DTO.

Hardware Impact: Cold I/O only. The parser allocates no managed tokens and adds 0 us to gameplay SlowTick.

## Decision 009: Shader CBuffer Dear Lie

Problem: Freezing feedback must be visible without CPU particles, per-status prefabs, or post-process volume churn.

Solution: Collapse metabolism presentation to a single frost scalar plus a 64-byte `MetabolismShaderGlobalsDTO` constant buffer. Shader code can spend GPU budget on frost crystals, caustic distortion, and visor edge growth while physiology remains O(N) scalar math.

Rejected Alternatives: Particle systems, UI overlay spawning, and per-entity status widgets were rejected because they scale with status count and move presentation work onto the CPU.

Scalability potential: Low reads `_HectonMetabolismFrostScalar`. Middle/High/Ultra read the same CBuffer and spend more shader ALU; no authoritative biology changes.

Hardware Impact: O(1) upload after completed SlowTick, no per-entity draw or managed object churn.

## Decision 010: Build Guard Honored

Problem: Project policy forbids launching dotnet build while CPU is loaded above 50% or another compiler is active.

Solution: Checked `csc.exe`, `dotnet`, and CPU before build. `csc.exe` was absent, but CPU sample was 100%, so compilation was deferred and static verification was used for this pass.

Rejected Alternatives: Forcing `dotnet build` under 100% CPU was rejected because it violates the explicit hardware-protection rule and risks stealing cycles from parallel agents.

Scalability potential: Developer hardware remains available for concurrent agents; compile verification resumes when load is below the threshold.

Hardware Impact: Avoided a full C# compile wall during detected saturation.

## Decision 011: Import Hygiene Without Compile-Wall Drift

Problem: Static source was implemented, but new Unity assets lacked stable `.meta` files and the runtime had a stale `using Hecton8.World;` namespace import. Even if that namespace is physically compiled through Core in this checkout, leaving it in a Physiology assembly weakens compile-wall evidence and creates avoidable import churn.

Solution: Added deterministic `.meta` GUIDs for SHINOBU_145's five new C# assets, removed the `Hecton8.World` import, and kept the asmdef reference list unchanged. Also removed the debug vector shader global so metabolism presentation stays limited to one scalar fallback plus the 64-byte frost CBuffer.

Rejected Alternatives: Adding a World or Thermodynamics asmdef reference was rejected as sibling coupling. Letting Unity mint GUIDs locally was rejected because it makes import evidence machine-dependent. Keeping a debug vector global was rejected because the Dear Lie route only needs gameplay-derived scalar/CBuffer data.

Scalability potential: Low devices read one scalar fallback. Middle/High/Ultra can bind the CBuffer and spend shader ALU without widening gameplay truth or adding CPU-side presentation work.

Hardware Impact: Runtime CPU impact is unchanged; import churn and shader global bandwidth are reduced. Expected frame gain is effectively 0 us in simulation, with lower visual-sync noise and stronger compile-wall proof.

## Decision 012: Editor-Only Layout Reflection Fence

Problem: `UnsafeUtility.GetFieldOffset(typeof(...).GetField(...))` is valid for layout proof but should not exist as player runtime reflection surface.

Solution: Kept DTO field-offset validation available only under `#if UNITY_EDITOR`; runtime carries explicit `[StructLayout(LayoutKind.Explicit, Size = 32)]` and raw field offsets as the actual ABI contract.

Rejected Alternatives: Leaving reflection guard in player runtime was rejected as unnecessary surface. Removing the validator entirely was rejected because Task 04 requires editor-time proof.

Scalability potential: No tier difference; this is import/editor hygiene only.

Hardware Impact: Player runtime carries no field-reflection guard path. Static proof remains editor-only and cold.

## Decision 013: Inactive Slot Vaccination For Uninitialized Vault Rows

Problem: `NativeArrayOptions.UninitializedMemory` is correct for avoiding allocator clear cost, but the no-mock bootstrap path originally initialized only rules/tuning. A serialized capacity above 5000 also left rows beyond the deterministic mock population without defined state. The hot integrator resolves capacity from buffer lengths, so an inactive uninitialized row could appear as a random live creature.

Solution: Added `InitInactiveMetabolismJob`, a deterministic Burst `IJobParallelFor` that stamps every resolved state/AUP/exertion/toxin/rule-index row to a known inactive record with `EntityHashID=0`. Mock bootstrap now runs `InitMetabolismRulesJob -> InitInactiveMetabolismJob -> InitMockMetabolismJob`, so mock rows are active and the rest of capacity is inert. Rules-only bootstrap runs `InitMetabolismRulesJob -> InitInactiveMetabolismJob`.

Rejected Alternatives: Zero-filled Vault allocation was rejected because Task 15 requires uninitialized requests and targeted init. Shrinking runtime count to 5000 was rejected because it would silently ignore designer capacity. Treating every row as active was rejected because it turns capacity padding into gameplay truth.

Scalability potential: Low/Middle/High/Ultra all pay one cold linear init at boot or service replacement. Gameplay cost improves when capacity exceeds active entity count because inactive rows exit before thermal sampling, toxin math, and signal emission.

Hardware Impact: Cold cost is one simple Burst row write. Hot savings are proportional to inactive capacity: each inactive row now performs one hash check and one flag write instead of AUP thermal mapping, rule sanitize, drains, toxicity math, and signal tests.

## Decision 014: Hot Path `new` Syntax Reduction

Problem: Value-type `new` initializers in Burst/job scheduling do not allocate GC memory, but the mandate is audited aggressively by source scans and the wording forbids `new` during gameplay.

Solution: Replaced gameplay schedule object initializers with `default` struct locals and field assignments. Replaced hot Burst `new float3`, `new MetabolicTelemetryEntry`, and similar value constructors with default locals and direct field writes. Remaining `new` usage is static marker setup, cold CSV/file IO, cold graphics buffer setup, and black-box dump I/O.

Rejected Alternatives: Keeping value-type `new` with a comment was rejected because it leaves audit noise in the hot path. Removing cold IO constructors was rejected because CSV ingest and dump emission are not gameplay hot path and require framework objects.

Scalability potential: No tier difference. This is source hygiene and audit hardening; tier scaling remains driven by `GlobalQualityWeight`.

Hardware Impact: Runtime machine code should be equivalent for the value-type cases, so no microsecond gain is claimed. The practical gain is reduced audit ambiguity and fewer false positives when proving zero-GC hot path.

## Decision 015: Chemical Grid Readback Without Sibling Dependency

Problem: Task 09 requires local toxin concentration from the Chemical Influence Grid, but Physiology must not reference the SHINOBU_138 runtime class or add a sibling asmdef dependency. There is no Core `IChemicalInfluenceService` contract in this checkout.

Solution: Consume SHINOBU_138's documented Vault readback route only: published `float4` grid `71152`, overlay grid `71153`, tuning `71161`, telemetry ring `71162`, and telemetry cursor `71163`. Physiology defines 64-byte explicit mirror DTOs only for the published tuning/telemetry layout, locks the readback buffers against compaction, and passes raw `float4*` pointers into `MetabolicIntegrationJob`. The job subtracts chemical `GridOriginAup` from entity double3 AUP before casting to local `float3`.

Rejected Alternatives: Direct `ChemicalInfluenceGrid.TrySampleNormalizedChannels` was rejected because it is an internal SHINOBU_138 runtime API and would force a concrete World dependency. Adding a new Core chemical service contract was rejected because existing public interfaces should not be widened during this batch without an owner review. Reflection was rejected as IL2CPP-hostile and non-Burst.

Scalability potential: Low uses nearest published toxin cell. Middle/High/Ultra blend toward trilinear sampling using the same continuous quality curve as thermal sampling. Authoritative entities are never dropped; only sample fidelity changes.

Hardware Impact: Chemical readback adds one toxin sample per active row when the SHINOBU_138 buffers are present. The path is fail-closed and pointer-only. Expected cost is one float4 tap at low quality and up to eight float4 taps at high quality, paid only on scheduled metabolism cadence.

## Decision 016: Fail-Closed External Readback

Problem: Cross-owner Vault buffers can be absent, uninitialized, or mid-integration while parallel agents are still wiring their domains. Metabolism must not make chemical-grid availability a boot blocker.

Solution: `TryResolveChemicalGrid` treats missing or invalid chemical readback as `HasChemicalGrid=0`. The integrator then uses the owner-local `ToxinSamples` buffer and toxin purge math. No exception, scene search, managed log, or fallback GameObject route is introduced.

Rejected Alternatives: Hard-failing metabolism boot on absent chemical buffers was rejected because it creates an inter-agent dependency. Spawning a chemical-grid runtime from Physiology was rejected as ownership violation.

Scalability potential: Low/Middle/High/Ultra all retain deterministic metabolism even without chemical readback. Higher tiers gain richer toxin spatial fidelity only when the owner-published snapshot exists.

Hardware Impact: Missing chemical grid costs 0 extra hot-path taps. Valid chemical grid costs bounded pointer reads only; no managed allocation.

## Decision 017: Signal Lane Authority Correction

Problem: Feature runtimes must not own global SignalBus lane capacity/category configuration. Configuring `PhysiologyStateSignal` or `CombatDamageSignal` in the metabolism runtime would create a second authority over Core signal lanes.

Solution: The runtime now forces Core `GlobalSignals` initialization through the existing damage writer and then only calls `SignalBus<T>.EnsureInitialized()` for the lanes it produces or reads. It does not call `SignalBus<T>.Configure()`.

Rejected Alternatives: Feature-owned lane configuration was rejected because Core's `GlobalSignals.InitializeCategorySignalLanes` already owns first-party lane setup. Adding a metabolism-specific lane was rejected because existing physiology/combat lanes carry the required facts.

Scalability potential: All tiers use the same lane route. Load shedding remains Core policy, not Physiology policy.

Hardware Impact: No claimed microsecond gain. This removes authority drift and prevents hidden lane-capacity fights between agents.

## Decision 018: Thermal/Chemical NaN Guard Before Grid Cell Cast

Problem: Finite but huge local grid coordinates can overflow or wrap during float-to-int casts if range checks happen after `math.floor`. Toxicity also needed a hard upper clamp after accumulation.

Solution: Both thermal and chemical sampling validate finite local/grid coordinates and range before converting to `int3`. Toxicity is clamped to `0..8` after accumulation and again during invalid-math recovery.

Rejected Alternatives: Relying on `math.clamp` after cast was rejected because the dangerous conversion has already happened. Leaving toxicity unbounded was rejected because it can amplify signal magnitudes and telemetry hashes.

Scalability potential: No tier difference. This is correctness and crash-resistance.

Hardware Impact: A few scalar comparisons per sampled active row. The cost is lower than one NaN propagation into combat or telemetry lanes.

## Decision 019: Dispatcher Fence And Optional Chemical Overlay

Problem: The runtime still had direct `JobHandle.Complete()` call sites and treated chemical overlay buffer `71153` as mandatory. Direct completes weaken dispatcher-fence evidence. A missing overlay should not disable toxin sampling from the published chemical grid.

Solution: Route cold bootstrap and late-frame job reclamation through Core `DispatcherJobFence.TryComplete`. Track chemical readback locks with `_chemicalReadbackLockedCount`: published grid, telemetry ring, telemetry cursor, and tuning are mandatory; overlay is locked and sampled only when present.

Rejected Alternatives: Re-adding `using Hecton8.World` for `DispatcherJobSwap` was rejected as a compile-wall regression. Keeping overlay mandatory was rejected because the published grid already contains the normalized toxin scalar needed by Task 09. Reading an unlocked overlay was rejected because Vault compaction locks must match pointer lifetime.

Scalability potential: Low/Middle/High/Ultra all keep authoritative toxin sampling from the published grid. Higher tiers can blend the overlay when the owner publishes it; absence of overlay degrades presentation fidelity, not survival truth.

Hardware Impact: No profiler-backed microsecond claim. Runtime branch cost is one integer lock-count check before overlay sampling. Removing direct completes improves scheduler discipline; optional overlay avoids a false-negative chemical path when only the published grid is available.

## Decision 020: Staged Signal Output Instead Of Burst Queue Writers

Problem: `SignalBus<T>.ParallelWriter` exposes a Burst-friendly queue writer but no producer job-handle registration route. Core flushes SignalBus queues in pre-simulation. If a metabolism job missed the late-frame non-blocking completion window, a later pre-simulation flush could read a queue while the unfinished job was still enqueuing starvation or toxin damage signals.

Solution: Add owner-local Vault buffers `70274` and `70275` for staged `PhysiologyStateSignal` and `CombatDamageSignal` outputs. `MetabolicIntegrationJob` now writes fixed per-row unmanaged signal slots and never touches `SignalBus<T>.ParallelWriter`. `LateFrameTick` publishes staged slots through `SignalBus<T>.TryPush` only after `DispatcherJobFence.TryComplete` confirms the job is finished. Cold init and each integrator pass clear the relevant slots, so stale signals cannot replay.

Rejected Alternatives: Keeping `ParallelWriter` inside the job was rejected because there is no Core producer-fence registration route. Blocking `publishHandle.Complete()` every SlowTick was rejected because the mandate forbids arbitrary main-thread job stalls. Adding a new Core SignalBus API was rejected because SHINOBU_145 must not widen shared Core headers during this batch. Managed event buffering was rejected as GC and ownership drift.

Scalability potential: Low devices pay one fixed-slot scan only on the stretched metabolism cadence. Middle/High/Ultra keep the same deterministic staging route; richer visuals still come from shader globals, not extra CPU signal fanout. Staging capacity scales continuously with entity capacity and does not drop authoritative entities.

Hardware Impact: Expected cost shifts from parallel NativeQueue enqueue contention to contiguous Vault writes plus a post-completion linear scan of staged slots. No profiler-backed microsecond number is claimed. The architectural gain is removal of a race-prone queue writer from unfinished jobs without a per-tick blocking sync point.
