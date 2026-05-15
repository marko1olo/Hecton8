# Rationale_LEVIATHAN_KINEMATICS_SOLVER

Status: PENDING VERIFICATION.

## Mandates Read Before Code

- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt
- ANIM_Contextual_Physical_IK.txt
- REND_GPU_Driven_Animation_VAT.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt

## Decision 0: Runtime Shape

Problem: Leviathan visual body clips because the current visual authority is a simple transform path, while the prompt requires no Unity Physics and no Animator/SkinnedMeshRenderer dependency.
Solution: Build a persistent native SOA solver around spine positions/velocities/matrices, schedule Burst jobs in simulation cadence, then upload matrices for GPU deformation/BRG-style rendering.
Rejected Alternatives: Standard Unity Animator IK and SkinnedMeshRenderer are rejected by the prompt and GPU-driven fauna mandate. Unity Physics raycasts are rejected because task requires SDF/MapMagic probing and no Unity Physics.
Scalability potential: Low uses eight segments and height fallback only; Middle uses 12-16 segments; High uses 20 segments with SDF pushout; Ultra keeps 20 segments and spends saved CPU on smoother tail whip and denser visual matrices.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding Animator CPU skinning and PhysX queries; actual microseconds require profiler evidence.

## Decision 1: Black Box Requirement

Problem: IK is critical creature presentation and can corrupt render matrices if NaN reaches GPU buffers.
Solution: Add a fixed 300-entry native telemetry ring for high-level spine state and non-finite flags, with dump path `Docs/AgentLogs/Dump_LEVIATHAN_KINEMATICS_SOLVER.bin`.
Rejected Alternatives: Debug.Log-only failure reporting is rejected because it allocates, loses preceding frames, and violates Black Box protocol.
Scalability potential: Low stores compact hashes and key positions; Ultra can preserve more per-segment detail if needed without changing external contract.
Hardware Impact: 300 compact entries are negligible native memory; avoids expensive crash diagnosis loops on weak hardware.

## Decision 2: Fauna Integration Boundary

Problem: Alpha Leviathan cognition owns stalking intent, but presentation was still coupled to a transform/Animator path that cannot respect terrain SDF contact.
Solution: Extend `FaunaKinematicsRuntime` and bind it from `FaunaBrain.EnsureLeviathanPresentationOwner`, consuming steering velocity, head look, telegraph, and strike intent without adding a singleton or direct cross-agent dependency.
Rejected Alternatives: Reusing `ProceduralLeviathanSpineIK` was rejected because it keeps transform-chain and Animator assumptions. Pulling AI data from a new global singleton was rejected because parallel-agent integration requires `GlobalRegistry` or existing owner calls.
Scalability potential: Low keeps an eight-segment intent-following presentation; Middle keeps more body coherence; High and Ultra spend saved Animator/SMR time on SDF pushout and stronger strike wave presentation.
Hardware Impact: Estimated i3/MX350 hot-path cost is one job schedule plus 8-20 segment math, roughly 6-35 us depending tier; no measured profiler capture was available.

## Decision 3: Terrain Contact Path

Problem: Unity Physics casts are forbidden, and terrain contact must work against voxel rock and MapMagic seabed.
Solution: The Burst job samples `VoxelSdfTexture3D` for the lower five segments, resolves a central-difference gradient, and pushes positive-density segments outward. When SDF is unavailable, it samples the quantized MapMagic height payload, preferring matching `GlobalDataVault` buffers where available.
Rejected Alternatives: `Physics.Raycast`, `Terrain.SampleHeight` per segment, and managed MapMagic queries inside the hot job were rejected because they violate the prompt, add sync cost, or allocate/dispatch through managed systems.
Scalability potential: Low disables SDF and accepts slight clipping; Middle uses height fallback; High uses SDF pushout; Ultra can raise segment count/iterations and shader deformation detail without changing contracts.
Hardware Impact: Estimated MX350 savings versus five PhysX casts are 35-120 us/frame; SDF byte trilinear cost is bounded to five tail segments on high tier only.

## Decision 4: GPU Presentation Contract

Problem: The Alpha Leviathan path must not depend on `SkinnedMeshRenderer`; bones must be visible to the project's GPU deformation/BRG-style render path.
Solution: Keep `NativeArray<float4x4> LeviathanBones` as the authoritative SOA output, upload it through `GraphicsBufferUploadUtility`, and bind `_H8LeviathanBones`, `_H8LeviathanBoneCount`, `_H8LeviathanIkTier`, and `_H8LeviathanTailWhip01` to material/global shader state.
Rejected Alternatives: CPU skinning, transform hierarchy writes, and per-frame managed matrix arrays were rejected because they add GC or main-thread deformation work.
Scalability potential: Low publishes eight useful matrices and pads the rest; Middle/High/Ultra use all 20 matrices, allowing shader-side visual overkill without changing CPU memory shape.
Hardware Impact: Estimated i3/MX350 gain versus CPU skinning is 150-600 us/frame depending mesh complexity; actual number remains unmeasured in this blocked compile state.

## Decision 5: Tail Strike Math

