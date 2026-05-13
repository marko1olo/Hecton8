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
Solution: Treat the post-polish Unity Roslyn response-file pass for `Hecton8.Animation.IK` as scoped evidence that the Burst IK asmdef syntax compiles, but mark the end-to-end Omega compile check as `[BLOCKED BY DEPENDENCY]` until the existing project compile wall clears.
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
