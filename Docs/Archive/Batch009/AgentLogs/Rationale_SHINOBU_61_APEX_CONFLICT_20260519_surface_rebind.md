# Rationale_SHINOBU_61

Date: 2026-05-19
Evidence status: LOOP 18 SOURCE HARDENED; ROSLYN RECHECK BLOCKED BY CPU GUARD; UNITY PLAY MODE/PROFILER PENDING

## Decision 00 - Runtime Shape Before Code

Problem: Leviathan AI must hunt predictively without `NavMeshAgent`, raycast-heavy LoS, managed state classes, or concrete dependencies on other agents' unfinished domains.
Solution: Build a math-only Burst/DOD `ApexBrain` surface under the AI domain with aligned unmanaged DTOs, AUP-localized float math, preallocated NativeArray buffers, utility scores, spatial/acoustic hashes, mock SDF potential-field steering, continuous `GlobalQualityWeight`, and signal DTOs for other domains.
Rejected Alternatives: Unity `NavMeshAgent`, full-body raycasts, C# OOP state classes, direct player/base/audio/HUD references, and binary quality toggles are rejected because they violate prompt constraints and AGENTS.md hot-path rules.
Scalability potential: Low uses 2 predictive nodes and head-only SDF lie; Middle adds midsection checks and more node samples; High uses head/mid/tail SDF gradients; Ultra keeps 16 ambush nodes plus richer telemetry/debug facade without bloating gameplay truth structs.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding raycast/body-fit checks and managed state churn; target saved time is roughly 80-180 microseconds per active leviathan versus naive raycast/path-state logic, pending profiler proof.

## Byte Layout Audit Targets

- `ApexStateDTO`: 64 bytes target, no `Pack=1`.
- `double3 AUP`: 24 bytes.
- `float3 Velocity`: 12 bytes.
- `float AggressionLevel`: 4 bytes.
- `uint TargetHash`: 4 bytes.
- `uint AcousticMemoryHash`: 4 bytes.
- `float Stamina`: 4 bytes.
- `ulong _pad0`: 8 bytes.

## Toaster / High-End Policy

Low: cheapest approximation first, stable frame rate, potential visual clipping accepted only as the Dear Lie.
Middle: additional steering samples and smoother aggression hysteresis.
High: fuller slither gradient and more ambush node density.
Ultra: visual overkill through richer debug/proximity outputs and max predictive node evaluation, not heavier authority coupling.

## Decision 01 - Graveyard Evidence and Emergency Mock Stats

Problem: Task 01 demanded legacy `apex_predator_curves.h8bin` evidence, but current binary ledger and archive recon did not prove an active apex predator payload.
Solution: Keep apex stats as explicit unmanaged emergency fallback rows in `ApexBrainDefaults`: aggression build seconds, turn radii, strike windows, visual overkill scalars. Vault boot hydrates these rows into `ApexEmergencyStats`.
Rejected Alternatives: Reusing `Data/AI/Navigation_Tuning.h8bin` as apex tuning was rejected because the ledger marks it script/tool-only and not apex-specific. Inventing a stale h8bin format was rejected as fake evidence.
Scalability potential: Low consumes only stable fallback scalars and two ambush nodes; Middle interpolates more nodes and mid SDF; High/Ultra spend cycles on full 16-node evaluation and richer debug scalars.
Hardware Impact: i3/MX350 gains by avoiding cold binary probes and per-frame curve lookups; estimated hot-path cost saved is 0 file IO and roughly 5-12 us versus a live lookup layer.

## Decision 02 - Compile Wall Buffer ID Isolation

Problem: New DataVault buffers were required, but editing the shared `BufferID` enum would increase merge risk and force broader rebuild ownership.
Solution: Reserve SHINOBU casted IDs `70609-70619` and `70626-70629` inside `ApexBrainVaultBufferIds`, avoiding active physics/dispatcher ranges already present in the project.
Rejected Alternatives: Modifying core enum headers or referencing sibling domains was rejected to preserve compile-wall isolation.
Scalability potential: Low/Middle/High/Ultra all use the same vault handles; quality changes mutate math only, not allocation topology.
Hardware Impact: Stable vault IDs avoid runtime discovery maps and reduce cold bootstrap churn; estimated i3/MX350 gain is small but real, 2-4 us at boot and 0 us/frame after boot.

