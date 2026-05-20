# SHINOBU_158 Rationale - Buoyancy And Displacement Solver

Date: 2026-05-19
Status: PENDING VERIFICATION

## Decision 0 - Mandate Selection

Problem: Buoyancy solver spans physics truth, AUP precision, native memory, Burst jobs, flow-field sampling, and editor diagnostics.
Solution: Loaded eight mandates before code: physics force routing, ARM64 DTO layout, AUP determinism, floating-origin precision, zero-GC, native jobs, execution phases, and abyssal currents.
Rejected Alternatives: Reading the entire registry would add context noise; reading only AGENTS.md would miss local layout and flow-field laws.
Scalability potential: Low uses flat scalar force math and sleep. Middle keeps full Archimedes plus basic flow. High adds richer telemetry/debug sampling. Ultra can spend saved CPU on presentation-side drift/foam/VFX, not more simulation truth.
Hardware Impact: Static source pass only. Runtime goal is to keep 1000-object solver below suspicious 0.1 ms class budget on i3/MX350 by skipping sleepers and using linear memory access.

## Decision 1 - Status/Rationale Creation

Problem: Context compression and parallel agents require disk-backed state before edits.
Solution: Created fresh SHINOBU_158 status and rationale files; no stale SHINOBU_158 files existed.
Rejected Alternatives: Chat-only tracking is rejected by batch protocol.
Scalability potential: Documentation does not affect runtime tiers.
Hardware Impact: None at runtime.

## Decision 2 - XML Rehydration Correction

Problem: The first extraction pass used a brittle PowerShell range pattern and could include neighboring prompt text under concurrent batch edits.
Solution: Re-extracted `SHINOBU_158` from `Docs/Tasks/CURRENT_BATCH.md` using raw `IndexOf('<AGENT_PROMPT id="SHINOBU_158"')` through the matching `</AGENT_PROMPT>`, then treated only that block as authority.
Rejected Alternatives: Using chat memory, MCP truncation, or neighboring SHINOBU prompts would contaminate the buoyancy domain with wrong task constraints.
Scalability potential: Correct prompt isolation prevents cross-domain global routes and unnecessary compile-wall growth. Low/Middle/High/Ultra behavior remains the same because this is process hygiene.
Hardware Impact: Static source discipline only; estimated runtime impact is avoiding accidental systems, not a measurable frame number.

## Decision 3 - GlobalDataVault Lane And Route Card

Problem: The solver needs cross-domain, job-visible state for replay, telemetry, editor tuning, and crash dumps, but owner-local scratch would hide state and direct global sprawl would violate the authority boundary.
Solution: Added the SHINOBU_158 Vault lane actually requested at boot: `71620..71627` and `71629..71631`. Wrote `Docs/ARCHITECTURE/SHINOBU_158_BUOYANCY_ROUTE_CARD.md` and mirrored the lane in the binary payload ledger.
Rejected Alternatives: A private `NativeQueue` in `BuoyancyDisplacementRuntime` was rejected because the H-PHI Vault law requires persistent memory to be DataVault-owned. A generic `SignalBus<T>` lane was rejected because force packets are not fan-out events and would add a global flush dependency. The implemented replacement is a Vault-owned force-packet window `71621` with a false-sharing-padded atomic counter, drained only by `PhysicsApplySystem`.
Scalability potential: Low evaluates fewer entities per fixed tick and sleeps stable objects. Middle runs normal Archimedes and linear/quadratic blend. High/Ultra spend saved CPU on richer presentation/VFX, not more simulation truth.
Hardware Impact: Static estimate on i3/MX350: flat 64-byte DTO traversal plus stride load-shed avoids per-object MonoBehaviour dispatch and should keep 1000-object math in the sub-0.1 ms class after Burst/profiler proof.

## Decision 4 - Legacy Buoyancy Archaeology Boundary

Problem: The XML says to delete per-object `FixedUpdate`/`Rigidbody.AddForce` buoyancy scripts, but the source scan found no such direct offender. Existing `BuoyancyObject` is a registration/acoustics/dry-zone facade and `HectonFluidEngine` is a central legacy system, not a simple per-object `FixedUpdate` script.
Solution: Added the new SHINOBU_158 DOD solver as a parallel owner-local route and logged the legacy dependencies instead of deleting cross-domain facades blindly.
Rejected Alternatives: Deleting `BuoyancyObject`, `HectonFluidEngine`, `Floater`, or `DeployableBeacon` would break acoustics, dry-zone handling, player gameplay, and existing route scripts outside SHINOBU_158 authority.
Scalability potential: Low can migrate dropped loose objects to the new solver first; Middle/High/Ultra can retain specialized player/submarine/floater behavior until owners integrate cleanly.
Hardware Impact: Static archaeology only. The intended gain is removal of future loose-item per-object fluid scripts; no measured runtime claim yet.

