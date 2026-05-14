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

## 2026-05-14T16:37+04:00

Status: PENDING VERIFICATION. Continued native ownership and GPU binding hygiene audit. `Hecton8.Core` response-file compile now exits 0, but no in-editor shader import/runtime/profiler validation is claimed.

What was wrong:
- `TryGetLeviathanBones` could return the backing native matrix array while the Burst job was scheduled.
- `BindSkinningMaterial` could leave the previous material with the Leviathan GPU skinning gate enabled.
- `ClearGpuSkinningBinding` reset bone count/tail/gpu gate but did not reset IK tier or segment length.
- `DisposePersistentBuffers` started from the current dependency and could lose a prior deferred dispose handle.
- `Docs/Tasks/CURRENT_BATCH.md` has rotated; the Leviathan prompt block is no longer present for mandatory re-extraction.

What was done:
- Gated `TryGetLeviathanBones` against `_solverScheduled`, `_disposed`, missing arrays, and invalid counts.
- Clamped exported native/graphics buffer segment counts to the backing storage size.
- Cleared old and new material GPU skinning gates during material rebinding.
- Centralized material gate clearing in `ClearMaterialGpuSkinningBinding`.
- Reset global IK tier and segment length during runtime GPU teardown.
- Chained native SOA disposal from the previous `_disposeHandle` plus the active job dependency.
- Recorded the batch-rotation extraction failure in the status/rationale files and ignored unrelated current-batch prompts.

Cinematic cheats used:
- No new physical simulation was added. Existing GPU matrix deformation and triangle-wave tail cheat remain the presentation path.
- Accessor gating returns false instead of stalling the main thread to complete a job.

Exact microseconds saved:
- Hot-path savings: 0 us. This pass is correctness and ownership hygiene.
- Avoided hidden stall: potentially one scheduled 8-20 segment job completion whenever a caller probes bones mid-solve; exact value unmeasured.
- Stale GPU deformation avoidance: prevents visual corruption, not a frame-time gain.
- Deferred dispose chaining cost: cold path only, negligible versus teardown/rebind.

Verification:
- CLI prompt extraction returned `Prompt block not found`; current batch no longer contains `LEVIATHAN_KINEMATICS_SOLVER`.
- Unity Roslyn `Hecton8.Core` csc with `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.rsp` and `.rsp2` exits 0.
- Static grep found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, `renderer.material`, or `Camera.main` in IK runtime/job/shader scope.
- `git diff --check` on touched files exits 0; only the existing LF-to-CRLF warning for `FaunaKinematicsRuntime.cs` is emitted.
- Final runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, and profiler data exist.

## 2026-05-14T16:53+04:00

Status: PENDING VERIFICATION. Continued AUP/GPU synchronization audit after the ownership pass.

What was wrong:
- A queued origin shift rebased CPU-side Leviathan matrices but `LateFrameTick` returned before uploading the corrected matrices.
- If no solver was scheduled, an origin-shift-only rebase could also leave the shader sampling the previous GPU buffer until the next solved frame.

What was done:
- In the no-solver late-frame path, a successful pending rebase now calls `UploadBonesToGpu()`.
- In the completed-solver path, `ApplyPendingOriginShiftRebase()` no longer returns early; telemetry and GPU upload still run after the rebase.
- Task 12 status now records immediate late-frame matrix publication after AUP rebase.

Cinematic cheats used:
- No new simulation. Existing matrices are rebased and uploaded instead of scheduling extra IK work.
- One shift-frame upload is accepted to prevent a visible large-body pop.

Exact microseconds saved:
- Hot-path savings: 0 us. This only runs on origin-shift frames.
- Shift-frame added cost: estimated 3-10 us for the existing 20-matrix GPU upload, unmeasured.
- Avoided artifact: one frame of stale pre-shift Leviathan bones on the shader path.

Verification:
- First Unity Roslyn `Hecton8.Core` csc probe timed out at 120 seconds without compiler output.
- Rerun with a 240-second timeout exits 0 using `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.rsp` and `.rsp2`.
- Static grep found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, `renderer.material`, or `Camera.main` in IK runtime/job/shader scope.
- `git diff --check` on touched files exits 0; output is only LF-to-CRLF warnings on touched files.
- Final runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, and profiler data exist.

## 2026-05-14T17:04+04:00

Status: PENDING VERIFICATION. Continued GPU upload performance hygiene pass.

What was wrong:
- `UploadBonesToGpu()` maintained graphics buffers even when there was no material and global publishing was disabled.

What was done:
- Added a no-consumer early return before `EnsureGraphicsBuffers()`.
- Left default global publishing behavior unchanged.

Cinematic cheats used:
- None added. This is a disabled-consumer fast path only.

Exact microseconds saved:
- No-consumer configuration saves one double-buffer validity path and one 20-matrix upload, estimated 3-10 us/frame unmeasured.
- Default material/global consumer path has 0 us behavioral change.

Verification:
- Unity Roslyn `Hecton8.Core` csc with `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.rsp` and `.rsp2` exits 0.
- Static grep found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, `renderer.material`, or `Camera.main` in IK runtime/job/shader scope.
- `git diff --check` on touched files exits 0; output is only LF-to-CRLF warnings on touched files.
- Final runtime status remains pending until Unity Editor import, shader compile, play-mode behavior, and profiler data exist.