## Decision 03 - Burst Defaults Outside Vault

Problem: The first `ApexBrainJob` draft called fallback builders through `ApexBrainVault`, a cold bridge class that also contains file IO and dump methods.
Solution: Move unmanaged fallback builders to `ApexBrainDefaults` in contracts and let Vault delegate to it. Burst jobs now call pure defaults without touching the cold bridge surface.
Rejected Alternatives: Keeping Burst references to the Vault class was rejected because it makes Burst/AOT analysis fragile even if the specific method body is pure.
Scalability potential: All quality levels get deterministic fallback data without managed side effects.
Hardware Impact: This is primarily compile/AOT risk reduction; i3/MX350 frame gain is 0-2 us, but it prevents fallback builder drag from becoming a Burst wall.

## Decision 04 - Sweet Lie LOS and Slither Fake

Problem: Full line-of-sight and 100m-body cave fitting would either trap the leviathan in rocks or burn CPU on physics queries.
Solution: Replace physical truth with a sweet lie: dot product of player forward versus vector to leviathan, distance falloff, center SDF wall shadow, and spatial-hash canyon noise. Steering uses one analytic cave SDF and optional mid/tail samples weighted by `GlobalQualityWeight`.
Rejected Alternatives: `Physics.Raycast`, `NavMeshAgent`, body capsule sweeps, mesh colliders, and full pathfinding were rejected because their cost scales with world complexity and cannot guarantee <0.2 ms.
Scalability potential: Low evaluates head/center and 2 nodes; Middle adds midsection and 5-9 nodes; High adds tail and 10-14 nodes; Ultra evaluates 16 nodes and richer visual scalars for downstream overkill.
Hardware Impact: Expected i3/MX350 win is 140-260 us per active leviathan versus naive raycast/path/body-fit logic; actual profiler evidence is still pending Unity Play Mode.

## Decision 05 - Acoustic Memory as Hash-Routed Signal

Problem: Apex cognition must react to loud events without taking a dependency on Agent 15 audio internals.
Solution: Consume decoupled `AcousticEchoTap` signal rows, select loudest decayed echo in a fixed NativeArray scan, convert AUP to local float3, and write only `AcousticMemoryHash` plus local target data.
Rejected Alternatives: Managed event queues, direct audio manager references, and per-frame allocations were rejected as compile-wall and GC violations.
Scalability potential: Low can cap tap count externally; Middle/High/Ultra keep the same 32-row max and spend saved cycles on node richness, not on audio coupling.
Hardware Impact: i3/MX350 estimate is 10-18 us saved and zero GC versus managed acoustic subscriptions.

## Decision 06 - Continuous Quality Instead of Tier Branches

Problem: The leviathan must scale from throttled VR/mobile to overkill desktop without visible binary pops or low/ultra dichotomy.
Solution: `GlobalQualityWeight` is saturated, smoothed, and used to lerp ambush nodes from 2 to 16. Midsection SDF starts at quality 0.25, tail SDF fades in toward 0.94, and visual scalar output grows with the same curve.
Rejected Alternatives: `if (IsLowEnd)` quality switches and separate low/high codepaths were rejected because they violate the scalability pillar and create divergent behavior.
Scalability potential: Low 0.1 equals 2 nodes and head/center SDF; Middle adds interpolated node density and midsection; High adds tail; Ultra reaches 16 nodes and visual-overkill scalar output.
Hardware Impact: On i3/MX350 the low curve should save 45-90 us per active leviathan; on high-end hardware those cycles are spent on extra ambush intelligence and debug/visual data.

## Decision 07 - Signals, Not Domain Calls

Problem: Breach damage, IK snapping, biome danger, and fauna scatter must be produced without direct dependency on WFC, animation, biome, or ecosystem runtimes.
Solution: Output only unmanaged data: `MockCombatDamageSignal`, local `IK_BiteTarget`, biome hash multiplier, and `GlobalPanicSignal`.
Rejected Alternatives: Direct calls to base integrity, animation rig, biome volume, or ecosystem systems were rejected as compile-wall and ownership violations.
Scalability potential: The same DTO surface works for Low/Middle/High/Ultra; quality changes adjust math density but not the integration contract.
Hardware Impact: Expected i3/MX350 gain is 20-40 us/frame by avoiding cross-domain lookups and virtual/domain dispatch; WFC physics remains owned by its domain.

