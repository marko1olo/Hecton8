# Status_LEVIATHAN_KINEMATICS_SOLVER

Status authority: PENDING VERIFICATION until Unity compile and runtime evidence exist.
Prompt: LEVIATHAN_KINEMATICS_SOLVER
Role: MOTION_ENGINEER
Domain: ECHELON 3 / FLORA, FAUNA & BIOTA / Leviathan Procedural IK
Batch source: Docs/Tasks/CURRENT_BATCH.md

## Hygiene

- [x] Prompt extracted from CURRENT_BATCH.md with CLI regex over full file | DOD: strict prompt isolation | Alternatives Rejected: MCP/basic reader because truncation risk | Estimate: 400 us
- [x] Prompt re-extracted with attribute-aware CLI regex | DOD: captured `<AGENT_PROMPT id="LEVIATHAN_KINEMATICS_SOLVER" role="MOTION_ENGINEER" ...>` cover-to-cover | Alternatives Rejected: strict id-only tag because batch uses attributes | Estimate: 80 us
- [x] Status file was absent before creation | DOD: fresh batch state | Alternatives Rejected: reuse old logs because batch hygiene forbids stale memory | Estimate: 40 us
- [x] Relevant mandates selected before code | DOD: registry-first compliance | Alternatives Rejected: coding from prompt only because Burst/native rules are stricter than task text | Estimate: 120 us

## Task Checklist

- [x] Task 1: Extend FaunaKinematicsRuntime, no singleton work | DOD: `FaunaKinematicsRuntime` is the presentation owner bound by `FaunaBrain` | Alternative Rejected: new singleton service | Estimate: 8 us hot scheduling overhead
- [x] Task 2: Consume Leviathan intended velocity vector | DOD: `UpdateLeviathanKinematicsMotionIntent` forwards steering/body velocity and head target | Alternative Rejected: pulling cognition from globals | Estimate: 2 us
- [x] Task 3: Add/align Hecton8.Animation.IK asmdef dependency to Contracts | DOD: `Hecton8.Animation.IK.asmdef` references `Hecton8.Core.Contracts`; `Hecton8.Core.asmdef` references IK | Alternative Rejected: dumping Burst job into monolithic core | Estimate: 0 us runtime
- [x] Task 4: Dead code hunt for Animator/SkinnedMeshRenderer on Alpha Leviathan path | DOD: Alpha presentation owner now uses `FaunaKinematicsRuntime`; static grep found no Animator/SkinnedMeshRenderer use in IK runtime or Alpha proxy prefab | Alternative Rejected: transform-chain `ProceduralLeviathanSpineIK` path | Estimate: saves 150-600 us versus CPU skinning path, unmeasured
- [x] Task 5: Define SOA NativeArray<float4x4> LeviathanBones | DOD: persistent `_leviathanBones` native array at 20 matrices | Alternative Rejected: managed `Matrix4x4[]` | Estimate: 0 B GC, 1.28 KB native matrix lane
- [x] Task 6: Burst Verlet spine constraint solver using math.rsqrt | DOD: `LeviathanTerrainIkJob` integrates followers and pulls sequential distance constraints with `math.rsqrt` | Alternative Rejected: `math.sqrt`/`Vector3.Distance` | Estimate: 12-35 us by tier
- [x] Task 7: SDF terrain hugging for lower five segments | DOD: lower five segments sample `VoxelSdfTexture3D`, calculate gradient, and push out of positive density | Alternative Rejected: Unity Physics casts | Estimate: 20-60 us high tier only
- [x] Task 8: MapMagic 2D height fallback through vault interface | DOD: `MapMagicBridge.QuantizedHeightmapPayload` plus `BufferID.TerrainSeamHeightmap` fallback prevent seabed clipping when SDF is absent | Alternative Rejected: `Terrain.SampleHeight` managed calls | Estimate: 4-12 us
- [x] Task 9: Upload LeviathanBones to GraphicsBuffer | DOD: double-buffered `GraphicsBuffer` upload through `GraphicsBufferUploadUtility.UploadNativeArray` | Alternative Rejected: per-frame managed arrays | Estimate: 3-10 us upload overhead
- [x] Task 10: Hook existing compute/GPU skinning path where available | DOD: material/global shader buffers are published and `Hecton_LeviathanOrganic.shader` consumes `_H8LeviathanBones` in forward and shadow passes | Alternative Rejected: CPU mesh deformation and serialized material defaults that shadow globals | Estimate: saves 150-600 us versus CPU skinning, unmeasured
- [x] Task 11: Strike tail whip impulse with one-second terrain bypass | DOD: strike starts `_tailWhipSecondsRemaining`, passes `_tailWhipDurationSeconds` into Burst, applies tail-half wave impulse, bypasses terrain constraints during active timer | Alternative Rejected: physical joints/impulses | Estimate: under 4 us
- [x] Task 12: AUP shift safety for all segments | DOD: `OnOriginShift` rebases segment positions, previous positions, matrices, and target positions | Alternative Rejected: reseeding spine after shift | Estimate: 5-15 us per shift only
- [x] Task 13: Math LOD: Low tier eight segments and SDF disabled | DOD: `HectonQualityTier.Unknown/Low/Mx350` clamps active segments to 8 and disables SDF terrain hugging | Alternative Rejected: balanced middle path | Estimate: saves 20-60 us versus high tier
- [x] Task 14: Zero-GC hot path audit | DOD: static grep found no hot-path managed collection creation, Mono `Update`, `Debug.Log`, `Camera.main`, `renderer.material`, or `GlobalRegistry.Get` in IK runtime/job files | Alternative Rejected: deferred audit | Estimate: 0 B intended hot path
- [x] Task 15: Omega compile check: verify math.rsqrt constraints [BLOCKED BY DEPENDENCY] | DOD: `math.rsqrt` verified in all distance constraints; isolated `Hecton8.Animation.IK` csc pass exits 0; full project compile blocked by unrelated assemblies/open Unity instances | Alternative Rejected: fake green build report | Estimate: compile wall cost external

