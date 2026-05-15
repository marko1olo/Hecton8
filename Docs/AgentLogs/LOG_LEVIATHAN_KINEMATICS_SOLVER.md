# LOG_LEVIATHAN_KINEMATICS_SOLVER

## 2026-05-14T03:05+04:00

Status: PENDING VERIFICATION. Isolated IK assembly compiled via Unity Roslyn response files after Omega polish (`Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Animation.IK.dll`). Full project compile remains blocked by unrelated assemblies and open Unity project state.

What was wrong:
- Alpha Leviathan visual presentation was still shaped by transform/Animator-era assumptions, causing body clipping through Voxel SDF and MapMagic seabed.
- Terrain contact could not use Unity Physics by prompt mandate.
- Presentation needed GPU/BRG-style matrix output, not CPU skinning.

What was done:
- Verified `FaunaKinematicsRuntime` as the Alpha presentation owner bound from `FaunaBrain`.
- Fed intended steering/body velocity into the IK runtime through `SetMotionIntent`.
- Verified `Hecton8.Animation.IK` asmdef against `Hecton8.Core.Contracts`.
- Implemented/verified `NativeArray<float4x4> LeviathanBones` with 20 persistent matrices.
- Implemented/verified Burst `LeviathanTerrainIkJob` using Verlet follower integration and `math.rsqrt` distance constraints.
- Implemented/verified SDF terrain pushout for lower five segments and MapMagic/DataVault height fallback.
- Implemented/verified double-buffered `GraphicsBuffer` upload and shader globals for GPU deformation.
- Implemented/verified one-second strike tail bypass and black-box telemetry dump path.
- Implemented Omega polish: runtime booleans converted to bitmask flags; SDF sample division converted to reciprocal multiply.
- Ran scoped Unity Roslyn csc pass for `Hecton8.Animation.IK`; exit code 0.

Cinematic cheats used:
- Tail strike uses deterministic triangle-wave `CheapSinSigned` as a sine-cheat instead of physical impulse simulation.
- Low tier clamps to eight spine segments and disables SDF hugging, accepting minor clipping to buy frame time.
- MapMagic quantized height fallback is a 2D seabed cheat when 3D SDF is absent.
- GPU matrix deformation replaces CPU skinning/transform chain solve.

Exact microseconds saved:
- Animator/SkinnedMeshRenderer CPU path avoided: estimated 150-600 us/frame, unmeasured.
- Five Unity Physics terrain casts avoided: estimated 35-120 us/frame, unmeasured.
- Low-tier SDF disable and eight-segment clamp: estimated 20-60 us/frame saved against high-tier path, unmeasured.
- Tail terrain bypass during strike: estimated 2-8 us/frame during the one-second strike window, unmeasured.
- Omega bitmask/reciprocal polish: estimated 0.3-1.0 us/frame, unmeasured.

Integrator notes:
- Do not treat this as green full-project compile. Current project wall is outside this task, with errors observed in unrelated assemblies/files including `HectonUnderwaterVisuals`, `PlayerKinematicsRuntime`, `GlobalDataVault`, and missing contract assets.
- `Hecton8.Animation.IK` validates in isolation after polish. `FaunaKinematicsRuntime` still needs the global `Hecton8.Core` compile wall to clear for authoritative validation.

## 2026-05-14T04:40+04:00

Status: PENDING VERIFICATION. This continuation rechecked the GPU presentation contract after the core 15-task pass. No green full-project compile is claimed.

What was wrong:
- The runtime published `_H8LeviathanBones`, but `Hecton_LeviathanOrganic.shader` did not deform vertices from the buffer.
- Hidden IK shader properties would risk material-local defaults overriding globals when `_publishGlobalBoneBuffer` is used.
- Shadow caster deformation briefly duplicated the GPU skinning math per vertex.
- Disable/dispose did not explicitly drop the shader skinning gate, so stale global state was possible after runtime teardown.

What was done:
- Added forward and shadow-pass GPU matrix deformation in `Hecton_LeviathanOrganic.shader`.
- Bound runtime-published `_H8LeviathanSegmentLength` and `_H8LeviathanGpuSkinning` without adding serialized material properties.
- Added shader-side normal/tangent blending so lighting follows the spine, not only position.
- Added a bounded `_H8LeviathanTailWhip01` visual layer using the existing cheap triangle wave.
- Removed dead `_H8LeviathanBodyRadius` shader/runtime publication.
- Added `ClearGpuSkinningBinding()` to zero material/global gate, bone count, and tail whip on disable/dispose.
- Removed duplicate shadow-pass skinning work by reusing one deformed world position for shadow bias and silhouette clipping.

