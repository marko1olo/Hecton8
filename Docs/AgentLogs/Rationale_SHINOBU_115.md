# Rationale_SHINOBU_115

Date: 2026-05-19
Agent: SHINOBU_115
Status: SOURCE_HARDENED_VISUAL_LOCKS_EDGE_CASCADE_AUDITED / COMPILE_BLOCKED_BY_CPU_LOAD / RUNTIME_PENDING

## Decision 001: Structural Collapse Must Be Scalar Truth Plus Presentation Lie

Problem: Base collapse driven by Unity joints or synchronized GameObject destruction is nondeterministic, expensive, and hostile to netcode snapshotting.

Solution: Use explicit unmanaged `IntegrityStateDTO` rows as the truth surface, calculate pressure/stress in Burst over flat buffers, mark collapse by flags, and feed `BucklingScalar` to visual sync/shader deformation.

Rejected Alternatives: Unity `FixedJoint`/`SpringJoint` stacks and recursive `Destroy(gameObject)` chains were rejected because PhysX order and neighbor recursion can diverge across clients and stall the main thread.

Scalability potential: Low uses sparse solver cadence and subtle buckling scalars; Middle increases cadence and threshold telemetry; High increases visual buckling response density; Ultra spends saved CPU on stronger deformation/audio/VFX consumers, not extra simulation truth.

Hardware Impact: Expected low-end i3/MX350 gain is deterministic O(N+E) scalar work instead of PhysX island solving; exact microseconds remain pending profiler proof.

## Decision 002: Authority Boundaries Before New Global Surface

Problem: The task requires Vault buffers and typed signals, but global authority changes are route-card controlled.

Solution: First inspect existing `GlobalDataVault`, `SignalBus<T>`, habitat deformation contracts, and construction graph contracts; extend existing local domain files if present, otherwise document the new route and keep integrations owner-local/cold until compile proof exists.

Rejected Alternatives: Creating standalone singletons or polling `GlobalRegistry` from hot paths was rejected because it violates owner-local and phase discipline.

Scalability potential: Existing Vault handles allow low-tier cadence shedding and high-tier richer presentation without widening gameplay truth.

Hardware Impact: Avoiding new global/polymorphic lookup surfaces saves cache misses on low-end silicon; exact microseconds pending source integration.

## Decision 003: BufferID Range Must Avoid Existing Hull Deformation Locals

Problem: `HullIntegrityRuntime` already casts local buffer ids 70090-70097 for deformation states, breach jets, material strengths, and CSV scratch, while `H8Memory.BufferID` only exposed 70080-70089 for older hull integrity buffers.

Solution: Assign structural integrity buffers to 70110-70119 and document them in `SHINOBU_115_STRUCTURAL_INTEGRITY_CALCULATOR.md`.

Rejected Alternatives: Reusing 70090-70097 was rejected because two owners would silently alias Vault memory. Renaming the existing deformation constants was rejected because it is unrelated SHINOBU_109 surface area.

Scalability potential: Low, Middle, High, and Ultra all read the same memory contract; only cadence and visual consumers scale. No quality tier creates new buffers.

Hardware Impact: Prevents alias-induced corruption and cache pollution. Estimated low-end gain is correctness first; microsecond savings are not measurable without profiler.

## Decision 004: Exact DTO, Raw Ref Mutation, and Deterministic Jobs

Problem: Structural stress needs netcode-stable binary layout and no CS1612 stack-copy traps under Burst.

Solution: Add explicit 32-byte `IntegrityStateDTO`, raw public fields, pads 24-31, `IntegrityStateDTO.AsRef`, and deterministic Burst jobs for depth pressure, SDF anchors, CSR stress, collapse, edge sever, and telemetry.

Rejected Alternatives: C# properties, sequential layouts, and managed class state were rejected because they are not safe for blind memcpy rollback or ARM64 cache alignment.

Scalability potential: Low uses sparse cadence over identical DTOs; Middle/High/Ultra increase evaluation frequency and shader intensity without changing truth layout.

Hardware Impact: Estimated 35 us pressure + 80 us graph + 60 us cascade per 5000 nodes on MX350-class CPU. This is a model until compile/profiler proof exists.

## Decision 005: Dear Lie Uses Global GraphicsBuffer, Not MPB

Problem: The XML asked for material-property buckling sync, but the repository AGENTS rules reject MPB churn on standard geometry and require structured GPU buffers for per-element data.