## Decision 5 - PhysicsApplySystem Vault Force Window

Problem: Burst jobs must emit forces without touching `Rigidbody`, while the current `PhysicsApplySystem.ForcePacket` is a managed/Unity `Vector3` packet and not directly writable by a deterministic Burst job keyed by entity hash.
Solution: Added `BuoyancyForcePacketDTO` and a `PhysicsApplySystem` partial drain bridge. The job writes unmanaged packets into Vault buffer `71621`; `PostFixedTick` drains after the solver fence and routes through `PhysicsApplySystem.QueueForce`.
Rejected Alternatives: Direct `Rigidbody.AddForce` in the job is illegal. Reworking the existing `ForcePacket` ABI would touch a large core file with `Pack=1` legacy debt. A `SignalBus<T>` NativeQueue would add a global queue/snapshot flush for a stream that has exactly one consumer and must remain rollback-visible.
Scalability potential: Low drains a capped queue and skips sleeping/strided objects; Middle/High/Ultra can raise active count without changing the ownership route.
Hardware Impact: Static estimate: removes per-object managed force calls from the calculation phase; exact i3/MX350 gain pending profiler.

## Decision 6 - Deterministic Burst Over Fast Float

Problem: The global mandate prefers `FloatMode.Fast`, but SHINOBU_158 XML explicitly names rollback/netcode state and blind memcpy compatibility.
Solution: Used `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` on all SHINOBU_158 jobs.
Rejected Alternatives: `FloatMode.Fast` was rejected for this domain because cross-platform x86/ARM64 rollback drift is a higher correctness risk than the small ALU win.
Scalability potential: Low/Middle/High/Ultra all share deterministic gameplay truth; visual overkill must live outside authoritative force math.
Hardware Impact: Static estimate: deterministic float may cost a small percentage versus Fast, repaid by stride/sleep load-shed; no measured number until Burst profiling.

## Decision 7 - Dear Lie Hydrodynamics

Problem: Exact submerged mesh volume and surface bobbing are too expensive for thousands of objects.
Solution: Used prebaked scalar volume, a cube-root height approximation, smooth submersion fraction, artificial near-surface vertical damping/snap, and triangle-wave flow fallback when no flow sample owns the object.
Rejected Alternatives: Runtime `MeshCollider` intersection, `Physics.ComputePenetration`, Navier-Stokes, per-bubble/per-droplet truth, and per-object helper transforms were rejected.
Scalability potential: Low uses stride, linear drag, cheap max-axis speed, and triangle flow. Middle blends toward quadratic drag and denser evaluation. High/Ultra may feed shaders/debug/VFX with richer scalar data after simulation stays under budget.
Hardware Impact: Static estimate: O(n) scalar math instead of O(n * mesh/sample count). For 1000 loose objects on i3/MX350, the intended saving is tens to hundreds of microseconds versus per-object sample-point volume logic; profiler proof pending.

## Decision 8 - Human Control Facade And CSV Table

Problem: Designers need volume/mass tuning without C# recompiles, but persistent `NativeHashMap` ownership is not exposed through the current `GlobalDataVault` typed-handle API.
Solution: Added `Hydrodynamic Buoyancy Tuner` UI Toolkit window and a cold byte/`ReadOnlySpan<byte>` CSV parser that hashes names with FNV-1a into a fixed Vault-owned open-address `NativeArray<BuoyancyMaterialVolumeDTO>`.
Rejected Alternatives: `string.Split`, LINQ, managed dictionaries, ScriptableObject hot reads, and private persistent `NativeHashMap` were rejected for GC and ownership reasons.
Scalability potential: Low can ship coarse material rows and stable defaults. Middle/High/Ultra can author denser material tables without changing runtime DTO shape.
Hardware Impact: Runtime hot path impact is zero; cold CSV parse cost is bounded by 64 KB scratch and runs outside gameplay ticks.

## Decision 9 - Cold Initialization Of Readable Uninitialized Buffers

