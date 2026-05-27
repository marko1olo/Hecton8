# Rationale 1315 - MEMORY_SOVEREIGN_VOXEL_ENGINE_EXORCIST

Date: 2026-05-26
Domain: Assets/_Project/Scripts/HectonVoxelEngine.cs; Assets/_Project/Scripts/World/Voxel

## Decision 000 - Session State Files

Problem: Agent 1315 status/rationale files were absent at session start; workflow requires disk-backed memory before code work.
Solution: Created Status_1315.md and Rationale_1315.md before source mutation.
Rejected Alternatives: Chat-only tracking rejected because context compression would erase task state.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 001 - Mandate Set Before Source Mutation

Problem: Voxel memory exorcism touches native ownership, jobs, DTO layout, telemetry, registry routes, and deformation cost.
Solution: Bound the task to eight registry mandates: native memory/job protocol, zero-GC, ARM64 layout, crash telemetry, voxel MC pipeline, voxel carving persistence, GlobalRegistry DI, and cinematic fake-first.
Rejected Alternatives: Reading unrelated AI/audio/UI mandates rejected because they do not own voxel native memory or the target file route.
Scalability potential: Keeps Low/Middle/High/Ultra constraints visible before implementation; no binary quality switch accepted.
Hardware Impact: 0 us runtime impact; prevents design drift that would cost >100 us in voxel rebuild paths on i3/MX350.

## Decision 002 - Roslyn Baseline As Primary Proof

Problem: The batch claims exactly 124 forbidden aliases; grep cannot distinguish job parameters from persistent fields.
Solution: Ran the existing Roslyn SyntaxTree scanner and filtered `Assets/_Project/Scripts/HectonVoxelEngine.cs`. Result: 124 forbidden non-job field declarations, 74 allowed job parameter fields, 0 parse failures.
Rejected Alternatives: Regex-only report rejected because it counted local variables and job structs without ownership semantics.
Scalability potential: Low/Middle/High/Ultra all require relocation-safe scratch ownership before increasing voxel detail.
Hardware Impact: 0 us runtime impact; prevents unsafe persistent aliases that can hard-fault on low-end i3/MX350 during arena relocation.

## Decision 003 - DataVault Scratch Buffer Range

Problem: Streaming scratch arrays need per-slot stable BufferIDs without colliding with existing enum IDs.
Solution: Reserved dynamic cast range 74500..74996 for slot-local voxel scratch handles; existing enum IDs occupy 74390..74405 and 75000..75002 around this range.
Rejected Alternatives: Editing `BufferID` enum with 392 named scratch IDs rejected as metadata bloat; reusing one BufferID per element type rejected because slots need independent buffers.
Scalability potential: Low tier can keep one slot and low capacities; Middle/High/Ultra can scale slot count and mesh raw capacity continuously via existing `GlobalQualityWeight` math.
Hardware Impact: Expected gain is relocation safety rather than raw frame savings; avoids crash-class faults on i3/MX350 under defrag pressure.

## Decision 004 - NativeList/NativeParallelHashMap Bridge Risk

Problem: `GlobalDataVault` owns `NativeArray<T>` buffers through generation handles, but exposes no native-list or native-hashmap generation-handle API.
Solution: Marked `NativeList<CaveSpawnData>` and `NativeParallelHashMap<int3,VoxelModifiedCell>` as required bridge work: spawn points can become NativeArray+count; modified cells require either array-backed lookup DTOs or a new DataVault collection contract.
Rejected Alternatives: Hiding NativeList/HashMap inside another wrapper rejected as fake sovereignty; keeping `DataVaultExempt` allocator rejected as the primary breach.
Scalability potential: Array-backed modified cells would need Low/Middle/High/Ultra lookup strategy, from bounded linear scan to bucketed overkill lookup.
Hardware Impact: A naive linear modified-cell scan can exceed 0.1 ms on i3/MX350; bucketed bridge is required before claiming performance.

## Decision 005 - MC Table Lease Descriptor Substitution

Problem: `MCTables.JobTableLease` stored persistent `NativeArray<int>.ReadOnly` aliases, so the MC lookup tables stayed live outside the Vault descriptor route.
Solution: Replaced those aliases with `VaultGenerationHandle<int>` descriptors and pure read properties that resolve `ReadOnly` views through `GlobalDataVault.TryReadOnlyHandle`. Added `try/finally` release around MC table fill locks.
Rejected Alternatives: Keeping readonly aliases because the tables are static rejected; static permanence is still a relocation hazard and still violates one owner/one route. Copying tables into managed arrays rejected as GC and Burst-incompatible.
Scalability potential: Low/Middle/High/Ultra tiers all share one cold MC table route; higher tiers spend saved stability budget on voxel density, not duplicate lookup ownership.
Hardware Impact: Expected frame saving is 0 us; the gain is removal of a crash-class stale alias on low-end i3/MX350 and ARM64 devices.

## Decision 006 - Voxel Blackbox Ownership Metadata

Problem: Voxel telemetry was 32B and used a 1304 dump route, so 1315 crash proof lacked BufferID/SystemID/generation context.
Solution: Expanded `VoxelMeshPipelineTelemetryEntry` to explicit 64B and wrote BufferID, SystemID, ring generation, Vault generation, state hash, and frame counters. Updated dump path to `Docs/AgentLogs/Dump_1315_VoxelEngine.bin`.
Rejected Alternatives: Adding a second 1315 telemetry ring rejected as duplicate ownership; the existing 300-frame voxel blackbox is the correct single route.
Scalability potential: Low tier pays one fixed 64B ring entry per sample; Middle/High/Ultra can add visual overkill without losing crash provenance.
Hardware Impact: Estimated write cost remains under 5 us per sample on i3/MX350 because the hot path writes one fixed-size struct and no managed strings.

## Decision 007 - Explicit Mesh DTO Padding

Problem: `VoxelSurfaceVertex` and `VoxelColliderVertex` were sequential runtime DTOs with implicit layout dependence.
Solution: Converted both to explicit layout, 80B and 16B respectively, and added unused 4-byte mesh vertex attributes to keep Unity MeshData stride equal to DTO size.
Rejected Alternatives: Relying on Unity/CLR sequential layout rejected because ARM64 offset stability must be explicit. Reducing payload fields rejected because the mesh pipeline currently needs the existing visual and collider data.
Scalability potential: Low tier retains cheaper cadence/capacity through `GlobalQualityWeight`; High/Ultra can increase deformation/mesh quality without changing DTO identity or save/authority routes.
Hardware Impact: Surface vertex bandwidth rises by 4 bytes per vertex, collider vertex bandwidth rises by 4 bytes per vertex. Cost is accepted for alignment proof; if profiling exceeds 0.1 ms, the next valid fix is DTO packing with explicit shader contract update, not implicit layout.

## Decision 008 - Validator Route Reuse

Problem: The existing editor validation path still called `HectonVoxelEngine.ValidateAgent1304EnginePrivateLayouts`, and after the telemetry expansion that route would fail against stale 32-byte offsets.
Solution: Added `ValidateAgent1315EnginePrivateLayouts(ref uint)` in the primary target and routed the legacy 1304 method through it. The validator now asserts the MC raw vertex, private engine DTOs, 64-byte telemetry entry, and explicit mesh DTOs.
Rejected Alternatives: Adding a new editor script under `Assets/_Project/Scripts/World/Voxel` rejected because that exact directory does not exist. Editing the adjacent `World/VoxelSurfaceNets/Editor` validator rejected because it is historical 1304 ownership and unnecessary when the primary target can expose the updated byte map.
Scalability potential: Layout proof is tier-independent; Low/Middle/High/Ultra all use identical DTO identity and can scale only cadence/capacity.
Hardware Impact: 0 us runtime impact; editor-only guard prevents ARM64 misalignment faults before runtime.

## Decision 009 - Scratch Collection Compile Wall

Problem: The remaining 122 native aliases are not the MC tables; they are the streaming scratch slot/lease/pipeline state and include `NativeList<CaveSpawnData>` and `NativeParallelHashMap<int3,VoxelModifiedCell>`.
Solution: Stopped short of wrapper fakery and recorded the wall. `GlobalDataVault` has `NativeArray<T>` generation handles, but no first-party generation-handle equivalent for `NativeList<T>` or `NativeParallelHashMap<K,V>`. A valid bridge requires replacing spawn output with array+count and replacing modified-cell lookup with an array-backed bucket/table contract, then changing Burst job signatures.
Rejected Alternatives: Hiding the collections inside custom wrapper fields rejected as fake zero aliasing. Keeping unmanaged pointers as "scratch only" rejected because the async pipeline holds them across Awaitable phases. Rewriting jobs blindly rejected because it would alter carving truth without a validated lookup contract.
Scalability potential: Low tier needs linear or tiny bucket modified-cell lookup; Middle/High/Ultra need wider bucket counts and higher scratch capacities under continuous `GlobalQualityWeight`, not binary device switches.
Hardware Impact: On i3/MX350, a naive array scan for every voxel cell can exceed 0.1 ms; the bridge must be bucketed before it can replace the hash map honestly.

## Decision 010 - Failed Metric Report Instead Of Fabricated Pass

Problem: Task 20 demands absolute zero aliases, but Roslyn pass2 still reports 122 forbidden candidates in the primary target.
Solution: Generated `Docs/Reports/VAULT_EXORCISM_REPORT_1315.json` with status `FAIL_REMAINING_FORBIDDEN_ALIASES`, exact before/after counts, remaining owner groups, blockers, and SHA-256 proof.
Rejected Alternatives: Filtering out VoxelStreamingScratchSlot/VoxelPipelineData rejected because the prompt's hit list includes every persistent native collection field in `HectonVoxelEngine.cs`.
Scalability potential: Honest failure preserves the path to a correct Low/Middle/High/Ultra bridge rather than hiding a relocation crash behind green metrics.
Hardware Impact: 0 us runtime impact; prevents a false report that would leave i3/MX350 and ARM64 devices exposed to stale alias faults during Vault relocation.

## Decision 011 - Scratch Alias Exorcism Completed