Solution: Upload `IntegrityStateDTO` to `_HectonStructuralIntegrityStateBuffer` using double-buffered `GraphicsBuffer.LockBufferForWrite`; shaders consume `BucklingScalar`.

Rejected Alternatives: MaterialPropertyBlock updates, mesh swaps, and rigidbody debris were rejected as allocation/stall risks or nondeterministic physics truth.

Scalability potential: Low consumes fewer updated frames with subtle buckling; Middle increases cadence; High and Ultra spend saved CPU on richer shader deformation and downstream VFX.

Hardware Impact: Visual sync model cost is 25 us CPU per upload. GPU cost scales with shader consumers, not simulation truth.

## Decision 006: Compile Deferred by Batch CPU Rule, Not Hidden

Problem: Verification is mandatory, but the CPU sample returned 100% and batch law forbids `dotnet` when CPU exceeds 50%.

Solution: Do not run build under load. Record static grep evidence now and keep compile status blocked until CPU falls below threshold and no `dotnet`/`csc` process is active.

Rejected Alternatives: Launching `dotnet build` under 100% CPU or claiming compile success without evidence were rejected.

Scalability potential: Verification policy does not alter runtime scalability. It prevents measurement noise and build contention with other agents.

Hardware Impact: Avoids adding compiler load to an already saturated machine; no runtime microsecond impact.

## Decision 007: Burst Alias Proof Must Be Explicit

Problem: The first source pass relied on Burst inferring that separate Vault handles do not overlap. That is weak architecture: alias uncertainty can disable vectorization and keep NEON/AVX lanes conservative.

Solution: Annotate every SHINOBU_115 job-owned `NativeArray` field and job-safe `NativeQueue<T>.ParallelWriter` field with `[NoAlias]`. Keep the owner invariant simple: one BufferID, one Vault handle, one writer route.

Rejected Alternatives: Relying on default compiler alias analysis was rejected because the mandate explicitly requires alias proof. Converting the solver to interface arrays was rejected because interface dispatch would block Burst inlining and rollback determinism.

Scalability potential: Low gets cheaper graph/telemetry loops; Middle keeps stable cadence; High and Ultra spend saved CPU on shader buckling, metal groan audio, leak VFX, and richer editor heatmap sampling.

Hardware Impact: Model estimate is 5-15 us saved on MX350/i3-class silicon for 4096-node graph/telemetry passes through improved auto-vectorization headroom. Measured proof is absent until compile/profiler run.

## Decision 008: Cold CSV Reload Must Not Steal The Simulation Fence

Problem: `ColdTick()` could reload material CSV and run a synchronous material-apply job while the structural solver fence was alive. That is not a hot-path allocation bug, but it is a concurrency bug: cold control data could race solver reads/writes or force a stall if fixed by completion.

Solution: `ColdTick()` now returns immediately when `_jobScheduled != 0`. CSV reload and material apply wait for the next cold cadence after the solver has completed in `LateFrameTick()`. Existing cold synchronous jobs are annotated with `COLD SYNC JOB` and stay limited to boot, editor mock generation, or no-active-solver CSV reload.

Rejected Alternatives: Calling `CompleteScheduled()` from `ColdTick()` was rejected because it would serialize worker work from a control path. Allowing CSV write-through during solver execution was rejected because it can corrupt deterministic state.

Scalability potential: Low avoids reload hitches when cadence is sparse; Middle/High/Ultra keep designer hot-reload without contaminating simulation fences.

Hardware Impact: Prevents a worst-case cold hitch and data race. Exact microsecond gain is not measurable without a Unity profiler capture; model impact is stall avoidance rather than steady-state frame cost.

## Decision 009: Compile Wall Route Stays Isolated

Problem: The polish mandate required auditing assembly route pressure. A direct sibling runtime reference to Agent 64 flood, Agent 108 netcode, or Agent 114 construction graph would create compile-wall coupling.

Solution: Keep SHINOBU_115 runtime inside `Hecton8.Habitat.Deformation` and route through Core, Core.Contracts, Core.Memory, local Deformation contracts, Vault buffers, and typed signals. The editor facade references only the runtime assembly and Unity editor/UI assemblies.

Rejected Alternatives: Direct concrete references to flood, construction, netcode, audio, or VFX owners were rejected because those systems must consume `SignalBus<T>` or Vault snapshots at their own phase boundary.

Scalability potential: Low-to-Ultra runtime behavior is unchanged; compile-wall isolation protects multi-agent iteration velocity and prevents sibling recompilation cascades.

Hardware Impact: Runtime microsecond impact is 0. Iteration impact is reduced assembly invalidation; exact seconds saved require Unity compiler log proof.