Problem: Strike needs an aggressive tail whip but must not become a real physical simulation or fight terrain constraints.
Solution: Inject a one-second lateral wave into the tail half and bypass terrain constraints for those segments while the strike timer is active. The current implementation uses a cheap triangle-wave sine-cheat for deterministic visual violence.
Rejected Alternatives: True spring dynamics, collision sweeps, and per-segment physics impulses were rejected because they buy realism instead of controllable predator staging. A full trigonometric sine was rejected for the current pass because the project mandate explicitly permits visual fakes for physical-looking effects.
Scalability potential: Low keeps the same authored silhouette with eight segments; High/Ultra get smoother wave falloff across more matrices and stronger shader deformation.
Hardware Impact: Estimated cost is under 4 us on MX350-class CPU for the tail half; bypassing terrain during strike avoids SDF probes for the same window.

## Decision 6: Verification State

Problem: Full project compile cannot be treated as clean evidence because unrelated assemblies are currently broken and multiple Unity instances are open.
Solution: Treat the post-polish Unity Roslyn response-file pass for `Hecton8.Animation.IK` as scoped evidence that the Burst IK asmdef syntax compiles, but mark the end-to-end Omega compile check as `[BLOCKED BY DEPENDENCY]` until the existing project compile wall clears. A continuation `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` probe timed out after 94 seconds.
Rejected Alternatives: Reporting a green build from stale/dotnet project files was rejected. Reverting this IK code was rejected because observed compile blockers are in unrelated files/assemblies, not this runtime.
Scalability potential: Verification block does not change runtime scalability. Low/Middle/High/Ultra behavior is encoded in the runtime and still needs in-editor execution proof.
Hardware Impact: No profiler-backed microsecond savings can be claimed. All microsecond values in this rationale are estimates pending Unity profiler capture.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat audit found the Burst job still exposed runtime gates as byte booleans and used a vector division in SDF sample-space conversion.
Solution: Replaced `EnableSdfHugging`, `EnableTerrainFallback`, and `LowTier` job fields with one `RuntimeFlags` bitmask using `(flags & MASK) != 0`. Replaced `(worldPosition - VoxelSdfOrigin) / safeCell` with reciprocal multiply via `math.rcp(safeCell)`.
Rejected Alternatives: Keeping byte gates was rejected because the polish mandate explicitly requires bitmask-style Burst gates. Keeping the division was rejected because reciprocal multiply is cheaper and deterministic enough for SDF address math.
Scalability potential: Low remains eight segments and SDF-off; Middle uses height fallback; High/Ultra use SDF with the cheaper gate path. No behavior contract changed.
Hardware Impact: Estimated MX350 gain is 0.3-1.0 us/frame in the job setup/contact branch region. This is a design estimate, not profiler evidence.

Exact cinematic cheats used:
- Tail strike uses `CheapSinSigned`, a deterministic triangle-wave sine-cheat, instead of a real physical tail impulse.
- Low tier clamps to eight spine matrices and disables SDF terrain hugging, allowing small visual clipping to buy frame time.
- MapMagic quantized height fallback is a 2D seabed cheat when 3D SDF contact is unavailable.
- GPU matrix deformation publishes 20 procedural matrices instead of CPU skinning or transform hierarchy solving.

Final Git Diff:
- `Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs`: runtime flags bitmask added; byte gates removed; SDF coordinate division converted to `math.rcp` multiply.
- `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs`: `ResolveRuntimeFlags()` added and passed into the Burst job.
- `Docs/Tasks/Status_LEVIATHAN_KINEMATICS_SOLVER.md`: 15 tasks recorded across five loops; task 15 marked dependency-blocked.
- `Docs/AgentLogs/Rationale_LEVIATHAN_KINEMATICS_SOLVER.md`: decisions and Omega polish entry recorded.
- `Docs/AgentLogs/LOG_LEVIATHAN_KINEMATICS_SOLVER.md`: final report appended/created for CTO log consumption.

Scoped compile evidence:
- Command: Unity 6000.4.1f1 Roslyn `csc.dll` with `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Animation.IK.rsp` and `.rsp2`.
- Result: exit 0 after Omega polish.
- Boundary: validates the isolated IK job assembly only; it does not validate `FaunaKinematicsRuntime` inside the currently red `Hecton8.Core` assembly.

## Decision 7: Organic Shader Consumption

Problem: The runtime published `_H8LeviathanBones`, but `Hecton_LeviathanOrganic.shader` did not consume those matrices. That left Task 10 as a contract publication, not visible GPU deformation, and hidden shader `Properties` would have risked material defaults shadowing global runtime uniforms.
Solution: Add shader-side GPU matrix deformation for forward and shadow vertices, sample two bone matrices by body Z, blend centers/axes, blend normals/tangents, and apply a bounded tail-whip triangle-wave layer from `_H8LeviathanTailWhip01`. Runtime now publishes `_H8LeviathanSegmentLength` and `_H8LeviathanGpuSkinning`, reuses upload scalars, and clears the shader gate on disable/dispose.
Rejected Alternatives: CPU mesh deformation was rejected because it violates the GPU-driven presentation mandate. Keeping hidden material properties was rejected because serialized defaults can override globals. Publishing `_H8LeviathanBodyRadius` to the shader was rejected as dead contract surface; the mesh authored radius is preserved by local vertex offsets.
Scalability potential: Low keeps eight matrices and a 0.08 m max shader whip layer; Middle/High/Ultra can use 20 matrices and a 0.18 m max visual whip layer without increasing CPU segment count. Cheap devices get bounded deformation; top-tier devices get stronger visible strike staging from the same matrix buffer.
Hardware Impact: Expected MX350 gain versus CPU deformation remains 150-600 us/frame depending mesh density, unmeasured. Removing duplicate shadow-pass skinning saves one extra matrix blend per shadow vertex, roughly 5-25 us on dense shadow casters, unmeasured.

