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