## Iteration Log

### Loop 0: Intake

- Read batch prompt and mandates. No code written yet.
- Compile status: PENDING.

### Loop 1: Tasks 1-5

- Implemented/verified runtime boundary, signal migration, asmdef isolation, Animator/SMR path purge, and native matrix SOA.
- Compile status: PENDING. No Unity full-project green state exists.

### Loop 2: Tasks 6-8

- Implemented/verified Burst Verlet constraints, SDF pushout, and MapMagic/DataVault fallback.
- Static check: `math.rsqrt` present in head clamp, distance constraints, length helpers, and normalization.

### Loop 3: Tasks 9-11

- Implemented/verified graphics buffer upload, shader/compute skinning buffer contract, and strike tail wave with terrain bypass.
- Static check: no managed matrix array upload path was introduced.

### Loop 4: Tasks 12-14

- Implemented/verified AUP rebase, low-tier eight-segment SDF-off gate, and zero-GC hot-path grep.
- Static check: IK runtime/job files do not define Mono `Update`, `LateUpdate`, or `FixedUpdate`.

### Loop 5: Task 15 Compile Wall

- Isolated evidence: Unity saved Roslyn response files for `Hecton8.Animation.IK` were executed after Omega polish and exited 0; `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Animation.IK.dll` timestamp updated.
- Full project evidence: BLOCKED. `dotnet build Hecton8.Core.csproj --no-restore` timed out under unrelated assembly failures; open Unity project logs show unrelated errors in `HectonUnderwaterVisuals`, `PlayerKinematicsRuntime`, `GlobalDataVault`, and missing contract files.
- Strike protocol: no IK chunk reverted because the observed wall is outside this task's files.

### Loop 6: Omega Polish