## Decision 010: Binary Ledger Read Did Not Authorize A New Payload

Problem: The ultra mandate required reading `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. Creating a `.bin` or `.h8bin` by hand for structural materials would violate binary sovereignty and duplicate the cold CSV/editor tuning route.

Solution: Do not create a structural binary payload. Keep `hull_materials.csv` as cold designer input parsed into Vault scratch and aligned DTOs, and document that no binary payload path is claimed.

Rejected Alternatives: Hand-authoring a generated binary table was rejected because the ledger forbids patching generated payload bytes by hand and no structural binary baker owner exists in this task.

Scalability potential: Low/Middle/High/Ultra all hydrate the same aligned runtime DTO contract; future binary promotion must go through an owner baker and ledger update.

Hardware Impact: Runtime hot-path impact is 0. Cold CSV parsing remains outside gameplay solver cadence and uses Vault scratch to avoid managed split allocation.

## Decision 011: Solver Jobs Must Hold Vault Relocation Locks

Problem: The solver resolves `NativeArray` aliases from Vault handles and then schedules Burst jobs that hold those pointers beyond the scheduling call. Without Vault locks, another owner or compaction path could relocate a buffer while the job fence is alive.

Solution: Before scheduling, lock every Vault buffer captured by the solver job chain: structural states, node AUPs, CSR offsets/destinations, edge flags, telemetry ring, telemetry cursor, tuning, and optional `VoxelSdfTexture3D`. Unlock only after `_scheduledHandle.Complete()` in `LateFrameTick()`, or immediately on pre-schedule validation failure.

Rejected Alternatives: Resolving handles without locks was rejected because it relies on a global no-relocation assumption not present in `GlobalDataVault`. Calling `Complete()` from editor or cold reload paths was rejected because it would serialize worker jobs outside the visual-sync fence.

Scalability potential: Low, Middle, High, and Ultra use the same lock discipline; cadence changes reduce how often locks are acquired on weak devices, while high-tier visual richness still consumes the same stable DTO contract.

Hardware Impact: Control-path lock/unlock calls add negligible model cost compared with the solver. The value is preventing use-after-relocation crashes and nondeterministic corrupt state on long-running QA sessions.

## Decision 012: Editor Facade Reads Telemetry, Not Live Truth During Jobs

Problem: The original editor graph sampled live node state, which proves current stress but not the required 300-frame Black Box telemetry path. It could also read while the solver job was writing.

Solution: Add `TryGetTelemetrySample(int framesBack, out StructuralTelemetryEntry)` and make `Hull Integrity Tuner` draw from the telemetry ring. Public editor-facing state/tuning/telemetry reads and tuning writes now return while `_jobScheduled != 0`. A literal runtime `OnDrawGizmos` hook was added for Task 19; it reads Vault state only after the solver fence is down.

Rejected Alternatives: Keeping only a `SceneView.duringSceneGui` delegate was rejected because the XML explicitly asks for `OnDrawGizmos`. Completing the solver from editor UI was rejected because editor tooling must not become a hidden synchronization point.

Scalability potential: Low devices still record sparse telemetry at the solver cadence; Middle/High/Ultra give denser graph history as cadence rises. Editor graph richness does not alter runtime truth.

Hardware Impact: Editor-only visualization cost is outside gameplay. Runtime safety improves by avoiding active-job Vault reads from editor calls.

## Decision 013: GPU Upload And SDF Quality Must Match The Actual Contracts

Problem: `LockBufferForWrite` on a `GraphicsBuffer` constructed without `UsageFlags.LockBufferForWrite` is a latent upload failure. SDF anchoring also needed explicit continuous quality math, not a fixed tap count pretending to scale.

Solution: Construct structural state buffers with `GraphicsBuffer.UsageFlags.LockBufferForWrite`. SDF anchoring now collapses to nearest-sample math below the quality threshold and smoothly blends six-neighbor cross taps using `math.step(0.3f, quality)` and a polynomial quality curve.

Rejected Alternatives: Using MaterialPropertyBlock was rejected by AGENTS because it breaks SRP Batcher on standard geometry. Always evaluating high-tap SDF was rejected because low-tier hardware pays the same per-node memory load for no gameplay-critical improvement.

Scalability potential: Low uses one SDF sample and sparse solver cadence; Middle blends into cross-tap anchoring; High and Ultra spend the saved CPU on shader buckling, audio groans, leaks, and richer designer overlays.

Hardware Impact: Low tier saves five SDF byte samples per node in the anchor job. GPU upload now uses the correct lockable buffer path; measured frame and driver proof remain pending.

## Decision 014: Cold And Editor Writers Must Not Become Hidden Fences

Problem: `RegenerateMockGraph()` still called `CompleteScheduled()`, which let an editor button steal the simulation fence. Cold boot/mock/CSV paths also scheduled immediate jobs or wrote directly into Vault scratch/material buffers without explicit relocation locks, relying on "no active solver" instead of proving pointer ownership.

Solution: Remove the editor-forced completion; `RegenerateMockGraph()` now returns while `_jobScheduled != 0`. Boot clear and mock graph generation acquire Vault locks before scheduling immediate cold jobs. `SetTuning()` and default tuning writes lock `StructuralIntegrityTuning`. CSV reload locks `StructuralIntegrityCsvScratch` during direct `FileStream.Read(Span<byte>)`, locks `StructuralIntegrityMaterialStrengths` during parse/upsert, and locks states/materials while the cold material-apply job owns those pointers.

Rejected Alternatives: Completing scheduled simulation work from an editor/mock command was rejected because it serializes worker jobs outside `LateFrameTick()`. Managed `byte[]` CSV staging was rejected because it would reintroduce cold allocation pressure and duplicate the Vault scratch path. Leaving cold jobs unlocked was rejected because Vault compaction/relocation safety must not depend on the caller's intent.

Scalability potential: Low avoids cold reload or editor stalls colliding with sparse solver cadence; Middle keeps designer reloads deterministic; High and Ultra keep the same scalar truth while using saved frame budget for richer shader/audio/VFX response.

Hardware Impact: Runtime hot-path cost is 0 us for the editor no-fence change. Cold-path lock/unlock calls are control-path overhead only. The material and scratch locks prevent relocation corruption rather than claiming measurable frame-time savings; build/profiler proof remains pending because CPU gate is still closed.

## Decision 015: Cold Writers Need Fail-Fast State And A Fixed Vault Hash Table

Problem: The lock patch made cold/editor paths safer, but several helpers still returned silently on lock failure while `TryInitialize()` continued. That could leave `NativeArrayOptions.UninitializedMemory` buffers partially dirty. Task 18 also asked for `NativeHashMap` material lookup, while the actual Vault contract exposes generation-checked `NativeArray` buffers, not persistent `NativeHashMap` ownership.

Solution: Convert boot clear, default material write, default tuning write, mock generation, and material apply into bool-returning fail-fast helpers. `TryInitialize()` now aborts on critical cold failure. Add `StructuralMutationGuardMask = 1UL << 45` around cold/editor writer paths. Convert `StructuralIntegrityMaterialStrengths` into a fixed 32-slot open-addressed Vault hash table: CSV upsert hashes directly to a slot and jobs resolve by hash probing with power-of-two wrapping.

Rejected Alternatives: Continuing boot after a lock failure was rejected because deterministic rollback state cannot start from unknown bytes. A persistent `NativeHashMap` field was rejected because it would violate the Vault law and introduce allocator ownership outside `GlobalDataVault`. A linear material list was rejected because it only pretended to satisfy the hash-map requirement.

Scalability potential: Low keeps the same 32-slot table and sparse cadence; Middle/High/Ultra resolve material strength with the same hash-addressed table while spending saved CPU on stronger shader buckling, groans, breach VFX, and editor telemetry.

Hardware Impact: Runtime solver hot path is unchanged for structural graph pressure. Mock/material cold jobs now get average O(1) material lookup instead of linear 32-entry scans; measured proof is absent because `dotnet` remained blocked by CPU/process gates.

## Decision 016: AUP Namespace Is Not A Sibling Runtime Reference

Problem: `StructuralIntegrityCalculatorTypes.cs` imports `Hecton8.World` for `AbsoluteUniversePosition`, and the ultra mandate requires deleting direct sibling Runtime dependencies. A namespace match alone is insufficient proof; assembly ownership must be checked.

Solution: Resolve the type definition and asmdef boundary. `AbsoluteUniversePosition` is declared in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`, which is under the parent `Assets/_Project/Scripts/Hecton8.Core.asmdef` because there is no enclosing `World` asmdef at that folder level. `Hecton8.Habitat.Deformation.asmdef` already routes through `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, local deformation contracts, and Unity packages. No direct World/Flood/Construction/Netcode/Audio/VFX runtime reference was introduced.

Rejected Alternatives: Creating a SHINOBU-local AUP clone was rejected because `FluidIncursionSignal.LeakAup` already uses the Core-owned AUP payload; duplicating it would fork signal truth and add conversion risk. Moving AUP to contracts was rejected because that would edit a core/global boundary outside this task and could destabilize other agents.

Scalability potential: Low, Middle, High, and Ultra all keep the same 48-byte AUP signal payload. No quality tier gains a second coordinate truth route.

Hardware Impact: Runtime cost is 0 us. The gain is compile-wall proof: no sibling runtime assembly invalidation is added by SHINOBU_115.

## Decision 017: Signal Profile Bytes And Layout Reflection Must Respect Their Actual Contracts

Problem: `BaseModuleCompromisedSignal.QualityTier` is a Core signal byte whose documented profile values are `ScalabilityTierProfiles.LowMx350 = 0` and `HighRtx = 1`. SHINOBU_115 was writing a rounded `0..4` value derived from `GlobalQualityWeight`, which polluted a binary downstream contract. `StructuralIntegrityLayout.Validate()` also used `System.Reflection.FieldInfo` during runtime boot, which violates the zero-GC/reflection discipline for player code.

Solution: Add a narrow bridge function in the collapse signal job: continuous `GlobalQualityWeight` is clamped and mapped with `math.step(0.5f, q)` into the existing Core profile byte range for `BaseModuleCompromisedSignal` only. The solver still consumes continuous quality for cadence, SDF anchoring, telemetry, and shader scalar intensity. Move offset reflection behind `#if UNITY_EDITOR`; player/runtime validation now uses `UnsafeUtility.SizeOf` only.