## Decision 08 - Black Box and Human Control Facade

Problem: The apex cortex needs forensic state and designer control without polluting hot gameplay with managed logs or recompiles.
Solution: Allocate telemetry ring in DataVault, write 300 frames of fixed-size `ApexTelemetryEntry`, expose cold dump helpers for `Dump_SHINOBU_61.bin` and legacy `Dump_LEVIATHAN_CORTEX.bin`, and add `Leviathan Cortex Tuner` for sliders/CSV/gizmos.
Rejected Alternatives: `Debug.Log` telemetry, scene debug GameObjects, ScriptableObject-only tuning, and runtime JSON were rejected as GC/recompile/editor-only leaks.
Scalability potential: Low writes the same compact telemetry; Middle/High/Ultra increase math detail but keep fixed telemetry memory. Editor gizmos remain outside player runtime.
Hardware Impact: i3/MX350 gains by paying 0 managed log allocation in-frame; estimated hot-path saved cost is 30-50 us versus string telemetry and runtime debug GameObjects.

## Decision 09 - NaN Fault Contract

Problem: SDF and LOS math must not silently poison spatial hashes or outputs if an invalid sample slips in.
Solution: After SDF/LOS evaluation the job validates local target, intercept, wall repulsion, SDF distances, and sweet-lie LOS. Faulted rows zero authority outputs and write `FaultCode`; cold caller can invoke `TryDumpBlackBoxOnFrameFault` for immediate binary forensic dump.
Rejected Alternatives: Throwing exceptions or file IO from Burst was rejected because Burst jobs cannot own managed IO and would break scheduling.
Scalability potential: All quality levels share the same fault contract; low-tier SDF collapse reduces the probability surface by avoiding tail samples.
Hardware Impact: Additional finite checks cost under 2 us for 10 rows; preventing corrupted downstream transforms is worth the branch cost on i3/MX350.

## Decision 10 - Verification Boundary

Problem: Unity project files were stale and no generated `Hecton8.AI.Cognition.csproj` included the new asmdef files yet.
Solution: Reused Unity Bee Roslyn response files and manually added SHINOBU runtime/editor sources into isolated compile checks. Runtime and editor checks passed; Play Mode/profiler proof remains pending because Unity Editor was not launched.
Rejected Alternatives: Claiming full Unity import proof from stale csproj was rejected as fake reporting.
Scalability potential: Verification method does not affect runtime scaling; it keeps compile evidence tied to Unity's actual references.
Hardware Impact: No frame impact. Compile-wall protection is preserved by avoiding sibling runtime references.

## Decision 11 - Scratchpad Literal Compliance and Telemetry Honesty

Problem: The first pass used `ApexInfluenceNode` rows as the ambush scratch map and wrote `InterceptComputeTimeMs = 0f`. That was functional, but not strict enough: the XML specifically requested a preallocated `NativeArray<float3>` scratchpad and telemetry with compute-time state.
Solution: Add `AmbushNodeScratch` as a vault `NativeArray<float3>` with BufferID `70629`; write every evaluated ambush candidate into it. Replace zero compute time with a deterministic estimate derived from evaluated nodes, acoustic tap cap, SDF sample gates, and `GlobalQualityWeight`. Add heartbeat overload that calls fault-frame dump after the scheduled job completes.
Rejected Alternatives: Keeping only rich influence rows was rejected because it made the prompt's scratchpad requirement arguable. Measuring wall-clock time inside Burst was rejected because it breaks determinism and cannot be done safely in the job.
Scalability potential: Low writes 2 scratch nodes and head/center SDF; Middle writes interpolated scratch density; High/Ultra write up to 16 nodes. Telemetry cost remains fixed-size and DataVault-owned.
Hardware Impact: Scratchpad write cost is sub-5 us for 16 nodes on i3/MX350, while removing ambiguity for downstream animation/steering consumers. Estimated compute telemetry now exposes budget drift instead of hiding it as zero.