Problem: Pass2 still left 122 persistent aliases in slot, lease, and pipeline scratch state.
Solution: Replaced every persistent scratch NativeArray/NativeList/NativeParallelHashMap field with `VaultGenerationHandle<T>` plus capacity integers. Runtime jobs receive only phase-local NativeArray views resolved from the Vault and counts stored as scalar fields.
Rejected Alternatives: Custom wrapper structs around NativeArray were rejected because Roslyn and runtime relocation would still see a persistent unmanaged alias. Keeping NativeList for spawn points was rejected because DataVault has no generation-handle list contract.
Scalability potential: Low uses smaller scratch capacities and sparse modified-cell scans; Middle/High/Ultra keep the same descriptor route and scale mesh/raw capacity continuously through quality-weight functions.
Hardware Impact: Removes stale-pointer crash class on i3/MX350 and ARM64. Runtime cost shifts to handle resolution and sparse modified-cell linear scan; expected below 0.1 ms when deltas are sparse.

## Decision 012 - Modified Cells Array Bridge

Problem: `NativeParallelHashMap<int3,VoxelModifiedCell>` was the last non-array scratch contract needed by density/color/dirty jobs.
Solution: Added explicit 24B `VoxelModifiedCellEntry` with `VoxelModifiedCell` plus absolute cell key and copied the delta processor's temporary map into a Vault-owned NativeArray plus count before scheduling jobs.
Rejected Alternatives: Editing `VoxelDeltaProcessor` outside the assigned file was rejected as cross-domain churn. Passing the temporary hash map through async pipeline state was rejected because it would survive across await boundaries.
Scalability potential: Low/Middle sparse edits use linear scan. High/Ultra can later replace the array with a bucketed array DTO without changing job ownership or persistent field policy.
Hardware Impact: Native temp map exists only inside the preparation phase and is disposed same call. Sparse lookup cost is acceptable; dense edits need bucketization before claiming frame-time savings.

## Decision 013 - Spawn Points Array Bridge

Problem: `NativeList<CaveSpawnData>` persisted in scratch slot/lease/pipeline and violated Memory Sovereignty.
Solution: Replaced spawn output with `NativeArray<CaveSpawnData>` plus `NativeArray<int>` counter. `VoxelSpawnPointJob` bounds-checks capacity and writes deterministic entries by counter.
Rejected Alternatives: Keeping NativeList and only clearing it was rejected because list capacity pointer remains a persistent native alias.
Scalability potential: Low keeps minimum spawn capacity; High/Ultra can increase capacity with the same count-buffer route.
Hardware Impact: Eliminates NativeList allocator churn and sentinel registration. Counter write cost is one integer update per accepted spawn.

## Decision 014 - AUP Float Cast Purge In Modified Paths

Problem: Voxel jobs were fed `ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble)`, producing direct absolute AUP casts before local-origin subtraction.
Solution: Modified-cell indexing now performs double-precision addition and cell division inside jobs using `double3 absoluteCellOffset`. Seam jobs no longer need absolute offset at all because boundary distance and terrain grid sampling are local to the volume. Procedural noise receives a 4096m wrapped offset computed in double before downcast.
Rejected Alternatives: Leaving the absolute offset cast because the legacy map is usually small was rejected; 100km boundary jitter would remain. Full double noise was rejected because it would buy precision where a wrapped visual fake preserves belief.
Scalability potential: Low avoids double work unless modified cells exist; High/Ultra keep stable noise and dirty-cell visuals without authority-route changes.
Hardware Impact: Double math is limited to modified-cell lookup paths. Seam jobs remove offset additions, saving small SIMD work per vertex on i3/MX350.

## Decision 015 - Compile Wall Classification

Problem: The project build must be verified, but `Assembly-CSharp.csproj` currently fails outside the voxel target.
Solution: Ran the build only after CPU was below 50% and no dotnet/csc processes were active. Captured failure: 177 errors in unrelated Hecton8.Core files, with no `HectonVoxelEngine.cs` diagnostics in output. Marked this as external compile wall, not a voxel static-gate failure.
Rejected Alternatives: Editing submarine, vegetation, fluid, PDA, or power files was rejected as domain sabotage. Fabricating compile success was rejected.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime; prevents hiding unrelated integration debt as voxel memory work.

## Decision 016 - Dead DataVaultExempt Allocator Removal

Problem: Pass7 hotpath scan still detected an unused cold `EnsureNativeArrayCapacity<T>` path capable of allocating `NativeArray<T>` through a DataVaultExempt allocator.
Solution: Removed the dead allocator constant, the unused capacity helper, and its sentinel registration helper. Kept only `DisposeTrackedNativeArray<T>` for local temporary arrays created elsewhere and disposed in the same owner scope.
Rejected Alternatives: Keeping dead cold allocation code rejected because it gives future code a direct route around Vault ownership.
Scalability potential: Low/Middle/High/Ultra all use the same Vault descriptor route; no hidden fallback allocator remains for voxel streaming scratch.
Hardware Impact: 0 us steady-state runtime; removes a crash-class future allocation path on i3/MX350 and ARM64.

## Decision 017 - MC Table Pin Lifetime Correction

Problem: Marching Cubes table locks were acquired before scheduling count/extract jobs and released after `await AwaitForJobCompletionAsync`, holding buffer pins across async yield boundaries.
Solution: Wrapped the schedule statements in `try/finally` and disposed `JobTableLease` immediately after `Schedule`. Added compaction-fence checks before `TryLockBuffer`, before `TryReadOnlyHandle`, and before telemetry/scratch write locks.
Rejected Alternatives: Keeping pins until completion rejected because it blocks Vault compaction across dispatcher boundaries. Removing table locking entirely rejected because acquisition still proves the table handle before the job receives a view.
Scalability potential: Low devices avoid compaction stalls; High/Ultra can keep larger MC workloads without forcing the Vault to wait on async table pins.
Hardware Impact: Saves compaction wait risk rather than direct frame time; prevents multi-frame pin stalls on i3/MX350.

## Decision 018 - Pointer-First Raw Vertex Layout

Problem: `MCRawVertex` was explicit but placed a 12-byte `float3` before the 8-byte `long edgeId`, violating the pointer/long-first ARM64 ordering rule.
Solution: Reordered `MCRawVertex` to `edgeId` at offset 0, `localPosition` at offset 8, and explicit `_pad0` at offset 20; updated the validator offsets.
Rejected Alternatives: Leaving the legacy field order rejected because explicit size alone is not enough for the prompt's byte-order mandate.
Scalability potential: Identical on all quality tiers; DTO identity is stable while capacity/cadence continues to scale through `GlobalQualityWeight`.
Hardware Impact: 0 us algorithmic cost; improves ARM64 alignment proof and cache predictability on low-end silicon.

## Decision 019 - Direct Delta Array Bridge

Problem: Pass10 still used a method-local `NativeParallelHashMap<int3,VoxelModifiedCell>` with `Allocator.TempJob` in `HectonVoxelEngine.TryPrepareModifiedCellsForPipeline`, so the final "green" claim still had a native allocation bridge in the rebuild path.
Solution: Added `VoxelDeltaProcessor.TryFillDeltaArrayForVolume` to write `VoxelModifiedCellEntry` records directly into the Vault-owned array and count buffer. Duplicate absolute cells are updated in place; overflow returns false with count `-1` and the engine fails closed through the existing scratch overflow telemetry route.
Rejected Alternatives: Keeping the hash map as "transient" was rejected because the user explicitly rejected that compromise. Adding a new Vault hash-map contract was rejected because it would be a cross-domain memory API change without time to prove compaction semantics.
Scalability potential: Low/Middle tiers keep sparse linear lookup and bounded capacities. High/Ultra can replace the array with a bucketed DTO later without restoring persistent native fields.
Hardware Impact: Removes one TempJob hash-map allocation plus `GetKeyValueArrays(Allocator.Temp)` per modified-volume rebuild. On i3/MX350 the exact microsecond win depends on dirty-cell count; the deterministic gain is zero allocator churn.

## Decision 020 - VoxelDeltaProcessor Lock Fence Bridge

Problem: The delta processor's blackbox, queued carve queue, and scheduled carve write buffer acquired DataVault locks without immediate compaction-fence checks. Scheduled carve writes also kept `_scheduledCarveWritesLocked` beyond schedule and through commit progress.
Solution: Added `IDataVault.IsCompactionFenceActive` checks before and after lock/pin acquisition. Scheduled carve write buffers now unlock in the schedule `finally`; commit reacquires the lock only for the current late-frame commit slice and releases it in `finally`.
Rejected Alternatives: Holding the lock until job completion or until the multi-frame commit drain was rejected because it blocks Vault compaction across dispatcher boundaries. Completing the carve job immediately was rejected as a hidden sync point.
Scalability potential: Low devices avoid compaction stalls during carving. Middle/High/Ultra preserve continuous commit budgets through `GlobalQualityWeight` without changing memory ownership.
Hardware Impact: Direct CPU gain is small; the important gain is removal of multi-frame pin stalls and dangling pointer risk under compaction on i3/MX350 and ARM64.

## Decision 021 - AUP Runtime Conversion Clamp

Problem: VoxelDeltaProcessor used `HectonFloatingOrigin.ToRuntimePosition` directly in touched carve paths; that helper subtracts in double but does not expose the explicit clamp required by the 1315 gate.
Solution: Added local conversion that computes `deltaAup = targetAup - HectonFloatingOrigin.CurrentTotalOffsetDouble` in double3, clamps to +/-1048576 meters, and only then casts to float3/Vector3. HectonVoxelEngine rebuild center conversion now uses the same explicit subtract-clamp-cast pattern.
Rejected Alternatives: Trusting the generic helper was rejected because the prompt requires the arithmetic order to be visible in the touched code. Full double runtime positions were rejected because Unity transform and mesh APIs remain float-bound.
Scalability potential: Low/Middle/High/Ultra get identical coordinate authority while visual fidelity continues scaling through capacity/cadence, not through precision loss.
Hardware Impact: Adds negligible double math on cold carve/rebuild conversion; prevents 100km-class vertex jitter that would be visible on every device tier.

## Decision 022 - Pass13 Legacy AUP Bridge Removal

Problem: Re-audit found HectonVoxelEngine still carried legacy float-space AUP bridges: MapMagic AUP height sampling, `AbsoluteUniverseOffsetAtStart` float storage, and rebuild paths that rebased captured data by downcasting committed absolute offsets before local subtraction.
Solution: Removed the legacy downcast helper and float AUP field. All touched spatial handoffs now compute `targetAup - originAup` in double3, clamp to +/-1048576m, then cast the local delta to float3/Vector3. Captured cave graph nodes are rebased through `TryRebaseCapturedRuntimeFloat3`; MapMagic height queries receive runtime-local positions only.
Rejected Alternatives: Keeping AUP-specific MapMagic methods as "bridge-safe" was rejected because their names and call sites hid absolute-to-float conversion. Full double MapMagic integration was rejected because the third-party bridge API is float-based and runtime terrain sampling is local-space presentation, not authority storage.
Scalability potential: Low/Middle/High/Ultra share the same coordinate authority. Quality scaling remains cadence/capacity based; it never changes AUP identity or save truth.
Hardware Impact: The extra double subtract/clamp is cold pipeline work. It prevents visible far-origin jitter at 100km-class map boundaries on all devices, including i3/MX350.