- Read all active `<POLISH_MANDATE id="OMEGA_POLISH">` blocks after core tasks were checked/blocked.
- Applied bitmask runtime flags to the Burst job and replaced SDF sample division with `math.rcp` multiply.
- Static polish grep: no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, Unity Physics casts, Animator/SMR dependency, managed Update loop, or Debug.Log in the IK runtime/job files.
- Scoped compile: polished `Hecton8.Animation.IK` csc pass exited 0.
- Final status remains PENDING VERIFICATION because full project compile is blocked outside this task.

### Loop 7: GPU Shader Contract Recheck

- Re-extracted the LEVIATHAN_KINEMATICS_SOLVER prompt with CLI regex before continuing.
- Fixed the presentation gap: the organic Leviathan shader now deforms forward and shadow vertices from the published bone buffer, not only from local vertex sways.
- Removed the dead body-radius shader uniform and removed hidden material properties for IK gates to prevent material defaults from shadowing global runtime uniforms.
- Added runtime shutdown gating so disable/dispose sets `_H8LeviathanGpuSkinning`, `_H8LeviathanBoneCount`, and `_H8LeviathanTailWhip01` to zero.
- Static check: no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, or `_H8LeviathanBodyRadius` remains in IK runtime/job/shader scope.
- `git diff --check` exits 0; line-ending warnings are repo-wide and unrelated.
- Compile probe: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` timed out after 94 seconds under the known project compile wall.
- Final status remains PENDING VERIFICATION because shader import/full Unity compile is still blocked by the known project compile wall.

### Loop 8: Lifecycle Race Recheck

- Re-read AGENTS, the domain file, the LEVIATHAN_KINEMATICS_SOLVER prompt, and the eight relevant mandates before code.
- Fixed a native lifecycle race: disable/re-enable/rebind now force-completes the scheduled IK job before reseeding persistent native arrays.
- Wired `_tailWhipDurationSeconds` into `LeviathanTerrainIkJob` so strike wave age matches authored duration instead of a hard-coded one-second constant.
- Scoped compile: Unity Roslyn `Hecton8.Animation.IK` csc pass exited 0 after the tail-duration field change; DLL timestamp updated to 2026-05-14 12:31:39.
- Static check: no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, Animator/SMR dependency, or `_H8LeviathanBodyRadius` in IK runtime/job/shader scope.
- `git diff --check` on touched IK runtime/job/shader files exits 0; line-ending warnings are repo-wide and unrelated.
- Final status remains PENDING VERIFICATION because `FaunaKinematicsRuntime` is still inside the project-wide `Hecton8.Core` compile wall.

### Loop 9: H-Phi Domain Hygiene Recheck

- User explicitly prohibited `dotnet` rebuilds; no `dotnet build`, rebuild, or Roslyn response-file compile was run in this loop.
- CLI prompt re-extraction from `Docs/Tasks/CURRENT_BATCH.md` returned `Prompt block not found`; current batch has rotated, so this loop used the persisted status/rationale and ignored unrelated prompts.
- Re-read AGENTS, the domain file, H-Phi atlas section, and six relevant mandates before code: IK, zero-GC, native memory/jobs, AUP, blackbox, GPU-driven animation.
- Rechecked current `FaunaKinematicsRuntime`: native bone accessor is gated against scheduled writers, AUP dirty upload is present, no-consumer GPU upload skip is present, material gate clearing is present, deferred dispose chaining is present, and hot `GlobalRegistry.ScalabilityTier` reads are cached to two source references.
- Fixed `FaunaBrain.EnsureLeviathanPresentationOwner()` so it resolves an existing `FaunaKinematicsRuntime` with `TryGetComponent` before adding a new component.
- Scoped H-Phi source counters for `FaunaKinematicsRuntime`: `GlobalRegistryRefs=11`, `ScalabilityTierRefs=2`, `NativeArrays=22`, `SignalBusRefs=0`, `UnityUpdateMethods=0`, `FindCalls=0`, `GetComponentCalls=3`.
- Static grep over IK runtime/job/shader scope found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code exits 0; only LF-to-CRLF warnings on `FaunaBrain.cs`.
- Final status remains PENDING VERIFICATION until Unity Editor import, shader compile, play-mode behavior, and profiler evidence exist.

### Loop 10: Blackbox Dump Format Recheck

- Fixed `DumpTelemetryBlackBox()` binary integrity: dumped entries are now written oldest-to-newest from the telemetry ring instead of physical ring order.
- Fixed payload-size honesty: each dumped telemetry entry now writes explicit padding floats so the binary body matches `TelemetryEntryPayloadBytes = 96`.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.
- Final status remains PENDING VERIFICATION until Unity Editor import, shader compile, play-mode behavior, and profiler evidence exist.

### Loop 11: Telemetry Cursor Wrap Recheck

- Fixed the Burst telemetry cursor overflow path so `int.MaxValue` wrap preserves full-ring state and the next write index instead of resetting the cursor to zero.
- DOD: `TelemetryHasInvalidFrame()` and dump ordering still resolve the newest/oldest retained entries correctly after long uptime wrap.
- Alternative Rejected: cursor reset to zero because it makes the post-wrap dump look empty and can inspect the wrong last telemetry entry.
- Estimate: 0 us hot-path meaningful cost; overflow branch is effectively unreachable during normal play, but it removes a blackbox integrity edge case.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 12: SDF Sampler Hot-Path Recheck

- Hoisted SDF inverse cell-size resolution once per job execution instead of recomputing it inside each trilinear density and gradient sample.
- Removed repeated voxel-count resolution from the private trilinear sampler; the outer `canUseSdf` gate remains the authoritative length/dimension validation.
- DOD: high-tier SDF hugging still samples density plus six central-difference gradient points, but avoids redundant validation/reciprocal work for the lower five terrain-hug segments.
- Alternative Rejected: changing gradient model or lowering sample count because visual contact quality is the point of high/ultra tiers.
- Estimate: 0.5-2 us saved on high-tier SDF-contact frames; 0 us on Low/MX350 because SDF remains disabled.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 13: MapMagic Height Payload Overflow Recheck

- Added `LeviathanTerrainIkJob.TryResolveTerrainHeightSampleCount()` using `long` multiplication before accepting or sampling any 2D fallback height buffer.
- Updated `FaunaKinematicsRuntime.ResolveMapMagicPayload()` to reject invalid height payload lengths before handing native buffers to the Burst job.
- Updated the Burst height fallback gate and private sampler to use the checked sample-count resolver instead of `TerrainResolution * TerrainResolution`.
- DOD: fallback terrain hugging cannot accept an overflowed resolution/sample-count pair into the lower-segment contact loop.
- Alternative Rejected: trusting `MapMagicBridge.QuantizedHeightmapPayload.IsValid` alone because that contract currently multiplies `HeightmapResolution * HeightmapResolution` in `int` space.
- Estimate: 0 us normal terrain cost; avoids catastrophic invalid native indexing on malformed payloads.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` on touched code/docs exits 0; output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 14: Global GPU Skinning Gate Recheck