Additional cinematic cheat:
- Shader tail whip uses the existing cheap triangle-wave helper over the already-solved bone path instead of simulating extra physics or per-vertex springs.

## Decision 8: Lifecycle Fence And Tail Duration

Problem: `OnDisable` could unregister the runtime while a scheduled IK job still owned `_segmentPositions`, `_previousSegmentPositions`, and `_leviathanBones`; a later `OnEnable` or `BindFromFauna` could reseed those arrays before the job completed. The Burst tail whip also used a hard-coded one-second duration despite runtime exposing `_tailWhipDurationSeconds`.
Solution: Add `CompleteScheduledSolverForLifecycle()` and call it before disable clear, re-enable reseed, and rebind reseed. Pass `TailWhipDurationSeconds` into `LeviathanTerrainIkJob` and normalize the cheap wave from that field.
Rejected Alternatives: Leaving the job to finish asynchronously was rejected because native array reseed would race a writer job. Adding a second job-state buffer was rejected as overbuilt for a single Alpha Leviathan owner. Keeping the hard-coded duration was rejected because serialized tuning would lie to the solver.
Scalability potential: Low/MX350 still runs eight segments and one constraint iteration; high/ultra keep 20 segments. Lifecycle completion is only on enable/disable/rebind, not the steady hot path, so it does not affect per-frame scalability.
Hardware Impact: Hot-path cost is 0 us. Lifecycle stalls are bounded to one already-scheduled 8-20 segment job and occur only during teardown/rebind. Prevented race cost is crash avoidance, not frame-time gain.

Scoped compile evidence:
- Command: Unity 6000.4.1f1 Roslyn `csc.dll` with `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Animation.IK.rsp` and `.rsp2`.
- Result: exit 0 after adding `TailWhipDurationSeconds`.
- Boundary: validates `Hecton8.Animation.IK` only; `FaunaKinematicsRuntime` remains blocked behind the red `Hecton8.Core` assembly.

## Decision 9: H-Phi Domain Hygiene Without Dotnet Rebuild

Problem: Current batch no longer contains the `LEVIATHAN_KINEMATICS_SOLVER` prompt, and the live status/rationale ended at Loop 8 while current source already contained later ownership hardening. `FaunaBrain.EnsureLeviathanPresentationOwner()` still had one concrete integration risk: if `_faunaKinematicsRuntime` was null but the component already existed, the method went straight to `AddComponent<FaunaKinematicsRuntime>()`.
Solution: Treat disk source as authority, recheck the current IK runtime/job/shader scope, and add a cold `TryGetComponent(out _faunaKinematicsRuntime)` before adding the presentation owner. Record scoped H-Phi counters instead of claiming a global score.
Rejected Alternatives: Running `dotnet build`/rebuild or a Roslyn response-file compile was rejected by explicit user instruction. Editing the global H-Phi report without a completed H-Phi audit was rejected as fake evidence. Leaving `AddComponent` as the only recovery path was rejected because `[DisallowMultipleComponent]` makes duplicate owner creation an avoidable integration fault.
Scalability potential: Low/MX350 gains no hot-path cost; the fix runs in cold presentation binding only. High/Ultra preserve the same GPU-driven Leviathan path. Local H-Phi hygiene improves by reducing component-ownership ambiguity and keeping hot tier reads cached in `FaunaKinematicsRuntime`.
Hardware Impact: Hot-path cost is 0 us. Cold-path added cost is one `TryGetComponent` only when `_faunaKinematicsRuntime` is null. Avoided failure is duplicate component/add failure on Alpha Leviathan presentation binding, not a measured frame-time gain.

Scoped H-Phi evidence:
- `FaunaKinematicsRuntime` counters: `GlobalRegistryRefs=11`, `ScalabilityTierRefs=2`, `NativeArrays=22`, `SignalBusRefs=0`, `UnityUpdateMethods=0`, `FindCalls=0`, `GetComponentCalls=3`.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code exits 0 with LF-to-CRLF warning only.
- Boundary: no runtime H-Phi or global H-Phi score is claimed; Unity Editor import, play mode, profiler, and GC evidence remain pending.

## Decision 10: Blackbox Dump Format Integrity

Problem: `DumpTelemetryBlackBox()` advertised `TelemetryEntryPayloadBytes = 96`, but the manual `BinaryWriter` path only wrote 68 bytes of explicit fields per entry. It also wrote the circular ring in physical index order, not oldest-to-newest order, making postmortem reconstruction slower and easier to misread.
Solution: Keep the 96-byte payload contract and write seven explicit zero padding floats per entry. Resolve the dump range from the current cursor and serialize entries chronologically from the oldest retained frame to the newest.
Rejected Alternatives: Reducing the header payload size to 68 was rejected because the telemetry struct is explicitly `[StructLayout(... Size = 96)]` and downstream tooling should be able to trust the fixed-size contract. Leaving physical ring order was rejected because the blackbox requirement is last-frame history, not array-storage order.
Scalability potential: Runtime Low/MX350/Ultra paths are unchanged. The dump is fault-path only and improves postmortem evidence quality without changing steady simulation cost.
Hardware Impact: Hot-path cost is 0 us. Fault-path dump writes the same entry count plus 28 padding bytes per entry, roughly 8.4 KB extra for 300 frames, negligible versus crash-dump usefulness and not a frame-time path.