Rejected Alternatives: Widening `BaseModuleCompromisedSignal.QualityTier` or adding new Core signal fields was rejected because this domain must not mutate a global contract to hide a local misuse. Leaving `0..4` was rejected because downstream owners can legally normalize or branch on `0/1`. Removing offset proof entirely was rejected because the editor audit remains useful; keeping reflection in runtime was rejected because boot must not allocate or invoke metadata paths.

Scalability potential: Low-to-Ultra still breathe through `GlobalQualityWeight` in the structural math. The only binary step is the unavoidable bridge into an already-binary Core signal profile. Low keeps sparse cadence and nearest SDF, Middle blends SDF taps, High and Ultra keep denser cadence and stronger shader buckling while emitting valid downstream profile bytes.

Hardware Impact: Breach emission adds one finite clamp and one `math.step` on a rare signal path. Player boot avoids runtime reflection/metadata calls; exact microseconds are pending profiler proof, but the architectural gain is contract correctness and zero-GC compliance.

## Decision 018: Visual Sync Must Still Own Vault Aliases And Cascade Must Cut Connected Edges

Problem: The scheduled solver correctly locked Vault buffers while jobs were alive, but the first visual-sync fence released those locks before `AfterSolverComplete()` uploaded the shader buffer and checked telemetry for fault dumps. That left a narrow relocation window while visual sync still held resolved Vault arrays. The cascade edge pass also only severed outgoing edges from collapsed sources, which was weaker than the task's connected-edge requirement when CSR ownership is one-sided. Cold CSV input still used `File.OpenRead`, which is a poor fit for designer hot reload.