## Decision 023 - Pass13 Modified Cell DTO Byte Order

Problem: `VoxelModifiedCellEntry` and `VoxelModifiedCell` were explicit and 8-byte aligned, but the prior report still showed payload-before-key order and the small modified-cell payload placed byte fields before ushort fields.
Solution: Reordered `VoxelModifiedCellEntry` to `AbsoluteCell` at offset 0, `Cell` at offset 12, `_pad0` at offset 20. Reordered `VoxelModifiedCell` to `half`, `ushort`, `ushort`, `byte`, `byte` for a deterministic 8B DTO. Updated the private layout validator and final byte map.
Rejected Alternatives: Leaving the pass12 map in reports rejected because it would be a false proof artifact. Moving the modified cell to a managed class rejected because Burst jobs and Vault arrays require unmanaged payloads.
Scalability potential: DTO identity is stable across all tiers; Low/Middle/High/Ultra scale array capacity and cadence only.
Hardware Impact: 0 us algorithmic change. The value is ARM64 offset proof and removal of report/code mismatch.

## Decision 024 - Pass13 Build Classification

Problem: Full `Assembly-CSharp.csproj` verification failed after compiling `Hecton8.Core.dll`, but the failure was a locked `Hecton8.Editor.dll` output owned by `VBCSCompiler`, not a source diagnostic.
Solution: Shut down build servers, waited for compiler processes to drain, and ran the target `Hecton8.Core.csproj` build with shared compilation disabled. It succeeded with 0 errors.
Rejected Alternatives: Editing Editor or Audio files rejected as outside the 1315 domain. Reporting full build green rejected because the editor DLL lock is still a real external verification blocker.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime; prevents misclassifying external editor infrastructure as voxel code failure.

## Decision 025 - Pass14 Dead HashMap Bridge Removal

Problem: `VoxelDeltaProcessor.cs` still exposed unused `TryFillDeltaMapForVolume` and `TryFillDeltaMapForVolumeAsync` APIs that accepted `NativeParallelHashMap<int3,VoxelModifiedCell>`. They were not fields and had no call sites, but they preserved a stale ingress back to the rejected hash-map bridge.
Solution: Removed both dead map-fill methods and their async budget helper. The only remaining modified-cell export route is the Vault-owned `NativeArray<VoxelModifiedCellEntry>` plus count buffer.
Rejected Alternatives: Keeping the unused public methods because they were not field declarations was rejected; future callers could revive TempJob hash-map allocation and undermine the proof.
Scalability potential: Low/Middle/High/Ultra keep the same array-count bridge. Dense edit scalability remains a future bucketed DTO problem, not a native hash-map fallback.
Hardware Impact: 0 us steady-state change. It removes dead code and eliminates one future allocator path on i3/MX350.

## Decision 026 - Pass14 Cancellation Fail-Closed Rewrite

Problem: HectonVoxelEngine async voxel pipeline still contained `ct.ThrowIfCancellationRequested()` and token-bearing `NextFrameAsync(ct)` calls, which can create managed exception paths under cancellation.
Solution: Replaced them with `ct.IsCancellationRequested` checks returning `false`, `default`, or early void returns. Token-free frame waits are followed by explicit fail-closed checks.
Rejected Alternatives: Treating cancellation exceptions as harmless was rejected because the task requires no managed throw paths in production simulation flow.
Scalability potential: No tier behavior change; cancellation now degrades by skipping/defering voxel work instead of throwing.
Hardware Impact: 0 us nominal frame gain. It removes exception construction/unwind risk during cancellation on low-end hardware.

## Decision 027 - Pass14 Final Build and Scanner Closure

Problem: The previous pass14 proof was static-green but build-unclosed because CPU load and external compiler processes violated the project build lane rule. The source then received final return-type cleanup after cancellation rewrite, so scanner hashes also had to be refreshed.
Solution: Waited until CPU was below 50 percent and no dotnet/csc/VBCSCompiler process existed. Re-ran the Roslyn native-alias scanner and the two voxel hotpath scanners, then built `Hecton8.Core.csproj` with shared compilation disabled. The target project includes both touched source files and succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reporting the earlier scanner hashes after source edits was rejected as stale evidence. Launching a build while VBCSCompiler or csc was active was rejected by project policy. Running a full editor build as the primary proof was rejected because prior full-project failures were external editor/audio/lock conditions outside the 1315 source boundary.
Scalability potential: No runtime behavior change. The proof preserves the Low/Middle/High/Ultra scratch descriptor route and confirms no native allocator fallback was reintroduced.
Hardware Impact: 0 us measured runtime gain. Verification result: target persistent native fields remain 0; HVE/VDP native TempJob/Persistent allocations remain 0 in the hotpath scanner; target compile passes.

## Decision 028 - Pass15 Solver Branch Reduction

Problem: A direct job-body branch scan still found avoidable solver branches in the Marching Cubes cube mask/count path, quantization snap, edge interpolation setup, and scheduled carve active selection. Leaving those as-is would make the branchless-SIMD proof weaker than the code could support.
Solution: Converted MC cube-index construction and triangle count to `math.select`, unrolled the five-triangle count, removed conditional edge interpolation by computing all twelve edge candidates, made MC interpolation use a branchless safe-diff midpoint path, converted quantization zero-snap to `math.select`, and changed `CarveSdfJob` to write an active mask instead of returning early. Fail-closed bounds guards remain because removing them would risk out-of-bounds writes.
Rejected Alternatives: Rewriting the entire Marching Cubes implementation into a new mesher was rejected as migration scope creep without seam/profiler/save proof. Removing safety branches was rejected because memory safety and fail-closed execution outrank branchless purity. Reporting branchless compliance without changing source was rejected as false evidence.
Scalability potential: Low/Middle/High/Ultra keep the same MC pipeline and continuous quality/capacity scaling. The change reduces branch pressure in the hottest deterministic math without changing DTO identity, save truth, or buffer ownership.
Hardware Impact: Microsecond gain not profiled. Expected effect is lower SIMD divergence in MC count/extract and carve write preparation on i3/MX350; measured runtime proof remains pending.

## Decision 029 - Pass16 AUP Clamp Closure

Problem: Re-audit found HectonVoxelEngine still calling `OriginShiftEventData.RebaseCapturedRuntimePosition`. That helper subtracts in double precision, but it casts to `Vector3` without the explicit clamp required by the 1315 AUP gate.
Solution: Replaced all four HVE call sites with local `TryResolveRuntimeFloat3FromAup` or `TryRebaseCapturedRuntimeFloat3`, both of which compute the double3 delta, clamp to +/-1048576m, then cast. Kept the fix inside the voxel target instead of changing shared origin-shift API ownership.
Rejected Alternatives: Editing `OriginShiftEventData.cs` was rejected because the domain owner for global origin-shift policy is not 1315. Keeping the calls because they already subtract in double was rejected because the prompt explicitly requires clamp before cast.
Scalability potential: Low/Middle/High/Ultra share identical coordinate authority; quality scaling remains capacity/cadence based and cannot alter AUP identity.
Hardware Impact: Negligible CPU cost on i3/MX350. The gain is deterministic prevention of far-origin mesh placement jitter and overflow-class float casts.

## Decision 030 - Pass17 Dirty Tree Boundary

Problem: The working tree contains many modified and untracked C# files outside the 1315 voxel domain. Treating all of them as 1315-owned would either fabricate ownership or force edits across unrelated agents' work.
Solution: Re-scanned the full source root for global context, but classified compliance JSON against the 1315 write set: `HectonVoxelEngine.cs` and the required `VoxelDeltaProcessor.cs` bridge. Recorded global out-of-domain forbidden candidates separately as non-1315 scope.
Rejected Alternatives: Reverting or editing unrelated dirty files was rejected by domain boundary and concurrent-agent rules. Reporting the full global forbidden count as 1315 failure was rejected because it would conflate other agents' work with this task.
Scalability potential: No runtime effect. It prevents cross-domain churn while preserving the voxel route's Low/Middle/High/Ultra scaling guarantees.
Hardware Impact: 0 us runtime. The value is integration safety under concurrent work.

## Decision 031 - Pass18 Attribute-Aware Re-Audit

Problem: The repeated rejection demanded a fresh prompt extraction and file re-audit. A strict literal `<AGENT_PROMPT id="1315">` regex failed because the actual active tag is `<AGENT_PROMPT id="1315" role="MEMORY_SOVEREIGN_VOXEL_ENGINE_EXORCIST" chat_name="1315">`.
Solution: Re-extracted with an attribute-aware XML tag regex, re-ran the native alias Roslyn scanner and both voxel hotpath Roslyn scanners, manually checked the AUP and lock source windows, regenerated the final JSON report, and rebuilt only `Hecton8.Core.csproj` under CPU/compiler guard. The target C# write set remains `HectonVoxelEngine.cs` and `VoxelDeltaProcessor.cs`.
Rejected Alternatives: Treating the first failed extract as a missing assignment was rejected because `Select-String` proved the tag exists at line 23. Rebuilding the whole dirty solution was rejected because unrelated agents own many modified C# files and the task proof requires the target project that explicitly includes the two 1315 files.
Scalability potential: No runtime behavior change. The pass preserves continuous `GlobalQualityWeight` scaling for voxel capacities/cadence and keeps the array-count modified-cell bridge as the low-tier path; dense edits still need a future bucketed DTO if profiler data proves linear sparse lookup too expensive.
Hardware Impact: 0 us measured runtime change in pass18. Static proof remains: target persistent native fields = 0, target native TempJob/Persistent allocations = 0, and Hecton8.Core build = 0 warnings/0 errors. Unity/Profiler/GCMonitor runtime proof is still absent.

## Decision 032 - Pass19 Bucketed Modified-Cell Bridge