- Added `_globalGpuSkinningPublished` ownership tracking so stale global Leviathan shader gates are cleared when global publishing is disabled or the runtime shuts down.
- DOD: `_H8LeviathanGpuSkinning` cannot remain globally enabled from an earlier publish if the runtime later uses material-only binding or no GPU consumer.
- Alternative Rejected: relying on `_publishGlobalBoneBuffer` alone because serialized/runtime toggles can change after a global buffer has already been published.
- Estimate: 0 us normal hot-path cost after state is stable; one cold clear only on publish-off transition or shutdown.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 15: Lifecycle Seed Dirty Recheck

- Updated lifecycle force-complete so a completed scheduled solver is checked for invalid telemetry before reseed/shutdown continues.
- Updated `SeedSpineFromOwner()` so cold reseed marks GPU upload dirty and clears `_motionIntentFrame`, preventing one-frame stale fallback intent or stale bone upload after bind/enable.
- DOD: bind/enable reseeds now produce explicit GPU dirty state and keep blackbox NaN detection active on forced lifecycle completion.
- Alternative Rejected: relying on the next simulation tick to dirty GPU state because consumers can query the buffer between bind and the next successful solver frame.
- Estimate: 0 us steady hot path; cold lifecycle only.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings for docs.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 16: Shader Scalar Sanitization Recheck