## Decision 12 - Endianness and Quality Gate Explicitness

Problem: The binary dump had no endian marker, and quality gates used smoothing but did not explicitly use `math.step` as demanded by the hardware-matrix mandate.
Solution: Write a `0x01020304` endian marker into the dump header and add `math.step` gates around low-quality collapse and mid/tail SDF activation while keeping polynomial `SmoothStep` and `math.lerp` interpolation.
Rejected Alternatives: Assuming all current targets are little-endian was rejected as a forensic risk. Binary quality branches were rejected as scalability violations.
Scalability potential: Low, Middle, High, Ultra remain a continuum: step gates only decide whether to spend sample ALU; weights still fade via polynomial curves.
Hardware Impact: Endian marker is cold IO only. `math.step` gates prevent wasted tail/mid SDF work on i3/MX350 while preserving high-tier overkill.

## Decision 13 - SignalBus Without Core Reference

Problem: The XML says signal pushes, but directly referencing `Hecton8.Core` from AI.Cognition to call `SignalBus<T>` caused a Roslyn compile conflict: `ISignal` exists in both `Hecton8.Core` and `Hecton8.Core.Contracts` reference assemblies.
Solution: Keep AI.Cognition routed through Core.Contracts/Core.Memory only. Add optional `NativeQueue<T>.ParallelWriter` fields to `ApexBrainJob` and `ApexBrainVault.AttachSignalWriters(...)`. A Core/SignalBus owner can pass writers into the job; Burst then enqueues proximity, combat, and panic signals without AI.Cognition directly referencing Core.
Rejected Alternatives: Forcing a direct `Hecton8.Core` reference was rejected because it broke compile proof and widened the compile wall. Managed post-job SignalBus reflection was rejected as GC and fragility.
Scalability potential: Low/Middle/High/Ultra use the same optional queue surface. If no external writer is attached, vault rows remain deterministic fallback outputs.
Hardware Impact: Queue enqueue is paid only when caller attaches writers and signal magnitude is non-zero. Avoiding direct Core dependency protects iteration time; estimated hot-path queue cost is under 5 us for the three active signal lanes.

## Decision 14 - Fault Semantics Are Not Inactivity

Problem: The first hardening pass treated inactive mock targets as faults, which could trigger forensic dumps on an empty scene before Player Kinematics or mock target hydration exists.
Solution: Keep inactive targets as Dormant/zero-output rows, but reserve `ApexBrainFlags.Fault` and `FaultCode` for non-finite input (`SHNI`) or non-finite computed SDF/LOS math (`SHNN`).
Rejected Alternatives: Dumping on inactive rows was rejected as black-box noise. Suppressing all fault rows was rejected because NaN autopsy is mandatory.
Scalability potential: Same semantics at all quality weights; low-tier collapse reduces computed-fault surface by bypassing tail samples.
Hardware Impact: No material frame cost. It prevents false forensic IO spikes during cold boot or empty test scenes.

## Decision 15 - Schedule Writer Bridge as the Core Boundary

Problem: The job could enqueue into optional `NativeQueue<T>.ParallelWriter` lanes, but the vault facade only exposed a manual two-step path: create job, attach writers, schedule. That is easy for an integrator to misuse and tempts a future direct `Hecton8.Core` reference from AI.Cognition.
Solution: Add `ApexBrainVault.TryScheduleWithSignalWriters(...)` as the explicit schedule entry point for Core/SignalBus owners, and document the boundary in `Docs/ARCHITECTURE/SHINOBU_61_APEX_COGNITION.md`. The runtime AI asmdef still references only Contracts/Memory plus Unity Burst/Collections/Jobs/Mathematics.
Rejected Alternatives: Direct SignalBus calls from AI.Cognition were rejected after duplicate `ISignal` reference evidence. Managed post-job relay was rejected because it moves hot signal traffic through managed code and weakens rollback determinism.
Scalability potential: Low, Middle, High, and Ultra use the same writer attachment surface; quality affects emitted signal intensity and node math, not assembly topology.
Hardware Impact: Zero extra cost when no writer is attached. With writers attached, enqueue cost is paid only for non-zero proximity, damage, or panic lanes; expected i3/MX350 cost remains below 5 us for three lanes.