Problem: The pass18 array-count modified-cell bridge removed the native hash map but left dense edits with a full modified-cell scan from `VoxelDensityJob`, `VoxelColorJob`, `VoxelDirtyBlendJob`, and duplicate detection in `VoxelDeltaProcessor.TryFillDeltaArrayForVolume`. That is correct for sparse edits but stupid under heavy carving.
Solution: Added Vault-owned `int` bucket heads and next-chain scratch arrays beside the existing `VoxelModifiedCellEntry` array. The delta processor now fills the bucket chain while writing entries; the three Burst jobs resolve cells through the bucket instead of scanning every modified cell.
Rejected Alternatives: Restoring `NativeParallelHashMap<int3,VoxelModifiedCell>` was rejected because it reintroduces the collection class the mandate removed. Keeping the linear scan was rejected because dense carving would scale as samples multiplied by dirty cells. A managed dictionary was rejected as GC and Burst-incompatible.
Scalability potential: Low tier keeps small bucket capacity and sparse edits cheap. Middle/High/Ultra increase scratch capacity through the same `GlobalQualityWeight`-governed route and get dense edit lookup without changing save truth or DTO identity.
Hardware Impact: Exact microseconds not profiled. Expected gain on i3/MX350 is removal of worst-case `O(voxelSamples * modifiedCells)` lookup pressure during dense carve rebuilds; static proof is zero new native TempJob/Persistent allocations.

## Decision 033 - Pass19 Scheduled Carve Raw Pointer Removal

Problem: `CarveSdfJob` wrote through `CarveCellWrite* WritesPtr` sourced from `NativeArrayUnsafeUtility.GetUnsafePtr(scheduledWrites)`. That bypassed NativeArray safety metadata and made the job harder to audit against the Vault descriptor route.
Solution: Removed the raw pointer field and scheduled pointer extraction. The Burst job now writes `Writes[index]` through the `NativeArray<CarveCellWrite>` job parameter with an explicit created/length guard.
Rejected Alternatives: Keeping the pointer because it is fast was rejected; the job already has a NativeArray writer and the pointer adds no proven profiler win. Completing the job immediately was rejected as a hidden sync point.
Scalability potential: Same carve math and same quality scaling. The change narrows the write surface without reducing Low/Middle/High/Ultra visual capacity.
Hardware Impact: Direct timing not measured. Expected cost is negligible relative to the SDF body; the safety gain is removal of a raw scheduled write path on ARM64 and desktop.

## Decision 034 - Pass19 Build Guard Blocker

Problem: After pass19 edits, target compile proof is required, but the machine stayed above the project CPU guard for six minutes and reached 100% while two external `Test-H8PublicationGate.ps1` PowerShell processes were active. The build rule forbids `dotnet build` under >50% CPU or concurrent compiler load.
Solution: Did not launch `dotnet`. Re-ran static Roslyn gates, direct forbidden-pattern scans, and regenerated the final JSON report with status `STATIC_VERIFIED_BUILD_BLOCKED_BY_CPU_GUARD` instead of fabricating green.
Rejected Alternatives: Killing other agents' publication gates was rejected. Launching `dotnet build` under 100% CPU was rejected by project policy. Reporting `VERIFIED_GREEN` without compile proof was rejected as a fake report.
Scalability potential: No runtime effect. This preserves concurrent-agent safety and keeps the build lane deterministic.
Hardware Impact: 0 us runtime. Verification gap remains: Hecton8.Core compile after pass19 is not proven until CPU drops below 50% and external publication gates finish.

## Decision 035 - Pass21 Unity Job Lifetime Correction

Problem: Re-audit plus Unity NativeContainer docs showed the earlier "release pins immediately after schedule" interpretation is unsafe for GlobalDataVault-backed scratch views. Unity jobs copy the NativeContainer wrapper, but all copies point to the same native memory; `Complete` is the documented point where owner-side access is safe again. If GlobalDataVault compacts backing memory after schedule while a job still holds a view, the job can read stale memory.
Solution: Added a scratch job-lifetime Vault fence in `HectonVoxelEngine.cs` and hold it only while scheduled jobs or awaited CPU phases actively use scratch views. MC table job leases are also kept until the jobs that consume the table views complete. This is a deliberate correction to match relocation safety, not a performance flourish.
Rejected Alternatives: Releasing the Vault pin immediately after `Schedule` was rejected because it creates a dangling-pointer compaction window. Completing jobs immediately was rejected because it would create a hidden sync point and waste worker parallelism. Copying MC tables per job was rejected because it adds allocator and bandwidth cost for static lookup data.
Scalability potential: Low/Middle devices avoid undefined compaction faults; High/Ultra can run larger mesh phases without changing DTO identity. Compaction may defer during active voxel jobs, which is preferable to corrupt terrain data.
Hardware Impact: Direct microsecond gain not profiled. Expected cost is one tiny Vault fence acquisition per phase; benefit is prevention of crash-class stale views on ARM64 and low-end i3/MX350 under memory pressure.

## Decision 036 - Pass21 Scheduled Carve Lock Lifetime Correction

Problem: `VoxelDeltaProcessor.TrySchedulePendingCarve` previously unlocked scheduled carve writes immediately after scheduling the job. That made the write buffer eligible for Vault movement while `CarveSdfJob` still wrote to it.
Solution: Keep scheduled carve writes locked until `_scheduledCarveHandle` is observed complete, then unlock before late-frame commit drain reacquires per-slice access. This narrows lock lifetime to actual job use and avoids holding a write lock through the multi-frame commit phase.
Rejected Alternatives: Unlocking immediately after schedule rejected as stale pointer risk. Holding the lock until all commit slices finish rejected because it blocks compaction longer than needed. Replacing with a managed staging list rejected as GC and Burst-incompatible.
Scalability potential: Low devices get fewer compaction stalls than pass18; Middle/High/Ultra retain continuous carve commit budgets without changing save truth.
Hardware Impact: Direct timing not measured. Expected benefit is removal of a scheduled-write relocation race; expected cost is a short active-job pin.

## Decision 037 - Pass21 Mesh Upload Guard And Runtime Position Naming

Problem: Mesh upload validation accepted counts that could still produce invalid triangle buffers, and the surface DTO/report used `AbsolutePositionWS` wording for a runtime-local float channel. That name was a contract lie after AUP fixes.
Solution: `CanUploadMeshData` now rejects too-small vertex/index counts and non-multiple-of-three triangle counts before MeshData apply. The DTO/report name is `RuntimePositionWS`, matching the actual float-space runtime presentation channel; absolute AUP remains in double routes only.
Rejected Alternatives: Letting Unity reject invalid MeshData was rejected because the failure would occur after partial setup and is harder to report fail-closed. Keeping the old field name was rejected because misleading absolute/local labels cause future AUP regressions.
Scalability potential: All tiers share the same validity gate. Low tier drops malformed collider/surface updates instead of spending time on doomed uploads; High/Ultra can push richer mesh data only when triangle contracts are valid.
Hardware Impact: Guard cost is constant and negligible. It prevents bad collider/mesh upload work and potential PhysX/MeshData exceptions.

## Decision 038 - Pass21 Build Lane Honesty

Problem: After pass21 scanner proof, target compile should be rerun, but CPU was 98.9 percent with active `csc` and `dotnet`, then 81.8 percent with active `dotnet`. The project explicitly forbids launching `dotnet build` under those conditions.
Solution: Did not start a competing build. Re-ran static Roslyn and direct scans, regenerated the JSON report with status `STATIC_VERIFIED_TARGET_BUILD_BLOCKED`, and recorded the exact blocking process state.
Rejected Alternatives: Killing other agents' build lane was rejected. Reporting `VERIFIED_GREEN` without a fresh compile was rejected. Editing external compile-error files was rejected because they are outside the 1315 domain.
Scalability potential: No runtime effect. The concurrency rule prevents build noise and avoids trampling other agents' work.
Hardware Impact: 0 us runtime. Verification gap remains: rerun `Hecton8.Core.csproj` when CPU is below 50 percent and no `dotnet/csc/VBCSCompiler/MSBuild` process is active.
## Decision 039 - Pass22 Final Scratch Fence Closure

Problem: Re-audit found that final mesh/collider phases still used Vault-backed scratch views after async/job boundaries. Projection, surface upload, selected pillar lookup, smooth pillar collider upload, and chunked collider upload could all read relocated backing memory if GlobalDataVault compacted after a prior await.
Solution: Re-resolve scratch arrays only inside `TryLockStreamingScratchJobLifetime` windows, hold the fence across jobs while their NativeArray views are live, and release before PhysX bake/debt waits where the job no longer owns scratch memory.
Rejected Alternatives: Keeping stale `NativeArray` locals after await was rejected as relocation-unsafe. Locking the full collider bake across PhysX/debt waits was rejected because it would block compaction longer than the actual native view lifetime. Copying scratch into managed arrays was rejected as GC and a truth-route violation.
Scalability potential: Low devices get relocation safety without forced sync. Middle/High/Ultra keep larger mesh/collider capacities through the same Vault descriptor route; compaction is delayed only for active native view ownership.
Hardware Impact: Direct microseconds not profiled. Expected cost is one short Vault fence per finalization phase; expected gain is removal of crash-class stale scratch reads on ARM64 and i3/MX350 under memory pressure.

## Decision 040 - Pass22 Collider Bucket Fail-Closed Bounds

Problem: The first pass22 collider refactor still read `bucketOffsets[chunkIndex]` before validating `IsCreated/Length`, and trusted classifier bucket bytes before indexing bucket arrays. That violated fail-closed execution even though Roslyn allocation gates were clean.
Solution: Added explicit bucket id, write-head, triangle-base, chunk-offset, triangle-multiple, remap-index, local-vertex-capacity, and touched-reset bounds checks before every derived collider array access and mesh upload.
Rejected Alternatives: Trusting the classifier job was rejected because fail-closed paths must prevent memory/index violations proactively. Catching exceptions was rejected by the no-throw mandate. Restoring NativeParallelHashMap was rejected because the bucket DTO bridge is the correct Vault-owned route.
Scalability potential: Low tier drops malformed collider chunks instead of spending time on doomed bakes. Middle/High/Ultra keep chunked collider visual density without changing DTO identity or save truth.
Hardware Impact: Bounds checks are current-frame CPU cost and not yet profiled; they are cheaper than undefined native memory access or invalid MeshData/PhysX work on low-end hardware.

## Decision 041 - Pass22 External Compile Wall

Problem: After pass22 source fixes and scanner closure, `Hecton8.Core.csproj` failed before target verification could finish because modified external `World/FloraRegrowthDirector.cs` has syntax errors at lines 1490-1548.
Solution: Recorded the compile wall and did not edit or revert the external World file. The 1315 write set remains `HectonVoxelEngine.cs` and `VoxelDeltaProcessor.cs`; both parse clean under Roslyn scanners.
Rejected Alternatives: Editing FloraRegrowthDirector was rejected as cross-domain interference. Reporting compile green was rejected as false. Killing or reverting another agent's work was rejected by the concurrent-agent rule.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime; this is an integration lane blocker, not a voxel algorithm cost.

