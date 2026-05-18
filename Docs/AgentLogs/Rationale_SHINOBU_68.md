# Rationale_SHINOBU_68

## 2026-05-19 Procedural Bone Lane Reassertion

Problem: `CURRENT_BATCH.md` contains duplicate `SHINOBU_68` XML blocks and disk memory was overwritten by the DRS lane. The active user request is procedural creature bone blending: Damped Harmonic Oscillator, flat `float4x4` output, direct `GraphicsBuffer`, GPU skinning, and low-quality secondary-bone shedding.
Solution: Treat `role="PROCEDURAL_BONE_MATRIX_BLENDER"` as authority for this pass. Keep edits inside `Assets/_Project/Scripts/Animation/FaunaProcedural` and restore SHINOBU_68 status/rationale to procedural animation.
Rejected Alternatives: Mixing DRS evidence into the animation report, editing render-scale code, or trusting chat memory over CLI-extracted XML.
Scalability potential: Low keeps primary spine/static secondary collapse; Middle restores selected secondary rows; High restores jaw/harmonics; Ultra spends saved CPU on shader/GPU visual overkill, not CPU skinning.
Hardware Impact: 0 us runtime; prevents wrong-domain compile churn.

## 2026-05-19 Solver Determinism And Phase Polish

Problem: `ProceduralBoneSolveJob` accepted `input.SimulationTime == 0` as authoritative. In fallback/mock rigs, the seeded input stayed zero and could freeze sine phase even while `_simulationTime` advanced.
Solution: Treat input simulation time as authoritative only when finite and greater than zero; otherwise use the runtime deterministic simulation clock passed into the job. The fallback path now moves without requiring Agent 61 to populate time on frame zero.
Rejected Alternatives: Keeping default zero as live authority, or reading Unity `Time` inside the job.
Scalability potential: Low/Middle/High/Ultra all keep deterministic phase progression without double precision or world-space AUP injection.
Hardware Impact: No extra containers; one scalar predicate in the job.

Problem: The previous `FastSin` was a low-order Taylor approximation over the wrapped `[-pi,pi]` domain. It is cheap but edge-biased and discontinuity-prone near the wrap boundary.
Solution: Replace it with a bounded parabolic sine approximation plus non-finite guard. It remains a deterministic Dear Lie trig fake and avoids calling `math.sin` in the hot spine loop.
Rejected Alternatives: Full transcendental sine per active bone, a managed LUT, or leaving NaN propagation possible for bad phases.
Scalability potential: Low gets cheap stable wave motion; Ultra keeps harmonic overtones on top of the same stable base.
Hardware Impact: Similar ALU class, less pathological edge error; no memory traffic increase.

## 2026-05-19 GPU Upload Bandwidth Gate

Problem: Runtime marked `_gpuUploadDirty` after every completed solve, forcing a whole `float4x4` `GraphicsBuffer` copy even when telemetry/matrix state did not change. That violates the bandwidth-discipline mandate.
Solution: Expand telemetry state hash to include matrix-affecting scalars: local simulation time, wave speed, amplitude, quality, active bone count, root position, computed count, and flags. Runtime now uploads matrices only when count, buffer validity, or state hash changes. Shader constants can be republished without remapping/copying matrix memory.
Rejected Alternatives: Blind upload every frame, `GraphicsBuffer.SetData`, or per-bone managed dirty lists.
Scalability potential: Low cadence and hidden/static rigs skip redundant copies; High/Ultra still upload every animated state change because the hash evolves with phase.
Hardware Impact: Saves one contiguous matrix upload on unchanged frames; exact microseconds require Unity profiler/Frame Debugger proof.

## 2026-05-19 NaN Guard And Jaw Nlerp Polish

Problem: Quaternion nlerp normalized the blended quaternion without an explicit finite/zero guard. Degenerate IK direction or future bad input could produce non-finite orientation.
Solution: Add finite and length-squared guard before `rsqrt`; fallback to sanitized source rotation when the blend degenerates.
Rejected Alternatives: Trusting `LookRotationSafe` alone or allowing the later matrix finite guard to clean up after corrupt local rotation.
Scalability potential: All tiers get same failure containment; lower tiers often skip jaw IK entirely via quality gate.
Hardware Impact: One guard only on jaw blend path, not on every secondary collapsed bone.

## 2026-05-19 GraphicsBuffer Cold Allocation Polish

Problem: Double-buffered `GraphicsBuffer` allocation was lazy in the first upload path. That keeps CPU skinning out, but first matrix publish could still pay managed/native graphics allocation cost in a gameplay frame.
Solution: Call `EnsureGraphicsBuffers()` after successful Vault setup in Awake, OnEnable, and DataVault hot-swap. LateFrame upload now normally performs only `LockBufferForWrite`, `MemCpy`, unlock, and shader binding.
Rejected Alternatives: Keeping first-upload lazy allocation or using `SetData`.
Scalability potential: Low-tier avoids a first visible hitch; High/Ultra keep the same double-buffered GPU upload path.
Hardware Impact: Moves allocation from first publish to lifecycle/cold path; exact hitch reduction requires Unity profiler proof.

## 2026-05-19 Verification Position

Problem: After polish, runtime compile evidence is stale, but the user explicitly forbids unnecessary builds and AGENTS forbids compile when CPU >50%.
Solution: Static forbidden scan passed after polish. CPU check reported 100%, so post-polish csc and full `dotnet build` were not launched.
Rejected Alternatives: Violating CPU/build gate for a scoped compile, or claiming stale compile as current proof.
Scalability potential: No runtime behavior change from this decision.
Hardware Impact: Developer machine protected under parallel-agent load.