- Added finite positive sanitization before `_H8LeviathanSegmentLength` and `_H8LeviathanTailWhip01` are published to material/global shader state.
- DOD: malformed serialized/programmatic segment length or tail-whip duration cannot push NaN/zero scale into GPU deformation.
- Alternative Rejected: trusting Unity `[Range]` attributes because runtime code can still assign non-finite values.
- Estimate: under 0.1 us on upload frames; no Low/MX350 behavior change.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 17: Burst Scalar Boundary Recheck

- Added Burst-side finite clamps for damping, segment length, body radius, terrain clearance, tail-whip remaining time, duration, and amplitude before solver math consumes them.
- Passed sanitized tail-whip values through `ApplyTailWhip()` and telemetry instead of reading raw job fields inside those paths.
- DOD: non-finite or negative tuning data cannot poison segment positions, matrices, or telemetry through scalar job fields.
- Alternative Rejected: sanitizing only shader upload because the Burst solver can corrupt native matrices before GPU publication.
- Estimate: under 0.2 us per scheduled solver; all checks are scalar and avoid downstream NaN failure.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 18: Terrain Payload Finite Gate Recheck

- Added finite gates for SDF origin, SDF cell size/range, Terrain origin, and Terrain size before terrain hugging samples execute.
- Passed sanitized SDF range/cell values through trilinear density sampling, gradient sampling, and decode instead of reading raw job fields.
- DOD: malformed terrain payloads cannot introduce NaN samples through SDF or height fallback contact math.
- Alternative Rejected: trusting terrain/voxel producers because this Burst job is the last safety boundary before writing native matrices.
- Estimate: under 0.3 us on terrain-contact solver frames; Low/MX350 still skips SDF.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warning on `LOG_LEVIATHAN_KINEMATICS_SOLVER.md`.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 19: Terrain Segment Pre-Sample Sanitize Recheck

- Sanitized each terrain-hugged segment position in-place before SDF or height sampling.
- DOD: bad previous-frame segment data cannot flow into `floor`, clamp, SDF index, or heightmap index math.
- Alternative Rejected: relying only on earlier constraint passes because the terrain loop is a separate native indexing boundary.
- Estimate: under 0.1 us for the lower five terrain-contact segments.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; unstaged output is only LF-to-CRLF warnings.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 20: Dead Phase Payload Recheck

- Removed the unused `_solverTimeSeconds` runtime accumulator and `PhaseTimeSeconds` Burst job field.
- DOD: `rg` confirms no remaining `PhaseTimeSeconds` or `_solverTimeSeconds` references in the IK runtime/job scope.
- Alternative Rejected: keeping a reserved phase field because dead scheduled payload increases state coherence risk without current solver value.
- Estimate: 0.01-0.05 us saved per schedule from one less accumulator write and one less job scalar copy; primary gain is state removal.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on docs.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 21: Runtime Finite Boundary Recheck