## Decision 042 - Pass23 Early Scratch Yield Purge

Problem: Re-audit found an overcorrection from pass21/pass22: early `ExecuteVoxelPipelineAsync` held the scratch lifetime fence while CPU code filled terrain height rows, biome modifier rows, and raw MC vertex offsets, then yielded for budget. That prevents stale views, but it also blocks GlobalDataVault compaction across async yields when no scheduled job owns the view.
Solution: Split CPU preparation into short lock windows. `FillTerrainHeightGridForPipelineAsync`, `FillBiomeModifierGridAsync`, and `BuildRawVertexOffsetsAsync` now lock the scratch fence, re-resolve the needed Vault view, mutate only the current row/slice, unlock in `finally`, and only then call the budget yield. Job phases still hold the fence until completion because Unity jobs retain native memory views until `Complete`.
Rejected Alternatives: Holding the early lock across row yields was rejected because it blocks compaction longer than necessary. Releasing job-owned views immediately after schedule was rejected because it reintroduces stale pointer risk. Copying scratch data to managed arrays was rejected as GC and a truth-route violation.
Scalability potential: Low devices get compaction opportunities between expensive preparation slices. Middle/High/Ultra keep larger voxel capacities and visual-overkill mesh density without changing DTO identity or authority routes. The approach scales by cadence/capacity through existing `GlobalQualityWeight`, not binary device switches.
Hardware Impact: Exact profiler proof is absent. Expected low-end effect is reduced memory-compaction stalls during chunk generation at the cost of extra short Vault pin/unpin calls per row/slice; this is preferable to holding a global compaction block through async yields on i3/MX350 and ARM64.

## Decision 043 - Pass23 Report Byte-Map Honesty

Problem: The final JSON report still described an older 24B `CarveCellWrite` layout, while the actual `VoxelDeltaProcessor.cs` source defines a 32B explicit layout with `BlendStrength` as a float and two trailing uint pads.
Solution: Marked the report as stale until regeneration. The truthful byte map is `AbsoluteCellX@0:int`, `AbsoluteCellY@4:int`, `AbsoluteCellZ@8:int`, `BlendStrength@12:float`, `SdfValueBits@16:ushort`, `MaterialId@18:byte`, `DeltaFlags@19:byte`, `IsActive@20:byte`, `_pad0@21:byte`, `_pad1@22:ushort`, `_pad2@24:uint`, `_pad3@28:uint`, total 32B.
Rejected Alternatives: Leaving the stale report rejected because proof artifacts must match source. Editing the DTO only to match the stale report was rejected because the current 32B layout is explicit and aligned.
Scalability potential: No runtime behavior change. Accurate proof prevents future agents from optimizing against a false memory contract.
Hardware Impact: 0 us runtime; evidence quality only.

## Decision 044 - Pass24 Scheduled Carve Frame Boundary Pin Closure

Problem: `VoxelDeltaProcessor` pass21 correctly kept `ShinobuDeltaCrusherCarveWrites` pinned until `CarveSdfJob` stopped owning the native view, but the nonblocking late-frame completion path could return when the job was still running. That leaves `_scheduledCarveWritesLocked` true across a frame boundary and blocks DataVault relocation during the next `PRE_SIMULATION` compaction window.
Solution: Added `TryFinalizeScheduledCarveJobBeforeFrameBoundary`. It first uses the existing nonblocking dispatcher swap completion. If the worker misses the same-frame late swap, it writes `VoxelBlackBoxScheduledCarveJobOverrunFlag`, publishes numeric telemetry with `_scheduledCarveWriteCount`, then performs an explicit fail-closed completion before releasing the Vault pin and entering commit slicing.
Rejected Alternatives: Unlocking before job completion was rejected because Unity jobs retain native memory views until completion. Holding the pin across frames was rejected because it violates the compaction contract. Replacing the carve job with a large synchronous CPU loop was rejected for now because a 131k-cell carve can exceed the frame budget on i3/MX350; the overrun path is visible telemetry debt, not a silent normal path.
Scalability potential: Low/Middle devices now get a hard compaction boundary and an explicit overrun signal if carve work is too large. High/Ultra keep the parallel job for normal carve staging. Future tuning can lower carve span or split candidates continuously by `GlobalQualityWeight` without changing DTO identity.
Hardware Impact: Normal path unchanged. Overrun path may spend a blocking completion spike, but it prevents a worse cross-frame Vault pin and records the exact write-count pressure for profiler follow-up.

## Decision 045 - Pass25 Scheduled Carve Slice Budget

Problem: Pass24 prevented a cross-frame Vault pin, but the root pressure remained: a single carve could stage up to `ChunkCellCount*4` candidates and then rely on a frame-boundary fail-closed completion if the worker missed the late swap.
Solution: Bounded scheduled carve staging to 2048..8192 candidates per job, scaled by continuous `GlobalQualityWeight` and backlog pressure. Large carve truth is preserved by fixed pending-queue continuation records using a linear candidate offset; the job writes only the current slice into the Vault buffer.
Rejected Alternatives: Keeping one 131k-candidate job was rejected because it makes the overrun completion path normal under low-end pressure. Dropping oversized carves was rejected because it changes gameplay truth. Allocating a NativeList/NativeQueue of active cells was rejected because the task specifically removed persistent native collection ownership from this domain.
Scalability potential: Low uses smaller slices and spreads deformation over more frames. Middle/High/Ultra increase slice size without changing DTO layout, save identity, or carve authority. The result is latency scaling, not truth scaling.
Hardware Impact: Expected low-end gain is removal of worst-case scheduled job spikes and a 4MB scheduled write buffer. New max scheduled write staging is 8192 * 32B = 256KB. Profiler proof remains pending because build/runtime lanes were blocked by CPU guard.

## Decision 046 - Pass25 Continuation Truth Safety

Problem: First pass25 draft enqueued the continuation before the current slice acquired the Vault write buffer. If lock/schedule failed afterward, the continuation would skip the failed slice and leave a carve hole.
Solution: Added a precheck for continuation slot capacity, but writes the continuation only after the current `CarveSdfJob` has been successfully scheduled. If the slot is somehow unavailable, the system records queue overflow telemetry instead of silently pretending the full carve is complete.
Rejected Alternatives: Enqueuing early was rejected after self-review. Cancelling a successfully scheduled Unity job to recover a continuation failure was rejected because there is no safe zero-GC cancellation path for the live job memory view.
Scalability potential: Same across Low/Middle/High/Ultra; correctness does not vary by tier. Only slice count and latency vary.
Hardware Impact: One fixed-array queue write per sliced continuation. No managed allocation. Prevents save/mesh divergence that would be more expensive to debug than the queue check.

## Decision 047 - Pass25 Clamp And NaN Fail-Closed

Problem: Explicit radius, box half-extents, blend strength, and thermal melt radius could exceed the existing `MaxCarveRadiusMeters`; thermal melt also accepted NaN radius far enough to enter volume selection.
Solution: Clamp explicit carve shape budgets to the existing max radius before queue/schedule and reject non-finite thermal melt radius with blackbox telemetry. Commit scan now separates inactive candidate scanning from active mutation budget so inactive SDF cells do not consume the limited write budget.
Rejected Alternatives: Letting candidate-count rejection drop huge requests was rejected as silent no-op behavior. Raising commit writes globally was rejected because it risks main-thread spikes. Changing gameplay truth based on low/high tier was rejected by `GlobalQualityWeight` authority rules.
Scalability potential: Low/Middle/High/Ultra share the same physical carve cap. Low pays less per frame through smaller slices; Ultra gets faster convergence through larger slices and scan budget.
Hardware Impact: Expected i3/MX350 benefit is fewer overrun completions, smaller Vault write memory, and faster inactive candidate drain. Exact microseconds remain pending profiler/build proof.

## Decision 048 - Pass26 Thermal Melt Fail-Closed Semantics

Problem: Pass25 clamped `ThermalMeltEvent.RadiusMeters` before validating the raw input. A negative radius could become a minimum-radius accepted carve, and normal no-op states such as low heat or no registered volumes wrote `InvalidCarveEvent` telemetry. That is both a potential false carve and a bad postmortem signal.
Solution: Validate raw radius and heat before clamp. Only non-finite or non-positive radius / non-finite heat writes invalid-carve blackbox telemetry. Low heat, missing volume, or no target volume returns false silently. The active melt queue also purges null-volume and non-finite entries before merge/stage, and non-finite `deltaTime` is treated as zero so it cannot poison queue state.
Rejected Alternatives: Keeping the clamp-first behavior was rejected because invalid input must fail closed, not become gameplay truth. Writing telemetry for low heat/no volume was rejected because it trains the blackbox to lie during normal absence of work. Dropping the entire queue on one bad entry was rejected because bounded per-entry removal preserves valid pending visual deformation.
Scalability potential: Low/Middle/High/Ultra all share the same truth rules. Quality can change carve cadence and slice budgets, but invalid thermal input cannot create different gameplay on weak or high-end hardware.
Hardware Impact: Runtime cost is a few scalar finite checks inside a 16-entry fixed queue. Expected gain is removal of false melt work and cleaner crash telemetry on i3/MX350 and ARM64; exact microseconds are not profiled because build/runtime lanes are currently CPU-guard blocked.

## Decision 049 - Pass27 Queued Carve Fail-Closed And Resolver Naming

Problem: Ordinary `VoxelCarveEvent` ingress still had the same class of clamp/default bug fixed for thermal melt: a negative direct-queue radius or blend strength survived finiteness checks, then later became a default carve budget. The pass27 audit also found mutating token-budget methods named `Resolve*ThisFrame`, violating the local purity doctrine for read accessors.
Solution: Added `HasInvalidQueuedCarveShapeBudget` and call it before clamp in `TryQueueCarveEvent`, and again when draining stale queued events. Negative radius, negative blend strength, and negative box half-extents now fail closed with invalid-carve blackbox telemetry instead of becoming carve truth. Renamed mutating budget consumers to `ConsumeQueuedCarveDrainBudgetThisFrame`, `ConsumeScheduledCarveCommitWriteBudgetThisFrame`, `ConsumeDeferredVoxelPhysicsBakeTeardownDrainBudgetThisFrame`, and `ConsumeDeferredVoxelColliderUploadBudgetThisFrame`.
Rejected Alternatives: Treating negative radius as zero/default was rejected because it can create work from invalid input. Taking absolute value of external box half-extents was rejected because half-extents are a non-negative contract at ingress; internal APIs can still sanitize their own parameters before queueing. Leaving the `Resolve*` names was rejected because future callers would reasonably assume purity.
Scalability potential: Low/Middle/High/Ultra share identical carve truth. Quality still controls cadence and slice size only; invalid budget input cannot create different terrain on weaker devices.
Hardware Impact: Expected runtime cost is a few scalar comparisons per queued carve and zero managed allocation. Expected low-end gain is avoidance of false carve scheduling and cleaner telemetry; exact microseconds are not profiled because CPU guard blocked build/runtime verification.