## Decision 11: Telemetry Cursor Long-Uptime Wrap

Problem: `LeviathanTerrainIkJob.WriteTelemetry()` reset `TelemetryCursor[0]` to zero after `int.MaxValue`. That branch is extreme-uptime only, but after it fires the next runtime-side `TelemetryHasInvalidFrame()` reads the physical last slot instead of the just-written slot, and `DumpTelemetryBlackBox()` can report zero retained entries.
Solution: Preserve ring semantics on overflow by writing `TelemetryRing.Length + nextIndex`, keeping the cursor saturated above full-ring count while preserving the next write index modulo ring length.
Rejected Alternatives: Leaving reset-to-zero was rejected because blackbox evidence must be trustworthy even in rare fault conditions. Adding a second native counter was rejected as unnecessary memory and write traffic for a single retained-ring state.
Scalability potential: Low/MX350/Ultra runtime behavior is unchanged. This is one cold overflow branch in the telemetry write path and does not change segment count, SDF, or GPU presentation tiers.
Hardware Impact: Hot-path cost is effectively 0 us; the branch only matters after about 2.1 billion telemetry writes. It prevents postmortem data loss instead of saving frame time.

## Decision 12: SDF Sampler Hoist

Problem: High-tier SDF contact called the trilinear sampler once for density and six more times for central-difference gradient. Each private sample repeated voxel-count validation and cell-size reciprocal setup even though the job had already proven the SDF payload valid.
Solution: Keep the outer `canUseSdf` payload gate as the validation authority, compute `sdfInvCellSize` once per job execution, and pass it into density/gradient samples.
Rejected Alternatives: Reducing gradient samples or replacing central differences with a cheaper 2D height normal was rejected because high/ultra tiers spend saved CPU on contact quality. Leaving repeated validation in the private sampler was rejected because it burns work inside the lower-five-segment terrain loop.
Scalability potential: Low/MX350 remains unchanged because SDF is disabled. Middle/high/ultra keep the same visual contact result while reducing repeated scalar setup; ultra can spend the saved cycles on denser visual deformation without changing this API.
Hardware Impact: Estimated gain is 0.5-2 us on high-tier SDF-contact frames, depending how many lower segments penetrate rock. No profiler-backed number is claimed.

## Decision 13: Fallback Height Sample Count Guard

Problem: The MapMagic fallback path multiplied `resolution * resolution` in `int` space before accepting and sampling `TerrainHeightSamples`. A malformed or cross-system payload could overflow the count and let an invalid native height buffer reach the Burst terrain-contact loop.
Solution: Add `TryResolveTerrainHeightSampleCount()` with `long` multiplication and use it in both runtime payload acceptance and Burst job/sample gates.
Rejected Alternatives: Trusting `QuantizedHeightmapPayload.IsValid` was rejected because that property currently performs the same multiplication in `int` space. Editing the MapMagic contract was rejected as outside this agent's domain for this pass.
Scalability potential: Low/MX350 and high/ultra visuals are unchanged. The guard keeps the cheap 2D fallback safe without adding allocations or changing SDF behavior.
Hardware Impact: Normal payload cost is effectively 0 us; the extra checked multiply is cold relative to the existing MapMagic query and prevents invalid native indexing on bad payloads.

## Decision 14: Global GPU Skinning Gate Ownership

Problem: Runtime global shader publication used `_publishGlobalBoneBuffer` as both desired state and cleanup gate. If a buffer had already been published and the runtime later switched to material-only binding or no GPU consumer, global `_H8LeviathanGpuSkinning` could remain enabled with stale bone data.
Solution: Track `_globalGpuSkinningPublished` separately from the serialized publish intent and clear global shader gate floats whenever publishing is disabled after a prior publish, or during shutdown.
Rejected Alternatives: Clearing globals every upload was rejected as unnecessary render-state traffic. Leaving cleanup gated only by `_publishGlobalBoneBuffer` was rejected because it cannot represent previously-published global state after runtime/inspector changes.
Scalability potential: Low/MX350/high/ultra visual tiers are unchanged. The fix improves global state hygiene for material-only or no-consumer configurations without touching the Burst solver.
Hardware Impact: Stable hot-path cost is 0 us. Publish-off transition adds five `Shader.SetGlobalFloat` calls once; this prevents stale global GPU deformation rather than saving frame time.

## Decision 15: Lifecycle Reseed Dirtiness And Telemetry

Problem: `SeedSpineFromOwner()` rewrote the authoritative native bone matrices but did not explicitly mark GPU upload dirty, leaving correctness dependent on the next solver completion. Forced lifecycle completion also skipped the same invalid-telemetry check used by normal late-frame completion.
Solution: Mark `_gpuUploadDirty` and reset `_motionIntentFrame` after reseed. After lifecycle force-complete, inspect the latest telemetry entry and dump the blackbox if invalid.
Rejected Alternatives: Waiting for the next scheduled solver tick was rejected because other presentation systems can query native/GPU buffers immediately after bind/enable. Ignoring telemetry on lifecycle force-complete was rejected because shutdown/rebind is still a possible NaN boundary.
Scalability potential: Low/MX350/high/ultra steady-state simulation is unchanged. The fix is cold lifecycle hygiene and keeps GPU-driven presentation coherent across enable/rebind.
Hardware Impact: Hot-path cost is 0 us. Lifecycle adds one telemetry flag read only when force-completing a scheduled job; reseed already loops all 20 matrices, so dirty marking is free.