- Removed dead `_strikeRange` state from the runtime-owned Burst path.
- Hardened runtime float boundaries before scheduling: invalid delta-time skips scheduling, seed scale values are finite, fallback intent uses sanitized segment length, strike targets/tail duration are finite, attack telegraph clamps NaN to zero, and origin-shift targets/matrix translation get finite fallbacks.
- DOD: invalid caller/inspector floats cannot poison cold seeding, fallback intent, strike target state, or AUP matrix rebasing before Burst-side sanitizers execute.
- Alternative Rejected: relying only on Burst job sanitizers because lifecycle seeding and origin-shift rebasing also write GPU matrices outside the scheduled job.
- Estimate: under 0.2 us on normal scheduling frames; cold/lifecycle branches only for seed/shift. No profiler-backed claim.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms no remaining `_strikeRange`, `PhaseTimeSeconds`, or `_solverTimeSeconds` references in the IK runtime/job scope.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 22: Origin-Shift Completion And SDF Normal Recheck

- Added frame-index advance and invalid-telemetry dump when `OnOriginShift` finalizes an already-completed solver job before LateFrame consumes it.
- Corrected SDF central-difference gradient direction by scaling each axis delta by the reciprocal sample step.
- DOD: completed solver jobs keep blackbox/frame bookkeeping even when an AUP shift interrupts the normal late-frame path, and anisotropic SDF cells produce correctly scaled terrain push normals.
- Alternative Rejected: relying on LateFrame only because origin shift can legally consume a completed job first; unscaled SDF gradients were rejected because non-uniform voxel cells bias contact normals.
- Estimate: 0 us on normal late-frame path; origin-shift parity is cold-path only. SDF gradient adds three reciprocal/multiply components on high-tier contact frames, estimated under 0.1 us.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 23: Dispatcher Registration Repair Recheck

- Updated `TryRegister()` to repair partial update/late-frame registration state before retrying.
- DOD: the runtime cannot remain in a one-sided dispatcher registration state if an external lifecycle edge clears only one registration path.
- Alternative Rejected: returning when `_registeredUpdate` is true because that preserves stale partial state and can strand GPU upload/telemetry completion.
- Estimate: 0 us hot path; cold enable/rebind registration only.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 24: Strike Contract And Read-Only Bone API Recheck

- Removed the dead `strikeRange` parameter from `FaunaKinematicsRuntime.SetStrikeIntent()` and the two `FaunaBrain` call sites.
- Removed unused `NativeMemoryOwner` and `_faunaBrain` runtime members.
- Aligned GPU upload `_H8LeviathanSegmentLength` fallback with seed/Burst fallback at 2.5 m.
- Changed `TryGetLeviathanBones()` to expose `NativeArray<float4x4>.ReadOnly` instead of a mutable native array.
- DOD: current Burst/GPU strike presentation carries no false range contract, segment length fallback is consistent across CPU/GPU, and external native bone readers cannot mutate solver-owned matrices.
- Alternative Rejected: leaving dead parameters/fields for future use because they create false ownership surface and warning noise.
- Estimate: 0 us hot-path meaningful savings; one removed strike-intent range calculation per `FaunaBrain` strike update and lower future mutation risk.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms no `NativeMemoryOwner`, `_faunaBrain`, old four-argument `FaunaKinematicsRuntime.SetStrikeIntent`, or old 1 m segment upload fallback remains in the IK runtime/direct call site scope.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0; output is only LF-to-CRLF warnings on touched runtime/docs files.
- No `dotnet` rebuild, compile, or response-file probe was run.

### Loop 25: Terrain Bounds And Low-Tier First Upload Recheck

- Set the pre-first-tick active Leviathan segment count to the low-tier eight-segment contract instead of the max 20-segment path.
- Added reset-time active segment resolution so enable/rebind uploads respect the current scalability tier before the first solver tick.
- Added shared segment-length constants for CPU seed, Burst solve, shader upload, and shader clear state.
- Added runtime finite/positive MapMagic terrain metadata filtering before passing height buffers into the Burst job.
- Updated the Burst height sampler to reject non-finite/out-of-tile XZ samples instead of clamping them to the terrain edge.
- DOD: Unknown/Low/MX350 cannot publish a 20-bone first upload, malformed terrain payload metadata fails closed before native sampling, and terrain contact no longer uses edge-clamped heights for segments outside the owning tile.
- Alternative Rejected: keeping edge clamp as a seam hide because it can push the tail against an unrelated border height; relying only on Tick-time quality resolution because GPU consumers can query after enable/rebind before Tick.
- Estimate: 0 us hot-path meaningful savings; prevents false 20-matrix low-tier upload and wrong edge-height push. In-bounds height fallback adds only scalar bounds checks already aligned with existing MapMagic consumers.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms no old segment-length clear fallback, old raw terrain-edge clamp pattern, or old `2.5f, 0.05f` segment fallback pair remains in the IK runtime/job scope.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- No `dotnet` rebuild, compile, Unity import, or response-file probe was run.