## Decision 050 - Pass27B MC Table BufferID Generated-Project Contract

Problem: A subsequent allowed `Hecton8.Core.csproj` build reported target errors at `HectonVoxelEngine.cs` lines 42-43: the standalone project excludes `Assets/_Project/Scripts/Core/Memory/**/*.cs`, so it does not compile the current `H8Memory.cs` enum where `VoxelMarchingCubesEdgeTable = 644` and `VoxelMarchingCubesTriTable = 645` exist. It instead sees a stale generated `BufferID` assembly.
Solution: Keep the source-owned numeric buffer IDs and cast them locally in `MCTables`: `(BufferID)644` and `(BufferID)645`. This removes the stale generated-project enum-member dependency while preserving the exact IDs already present in `H8Memory.cs`.
Rejected Alternatives: Editing `H8Memory.cs` was rejected because the values already exist there and it is outside the 1315 write boundary. Allocating new high numeric IDs was rejected because 74317-74319 are already occupied and changing the IDs would break the established Vault route. Ignoring the build diagnostic was rejected because it was a real target compile error for the standalone build lane.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra still use one Vault-owned MC lookup table route; quality scaling remains in chunk cadence and carve slice budgets, not table identity.
Hardware Impact: 0 us runtime. Compile-route stability improves; fresh build proof remains pending because the post-fix guard samples were CPU 71% then 57%, above the project build threshold.

## Decision 051 - Pass27C MC Table Write-Lock Fence Check

Problem: `MCTables.TryAcquireWritableVaultTable` checked the compaction fence before `TryAcquireWriteLock`, but did not re-check immediately after the write lock was acquired. Other HVE/VDP lock paths already do the post-lock fence check; this cold initialization path lagged behind the mandate.
Solution: Added a post-lock `vault.IsCompactionFenceActive` check that releases the write lock, clears the local `NativeArray`, and fails closed before any table write occurs.
Rejected Alternatives: Relying on the pre-lock fence check was rejected because compaction can start between the check and lock acquisition. Wrapping the whole static initialization in a long global fence was rejected because MC table writes are already protected by per-buffer write locks and should not block unrelated compaction longer than necessary.
Scalability potential: No visual tier change. All tiers keep the same MC table identity; the fix improves relocation safety during cold boot/editor reload.
Hardware Impact: 0 us hot path. One cold branch during table publication prevents a stale write-lock window under memory pressure.

## Decision 052 - Pass28 Runtime Telemetry Acquisition Must Not Allocate

Problem: `VoxelDeltaProcessor.TryAcquireBlackBoxBuffer` and `HectonVoxelEngine.WriteVoxelMeshPipelineBlackBoxSample` still had lazy initialization routes in runtime telemetry write paths. If cold boot missed a buffer, the next simulation frame could call `EnsureGenerationHandle` instead of failing closed.
Solution: Both paths now require cached Vault handles and resolve existing buffers before acquiring write locks. Missing, stale, short, or fenced buffers return without allocation.
Rejected Alternatives: Keeping lazy telemetry allocation was rejected because blackbox writes happen inside runtime frames. Dropping telemetry entirely was rejected because postmortem proof is mandatory.
Scalability potential: Low/Middle devices avoid hidden Vault growth during pressure. High/Ultra keep the same 300-frame proof ring and can spend saved stability budget on visual density, not telemetry allocation.
Hardware Impact: Removes cold-path allocation risk from hot telemetry samples. Direct microseconds are not profiled; expected steady-state cost is unchanged fixed struct write.

## Decision 053 - Pass28 Queued Carve Acquisition Fail-Closed

Problem: `TryAcquireQueuedCarveEventBuffer` called `EnsureCarveEventQueue()` before every queue lock. That made gameplay ingress/drain capable of creating or growing the queue buffer during runtime instead of using the cold-created descriptor.
Solution: The acquisition path now resolves the cached `ShinobuDeltaCrusherCarveEventQueue` handle, checks the compaction fence before and after resolve, then acquires the write lock. If the handle is absent or short, it returns false.
Rejected Alternatives: Reinitializing the queue from `TryQueueCarveEvent` was rejected because it hides boot failure and can allocate under input pressure. Using a managed fallback queue was rejected as GC and authority-route drift.
Scalability potential: Low/Middle/High/Ultra all share fixed queue identity. Quality can change drain cadence, not whether invalid runtime allocation creates a new event lane.
Hardware Impact: Eliminates runtime Vault allocation risk on carve ingress. Expected cost is one extra existing-handle resolve before lock.

## Decision 054 - Pass28 Chunk-State Pool Resolve Split

Problem: `TryLeaseChunkState()` called `EnsureChunkStatePool()` in carve/load runtime, and `TryResolveChunkStateStorage()` used a helper that always called four `EnsureGenerationHandle` methods. Dirty-cell storage access could therefore allocate or grow Vault buffers while applying deformation.
Solution: Split cold `TryEnsureVaultChunkStatePoolStorage` from runtime `TryResolveVaultChunkStatePool`. `TryLeaseChunkState` now requires `_chunkStatePoolCreated`, `_chunkStatePoolVaultBacked`, and resolvable cached handles; failures write numeric blackbox samples and return false.
Rejected Alternatives: Letting the first carve repair the pool was rejected because it turns gameplay into memory initialization. Falling back to disposable per-chunk native arrays was rejected because it reintroduces unmanaged ownership outside the Vault.
Scalability potential: Low devices fail closed instead of stalling on pool allocation; Middle/High/Ultra still use the same fixed pool and can scale carve slice latency through `GlobalQualityWeight`.
Hardware Impact: Removes four possible `EnsureGenerationHandle` calls from dirty-state resolve paths. Exact microseconds are not profiled because CPU guard blocked the runtime lane.

## Decision 055 - Pass28 Hot Managed Warning Purge

Problem: Scheduled carve overrun, commit-budget warning, carved-mass metric, and chunk-state pool exhaustion used `GlobalTelemetryBus.PublishPerformanceWarning` from runtime carve paths. Even if that bus is usually cheap, it is managed dispatch inside simulation pressure.
Solution: Replaced those hot managed warning calls with fixed blackbox samples: overrun, commit budget, carved mass, and pool exhaustion each writes a numeric flag/value to the unmanaged ring. Cold DataVault rebind warnings remain managed because they are migration/control-plane events.
Rejected Alternatives: Keeping both blackbox and managed warnings in hot paths was rejected because the ring is the required proof channel. Removing all warnings including cold rebind was rejected because rebind is outside the hot simulation loop and already uses managed control-plane telemetry.
Scalability potential: Low tier avoids managed telemetry spikes during carving. High/Ultra keep richer deformation while pressure evidence remains fixed-size and allocation-free.
Hardware Impact: Removes managed warning dispatch from carve commit pressure paths. Microseconds not profiled; the deterministic gain is no managed telemetry route in those hot branches.

## Decision 056 - Pass29 Streaming Scratch Slot Cold Allocation

Problem: `TryAcquireStreamingScratchLease` still called `EnsureStreamingScratchSlots()`. If the first generation request reached this path before cold setup prepared slots, the lease acquisition path could allocate a managed `VoxelStreamingScratchSlot[]` and per-slot class objects.
Solution: Moved slot descriptor initialization to `OnEnable` and removed the slot-allocation call from runtime lease acquisition. Added canonical `COLD ALLOC` markers on the managed slot array and per-slot descriptor object allocations.
Rejected Alternatives: Preallocating every scratch buffer to the 128^3 worst case was rejected because it would burn low-end memory to solve a descriptor allocation problem. Removing generation-admission capacity growth was rejected because it would make normal chunk generation fail unless the engine reserved worst-case scratch lanes up front.
Scalability potential: Low/Middle/High/Ultra keep the existing continuous `GlobalQualityWeight` scratch capacity route. Slot descriptors are cold and fixed; buffer capacities still scale by owner admission rather than binary tier switches.
Hardware Impact: Removes one managed array allocation and up to eight managed descriptor allocations from the first runtime lease path. Exact microseconds are not profiled; build/profiler lanes are CPU-guard blocked.

## Decision 057 - Pass30 Explicit Scratch Buffer Pin List

Problem: `TryLockStreamingScratchJobLifetime` pinned only `ScratchLaneJobLifetimeFence`, while scheduled jobs and guarded CPU phases receive many other Vault-backed scratch `NativeArray` views. Current `GlobalDataVault` blocks arena compaction when any buffer is locked, so the dummy lock worked today, but the source contract did not prove which actual scratch buffers were protected.
Solution: `VoxelStreamingScratchLease` now stores a `FixedList512Bytes<BufferID>` of the actual scratch buffers locked for the current phase. The lock path collects 55 slot handles, checks the compaction fence before and after each `TryLockBuffer`, rolls back partial locks, and the unlock path releases exactly those buffers in reverse order.
Rejected Alternatives: Keeping the dummy fence was rejected because it relies on a global side effect rather than a direct proof artifact. Locking buffers by allocating a managed list was rejected as GC. Pre-locking the whole slot across the entire async pipeline was rejected because it would hold Vault pins across phase/yield boundaries.
Scalability potential: Low/Middle/High/Ultra share the same memory safety route. Quality can still scale scratch capacity and generation cadence; it cannot alter whether a job owns a valid Vault pin.
Hardware Impact: Adds bounded per-phase lock bookkeeping for up to 55 buffers and removes relocation ambiguity. Expected low-end benefit is fewer compaction/use-after-relocation failure modes; exact microseconds are not profiled because build/profiler lanes are blocked by CPU guard and active dotnet.

## Decision 058 - Pass31 CPU Scratch Mutations Require Pins