## Decision 16: Shader Scalar Sanitization

Problem: GPU deformation received `_H8LeviathanSegmentLength` and tail-whip normalization from serialized floats. `[Range]` protects inspector authoring, not runtime/programmatic NaN or zero-duration assignments.
Solution: Add `SanitizePositiveFinite()` and publish safe segment length and safe tail-whip duration-derived intensity to material/global shader state.
Rejected Alternatives: Relying on shader-side `max()` alone was rejected because NaN can propagate differently across graphics backends. Clamping only in the Burst job was rejected because shader globals are a separate contract.
Scalability potential: Low/MX350/high/ultra visuals are unchanged for valid data. The fix protects all tiers from malformed tuning without adding allocations or changing GPU buffer layout.
Hardware Impact: Upload path adds two finite checks and two clamps on frames where bones are uploaded; estimated under 0.1 us and not profiler-backed.

## Decision 17: Burst Scalar Boundary Sanitization

Problem: The Burst job clamped several scalar inputs with `math.max`/`math.clamp`, but those helpers are not a complete contract against NaN data entering from serialized/runtime fields. A bad scalar could still contaminate segment positions or matrices before shader upload sanitization.
Solution: Add Burst-compatible finite scalar sanitizers and consume sanitized damping, segment length, radius, clearance, and tail-whip values through the solver, tail-whip, and telemetry paths.
Rejected Alternatives: Sanitizing only at the MonoBehaviour upload boundary was rejected because the native solver owns the authoritative matrices. Removing runtime tunability was rejected because high/ultra visual overkill needs authored ranges.
Scalability potential: Low/MX350/high/ultra behavior is unchanged for valid data. Invalid data now collapses to conservative defaults instead of breaking the IK ring or GPU deformation.
Hardware Impact: Estimated cost is under 0.2 us per scheduled solver; scalar checks buy deterministic failure containment, not frame-time savings.

## Decision 18: Terrain Payload Finite Gate

Problem: The terrain contact path validated buffer lengths but still trusted SDF origin/cell/range and terrain origin/size values. A malformed producer could feed finite-length buffers with NaN transform metadata, contaminating density samples and matrix output.
Solution: Gate SDF and height fallback on finite metadata. Sanitize SDF cell size/range once and pass those resolved values into trilinear sampling, gradient sampling, and SDF decode.
Rejected Alternatives: Trusting upstream voxel/MapMagic producers was rejected because IK is the last native writer before GPU deformation. Disabling SDF on all questionable frames by broad exception handling was rejected because Burst has no managed exception path and predictable branch gates are cheaper.
Scalability potential: Low/MX350 still disables SDF. High/ultra retain full SDF quality on valid data and fail closed to height fallback/no terrain push on malformed metadata.
Hardware Impact: Estimated cost is under 0.3 us on terrain-contact solver frames; prevents NaN matrix writes rather than saving frame time.

## Decision 19: Terrain Segment Pre-Sample Sanitize

Problem: The terrain loop sampled `SegmentPositions[index]` directly. Earlier solver phases sanitize most writes, but terrain contact is a native indexing boundary and should not depend on prior phase success.
Solution: Sanitize the segment position in-place with a parent-derived fallback before SDF or height sampling.
Rejected Alternatives: Adding checks inside only the SDF sampler was rejected because height fallback also consumes `position.xz`. Re-running a full constraint pass before terrain was rejected as unnecessary cost.
Scalability potential: All tiers preserve behavior for valid data. Low/MX350 pays only the cheap finite check on fallback-contact segments; high/ultra protect SDF indexing.
Hardware Impact: Estimated cost is under 0.1 us for the terrain-hug segment set; it prevents invalid native indexing and NaN matrix propagation.

## Decision 20: Dead Phase Job Payload

Problem: `FaunaKinematicsRuntime` maintained `_solverTimeSeconds` only to assign `PhaseTimeSeconds` into `LeviathanTerrainIkJob`, but the Burst solver did not consume that field.
Solution: Remove the runtime accumulator, wrap logic, and job scalar field so the scheduled payload matches the actual solver contract.
Rejected Alternatives: Keeping the field as future reserve was rejected because unused time state becomes a false dependency and can drift from authored timing fields already used by tail whip.
Scalability potential: All tiers preserve behavior. Low/MX350 carry one less scalar through scheduling; high/ultra keep their terrain/SDF/whip behavior unchanged.
Hardware Impact: Estimated gain is 0.01-0.05 us per scheduled solver from removing one accumulator write and one job scalar copy. The more important effect is lower state surface, not measurable frame time.

## Decision 21: Runtime Finite Boundary Hygiene

Problem: Several Mono-side runtime paths still trusted caller or serialized floats before the Burst job could sanitize them: delta-time, seed segment scale, fallback intent distance, strike target state, tail-whip duration, attack telegraph, and AUP matrix translation.
Solution: Reject non-finite delta-time before scheduling, sanitize seed/fallback lengths, sanitize strike target and tail duration values, clamp NaN telegraph to zero, sanitize origin-shift target vectors and matrix translation, and remove the now-unused `_strikeRange` state.
Rejected Alternatives: Relying only on Burst-side clamps was rejected because seeding, origin-shift rebasing, and GPU upload can run outside the scheduled solver. Keeping `_strikeRange` as a future hook was rejected because the current GPU-driven path does not consume it.
Scalability potential: Low/MX350/high/ultra behavior is unchanged for valid data. Invalid authoring/caller data now fails closed before native matrices or shader buffers are touched.
Hardware Impact: Estimated normal scheduling cost is under 0.2 us for scalar finite gates; seed/shift sanitization is cold-path only. No profiler-backed frame-time claim.