### Loop 26: GPU Buffer Validity Gate Recheck

- Added `_gpuBufferDataValid` to distinguish a valid `GraphicsBuffer` allocation from current solver data being uploaded into it.
- Gated `TryGetLeviathanBoneGraphicsBuffer()` on both `_gpuUploadDirty == false` and `_gpuBufferDataValid == true`.
- Invalidated external GPU buffer access after seed, origin rebase, persistent-buffer disposal, graphics-buffer release, skinning clear, and no-consumer upload skip.
- DOD: external GPU consumers cannot read stale Leviathan bone data after reseed/rebase or after material/global publishing is disabled.
- Alternative Rejected: trusting `GraphicsBuffer.IsValid()` because it only proves allocation health, not data freshness. Forcing an upload inside the getter was rejected because it would add hidden main-thread GPU traffic to a query path.
- Estimate: 0 us meaningful hot-path cost; one boolean branch in an internal accessor. Prevents stale visual deformation rather than saving frame time.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms `_gpuBufferDataValid` is set false on all known stale-buffer transitions and true only after `UploadNativeArray()` completes.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- No `dotnet` rebuild, compile, Unity import, or response-file probe was run.

### Loop 27: Lifecycle Completion Frame Parity Recheck

- Added `AdvanceFrameIndex()` and routed late-frame, origin-shift finalization, and forced lifecycle completion through the same frame-index advance path.
- DOD: a scheduled solver consumed by disable/re-enable/rebind no longer leaves the next scheduled telemetry entry with a duplicate runtime frame index.
- Alternative Rejected: leaving forced lifecycle completion as telemetry-only because the job is still consumed and blackbox order must remain coherent.
- Estimate: 0 us steady hot path; lifecycle-only branch plus replacing duplicate scalar code in normal completion paths.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms only `AdvanceFrameIndex()` writes `_frameIndex` and all scheduled-job completion paths call it.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- No `dotnet` rebuild, compile, Unity import, or response-file probe was run.

### Loop 28: GPU Buffer Getter Fail-Closed Recheck

- Updated `TryGetLeviathanBoneGraphicsBuffer()` so dirty/invalid data returns `false` with `buffer = null` and `activeSegmentCount = 0`.
- DOD: a failed GPU bone-buffer query no longer leaks a stale allocation handle or nonzero segment count through out parameters.
- Alternative Rejected: leaving stale out params for callers to ignore because defensive contracts must fail closed under parallel integration.
- Estimate: 0 us meaningful hot-path cost; one local candidate variable and existing branch path only.
- Static grep over IK runtime/job/shader scope still found no `math.sqrt`, `math.normalize`, managed array creation, `foreach`, `string.Format`, `.ToString()`, `Debug.Log`, Unity Physics casts, `SkinnedMeshRenderer`, `renderer.material`, `Camera.main`, `GlobalRegistry.Get`, `GameObject.Find`, or `FindObject`.
- `rg` confirms `TryGetLeviathanBoneGraphicsBuffer()` only assigns non-null `buffer` inside the fresh-upload success branch.
- `git diff --check` and `git diff --cached --check` on touched code/docs exit 0.
- No `dotnet` rebuild, compile, Unity import, or response-file probe was run.