## Decision 16 - Cold-Boot Entropy and Trig Removal

Problem: The previous version technically satisfied `NativeArrayOptions.UninitializedMemory`, but cold rows could carry random finite bits before Player Kinematics or mock target hydration. That can schedule phantom active leviathans, pollute telemetry, and spend SDF/ambush ALU on empty scenes. The ambush loop also used `math.sincos` per evaluated node, which is not acceptable for Quest-class thermal budgets when the prompt asked for dot product plus spatial hash aggression.
Solution: On first fallback hydration, use `UnsafeUtility.MemClear` over all runtime rows and scratch arrays before installing emergency tuning. The locked-DataVault path now resolves existing handles and runs the same validation/hydration without requesting new allocation. In the Burst job, inactive targets early-out into a Dormant row with cleared outputs/signals and cheap telemetry. `ActiveLeviathans` telemetry records per-row active truth (`1` active, `0` Dormant) instead of schedule capacity. Ambush node placement uses a deterministic 16-lane octant lattice with spatial-hash radial jitter, not trig. Distance/SDF radial lengths use guarded `x * rsqrt(x)` instead of direct `sqrt`.
Rejected Alternatives: Keeping uninitialized rows "because the prompt said uninitialized" was rejected; uninitialized allocation is a boot-cost optimization, not permission to run on garbage bits. Keeping per-node sine/cosine was rejected because it burns scalar ALU and violates the spirit of math-only spatial hashing. A precomputed managed direction array was rejected because the hot job must stay DataVault/unmanaged and allocation-free.
Scalability potential: Low keeps two octant nodes and dormant rows cost near-zero. Middle increases node count without trig spikes. High and Ultra still evaluate up to 16 lanes, but the saved ALU can buy richer downstream visual overkill instead of authority math.
Hardware Impact: Low-end i3/MX350/Quest-class gain is estimated at 6-14 us per active leviathan from trig removal at 16 nodes, plus 20-60 us/frame saved in empty or partially hydrated scenes by skipping SDF/acoustic/ambush work for dormant rows. Fresh runtime/editor Roslyn recheck remains pending; the local Bee artifact timestamp did not update after the Loop 10/11 source edits and CPU/process guards prevented launching a new targeted compiler.

## Decision 17 - Optional NativeQueue Writers Need Disabled Container Safety

Problem: `ApexBrainJob` must support a no-writer schedule path and a SignalBus-writer schedule path. Keeping default `NativeQueue<T>.ParallelWriter` fields in the same job can trigger Unity Jobs safety validation even when `EnableSignalQueueWrites` is zero and the fields are never accessed.
Solution: Mark the three optional writer fields with `NativeDisableContainerSafetyRestriction` and keep all enqueue calls gated by `EnableSignalQueueWrites`. The normal vault signal rows remain the deterministic fallback output.
Rejected Alternatives: Splitting a second full ApexBrain job just for queue writes was rejected because it would duplicate the entire hunting kernel and raise drift risk. Managed post-processing was rejected for GC/rollback reasons.
Scalability potential: Low/Middle/High/Ultra keep one kernel; writer traffic scales with signal intensity, not duplicated AI evaluation.
Hardware Impact: No-writer path avoids safety-blocked scheduling without added frame cost. Writer path still pays only the enqueue cost for active non-zero signals.

## Decision 18 - GlobalQualityWeight Must Gate Schedule Frequency Too

Problem: Node density and SDF samples were already continuous, but low-quality hardware still had to enter the scheduler every frame. The hardware-matrix mandate requires update frequency to breathe from 5 Hz to 60 Hz with `GlobalQualityWeight`.
Solution: Add `ApexBrainVault.ShouldEvaluateFrame(...)` and call it from both scheduler facade paths. The cadence derives from `math.lerp(5f, 60f, Smooth01(...))`, then a deterministic 60-frame mask evaluates `round(updateHz)` frames per window. This keeps 5..60 Hz without platform names or binary low/high branches.
Rejected Alternatives: A hard `if (quality < 0.3f) run every 12th frame` was rejected because it creates a visible step. A simple `round(60 / updateHz)` stride was also rejected because high-quality ranges collapse into coarse 30/60 Hz jumps. Moving the gate into the Burst row loop was rejected because the scheduling cost would still be paid.
Scalability potential: Low quality holds roughly 5 Hz authority updates plus 2 ambush nodes; Middle continuously increases cadence and node density; High/Ultra run every frame with richer node and SDF evaluation.
Hardware Impact: At quality 0.1 the scheduler evaluates 5 of 60 frames, saving the full job cost on skipped frames. At quality 1.0 there is no cadence loss. Runtime/editor Roslyn proof passed after the latest 60-frame mask correction.