## Decision 22: Origin-Shift Completion Parity And SDF Gradient Scale

Problem: `OnOriginShift` could finalize an already-completed solver job before `LateFrameTick`, but did not advance frame index or inspect invalid telemetry. The SDF central-difference normal also used raw density deltas, biasing contact normals when voxel cell steps are anisotropic.
Solution: Mirror the late-frame completion bookkeeping inside the origin-shift finalize branch, then scale SDF gradient components by reciprocal axis step before normalization.
Rejected Alternatives: Waiting for LateFrame was rejected because the origin-shift branch explicitly consumes the completed job. Leaving raw SDF deltas was rejected because high/ultra contact quality should not depend on cubic voxel cells.
Scalability potential: Low/MX350 does not run SDF, so cost is unchanged. High/ultra get more stable terrain hugging on non-uniform SDF volumes; origin-shift bookkeeping is cold-path only for all tiers.
Hardware Impact: Normal late-frame cost is 0 us. SDF gradient scaling adds three reciprocal/multiply components only on high-tier SDF contact frames, estimated under 0.1 us and not profiler-backed.

## Decision 23: Dispatcher Registration Repair

Problem: `TryRegister()` returned immediately when `_registeredUpdate` was true, even if `_registeredLateFrame` was false. A partial registration state would stop late-frame solver completion and GPU upload from being repaired on a later enable/rebind.
Solution: Treat only the fully registered pair as stable. If exactly one registration flag is set, unregister both paths and retry registration from a clean state.
Rejected Alternatives: Trusting the original flags was rejected because lifecycle/event systems can be interrupted by parallel integration work, and this runtime needs both update and late-frame callbacks to stay coherent.
Scalability potential: Low/MX350/high/ultra runtime behavior is unchanged. The fix protects cold lifecycle wiring without adding hot-path cost.
Hardware Impact: Hot-path cost is 0 us. Cold registration may perform one unregister pair before retrying only when state is already partial.

## Decision 24: Strike Contract And Native Bone Read Access

Problem: The new Burst/GPU Leviathan presentation no longer consumes strike range, but `FaunaBrain` still computed and passed it into `FaunaKinematicsRuntime`. The runtime also kept unused owner fields and exposed mutable native bone matrices to potential external readers. Segment-length fallback differed between CPU seed/Burst paths and GPU upload.
Solution: Remove the dead strike range parameter and call-site calculations, remove unused runtime owner fields, align segment-length upload fallback to 2.5 m, and return `NativeArray<float4x4>.ReadOnly` from the bone accessor.
Rejected Alternatives: Keeping dead API surface for hypothetical future behavior was rejected because current contracts should describe current data flow. Returning mutable `NativeArray` was rejected because native matrix ownership belongs to the solver.
Scalability potential: All tiers preserve valid behavior. Low/MX350 avoids dead call-site work; high/ultra keep identical GPU deformation with safer fallback consistency.
Hardware Impact: Hot-path frame savings are not claimed. Strike updates remove one unnecessary range calculation in `FaunaBrain`; the read-only API reduces future corruption risk rather than measurable frame time.

## Decision 25: Terrain Bounds And First Upload LOD

Problem: `_activeSegmentCount` still defaulted to 20 until the first Tick resolved quality, so an enable/rebind upload could expose a 20-bone buffer while `Unknown/Low/MX350` policy says eight. The MapMagic fallback also clamped out-of-tile sample positions to terrain edges, which can push lower segments against the wrong border height instead of failing closed.
Solution: Default active segments to `LowTierSegments`, resolve active segment count during reset before first upload, centralize segment-length fallback constants, reject malformed terrain metadata in the runtime, and reject non-finite/out-of-tile XZ samples inside the Burst height sampler.
Rejected Alternatives: Leaving first upload to be corrected by the first solver Tick was rejected because GPU consumers may query immediately after enable/rebind. Keeping terrain-edge clamp was rejected because it hides seams by applying unrelated heights to the tail. Editing `MapMagicBridge.QuantizedHeightmapPayload.IsValid` was rejected as outside this agent's domain for this pass.
Scalability potential: Low/MX350 now publish the cheap eight-bone contract immediately; Middle/High/Ultra keep 20-bone visual overkill after quality resolution. Low - eight matrices and height fallback only. Middle - in-tile 2D height fallback. High - SDF contact first, height fallback only when valid. Ultra - same correctness with full shader deformation headroom.
Hardware Impact: No measured frame-time saving is claimed. The change prevents a false first-frame 20-matrix low-tier upload and wrong edge-height pushes; added in-bounds checks are scalar and estimated below 0.05 us on terrain-contact frames, pending profiler proof.

## Decision 26: GPU Buffer Data Freshness