Problem: `UninitializedMemory` is correct for boot speed only when every later read observes authored data. Flow samples, material-volume rows, debug rows, telemetry, body bindings, and counters can be read before an upstream owner populates them, so random memory could masquerade as active flow/current/state flags.
Solution: Added `InitializeBuoyancyColdBuffersJob`, a cold Burst job that clears the readable Vault buffers once after handle acquisition and before CSV/mock seeding. Runtime state arrays remain uninitialized because mock generation or external owners write them explicitly.
Rejected Alternatives: Requesting every buffer with `ClearMemory` would pay OS zero-fill cost for large capacity windows. Clearing in managed C# loops would add cold startup overhead and violate the vectorized initialization requirement.
Scalability potential: Low/Middle/High/Ultra all get deterministic cold state. Runtime quality scaling remains in `EvaluateBuoyancyJob`, not the initializer.
Hardware Impact: Cold boot only. It prevents garbage flow flags from adding O(n) fake current checks during the first fixed tick.

## Decision 10 - Source Review Corrections

Problem: The first implementation still had four rot points: fallback current math cast absolute AUP X/Z to `float`, low-quality drag could still enter exact-speed work, `Awake` could request Vault buffers in edit mode, and the scheduler overwrote the authored quality cap with the current thermal value, making quality recovery sticky.
Solution: Fallback current now uses `CurrentAUP - SectorAUP` before casting to local `float3`; sample radius checks still subtract sample AUP before cast. Drag uses `math.step` thresholds plus smooth polynomial blends: below q=0.25 it remains linear and skips the relative-speed path, q=0.25..1 blends quadratic drag, and exact sqrt only becomes reachable above q=0.3. `Awake` now returns outside Play Mode. `BuoyancyTuningDTO` now keeps authored `GlobalQualityWeight` and writes runtime `ResolvedQualityWeight` into the previous 124-byte padding slot.
Rejected Alternatives: Leaving absolute AUP in the triangle-current fallback was rejected because 100 km edge precision drift would desync debris motion. A binary `IsLowEndHardware` branch was rejected; the branch is keyed only by continuous `GlobalQualityWeight`/`math.step` and sheds uniform ALU under thermal pressure. Keeping quality in one field was rejected because it permanently collapsed the designer cap after one low thermal frame.
Scalability potential: Low keeps stride-heavy O(n/12), linear drag, no exact speed sqrt, local triangle current, and surface snap. Middle blends quadratic drag and tighter stride. High evaluates every record with quadratic drag and exact speed blend. Ultra remains deterministic simulation truth and spends visual overkill outside the authoritative force route.
Hardware Impact: Static source estimate on i3/MX350: q<0.25 now avoids per-object `lengthsq` and `sqrt` for drag, while local-AUP current prevents precision churn that would otherwise defeat sleep and force stable objects back into evaluation.

## Decision 11 - Bottom Sleep And Live Sector AUP

Problem: Local current math depends on `SectorAUP`, but default tuning left it at zero unless an external owner wrote it. Seafloor sleep also still required force equilibrium, which keeps heavy/sunken bodies alive forever even when they are already resting on the bottom.
Solution: The scheduler now stamps `BuoyancyTuningDTO.SectorAUP` from `HectonFloatingOrigin.CurrentTotalOffsetDouble` every fixed tick before scheduling the Burst job. The sleep predicate now treats seafloor contact as speed-based rest: if the object is slow enough and touches the bottom plane, it sleeps regardless of residual gravity/buoyancy force; surface sleep still requires force balance.
Rejected Alternatives: Leaving `SectorAUP` as a designer-authored static value was rejected because origin shifts would silently reintroduce absolute-float fallback drift. Requiring force balance at the bottom was rejected because terrain/contact is the support constraint, not buoyancy equilibrium.
Scalability potential: Low-tier devices gain more persistent sleepers for sunk cargo and debris. Middle/High/Ultra keep identical authoritative sleep truth; additional visual richness remains presentation-side.
Hardware Impact: Static source estimate: sunk objects collapse to sleeper branch after velocity falls under threshold, cutting recurring buoyancy/drag/packet work for cargo piles on low-end silicon. Exact microseconds remain unclaimed until profiler proof.

## Decision 12 - Deferred Force Drain Fence