## Decision 19 - Rollback Determinism and Stale Scratch Erasure

Problem: The authority apex jobs were still compiled with `FloatMode.Fast`, even though the mandate says rollback-relevant simulation state must use deterministic floating-point behavior. Also, Dormant and faulted rows could leave old ambush scratch/influence nodes in DataVault, which risks editor gizmos and downstream consumers reading stale predator intent after a target deactivates or a NaN guard fires.
Solution: Change both Apex Burst jobs to `FloatMode.Deterministic` while preserving `CompileSynchronously = true` and `FloatPrecision.Standard`. Add `ClearAmbushRows(...)` and call it for Dormant and faulted rows. Faulted outputs now zero non-authority utilities, vectors, node counts, and visual scalars while retaining fault flags/state hash for telemetry.
Rejected Alternatives: Keeping Fast mode was rejected because cross-platform rollback correctness is more important than marginal ALU throughput for authority AI. Leaving stale scratch rows was rejected because the editor facade and any animation bridge may consume `AmbushNodeScratch` independently of `Outputs[index].Phase`.
Scalability potential: Low quality still benefits from cadence/node collapse; High/Ultra retain deterministic 16-node overkill without trig. Scratch clearing adds fixed 16-row writes only on Dormant/fault rows, not on normal active evaluation.
Hardware Impact: Deterministic Burst may cost a small amount of FP throughput, but it prevents multiplayer drift. Clearing stale rows costs under 2-4 us for 16 rows and avoids downstream visual/debug corruption.

## Decision 20 - Duplicate-ID Audit Trail Closure

Problem: The workspace has two historical `SHINOBU_61` meanings. At Loop 13, the active `Status_SHINOBU_61.md` and `Rationale_SHINOBU_61.md` belonged to the later voxel Surface Nets prompt, while the user instruction resumed the earlier Apex Leviathan prompt.
Solution: Loop 13 preserved Apex evidence in the `*_APEX_LEVIATHAN_ARCHIVE_20260518` files and added a pointer instead of overwriting active Voxel evidence. Loop 15 superseded that temporary stance after the user explicitly rebound the active prompt to Apex; active SHINOBU_61 files now carry Apex evidence, and Voxel evidence remains in `_VOXEL_SURFACE_NETS_ARCHIVE_20260518`.
Rejected Alternatives: Mixing Apex and Voxel evidence in one active file was rejected. Deleting either prompt history was rejected because both duplicate-ID trails are audit evidence.
Scalability potential: Runtime behavior is unchanged. The decision protects compile-wall/domain hygiene by making duplicate prompt routing explicit.
Hardware Impact: 0 us frame cost. Prevents integration time waste by pointing reviewers to the correct Apex files.

## Decision 21 - Acoustic Memory Must Scale Too

Problem: The previous hardening pass made ambush nodes, SDF samples, and scheduler cadence respond continuously to `GlobalQualityWeight`, but the acoustic memory bank still scanned the full 32-row tap array whenever the apex job ran. At low quality this left unnecessary ALU and memory traffic in the exact subsystem that should collapse to a small blind-hearing approximation.
Solution: Add `ResolveAcousticTapLimit(...)` to lerp the acoustic scan window from 4 taps at survival quality to 32 taps at full quality using the existing polynomial `qualityCurve`. Pass that resolved limit into `ResolveAcousticMemory(...)`, and feed the same evaluated count into telemetry compute-time estimation.
Rejected Alternatives: A binary `if (quality < 0.3) use 4 taps` branch was rejected because it violates the continuous scalability law. Keeping the full scan was rejected because it leaves low-quality frames paying high-tier sensory cost.
Scalability potential: Low quality now runs 5 Hz cadence, 2 ambush nodes, head/center SDF, and 4 acoustic taps. Middle quality smoothly increases tap count and node count. High and Ultra evaluate the full 32-tap acoustic bank and 16-node lattice without changing the authority contract.
Hardware Impact: On i3/MX350/Quest-class hardware this removes up to 28 acoustic tap iterations on low-quality evaluated frames. Estimated saving is 4-8 us per active 10-row batch on frames where acoustic memory is evaluated, pending Roslyn/profiler proof. A 24-sample guard-aware Roslyn wait exited `ROSLYN_RECHECK_SKIPPED_CPU_GUARD` because CPU stayed 72-100% and compiler count was often 1-2; no new `dotnet` was launched.

