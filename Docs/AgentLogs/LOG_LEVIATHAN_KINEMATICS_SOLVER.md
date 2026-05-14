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