Problem: The solver can legally finish after the post-fixed drain window and be completed by `LateFrameTick`. Without a retained drain fence, the next `FixedTick` can reset the Vault force-packet window before `PhysicsApplySystem` consumes the previous frame's packets.
Solution: Added `_forcePacketsReadyToDrain`. `CompletePendingSolver` sets it when a scheduled job finishes. `FixedTick` refuses to schedule or clear packet counters while this flag is set. `PostFixedTick` drains the Vault packet window and then clears the flag. Disable/teardown paths clear the flag after forced completion or handle reset.
Rejected Alternatives: Blocking in `PostFixedTick` until every solver finishes was rejected because it violates the dependency-chain mandate. Letting `LateFrameTick` apply forces directly was rejected because force application belongs to the post-fixed physics owner. Double-buffering packet windows was rejected for now because the single-window fence closes the correctness hole without doubling Vault memory.
Scalability potential: Low/Middle/High/Ultra preserve one-owner force routing without overwriting late packets. High-end devices will usually drain same post-fixed step; low-end devices may skip one fixed scheduling slot rather than drop forces.
Hardware Impact: Prevents wasted solver work from overwritten force packets. Worst-case low-end cost is one skipped scheduling tick after a late completion; no measured runtime claim yet.

## Decision 13 - True Strided Scheduling And Telemetry Freshness

Problem: The first low-quality stride implementation skipped work inside `EvaluateBuoyancyJob`, but the scheduler still launched one parallel-for item for every active object. That is not real thermal load shedding. It also allowed stale debug rows to inflate evaluated/force-packet telemetry on frames where those rows were intentionally not scheduled.
Solution: `FixedTick` now computes a deterministic `EvaluationOffset` from `_simulationFrame % EvaluationStride` and schedules only the strided subset count. `EvaluateBuoyancyJob` maps `workIndex` back to the actual state index. If active count is smaller than stride and the offset owns no rows, a reduce-only telemetry job runs so `_simulationFrame` still advances and the next offset can be reached. The telemetry reducer now preserves packet count from the false-sharing-padded atomic counter and only accumulates evaluated forces from debug rows whose `FrameIndex` equals the current frame.
Rejected Alternatives: Branch-only stride was rejected because it still pays scheduling overhead for skipped rows. Returning early on empty offsets was rejected because it can freeze the round-robin frame counter. Counting `FlagForceQueued` from debug rows was rejected because strided rows are intentionally stale.
Scalability potential: Low evaluates roughly `active/12` rows per fixed tick and reports only current-frame force totals. Middle tightens the stride continuously. High/Ultra use stride 1 and therefore keep full per-frame authority without changing DTOs or routes.
Hardware Impact: Static estimate on i3/MX350: q~0.1 and 1000 active objects now schedules about 83-84 `IJobParallelFor` iterations instead of 1000. Compile/profiler proof remains blocked by the CPU gate.

## Decision 14 - Unity Asset Meta Stabilization

Problem: The new SHINOBU_158 C# source assets did not have checked-in `.meta` files. Unity would generate GUIDs on import, creating avoidable reference churn and nondeterministic editor state across machines.
Solution: Added fixed `MonoImporter` `.meta` files for all six new script assets and fixed `DefaultImporter` `.meta` files for the two new asset folders in the buoyancy/editor route.
Rejected Alternatives: Relying on Unity-generated meta files was rejected because this is a multi-agent workspace and GUID churn is unnecessary integration noise.
Scalability potential: No runtime tier effect. It protects editor/import determinism across Low/Middle/High/Ultra authoring machines.
Hardware Impact: No frame impact. It avoids import/reference churn, not gameplay cost.

## Decision 15 - Runtime Rot Pass

Problem: The previous source still had reflection in layout validation, duplicate cold work across `Awake` and `OnEnable`, a possible permanent `_forcePacketsReadyToDrain` block when Vault handles disappeared before drain, stale `LastNetForce` under striding, and only one of the two documented black-box dump paths.
Solution: Layout validation now uses explicit constants and contains no `System.Reflection` path. `_coldBootCompleted` makes CSV parsing and mock seeding idempotent per Vault acquisition. Post-fixed drain clears stale readiness if the Vault route cannot be resolved. Telemetry stores only a current-frame sanitized `LastNetForce`. Fatal dumps now write both `Dump_FLUID_DYNAMICS.bin` and `Dump_SHINOBU_158.bin`. The UI Toolkit tuner is explicitly wrapped in `#if UNITY_EDITOR`.
Rejected Alternatives: Leaving reflection in boot code was rejected because compile-wall mandates forbid runtime reflection caches. Leaving duplicate cold boot was rejected because Unity invokes `Awake` and `OnEnable` in sequence. Holding a drain flag without a valid Vault was rejected because it deadlocks the solver. Keeping a single dump name was rejected because XML and AGENTS specify different artifact names.
Scalability potential: Low-tier devices avoid duplicate cold jobs and stale drain stalls. Middle/High/Ultra keep the same deterministic force truth. Visual overkill remains outside authoritative buoyancy math.
Hardware Impact: Static estimate: normal Play Mode startup avoids one redundant CSV read and one redundant full-state mock schedule. Runtime hot path remains unchanged except for telemetry freshness.