## Decision 22 - Sweet Lie Needs Bounded Line Evidence, Not Raycasts

Problem: The sweet-lie LOS used dot product, target distance, center SDF, and canyon hash. That avoided raycasts, but it under-sampled the exact failure mode the user called out: apex AI visually believing a target is reachable while a rock lies between the head and prey. At the same time, adding physics rays would violate the core constraint and burn CPU.

Solution: Add one midpoint analytic SDF line sample gated by `GlobalQualityWeight` using `math.step(0.28f, quality)` and a smooth polynomial weight. Low quality still pays only center SDF plus canyon hash; high quality blends in midpoint wall evidence. Also clear all unevaluated ambush scratch/influence rows when quality drops so high-tier stale nodes cannot survive into low-tier execution. The same pass made CSV hot-reload metadata explicit inside unused `ApexBrainTuning` padding and cleaned remaining `Pack=1`/struct-property/Burst-directive rot in adjacent legacy AI Cognition files that share the same runtime assembly.

Rejected Alternatives: `Physics.Raycast`, `Linecast`, capsule sweeps, or `NavMeshAgent` were rejected again because cost scales with scene geometry. Leaving stale high-quality nodes was rejected because editor gizmos and future animation consumers can read the scratchpad directly. Keeping legacy packed structs was rejected because a single `Pack=1` in the same AI assembly undermines the ARM64 audit.

Scalability potential: Low keeps dot product + center SDF + canyon hash, Middle fades in the midpoint SDF lie, High/Ultra use the extra line shadow plus up to 16 interpolated nodes and full acoustic tap range. No binary hardware switch was added.

Hardware Impact: The midpoint SDF sample costs roughly 0.3-0.5 us per active high-quality row and is bypassed below the quality gate. Removing stale node reads prevents downstream debug/animation recomputation and avoids false pursuit intent; legacy packing cleanup protects ARM64 cache alignment. Roslyn proof remains pending because the current CPU/compiler guard still forbids launching `dotnet`.

## Decision 23 - Fault Must Quarantine Before Spatial Hash

Problem: The fault path detected non-finite AUP/velocity input, but the job still continued into AUP delta downcast, SDF sampling, dot-product LOS, and `HashSpatial` before zeroing the final output. That is structurally wrong: a NaN can poison integer cell casts or SDF math before the later fault clamp runs.

Solution: Add `WriteFaultRow(...)` and return immediately when input AUP or velocity is non-finite. The fault row sanitizes invalid state AUP to default, zeros velocity/aggression/signal rows, clears ambush scratch/influence spans, preserves fault flags and `SHNI` fault code, and writes black-box telemetry before any spatial hash or SDF math executes.

Rejected Alternatives: Letting the later `computedFinite` guard catch the NaN was rejected because it occurs after potentially unsafe work. Throwing or logging from Burst was rejected because the hot kernel cannot own managed error handling. Silently treating NaN as Dormant without fault telemetry was rejected because post-mortem evidence is mandatory.

Scalability potential: All quality levels share the same early quarantine. Low-quality frames avoid wasting SDF/acoustic/node work on corrupted rows; High/Ultra retain full math only for finite authority inputs.

Hardware Impact: Early return saves the entire apex row cost on corrupted inputs and prevents downstream Spatial Hash corruption. Expected normal-frame cost is one finite-input branch already paid by the existing guard.

## Decision 24 - Parallel Rows Need 64B Stride Multiples