Cinematic cheats used:
- Shader tail whip is a cheap triangle-wave visual overlay capped at 0.08 m low tier and 0.18 m high tier.
- Global/material GPU gate enables matrix deformation only when the runtime has uploaded a valid buffer.
- Low-tier CPU behavior still clamps to eight solved spine matrices.

Exact microseconds saved:
- CPU skinning/mesh deformation still avoided: estimated 150-600 us/frame, unmeasured.
- Duplicate shadow vertex deformation removed: estimated 5-25 us on dense shadow casters, unmeasured.
- Dead shader/runtime property removed: negligible frame cost; reduces contract surface and material override risk.
- Upload scalar reuse avoids one duplicate tier branch and reciprocal per frame: less than 1 us, unmeasured.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex.
- Static grep found no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, or `_H8LeviathanBodyRadius` in the IK runtime/job/shader scope.
- `git diff --check` exits 0. Repo-wide line-ending warnings remain unrelated.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` timed out after 94 seconds under the existing project compile wall.
- Full shader import, `Hecton8.Core`, and runtime validation remain blocked by the existing project compile wall.

## 2026-05-14T12:38+04:00

Status: PENDING VERIFICATION. Continued lifecycle and Burst-contract audit after the shader contract pass.

What was wrong:
- `OnDisable` could leave the IK job alive while unregistering the runtime; `OnEnable` or `BindFromFauna` could then reseed native arrays before the old writer job completed.
- The Burst tail whip normalized wave age against a hard-coded one-second duration instead of the serialized `_tailWhipDurationSeconds`.

What was done:
- Added `CompleteScheduledSolverForLifecycle()` and called it before lifecycle reseed/clear points.
- Wired `TailWhipDurationSeconds` from `FaunaKinematicsRuntime` into `LeviathanTerrainIkJob`.
- Re-ran isolated Unity Roslyn csc for `Hecton8.Animation.IK`; exit code 0.

Cinematic cheats used:
- Tail whip remains a deterministic triangle-wave visual impulse, now duration-correct.
- Lifecycle completion is a teardown/rebind fence only; the steady simulation path remains asynchronous.

Exact microseconds saved:
- Hot-path savings: 0 us. This is a correctness fence outside steady frame execution.
- Prevented failure cost: avoids native writer/read reseed race and possible crash/NaN dump.
- Tail duration wiring cost: 0 us meaningful; one extra float in the job payload.

Verification:
- Scoped `Hecton8.Animation.IK` Roslyn csc pass exits 0 after the job field change.
- Static grep found no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, or `_H8LeviathanBodyRadius` in IK runtime/job/shader scope.
- `git diff --check` on touched IK runtime/job/shader files exits 0; line-ending warnings only.
- Full Unity/Core validation remains blocked by the existing project compile wall.

## 2026-05-15T00:59+04:00

Status: PENDING VERIFICATION. Continued domain H-Phi and presentation-owner hygiene. No `dotnet build`, rebuild, or Roslyn response-file compile was run because the user explicitly prohibited dotnet rebuilds.

What was wrong:
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains `LEVIATHAN_KINEMATICS_SOLVER`; prompt re-extraction returned `Prompt block not found`.
- Current source already contained later ownership/GPU hardening, but live status/rationale still ended at Loop 8.
- `FaunaBrain.EnsureLeviathanPresentationOwner()` could call `AddComponent<FaunaKinematicsRuntime>()` without first recovering an already-existing component if the cached field was null.

What was done:
- Re-read AGENTS, the domain file, H-Phi atlas section, and relevant mandates.
- Rechecked current `FaunaKinematicsRuntime` source for native ownership gates, AUP dirty upload, no-consumer GPU upload skip, material gate clearing, deferred dispose chaining, and hot scalability-tier caching.
- Added a cold `TryGetComponent(out _faunaKinematicsRuntime)` before `AddComponent<FaunaKinematicsRuntime>()` in `FaunaBrain.EnsureLeviathanPresentationOwner()`.
- Recorded scoped H-Phi counters instead of claiming a global metric.

Cinematic cheats used:
- None added. Existing Leviathan GPU matrix deformation and triangle-wave tail whip remain the visual fake path.

Exact microseconds saved:
- Hot-path savings: 0 us from this code diff. The new `TryGetComponent` executes only when binding/recovering the presentation owner.
- Avoided failure cost: duplicate component/add failure or lost cached presentation owner on Alpha Leviathan path.
- Existing scoped H-Phi runtime reads remain bounded: `GlobalRegistry.ScalabilityTier` has two source references in `FaunaKinematicsRuntime`.

Verification:
- CLI prompt extraction from current batch returned `Prompt block not found`; unrelated current-batch prompts were ignored.
- Scoped H-Phi counters for `FaunaKinematicsRuntime`: `GlobalRegistryRefs=11`, `ScalabilityTierRefs=2`, `NativeArrays=22`, `SignalBusRefs=0`, `UnityUpdateMethods=0`, `FindCalls=0`, `GetComponentCalls=3`.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code exits 0; output is only the LF-to-CRLF warning on `FaunaBrain.cs`.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:06+04:00

Status: PENDING VERIFICATION. Continued blackbox dump integrity audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `DumpTelemetryBlackBox()` wrote a header saying each telemetry entry was 96 bytes, but the manual writer only emitted 68 bytes of explicit fields.
- The circular telemetry ring was dumped in physical array order, not chronological oldest-to-newest order.

What was done:
- Added explicit zero padding writes so each serialized entry matches `TelemetryEntryPayloadBytes = 96`.
- Resolved `ringLength`, `entryCount`, and `firstEntryIndex` from the telemetry cursor and writes retained entries oldest-to-newest.

Cinematic cheats used:
- None. This is fault-path evidence integrity, not presentation simulation.

Exact microseconds saved:
- Hot-path savings: 0 us. This path runs only on blackbox dump.
- Fault-path added bytes: 28 padding bytes per entry, 8.4 KB for 300 frames.
- Debug time saved: prevents parser desync and false postmortem ordering; no runtime profiler number claimed.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:09+04:00

Status: PENDING VERIFICATION. Continued telemetry long-uptime edge audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `LeviathanTerrainIkJob.WriteTelemetry()` reset the cursor to zero after `int.MaxValue`.
- After that wrap, runtime last-frame inspection could read the wrong slot and dump code could report an empty retained history despite a full ring.

What was done:
- Changed the overflow branch to preserve full-ring state and next write index with `TelemetryRing.Length + nextIndex`.
- Kept the existing single native cursor; no extra native allocation or second counter was added.

Cinematic cheats used:
- None. This is blackbox evidence integrity.

Exact microseconds saved:
- Hot-path savings: 0 us.
- Added cost: one branch only when the cursor reaches `int.MaxValue`, effectively outside normal frame budgets.
- Debug time saved: prevents false empty dumps and wrong last-frame checks after long uptime.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:13+04:00

Status: PENDING VERIFICATION. Continued Burst SDF hot-path audit. No `dotnet` rebuild/compile was run.

What was wrong:
- SDF trilinear sampling repeated voxel-count validation and cell-size reciprocal setup for the density sample and six gradient samples.
- This work only exists on high/ultra terrain-contact frames, but it sits directly inside the lower-five-segment hugging loop.

What was done:
- Hoisted `sdfInvCellSize` once per job execution after the outer SDF payload gate.
- Threaded the resolved reciprocal into density and gradient sampling.
- Kept full central-difference SDF normal quality; no downgrade to 2D normals or fewer samples.

Cinematic cheats used:
- None added. Low/MX350 already uses the existing 2D MapMagic fallback cheat when SDF is off.

Exact microseconds saved:
- Low/MX350: 0 us; SDF remains disabled.
- High/Ultra: estimated 0.5-2 us on SDF-contact frames by removing repeated validation/reciprocal setup.
- Measurement status: estimate only; no Unity profiler run was performed.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:25+04:00

Status: PENDING VERIFICATION. Continued MapMagic fallback safety audit. No `dotnet` rebuild/compile was run.

What was wrong:
- The fallback height path used `resolution * resolution` in `int` space before accepting or sampling `TerrainHeightSamples`.
- A malformed payload could overflow the count and let an invalid native buffer reach the Burst terrain-contact loop.

What was done:
- Added `LeviathanTerrainIkJob.TryResolveTerrainHeightSampleCount()` with `long` multiplication.
- Used that resolver in runtime payload acceptance, the Burst fallback gate, and private terrain-height sampling.
- Kept the existing MapMagic 2D height fallback behavior unchanged for valid payloads.

Cinematic cheats used:
- Existing 2D MapMagic height fallback remains the cheap seabed cheat when SDF is unavailable.

Exact microseconds saved:
- Normal valid terrain payload: 0 us meaningful change.
- Bad payload path: avoids invalid native indexing and corrupt terrain-contact output.
- No profiler number claimed.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:29+04:00

Status: PENDING VERIFICATION. Continued GPU shader-state ownership audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `_publishGlobalBoneBuffer` described desired publish behavior, not whether this runtime had already published global shader state.
- If global publishing was disabled after a successful publish, `_H8LeviathanGpuSkinning` could remain globally enabled with stale bone data.

What was done:
- Added `_globalGpuSkinningPublished` tracking.
- Clear global Leviathan shader gates on publish-off transition, shutdown, and any no-consumer path after prior global publication.
- Kept material-only binding intact.

Cinematic cheats used:
- None. This is GPU state hygiene for the existing matrix deformation path.

Exact microseconds saved:
- Stable hot path: 0 us.
- Transition cost: five `Shader.SetGlobalFloat` calls only when clearing stale global publication.
- Prevented fault: stale global deformation on material-only/no-consumer configurations.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T01:51+04:00

Status: PENDING VERIFICATION. Continued lifecycle/rebind audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `SeedSpineFromOwner()` rewrote native bone matrices without explicitly dirtying GPU upload state.
- Lifecycle force-complete did not run the normal invalid-telemetry blackbox check.

What was done:
- Reseed now sets `_gpuUploadDirty = true`.
- Reseed resets `_motionIntentFrame` so fallback intent can refresh on the next tick.
- Forced lifecycle completion now checks `TelemetryHasInvalidFrame()` and dumps the blackbox once if needed.

Cinematic cheats used:
- None. This is lifecycle integrity around the existing GPU matrix presentation.

Exact microseconds saved:
- Hot path: 0 us.
- Lifecycle cost: one telemetry flag read only on force-complete, plus free dirty-bit assignment after the existing 20-matrix reseed loop.
- Prevented fault: stale GPU bone upload or missed blackbox dump on bind/enable/shutdown boundaries.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings for docs.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T02:00+04:00

Status: PENDING VERIFICATION. Continued shader scalar contract audit. No `dotnet` rebuild/compile was run.

What was wrong:
- Shader deformation scalars were published from serialized floats without finite positive sanitization.
- Runtime code can assign NaN or invalid values despite inspector `[Range]` metadata.

What was done:
- Added `SanitizePositiveFinite()`.
- Published safe `_H8LeviathanSegmentLength`.
- Normalized `_H8LeviathanTailWhip01` using a safe positive tail-whip duration.

Cinematic cheats used:
- None. Existing GPU deformation and tail-whip visual fake remain unchanged for valid data.

Exact microseconds saved:
- No frame-time saving claimed.
- Upload path adds two finite/clamp checks, estimated under 0.1 us on upload frames.
- Prevented fault: NaN or zero-duration shader deformation state.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T02:15+04:00

Status: PENDING VERIFICATION. Continued Burst scalar boundary audit. No `dotnet` rebuild/compile was run.

What was wrong:
- The Burst job still consumed several raw scalar fields after basic `math.max`/`math.clamp` operations.
- NaN or malformed runtime tuning could contaminate positions, matrices, or telemetry before shader-side sanitization.

What was done:
- Added Burst-side finite scalar sanitizers.
- Sanitized damping, segment length, body radius, terrain clearance, tail-whip remaining time, tail-whip duration, and tail-whip amplitude before solver math.
- Passed sanitized tail-whip values into `ApplyTailWhip()` and telemetry.

Cinematic cheats used:
- Existing triangle-wave tail whip remains unchanged for valid data.

Exact microseconds saved:
- No frame-time saving claimed.
- Added scalar checks are estimated under 0.2 us per scheduled solver.
- Prevented fault: NaN propagation through native matrices and blackbox telemetry.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T02:24+04:00

Status: PENDING VERIFICATION. Continued terrain payload boundary audit. No `dotnet` rebuild/compile was run.

What was wrong:
- SDF and fallback height buffer length gates existed, but terrain transform metadata could still be non-finite.
- SDF decode and gradient sampling read raw range/cell fields after the outer payload gate.

What was done:
- Added finite metadata gates for SDF origin, terrain origin, and terrain size.
- Sanitized SDF cell size and range once per job execution.
- Passed sanitized SDF values through density, gradient, and decode paths.

Cinematic cheats used:
- Existing 2D height fallback remains the cheap non-SDF contact path.

Exact microseconds saved:
- No frame-time saving claimed.
- Added scalar/vector finite gates estimated under 0.3 us on terrain-contact solver frames.
- Prevented fault: NaN terrain metadata reaching native matrices.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warning on `LOG_LEVIATHAN_KINEMATICS_SOLVER.md`.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T02:27+04:00

Status: PENDING VERIFICATION. Continued terrain native-index safety audit. No `dotnet` rebuild/compile was run.

What was wrong:
- The terrain loop read raw segment positions immediately before SDF/height sampling.
- If previous-frame data was already invalid, that value could reach floor/clamp/index math.

What was done:
- Sanitized terrain-hugged segment positions in-place before contact sampling.
- Used a parent-derived fallback for tail/body segments.

Cinematic cheats used:
- None. Existing SDF/height fake contact behavior is unchanged for valid data.

Exact microseconds saved:
- No frame-time saving claimed.
- Added finite check estimated under 0.1 us for lower terrain-contact segments.
- Prevented fault: invalid segment positions reaching terrain native indexing.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.
## 2026-05-15T02:45+04:00

Status: PENDING VERIFICATION. Continued dead-payload audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `_solverTimeSeconds` was maintained every tick solely to fill `PhaseTimeSeconds`.
- `LeviathanTerrainIkJob` did not consume `PhaseTimeSeconds`, so the field added schedule payload and state coherence surface without behavior.

What was done:
- Removed `_solverTimeSeconds` and its wrap logic.
- Removed `PhaseTimeSeconds` from the Burst job payload and job initializer.

Cinematic cheats used:
- None. This is state removal only; terrain hugging, tail whip, and GPU deformation behavior are unchanged.

Exact microseconds saved:
- Estimated 0.01-0.05 us per scheduled solver from one less accumulator write and one less scalar copied into the job payload.
- No profiler-backed frame-time claim.

Verification:
- `rg` confirms no remaining `PhaseTimeSeconds` or `_solverTimeSeconds` references in the IK runtime/job scope.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on docs.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T02:57+04:00

Status: PENDING VERIFICATION. Continued runtime finite-boundary audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `_strikeRange` was assigned but unused in the new Burst/GPU path.
- Invalid caller or serialized floats could still affect runtime seeding, fallback intent, strike target state, attack telegraph, or origin-shift matrix rebasing before the Burst job clamps executed.

What was done:
- Removed `_strikeRange`.
- Rejected non-finite `deltaTime` before scheduling.
- Sanitized seed segment length/body radius, fallback intent segment length/body speed reuse, strike target position, tail-whip duration, attack telegraph, and AUP-rebased target/matrix translation values.

Cinematic cheats used:
- None. Existing Math LOD, height fallback, and triangle-wave tail whip behavior are unchanged.

Exact microseconds saved:
- No frame-time saving claimed.
- Added finite gates are estimated under 0.2 us on normal scheduling frames; seed/shift work is cold-path only.
- Removed dead `_strikeRange` state avoids one write per strike-intent update.

Verification:
- `rg` confirms no remaining `_strikeRange`, `PhaseTimeSeconds`, or `_solverTimeSeconds` references in the IK runtime/job scope.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:00+04:00

Status: PENDING VERIFICATION. Continued AUP/terrain-contact audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `OnOriginShift` could finalize a completed solver job before `LateFrameTick` without frame-index or invalid-telemetry bookkeeping.
- SDF gradient normals used unscaled central differences, so non-uniform voxel cell steps could bias the terrain push direction.

What was done:
- Added frame-index advance and invalid telemetry dump check in the origin-shift finalize branch.
- Scaled SDF gradient components by reciprocal axis step before normalization.

Cinematic cheats used:
- Existing SDF pushout remains the high-tier visual terrain fake; no Unity Physics or physical collision path was added.

Exact microseconds saved:
- No frame-time saving claimed.
- Origin-shift fix is cold-path only.
- SDF gradient correction adds under 0.1 us estimated on high-tier contact frames and buys contact quality.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:03+04:00

Status: PENDING VERIFICATION. Continued dispatcher lifecycle audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `TryRegister()` treated `_registeredUpdate` alone as enough to skip registration.
- If the late-frame registration flag was false, solver completion and GPU upload could stay stranded after an external lifecycle edge.

What was done:
- `TryRegister()` now exits only when both update and late-frame registrations are present.
- Partial registration state is unregistered and retried from a clean pair.

Cinematic cheats used:
- None. Lifecycle wiring only.

Exact microseconds saved:
- 0 us hot-path impact.
- Cold path may execute one unregister pair before retrying only when state is already inconsistent.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:20+04:00

Status: PENDING VERIFICATION. Continued contract-surface and native ownership audit. No `dotnet` rebuild/compile was run.

What was wrong:
- `FaunaBrain` still computed and passed strike range into the new `FaunaKinematicsRuntime` path, but the Burst/GPU solver no longer used range.
- `FaunaKinematicsRuntime` carried unused `NativeMemoryOwner` and `_faunaBrain` members.
- GPU segment-length upload fallback was 1 m while seed and Burst solver fallback were 2.5 m.
- `TryGetLeviathanBones()` exposed a mutable native matrix array.

What was done:
- Removed the dead strike range API parameter and direct call-site calculations.
- Removed the unused runtime owner members.
- Aligned shader segment-length upload fallback to 2.5 m.
- Returned `NativeArray<float4x4>.ReadOnly` from the bone accessor.

Cinematic cheats used:
- None. Existing terrain fake, GPU deformation, and triangle-wave strike presentation are unchanged.

Exact microseconds saved:
- No frame-time saving claimed.
- Removed one unnecessary range calculation per procedural strike-intent update.
- Prevented fault: external future readers cannot mutate solver-owned bone matrices through this accessor.

Verification:
- `rg` confirms no `NativeMemoryOwner`, `_faunaBrain`, old four-argument `FaunaKinematicsRuntime.SetStrikeIntent`, or old 1 m segment upload fallback remains in the IK runtime/direct call site scope.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:36+04:00

Status: PENDING VERIFICATION. Continued terrain/LOD boundary audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `_activeSegmentCount` defaulted to 20 until the first solver Tick, so enable/rebind could publish a max-count buffer before low-tier policy resolved.
- MapMagic height fallback accepted finite-length buffers before checking terrain origin/size metadata on the runtime side.
- The Burst height sampler clamped out-of-tile XZ positions to terrain edges, which can push tail segments with unrelated border heights.
- Segment-length fallback was still expressed as repeated literals across runtime/job boundaries.

What was done:
- Set the cold active segment default to `LowTierSegments` and resolved active segment count during reset.
- Added shared `DefaultSegmentLength`, `MinSegmentLength`, and `MinTerrainSize` constants in the IK contract.
- Added runtime finite/positive terrain metadata filtering before passing MapMagic payloads into the job.
- Changed the Burst height sampler to reject non-finite/out-of-tile XZ samples instead of edge-clamping them.

Cinematic cheats used:
- Existing 2D MapMagic seabed fallback remains a cheap terrain-contact fake when 3D SDF is unavailable.
- Low tier continues to spend only eight matrices and disables SDF contact.

Exact microseconds saved:
- No measured frame-time saving claimed.
- Prevented one false first-upload 20-matrix low-tier exposure.
- Prevented wrong edge-height terrain pushes for out-of-tile tail samples.
- Added scalar bounds checks estimated below 0.05 us on terrain-contact frames; profiler proof absent.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms no old segment-length clear fallback, old raw terrain-edge clamp pattern, or old `2.5f, 0.05f` segment fallback pair remains in the IK runtime/job scope.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:47+04:00

Status: PENDING VERIFICATION. Continued GPU upload ownership audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `TryGetLeviathanBoneGraphicsBuffer()` treated allocation validity as data freshness.
- Reseed/rebase and no-consumer upload skips could leave an old buffer allocation available to external consumers.
- A getter-side forced upload would hide GPU bandwidth cost in a query path.

What was done:
- Added `_gpuBufferDataValid` to separate allocation health from fresh uploaded bone data.
- Gated external buffer access on `_gpuUploadDirty == false` and `_gpuBufferDataValid == true`.
- Invalidated the buffer freshness flag on seed, origin rebase, persistent-buffer disposal, graphics-buffer release, skinning clear, no-consumer upload skip, and material/global unbind.
- Set the flag true only after `GraphicsBufferUploadUtility.UploadNativeArray()` completes.

Cinematic cheats used:
- None. Existing GPU matrix deformation and low-tier eight-matrix cheat are unchanged.

Exact microseconds saved:
- No frame-time saving claimed.
- Added one boolean gate in an internal accessor.
- Prevented stale GPU bone deformation after lifecycle/rebase/publish-state transitions.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms `_gpuBufferDataValid` is invalidated on stale-buffer transitions and set true only after upload.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T03:54+04:00

Status: PENDING VERIFICATION. Continued lifecycle/blackbox audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- Forced lifecycle completion consumed a scheduled solver but did not advance `_frameIndex`.
- Normal late-frame and origin-shift completion already advanced the frame index, creating inconsistent blackbox chronology across completion paths.

What was done:
- Added `AdvanceFrameIndex()` as the single runtime frame-index advancement helper.
- Routed normal late-frame completion, origin-shift job finalization, and forced lifecycle completion through that helper.

Cinematic cheats used:
- None. This is telemetry/lifecycle correctness only.

Exact microseconds saved:
- No frame-time saving claimed.
- Steady hot-path cost is unchanged; duplicated scalar assignment was centralized.
- Prevented duplicate runtime frame IDs after disable/re-enable/rebind consumes an in-flight solver.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms only `AdvanceFrameIndex()` writes `_frameIndex` and all scheduled-job completion paths call it.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T04:04+04:00

Status: PENDING VERIFICATION. Continued GPU consumer contract audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `TryGetLeviathanBoneGraphicsBuffer()` returned `false` for dirty/invalid data but could leave a stale non-null buffer and nonzero segment count in its out parameters.
- That made the boolean the only protection against stale GPU deformation for any future or parallel consumer.

What was done:
- Changed the getter to resolve a local candidate buffer.
- Published the buffer/count only after `_gpuUploadDirty == false`, `_gpuBufferDataValid == true`, and `HasValidGraphicsBuffer(...)` all pass.
- Cleared `buffer` and `activeSegmentCount` on every failed query.

Cinematic cheats used:
- None. Existing low-tier eight-matrix and GPU matrix deformation paths are unchanged.

Exact microseconds saved:
- No frame-time saving claimed.
- Added no allocations and no GPU upload work.
- Prevented stale GPU buffer consumption on failed query states.

Verification:
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms `TryGetLeviathanBoneGraphicsBuffer()` only assigns non-null `buffer` inside the fresh-upload success branch.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T04:15+04:00

Status: PENDING VERIFICATION. Removed dead legacy Leviathan presentation file. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `ProceduralLeviathanSpineIK` still existed as an older Animator/SkinnedMeshRenderer transform-chain route.
- It carried a stale strike API and managed scratch-list setup despite the active Alpha path using `FaunaKinematicsRuntime`.

What was done:
- Verified the MonoScript GUID `409e50cc5c5dffc4790462e3a0eafe0f` had no asset references.
- Verified type-name scans only hit the legacy file itself.
- Deleted `ProceduralLeviathanSpineIK.cs` and `ProceduralLeviathanSpineIK.cs.meta` together.

Cinematic cheats used:
- None. This is dead-path removal; active GPU matrix deformation and low-tier eight-bone cheat remain unchanged.

Exact microseconds saved:
- No runtime frame-time saving claimed because the file had no found consumers.
- Removed roughly 1,000 lines of dead compile surface and eliminated an accidental CPU skinning/Animator route.

Verification:
- Post-delete `rg` found no remaining `ProceduralLeviathanSpineIK` or `409e50cc5c5dffc4790462e3a0eafe0f` references.
- Both deleted paths return `False` from `Test-Path`.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T04:36+04:00

Status: PENDING VERIFICATION. Continued dispatcher-cadence hygiene. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `FaunaKinematicsRuntime` used `Time.frameCount` to decide whether authored motion intent was current.
- That tied the IK solver to Unity rendered-frame equality instead of dispatcher consumption, so intent published after the runtime tick could be overwritten by fallback motion before being used.

What was done:
- Replaced `_motionIntentFrame` with `_motionIntentPending`.
- `SetMotionIntent()` now marks intent pending.
- `CaptureFallbackMotionIntent()` consumes pending intent once and otherwise resolves fallback body velocity.
- `SeedSpineFromOwner()` clears pending intent on lifecycle reseed.

Cinematic cheats used:
- None. Existing low-tier eight-bone and shader tail-wave cheats are unchanged.

Exact microseconds saved:
- No frame-time saving claimed.
- One `bool` replaces one `int`; two `Time.frameCount` reads are removed from the IK runtime path.

Verification:
- `rg` confirms no `_motionIntentFrame` or `Time.frameCount` references remain in `FaunaKinematicsRuntime`.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T04:52+04:00

Status: PENDING VERIFICATION. Continued Leviathan tentacle lifecycle audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `LeviathanTentacleVerletSolver.TryRegister()` could return with only the update dispatcher path registered.
- Late-frame owns Burst job completion, blackbox telemetry, GPU upload, and indirect draw submission, so a partial registration state can stall the solver/render path.

What was done:
- Repaired partial dispatcher registration by unregistering both update and late-frame paths, clearing both flags, and retrying from a clean state.

Cinematic cheats used:
- No added physical simulation. The fix protects the existing cheap Verlet + indirect-render visual path instead of adding fallback render work.

Exact microseconds saved:
- 0 us claimed in the hot path.
- Cold lifecycle-only repair prevents missing completion/upload; no profiler-backed frame-time number exists.

Verification:
- Static snippet inspection confirms both partial registration flags are cleared before retry.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T04:58+04:00

Status: PENDING VERIFICATION. Continued Leviathan tentacle native-memory audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- `LeviathanTentacleVerletSolver` owned thirteen persistent SOA/blackbox arrays through direct `new NativeArray<T>` calls.
- Tick only gated on `_positions`, so a partial allocation state could still enter scheduling with missing lanes.

What was done:
- Added `Hecton8.Core.Memory` and moved persistent arrays to `H8Memory.Allocate<T>(..., SystemID.External, ...)`.
- Released arrays through `H8Memory.Release` while preserving `NativeMemorySentinel` labels.
- Added `HasPersistentBuffers()` and required the complete array set before scheduling.
- Cleaned up immediately if a cold allocation attempt does not produce the full buffer set.

Cinematic cheats used:
- None added. The existing fixed 8 tentacle / 20 node SOA budget and indirect render path are unchanged.

Exact microseconds saved:
- 0 us hot-path savings claimed.
- Added only fixed `IsCreated` gates before scheduling; the gain is owner tracking and fail-closed memory behavior, not measured frame time.

Verification:
- `rg` confirms no direct `new NativeArray<` or `array.Dispose(` remains in `LeviathanTentacleVerletSolver`.
- `git diff --check` on touched code/docs exits 0 with only LF-to-CRLF warnings.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.

## 2026-05-15T05:03+04:00

Status: PENDING VERIFICATION. Continued Leviathan tentacle blackbox/lifecycle audit. No `dotnet` rebuild/compile, Unity import, or response-file probe was run.

What was wrong:
- Tentacle telemetry was written only on the normal late-frame render path.
- Disable, origin-shift finalization, queued origin-shift rebase, and forced lifecycle completion could consume a scheduled solve without recording the frame.

What was done:
- Wrote blackbox telemetry after scheduled-job finalization on disable.
- Wrote telemetry after origin-shift completion and after pending-origin-shift late-frame completion.
- Wrote telemetry after forced lifecycle completion through `CompletePendingJob()`.
- Required the complete persistent buffer set before origin-shift rebase.

Cinematic cheats used:
- No new simulation. Origin-shift frames still skip render submit; only the blackbox state is recorded.

Exact microseconds saved:
- 0 us normal hot-path saving claimed.
- Lifecycle/rebase completion writes one fixed telemetry entry; estimated below 0.02 us, pending profiler proof.

Verification:
- Static grep confirms every `_pendingSolverHandle` completion/finalization site now writes telemetry when it consumes a scheduled solve.
- `git diff --check` on touched code/docs exits 0.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.