Problem: Pass30 made job lifetime pin actual scratch buffers, but the pass31 audit found CPU-side preparation still writing Vault-backed scratch arrays with only `EnterStreamingScratchGate`: modified-cell fill buffers, spawn count reset, and node/tunnel spatial buckets. The gate protects slot descriptor consistency, not Vault relocation.
Solution: Moved those writes behind `TryLockStreamingScratchJobLifetime` and `finally` unlock. Spatial bucket builders now fail closed, re-resolve arrays under the lock, and bounds-check write heads before writing indices. Capacity helpers are capacity-only again and no longer mutate scratch counters.
Rejected Alternatives: Keeping writes under the descriptor gate was rejected because it does not pin backing buffers. Holding a single lock across the whole async rebuild was rejected because it would pin Vault scratch across yield boundaries and block compaction. Moving bucket construction into new tiny jobs was rejected because the data set is small and would add scheduler debt without profiler proof.
Scalability potential: Low tier gets safer compaction under memory pressure; Middle/High/Ultra keep the same bucket fidelity and can still scale rebuild cadence/capacity via `GlobalQualityWeight`. No binary quality switch was introduced.
Hardware Impact: Adds short lock/unlock windows around CPU writes and removes relocation risk. Expected low-end gain is stability, not raw speed. Exact microseconds are not profiled because build/runtime lanes are blocked by CPU guard.

## Decision 059 - Pass31 Sonar Publish Scratch Snapshot Bridge

Problem: `ConfigureVolumeRuntimeDataAsync` passed `SmoothDensityField` from streaming scratch into an awaited sonar publication. Pinning that scratch view through the await would violate the compaction boundary, while not pinning it risks a job reading a relocated Vault buffer.
Solution: Added `ConfigureVolumeRuntimeDataFromPipelineAsync` and `TryCopySmoothDensitySnapshotFromScratch`. The engine allocates one registered transient `Allocator.TempJob` `NativeArray<float>`, locks the scratch slot, copies the smooth density source, releases the scratch lock, then awaits sonar publication from the independent snapshot and disposes it.
Rejected Alternatives: Holding the scratch lock through `PublishSonarSdfSnapshotAsync` was rejected because no Vault pin may cross async yields. Disabling sonar snapshot publication was rejected because it would silently degrade scanner/delta consumers. Editing `HectonVoxelVolume` was rejected because the source hazard can be resolved at the engine boundary without expanding the domain write set.
Scalability potential: Low devices pay one transient native copy at volume publication instead of risking relocation faults; High/Ultra keep the same sonar fidelity. Future optimization can replace the copy with a volume-owned Vault staging descriptor, but this pass needed a minimal contract-correct bridge.
Hardware Impact: Adds one transient native allocation/copy during cold volume publication. It is not a Tick/SlowTick/LateFrameTick path and is Sentinel-registered. Expected MX350 impact is bounded to generation admission; exact microseconds are not profiled because build/runtime lanes are blocked by CPU guard.

## Decision 060 - Pass32 Async Snapshot Lifetime Correction

Problem: Pass31 used `Allocator.TempJob` for a `SmoothDensityField` snapshot that is intentionally held across `await volume.PublishSonarSdfSnapshotAsync(...)`. The downstream encode job can wait multiple frames through `AwaitableDebtMonitor.NextFrameAsync()`, so the TempJob lifetime was not legally bounded. `HectonVoxelVolume` also allocated encoded sonar scratch and audio-material scratch as TempJob across the same awaited loop.
Solution: Changed the engine snapshot and both volume-side sonar scratch arrays to `Allocator.Persistent` registered as `NativeAllocationLifetime.TransientArena`, then disposed them in existing `finally` paths. Added `TryAcquirePublishedSonarWriteLock` so sonar Vault payload writes check the compaction fence before and after write-lock acquisition and release immediately if the fence appears after lock acquisition.
Rejected Alternatives: Keeping TempJob was rejected because the async wait can exceed the four-frame window. Holding the streaming scratch lock until sonar publication finishes was rejected because it would pin Vault scratch across frame boundaries. Writing directly into Vault payload buffers from the encode job was rejected because it would hold Vault write locks/pins through awaited job completion. Disabling sonar publication was rejected because it deletes a dependent visual/scan feature instead of fixing ownership.
Scalability potential: Low tier pays bounded local native memory during cold volume publication and avoids compaction stalls or TempJob leak warnings. Middle/High/Ultra keep identical sonar fidelity; future visual-overkill can replace this local snapshot with a volume-owned Vault staging lane once a route card exists.
Hardware Impact: Expected i3/MX350 cost is one local allocation and copy during volume publication, not per-frame simulation. It trades cold memory bandwidth for legal multi-frame lifetime and compaction safety. Exact microseconds remain pending because the build/profiler lane was blocked by CPU guard.

## Decision 061 - Pass32 Float AUP SDF API Removal

Problem: `HectonVoxelVolume.GetSDFDensity(float3 aupPosition)` accepted absolute-universe coordinates after precision had already been reduced to float. The method then promoted to double, but that does not recover lost precision at 100km-scale boundaries.
Solution: Removed the unused float3 overloads. Current static callers already pass `double3` AUP, and `HectonFloatingOrigin.ToRuntimePosition(double3)` subtracts `CurrentTotalOffsetDouble` before converting to `Vector3`.
Rejected Alternatives: Marking the float overload obsolete was rejected because it still leaves a callable precision trap in the runtime API. Converting through `Vector3` was rejected for the same reason. Editing acoustic or thermal callers was unnecessary because both already use `double3`.
Scalability potential: All tiers keep identical AUP truth. Low-tier presentation can interpolate, but density queries no longer expose a float absolute-position ingress route.
Hardware Impact: 0 us runtime cost; this is an API-safety deletion. It reduces future high-distance jitter risk without adding work.

## Decision 062 - Pass33 Published Sonar Payload Fence Harden

Problem: `HectonVoxelVolume` published sonar payload helpers still had weak compaction boundaries: capacity ensure did not check the fence after each `EnsureGenerationHandle`, validation used writable `TryResolveHandle`, and the read lease called `TryLockBuffer` directly without a local pre/post fence wrapper.
Solution: Added explicit fence checks to sonar payload ensure/resolve paths, switched validation to `TryReadOnlyHandle`, and routed SDF read lease pinning through `TryLockPublishedSonarSdfReadBuffer`. Owner-local SDF density, audio-material, raymarch, and gradient methods now pin the exact payload buffer during indexing and release in `finally`.
Rejected Alternatives: Trusting `GlobalDataVault` internal fence checks alone was rejected because the 1315 proof artifact needs local evidence. Returning copied managed arrays was rejected as GC and data-route drift. Holding a read pin beyond the method was rejected because it would cross unknown caller phases.
Scalability potential: Low/Middle/High/Ultra use the same payload truth; quality can scale sampling cadence or ray steps, not payload ownership. The remaining correct long-term path is a lease/snapshot API for external consumers that schedule jobs over published SDF data.
Hardware Impact: Adds one buffer lock/unlock pair to owner-local sampling methods. This is a stability trade, not a measured speed win. Expected low-end cost is below the SDF ray/sample work itself; profiler proof is pending because build/runtime lanes are blocked.

## Decision 063 - Pass33 External Sonar Read-Model Debt Not Hidden

Problem: The broader project still has consumers calling `HectonVoxelVolume.TryGetClosestPublishedSonarSdfPayload` or `TryGetPublishedSonarSdfPayload` and receiving unpinned `NativeArray<byte>.ReadOnly` views. Some consumers schedule jobs over those views in audio/UI/fauna domains. That is outside the 1315 file set but originates from the voxel owner API.
Solution: Recorded the debt as a failed gate in `Docs/Reports/VAULT_EXORCISM_REPORT_1315.json` instead of claiming green. I did not rewrite every external domain in this pass because that would touch audio, UI, fauna, player, and radar code during parallel-agent activity. The next valid fix is an explicit cross-domain `PublishedSonarSdfReadLease`/snapshot contract with release ownership per scheduled job.
Rejected Alternatives: Pretending the current-phase read view is safe for scheduled jobs was rejected by the `GlobalDataVault` interface comment. Disabling published sonar SDF was rejected because it would delete gameplay/presentation features. Auto-holding a global read lock until next frame was rejected because it would violate the no-cross-frame pin rule.
Scalability potential: Low tier can use a copied or coarser SDF snapshot at lower cadence; Middle/High/Ultra can keep richer SDF payloads, but all tiers need the same lease/snapshot authority route.
Hardware Impact: No source change in external domains. Risk remains: scheduled readers can observe relocated Vault memory if compaction happens after an unpinned read view escapes. This is correctness debt, not a microsecond optimization issue.

## Decision 064 - Pass34 Legacy Sonar Read Route Closed For External Callers

Problem: Pass33 correctly identified unpinned external sonar SDF views. After the cross-domain lease migration, that claim needed a fresh route scan instead of prose.
Solution: Re-scanned `Assets/_Project/Scripts`. Direct calls to `TryGetClosestPublishedSonarSdfPayload` and `TryGetPublishedSonarSdfPayload` now remain only inside `HectonVoxelVolume.cs` owner-local legacy helpers. `TryReadNearestSonarSdf` remains only as the legacy interface declaration and `HectonVoxelEngine` legacy implementation; `GroundRadarSdfUtility` fallback now requires `IVoxelSonarSdfReadLeaseModel`.
Rejected Alternatives: Deleting the legacy methods outright was rejected because interface immutability forbids signature removal during the batch. Leaving the utility fallback on `IVoxelSonarSdfReadModel` was rejected because it would reopen the unpinned view route.
Scalability potential: Low/Middle/High/Ultra keep one published SDF truth route. Consumers can scale cadence/ray counts continuously while obtaining data through the lease model.
Hardware Impact: No measured speed win. Correctness gain: external consumers no longer get raw current-phase SDF views without a release contract.

## Decision 065 - Pass34 Stack-Only Hand Placement Solver

Problem: Roslyn pass34 target filtering found two remaining persistent native-field candidates in `PlayerKinematicsHandPlacementSolver`: `NativeArray<PlayerKinematicsProbeHit>` and `NativeArray<PlayerKinematicsHandTarget>`. The solver is not an `IJob`, but it is instantiated and executed synchronously inside `ResolveHandPlacementDirect`.
Solution: Converted `PlayerKinematicsHandPlacementSolver` to `ref struct`. This preserves the algorithm and makes the native views stack-only, preventing storage in class fields, boxing, async capture, or heap escape.
Rejected Alternatives: Moving the fields to a class-level cache was rejected because it would create the exact persistent alias violation. Scheduling it as a tiny job was rejected because it is a one-player scalar control kernel and would add scheduler debt. Rewriting it into many static method parameters was rejected because `ref struct` gives the same lifetime proof with less churn.
Scalability potential: No quality-tier behavior change. Low devices keep direct scalar execution; high-tier visual overkill remains outside this hand-placement memory proof.
Hardware Impact: 0 us expected runtime delta. The change is a C# lifetime guarantee, not a math optimization.

