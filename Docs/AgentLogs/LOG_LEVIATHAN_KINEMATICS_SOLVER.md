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
- Pending static checks after this edit.
- Runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, GC, and profiler evidence exist.