Problem: Several rows written by `IJobParallelFor` were aligned to 8/16 bytes but not to 64-byte stride multiples. `MockPlayerAUP` was 96B, `ApexBrainOutputDTO` was 160B, legacy `AlphaLeviathanCognitionState` was 144B, and legacy `AlphaLeviathanSteeringOutput` was 88B. Adjacent job indices could share cache lines and invalidate each other under worker-thread writes.

Solution: Pad those parallel-written DTOs to exact 64-byte multiples: `MockPlayerAUP=128B`, `ApexBrainOutputDTO=192B`, `AlphaLeviathanCognitionState=192B`, and `AlphaLeviathanSteeringOutput=128B`. Update `ValidateLayouts()` so runtime layout proof rejects stale sizes.

Rejected Alternatives: Relying on scheduler batch size was rejected because ownership can change and output arrays are still contiguous. Leaving legacy Alpha rows alone was rejected because they share the same AI Cognition assembly and job pipeline. Splitting all outputs into SoA arrays was rejected for this pass because it would widen integration surface; padding is the bounded correction.

Scalability potential: Low/Middle/High/Ultra use the same stable row stride. The cost is a small memory increase for max 10 Apex rows and legacy cognition rows; the benefit is predictable multicore writes on ARM64/PC.

Hardware Impact: Removes false-sharing risk on Quest/i3 worker threads. Added Apex memory is 320B for `MockPlayerAUP[10]` and 320B for `ApexBrainOutputDTO[10]`, negligible relative to the 300-frame telemetry ring.

## Decision 25 - Computed Faults Must Stop Before Hashing

Problem: Loop 16 quarantined non-finite input before SDF and LOS work, but computed SDF/LOS faults still stayed inside the active path. A bad sampler value could mark `computedFinite == false` and then still flow through aggression, ambush-node scoring, signal construction, and `HashSpatial(interceptLocal)` before the later zero-output path.

Solution: Route computed SDF/LOS faults through `WriteFaultRow(..., 0x53484E4Eu)` and return immediately. After that early return, the active path has no fault rows, so the dead `faulted` `math.select` branches and active-path `faultCode` carrier were removed. Normal rows now write direct finite outputs; fault rows use the single quarantine writer.

Rejected Alternatives: Keeping the late clamp was rejected because telemetry/signals could still observe NaN-derived utility scalars before zeroing. Duplicating a second computed-fault output writer was rejected because it creates drift from the input fault path. Throwing/logging from Burst remains rejected.

Scalability potential: Low/Middle/High/Ultra all share the same quarantine path. Low quality avoids wasting acoustic/SDF/node work after a computed fault; high quality still gets full midpoint/tail/node overkill only for finite math.

Hardware Impact: Normal finite rows save several dead `math.select` operations. Faulted rows skip biome, aggro, node scoring, signals, and spatial hash. Expected gain is small in clean frames and material only during corrupted sampler/input scenarios; the main value is preventing Spatial Hash contamination.

## Decision 26 - Sanitize Cold Tuning Before Authority Math

Problem: The row-level finite guards catch poisoned math, but several cold tuning inputs could still create unnecessary fault rows or NaN presentation scalars: corrupt head/mid/tail offsets, corrupt emergency `float4` stats, non-finite sampler origin/floor/ceiling/canyon bias, non-finite target noise, and non-finite target acoustic magnitude.

Solution: Sanitize offsets inside `ResolveTuning()`, sanitize all emergency stat `float4` fields against emergency mock fallback rows, sanitize sampler origin and vertical span before SDF use, saturate canyon bias, sanitize target noise before aggression, and sanitize fallback target acoustic magnitude before acoustic override. The computed finite gate now also checks pursuit vectors and intermediate LOS scalars.

Rejected Alternatives: Relying only on the final `computedFinite` trap was rejected because it turns recoverable cold tuning corruption into a fault row and loses useful predator output. Sanitizing in editor only was rejected because runtime CSV and binary hydration can change values without the editor facade.

Scalability potential: Low quality avoids pointless fault exits from bad cold data; Middle/High/Ultra retain richer SDF/LOS/node work only after the scalar inputs are finite and bounded.

Hardware Impact: Normal-row cost is a small fixed set of cold/row scalar clamps. It prevents corrupted tuning from causing black-box dumps or stale output loss, and it keeps high-quality visual scalar output finite.