## Decision 16 - AsRef State Mutation

Problem: `BuoyancyStateDTO` had no C# properties, but the jobs still used `NativeArray` indexer writeback for state mutation. That is valid Burst syntax, but the batch mandate explicitly calls for `UnsafeUtility.AsRef<T>` mutation to avoid CS1612-style defensive copy drift.
Solution: `EvaluateBuoyancyJob` and `GenerateMockBuoyantObjectsJob` now get a raw `BuoyancyStateDTO*`, convert the row to `ref BuoyancyStateDTO` with `UnsafeUtility.AsRef<BuoyancyStateDTO>`, and write state through the ref. A scan confirms no direct `States[index]` setter remains.
Rejected Alternatives: Leaving the indexer writeback was rejected because it was a weaker proof against defensive copies even if Burst would likely optimize it. Rewriting every debug/packet write to raw refs was rejected because the mandate targets mutable state DTOs and the packet/debug buffers are not the authoritative state array.
Scalability potential: Low/Middle/High/Ultra get the same deterministic state truth with a stricter mutation route. The main payoff is compile/runtime proof clarity, not a claimed visual-tier change.
Hardware Impact: Static estimate only: removes one NativeArray indexer writeback route per evaluated state and one cold mock writeback route per seeded state. No measured microseconds until Burst profiler proof.

## Decision 17 - Strided Parallel Writer Safety

Problem: After true strided scheduling, `EvaluateBuoyancyJob` no longer writes `States[workIndex]` or `DebugForces[workIndex]`; it writes the mapped row `(workIndex * EvaluationStride) + EvaluationOffset`. Unity's default `IJobParallelFor` safety restriction cannot prove this non-workIndex write is disjoint even though the math is injective.
Solution: Marked only the affected writer buffers with `[NativeDisableParallelForRestriction]`: solver `States`, solver `DebugForces`, mock `States`, plus the already intentional force-packet/counter shared writers. The disjointness proof is explicit: with fixed `stride >= 1` and fixed `offset`, `workIndexA != workIndexB` implies `(workIndexA * stride + offset) != (workIndexB * stride + offset)`.
Rejected Alternatives: Reverting to branch-only stride was rejected because it would schedule every active object again. Disabling safety broadly on every buffer was rejected because only the mapped writers need it. A managed staging queue was rejected because it would violate the Vault/Burst route.
Scalability potential: Low quality keeps the real `ceil(active/stride)` scheduler collapse without Unity safety exceptions. Middle/High/Ultra retain stride 1 behavior and therefore the annotation is a no-op in practice.
Hardware Impact: No speed claim. This preserves the previously documented low-tier scheduling reduction while keeping the writer proof local and auditable.

## Decision 18 - Emergency Mock Fail-Open Gate

Problem: The fallback mock generator is useful for CI/profiling when inventory drop streams do not exist, but unconditional cold-boot mock seeding can overwrite a producer-owned Vault state buffer if an external owner has already published live active rows.
Solution: Default tuning now starts with `ActiveStateCount = 0`, and cold boot calls `GenerateMockBuoyantObjects()` only when `_seedEmergencyMockObjects` is enabled and the tuning row still reports zero active states. The mock remains available for isolated profiling while live producer data wins by default.
Rejected Alternatives: Keeping unconditional mock seeding was rejected because it violates one fact -> one owner. Disabling the mock entirely was rejected because Task 05 requires an emergency 1000-object stress source.
Scalability potential: Low/Middle/High/Ultra runtime tiers no longer pay or risk mock overwrite when a real producer is present. CI/no-inventory runs still seed the deterministic 1000-object stress buffer.
Hardware Impact: No hot-path speed claim. Cold boot avoids one 4096-row mock write when live producer state already exists.