## Decision 066 - Pass34 Scheduled Lease Debt Classification

Problem: The cross-domain migration replaced dangling SDF views with leases, but scheduled consumers can still hold those leases until job completion. `GlobalDataVault` explicitly requires `TryLockBuffer` while an external job owns a pointer, so releasing immediately after `Schedule` would be unsafe; holding until completion is safe for relocation but can cross frames.
Solution: Marked this as a failed strict gate in `Docs/Reports/VAULT_EXORCISM_REPORT_1315.json`. The next correct architecture is per-consumer snapshot lanes with `VaultGenerationHandle<byte>` descriptors, or direct same-phase visual-cheat execution for small visual-only scans where profiler proof accepts it.
Rejected Alternatives: Releasing leases immediately after scheduling was rejected because jobs would read unpinned relocated memory. Forcing `.Complete()` after every schedule was rejected as a hidden synchronization stall. Adding persistent `NativeArray<byte>` snapshot fields to consumers was rejected because it violates the native field mandate.
Scalability potential: Low tier should use coarser/cached SDF snapshots at lower cadence; Middle/High/Ultra can increase ray/sample budgets against the same snapshot contract. No binary quality switch is acceptable.
Hardware Impact: Current lease route blocks compaction for scheduled duration. Snapshot lanes would trade bounded copy bandwidth for compaction freedom. No microsecond claim without Unity Profiler.

## Decision 067 - Pass35 Topographical Sonar Published-SDF Snapshot

Problem: `TopographicalSonarSynthesizer` held a voxel-owned `PublishedSonarSdfReadLease` in fields until the scheduled sonar scan completed. That made a UI presentation scan capable of pinning `BufferID.VoxelSdfTexture3D` across a job boundary.
Solution: Removed the persistent topo sonar published-SDF lease fields and copied the published SDF/audio-material bytes into the existing topo sonar Vault-backed `mockSdf` and `mockMaterialIds` buffers before scheduling `SonarRaymarchJob`. The voxel lease is released in `finally` inside `TryResolvePublishedSdfSnapshot`, before the scan jobs are scheduled.
Rejected Alternatives: Keeping the lease until scan completion was rejected because UI sonar is a presentation lane and can consume a copied snapshot. Releasing the lease immediately without copying was rejected because the scheduled job would read a potentially relocated voxel buffer. Allocating a new local `NativeArray<byte>` per ping was rejected because it would move the problem into native allocation churn. Adding new persistent native fields was rejected by the 1315 mandate.
Scalability potential: Low/Middle keep the same continuous ray/step quality scaling and can fall back to the generated mock SDF when the published payload is larger than the existing snapshot capacity. High/Ultra keep published-SDF visual fidelity when the payload fits, without changing gameplay truth ownership.
Hardware Impact: Removes one cross-domain voxel Vault pin from each scheduled topo sonar scan. It adds a bounded byte copy into already-owned topo sonar buffers when published SDF is used; exact microseconds are pending because CPU guard blocked build/profiler lanes.

## Decision 068 - Pass36 Topographical Sonar Owner Job Pins

Problem: Pass35 removed the cross-domain voxel SDF lease from `TopographicalSonarSynthesizer`, but the scan and fade jobs still received UI-owned Vault-backed `NativeArray` views without explicit `TryLockBuffer` protection. A Vault compaction during a scheduled sonar job could relocate `Points`, `HitMask`, `Counters`, `MockSdf`, `MockMaterialIds`, or `MaterialColorLut` while Burst still owns the pointer.
Solution: Added explicit owner-tagged buffer locks around the exact scheduled-job buffer set. Scan scheduling pins `Points`, `HitMask`, `Counters`, `MockSdf`, `MockMaterialIds`, and `MaterialColorLut`; fade scheduling pins `Points`. Partial lock acquisition rolls back immediately. Failed schedules release in `finally`. Completed jobs release after `TryFinalizeCompleted` and commit/upload reads finish. Forced completion during dispose also releases.
Rejected Alternatives: Releasing locks immediately after `Schedule` was rejected because the jobs would then read unpinned relocatable memory. Forcing same-frame `.Complete()` was rejected as hidden synchronization debt. Allocating local per-ping native output arrays was rejected because the results must survive for GPU upload and would introduce native allocation churn or persistent native fields.
Scalability potential: Low/Middle keep the continuous ray/step/fade cadence. High/Ultra keep richer topographical sonar points without adding a cross-domain voxel pin. This is still not strict green: if the job completes on a later LateFrame, UI-owned Vault pins can span a frame. The next better route is a presentation-owned snapshot/staging lane with same-phase commit, or a visual-cheat scan path with bounded synchronous slices under `GlobalQualityWeight`.
Hardware Impact: Adds six scan buffer lock/unlock pairs and one fade buffer lock/unlock pair per scheduled job. Expected i3/MX350 cost is lower than a full SDF raymarch and buys relocation correctness. Exact microseconds are pending because CPU guard blocked build/profiler lanes.

## Decision 069 - Pass37 Voxel Delta Compaction SDF Snapshot

Problem: `VoxelDeltaProcessor` scheduled compaction copied a published voxel SDF through `VoxelDeltaCopyEncodedSdfJob`, so it had to keep `PublishedSonarSdfReadLease` in `ScheduledCompactionRequest` until job completion. That protected the pointer, but it kept a voxel-owned SDF read pin alive from a delta-save compaction lane.
Solution: Replaced the scheduled source-SDF copy with a synchronous copy into the existing `SaveVoxelDeltaCompactionSourceSdfScratch` Vault buffer before scheduling compaction. The voxel lease is released immediately after the copy, before `VoxelDeltaCompactionJob` is scheduled. Compaction scratch now pins the exact nine scratch buffers with owner-tagged `TryLockBuffer`, rolls back partial locks, and unlocks when the scheduled compaction result is committed or discarded.
Rejected Alternatives: Keeping the scheduled copy was rejected because it forces a cross-domain voxel read lease to live until compaction completion. Allocating a separate local native snapshot was rejected because the scratch buffers already exist in the Vault and adding per-compaction allocations would regress memory discipline. Forcing `.Complete()` on the copy job was rejected as a hidden synchronization point that adds scheduler debt without improving the player-facing result.
Scalability potential: Low/Middle tiers pay a bounded CPU byte copy on background compaction only; High/Ultra keep the same compaction output fidelity. Future overkill path can chunk this copy across quality-scaled slices if profiler data proves the synchronous copy exceeds the frame budget.
Hardware Impact: Removes a cross-domain voxel SDF pin from scheduled delta compaction. Adds a synchronous byte copy into existing scratch; exact microseconds pending because CPU guard blocked build/profiler. Expected MX350 risk is bounded to background compaction, not normal Tick.

## Decision 070 - Pass38 Laser Cutter SDF Snapshot

Problem: `LaserCutterDodRuntime` acquired a voxel SDF read lease for `BuildCutterSdfProbeHitsJob` and kept that lease alive until the scheduled probe job completed. That removed dangling raw views but still pinned voxel-owned SDF memory from a tool presentation/deformation lane.
Solution: Added a tool-owned `SdfSnapshotBuffer` alias over the unused legacy probe buffer ID. `TryReadCutterSdfSnapshot` now acquires the voxel read lease, copies the required SDF bytes into the tool-owned Vault buffer while the lease is valid, releases the voxel lease in `finally`, and schedules the probe job against the snapshot. Probe and evaluation job buffers now use owner-tagged `TryLockBuffer`/`TryUnlockBuffer` coverage with rollback and finalization release.
Rejected Alternatives: Holding the voxel read lease until job completion was rejected because it keeps cross-domain voxel memory pinned. Releasing the lease immediately without copying was rejected because the job would read relocatable memory. Allocating a per-shot `NativeArray<byte>` was rejected because laser fire can be frequent and would introduce native allocation churn. Forcing same-frame `.Complete()` was rejected as a hidden synchronization stall.
Scalability potential: Low/Middle tiers can pay one bounded SDF byte copy per scheduled cutter batch and keep lower ray-step counts via `GlobalQualityWeight`. High/Ultra can keep richer SDF probe resolution and spark/decal budgets without changing the memory ownership route.
Hardware Impact: Expected direct speed gain: 0 us. Stability gain: removes one cross-domain voxel SDF pin from each scheduled cutter SDF probe batch. Cost: bounded byte copy into an existing Vault buffer plus five probe buffer locks and eleven evaluation buffer locks. Exact microseconds pending because CPU guard blocked build/profiler lanes.

## Decision 071 - Pass39 Scheduled SDF Lease Snapshot Sweep

Problem: After the laser fix, scheduled SDF lease debt still existed in ground radar, radiation, fauna terrain IK, and spatial audio occlusion. Each path protected the job pointer by holding a voxel-owned SDF read lease until scheduled job completion, which is relocation-safe but blocks voxel SDF compaction from unrelated domains.
Solution: Converted each scheduled consumer to an owner-owned snapshot route. Ground radar copies to `(BufferID)71339`, radiation copies to `(BufferID)72752`, fauna IK copies to `(BufferID)71337`, and spatial audio copies to `(BufferID)72447`. Each path releases the voxel lease in the acquisition method-local `finally` before scheduled job state is claimed, then holds only its own snapshot/job-buffer lock until completion.
Rejected Alternatives: Releasing voxel leases immediately without snapshot copies was rejected because jobs would read relocatable memory. Forcing `.Complete()` after schedule was rejected as a frame-time spike and a hidden synchronization debt. Reusing tiny CSV scratch buffers was rejected because published SDF payloads can exceed those capacities. Adding persistent `NativeArray<byte>` fields was rejected by the native-field mandate.
Scalability potential: Low/Middle tiers can reduce SDF scan cadence, ray/sample counts, or terrain IK quality continuously through existing `GlobalQualityWeight`; High/Ultra can keep richer SDF probes against the same snapshot ownership route. This preserves one truth owner and changes only presentation/job input staging.
Hardware Impact: Direct speed gain is not claimed. Stability gain: removes remaining known cross-domain voxel SDF pins from scheduled consumer jobs. Cost: one bounded byte copy per scheduled SDF consumer execution plus owner-tagged snapshot locks. Exact microseconds pending because CPU guard blocked build/profiler lanes.