Solution: Keep solver locks through `CompleteScheduled(false)` and `AfterSolverComplete()`, then release in `LateFrameTick()` `finally`. Add `CsrDestinations` to `StructuralEdgeSeverJob` and sever each owned edge if its source is collapsed or its destination points at a collapsed node. Change material CSV reload to `FileStream(FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CsvScratchBytes, FileOptions.SequentialScan)` while retaining the structural mutation guard and Vault locks.

Rejected Alternatives: Unlocking before visual sync was rejected because GPU upload and Black Box dump still resolve Vault-backed arrays. Expanding cascade into recursive neighbor mutation was rejected because that would reintroduce nondeterministic destruction semantics. Keeping `File.OpenRead` was rejected because it can contend with external CSV writers during editor tuning.

Scalability potential: Low keeps sparse cadence and cheap O(E) edge severing; Middle and High get denser structural propagation without new object truth; Ultra spends the same scalar state on richer shader buckling and downstream signal consumers. The CSV change is cold-only and does not create a low/ultra branch.

Hardware Impact: Visual lock retention is correctness, not a claimed frame-time win. Destination-aware sever adds one bounded read per edge only when the edge owner is processed, still deterministic O(E). CSV `FileShare.ReadWrite` and sequential scan avoid editor reload contention; gameplay hot-path impact is 0 us.