Problem: `TryGetLeviathanBoneGraphicsBuffer()` only checked `GraphicsBuffer.IsValid()`. A buffer can remain allocated after solver data becomes dirty through reseed/rebase, or after publishing is disabled and uploads are skipped, so external consumers could bind stale Leviathan bones.
Solution: Track `_gpuBufferDataValid` separately from buffer allocation. Set it true only after `GraphicsBufferUploadUtility.UploadNativeArray()` completes, and clear it on seed, rebase, disposal, graphics-buffer release, skinning clear, no-consumer upload skip, and material/global unbind paths.
Rejected Alternatives: Forcing upload inside the getter was rejected because a query method must not allocate GPU bandwidth or create hidden main-thread render work. Relying on `_gpuUploadDirty` alone was rejected because skipped uploads can clear dirty state without creating fresh buffer data.
Scalability potential: Low/MX350/high/ultra visuals are unchanged when data is fresh. All tiers fail closed instead of showing stale deformations when the GPU binding contract is not current; high/ultra keep full visual overkill only with proven fresh upload state.
Hardware Impact: Hot path cost is one boolean gate in an internal accessor. No frame-time savings are claimed; the gain is correctness and avoiding stale GPU deformation on weak and high-end devices alike.

## Decision 27: Lifecycle Completion Frame Parity

Problem: `CompleteScheduledSolverForLifecycle()` force-completed a scheduled solver and inspected telemetry, but did not advance `_frameIndex`. Normal late-frame completion and origin-shift finalization already advanced it, so disable/re-enable/rebind could consume a solver and leave the next telemetry entry with a duplicate runtime frame index.
Solution: Add `AdvanceFrameIndex()` and use it from normal late-frame completion, origin-shift finalization, and forced lifecycle completion.
Rejected Alternatives: Leaving lifecycle completion as a teardown-only exception was rejected because rebind/re-enable can immediately schedule another solver and blackbox frame order must remain coherent. Resetting the index on lifecycle was rejected because chronological continuity is more useful than lifecycle-local numbering.
Scalability potential: Low/MX350/high/ultra runtime visuals are unchanged. All tiers get cleaner blackbox ordering around lifecycle edges without changing segment count, SDF, or GPU deformation.
Hardware Impact: Steady hot-path cost is unchanged; the helper replaces duplicated scalar code in normal paths and adds one lifecycle-only scalar increment when a scheduled job is force-consumed.

## Decision 28: GPU Buffer Getter Fail-Closed Contract

Problem: `TryGetLeviathanBoneGraphicsBuffer()` could return `false` while leaving `buffer` set to the last completed graphics buffer and `activeSegmentCount` set to a nonzero value. Correct callers should honor the boolean, but a defensive GPU contract should not leak stale render state through failed out parameters.
Solution: Resolve a local candidate buffer, publish it only inside the fresh-upload success branch, and clear both out parameters on every failure path.
Rejected Alternatives: Keeping the previous contract was rejected because parallel consumers can make mistakes and the getter is the boundary that knows whether data is fresh. Forcing an upload from the getter was still rejected because queries must not create hidden GPU bandwidth or main-thread render work.
Scalability potential: Low/MX350/high/ultra visuals are unchanged when uploads are fresh. All tiers now fail closed on stale data, preserving the cheap low-tier path and high-tier visual overkill only when the buffer freshness contract is proven.
Hardware Impact: Hot-path frame-time savings are not claimed. The change adds no allocation and only reuses the existing branch; it prevents stale GPU deformation from invalid query states.

## Decision 29: Legacy Animator Spine Path Removal

Problem: `ProceduralLeviathanSpineIK` remained in the fauna domain as an older transform-chain presentation owner with `Animator`, `SkinnedMeshRenderer`, managed scratch lists, and a stale four-argument strike API. The current Alpha Leviathan integration uses `FaunaKinematicsRuntime`, and scans showed no active references to the legacy MonoScript.
Solution: Delete the unused legacy `.cs` and matching `.meta` together after verifying no type-name references and no GUID references in code, prefabs, scenes, assets, materials, controllers, packages, or project settings.
Rejected Alternatives: Leaving the file as "unused" was rejected because dead components with forbidden dependencies keep architectural drift alive and can be rebound accidentally by parallel work. Porting the legacy class to the new API was rejected because `FaunaKinematicsRuntime` is already the domain owner.
Scalability potential: Low/MX350/high/ultra runtime behavior is unchanged through the active path. The cleanup removes a fallback route that could reintroduce CPU skinning/transform writeback instead of the eight-to-twenty matrix GPU deformation contract.
Hardware Impact: No runtime microsecond savings are claimed because no references were found. The hardware gain is risk removal: no accidental Animator/SkinnedMeshRenderer presentation path for Leviathan on i3/MX350-class hardware.

## Decision 30: Motion Intent Freshness Without Unity Frame Globals

Problem: `FaunaKinematicsRuntime` used `Time.frameCount` to decide whether `FaunaBrain` had supplied motion intent for the current frame. That creates a subtle ordering dependency between dispatcher phases and Unity's rendered-frame counter: intent published after the IK runtime tick can be overwritten by fallback velocity on the next solver tick.
Solution: Replace the integer frame marker with `_motionIntentPending`. `SetMotionIntent()` marks pending intent, `CaptureFallbackMotionIntent()` consumes it once, and `SeedSpineFromOwner()` clears it during lifecycle reseed.
Rejected Alternatives: Passing frame indices through `SetMotionIntent()` was rejected because it widens the internal call contract and still couples the runtime to external frame bookkeeping. Keeping `Time.frameCount` was rejected because dispatcher cadence, not Unity frame equality, is the authoritative update boundary.
Scalability potential: Low/MX350/high/ultra behavior is unchanged for valid ordering. All tiers now tolerate intent publication on either side of the runtime tick without losing the next solver input; high/ultra keep smoother body pursuit because authored intent is not discarded by frame-count drift.
Hardware Impact: No profiler-backed saving is claimed. The change replaces one `int` field with one `bool` and removes two Unity frame-global reads from the IK runtime hot/caller path.

