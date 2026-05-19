# Rationale_SHINOBU_115

Date: 2026-05-19
Agent: SHINOBU_115
Status: SOURCE_HARDENED_NOALIAS / COMPILE_BLOCKED_BY_CPU_LOAD / RUNTIME_PENDING

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