## Decision 31: Tentacle Dispatcher Registration Repair

Problem: `LeviathanTentacleVerletSolver.TryRegister()` returned when `_registeredUpdate` was true even if `_registeredLateFrame` was false. That can strand a scheduled Burst Verlet solve because late-frame owns completion, blackbox telemetry, and indirect render submission.
Solution: Treat only the full update+late-frame pair as stable. If either registration flag is set alone, unregister both dispatcher paths, clear both flags, and retry from a clean state.
Rejected Alternatives: Trusting the update registration flag was rejected because a partial lifecycle state can keep simulation ticking while completion/upload is absent. Adding a second callback fallback was rejected because it duplicates dispatcher ownership instead of repairing the broken registration invariant.
Scalability potential: Low/MX350/high/ultra motion quality is unchanged for normal registration. All tiers now fail back into a coherent dispatcher pair after lifecycle/rebind disturbance, preserving cheap low-tier tentacle simulation and high-tier indirect visual overkill.
Hardware Impact: Hot-path cost is 0 us. Cold enable/rebind can pay one unregister pair only when the state is already partial; this protects job completion and render submission rather than claiming frame-time savings.

## Decision 32: Tentacle Native Memory Ownership

Problem: `LeviathanTentacleVerletSolver` allocated thirteen persistent SOA/blackbox `NativeArray<T>` buffers directly, outside the H8Memory owner/cap path used by the current Leviathan spine runtime. If allocation failed or was interrupted, the old code could also leave `_positions` created while other lanes were absent.
Solution: Add `Hecton8.Core.Memory`, allocate every persistent tentacle array through `H8Memory.Allocate<T>(..., SystemID.External, ...)`, release through `H8Memory.Release`, retain NativeMemorySentinel labels, and require `HasPersistentBuffers()` before scheduling.
Rejected Alternatives: Keeping direct `NativeArray<T>` allocations was rejected because H-Phi memory policy requires explicit owner tracking. Dropping `NativeMemorySentinel` registration was rejected because scene-lifetime leak telemetry is still useful for the tentacle blackbox and indirect-render buffers.
Scalability potential: Low/MX350/high/ultra visual behavior is unchanged when memory is available. Under memory pressure, all tiers now fail closed by refusing to schedule without a complete buffer set, preserving predictable low-tier behavior and preventing high-tier partial-buffer crashes.
Hardware Impact: No runtime frame-time saving is claimed. Hot-path cost is a fixed `IsCreated` gate before scheduling; cold allocation is now visible to H8Memory caps, which matters most on i3/MX350-class memory budgets.

## Decision 33: Tentacle Completion Telemetry Parity

Problem: `LeviathanTentacleVerletSolver` wrote blackbox telemetry only on the normal late-frame render path. A scheduled job completed during disable, origin-shift finalization, queued origin-shift rebase, or forced lifecycle completion could be consumed without a telemetry entry.
Solution: Call `WriteTelemetryFrame()` after every scheduled-job completion/finalization path that consumes the solver result. Origin-shift paths rebase first, then record telemetry, while still skipping render submission on that rebase frame.
Rejected Alternatives: Rendering immediately from the origin-shift path was rejected because the barrier should not add GPU submit work. Leaving lifecycle completion invisible was rejected because the blackbox requirement is about explainable state, not only rendered frames.
Scalability potential: Low/MX350/high/ultra visuals are unchanged. All tiers now preserve coherent last-300-frame state across lifecycle and origin-shift edges, so invalid tentacle positions have a dump trail even when no render upload happens.
Hardware Impact: Normal render-frame cost is unchanged. Lifecycle/rebase completion writes one fixed-size telemetry entry, estimated below 0.02 us and not profiler-backed; no hot-path saving is claimed.

## Decision 34: Tentacle Runtime Scalar Boundary Hygiene

Problem: Several tentacle scalar fields relied on `OnValidate()` or Burst-side clamps: rest length, stretch length, damping, radii, flow gains, suction pulse, and grab damage. Runtime mutation or corrupted serialized state could still pass NaN/invalid values into job payloads, material state, or combat damage signals before the blackbox marked the frame invalid.
Solution: Add central default constants plus `SanitizeFiniteMinInput()` and `SanitizeFiniteRangeInput()`. Use them when scheduling the Burst job, seeding/resetting matrices, binding material radius references, and queuing grab damage; keep grab-damage scalar invalidation behind an actual target check.
Rejected Alternatives: Trusting `OnValidate()` was rejected because builds and scripts can bypass it. Sanitizing only inside the Burst job was rejected because material and combat paths are Mono-side consumers.
Scalability potential: Low/MX350/high/ultra visuals are unchanged for valid input. Invalid scalar data now fails into cheap predictable defaults on all tiers, preserving low-end stability and high-end visual overkill without NaN-driven shader/combat corruption.
Hardware Impact: Added fixed scalar guards are estimated below 0.05 us per scheduled tentacle frame and are not profiler-backed. The benefit is deterministic failure behavior, not measured frame-time savings.
