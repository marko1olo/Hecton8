# Status_ANIM_PROCEDURAL_BEHAVIOR

PROMPT IDENTIFIED: ANIM_PROCEDURAL_BEHAVIOR
ROLE: MOTION_ENGINEER
DOMAIN: ECHELON 3 FAUNA/BIOTA PROCEDURAL IK
TASK COUNT: 15
STATUS: PENDING VERIFICATION

Relevant mandates loaded:
- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt
- ANIM_Contextual_Physical_IK.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_Rsqrt_i3_SIMD.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- REND_GPU_Driven_Animation_VAT.txt

## Loop 0 - Setup
- [x] Extract XML prompt | Justification: strict batch protocol via PowerShell regex against CURRENT_BATCH.md; rejected MCP/resource read because prompt protocol requires CLI extraction | Estimated cost: 1200us
- [x] Verify status/rationale hygiene | Justification: missing files mean no stale task data; rejected reading previous batch logs because AGENTS forbids archived batch contamination | Estimated cost: 400us
- [x] Load relevant mandates | Justification: task touches IK, Burst jobs, raycasts, AUP shift, spatial hash, GPU render upload; rejected broad mandate ingest because 2-8 registry selection is required | Estimated cost: 9000us

## Loop 1 - Tasks 1-5
- [x] Task 1: Kinematic Leg S.O.A. | Justification: added `NativeArray<float3> _footPositions` and `_targetFootPositions` with `MaxLegsPerEntity=6`, clamping entities to 4/6 legs; rejected per-leg GameObjects because task requires data-only S.O.A. | Estimated cost: 14us/entity registration, 0.08us/leg read in job
- [x] Task 2: Step Scheduler | Justification: `ProceduralCrabStepSchedulerJob` locks one active step per side before triggering stride-over-threshold legs; rejected free-running gait phases because feet would cross under load. | Estimated cost: 0.35us/entity low-tier, 0.62us/entity high-tier
- [x] Task 3: Parabolic Step Math | Justification: `AdvanceStep` uses XZ lerp and centered `1.0 - (t*t)` lift arc; rejected animation curves because managed curve sampling is not Burst-safe. Prompt re-extracted after task 3. | Estimated cost: 0.09us/stepping leg
- [x] Task 4: Async Ground Raycasts | Justification: ground targets use `RaycastCommand.ScheduleBatch` through `ProceduralCrabGroundRaycastBuildJob`; rejected synchronous `Physics.Raycast` because it would serialize probes on the main thread. | Estimated cost: main-thread enqueue only; physics job cost deferred
- [x] Task 5: Raycast Budgeting Math LOD | Justification: high tier raycasts all active legs, low/MX350 alternates two legs per entity per frame using frame-indexed pairs; rejected random staggering because deterministic budget matters for replay. | Estimated cost: Low/MX350 2 rays/entity/frame, High 4-6 rays/entity/frame
- [x] Compile/check after loop 1 | Justification: `dotnet build Hecton8.Core.csproj --no-restore` reached project compile and reported only pre-existing unrelated errors in survival/arena/tether/thermal files; rejected patching foreign domains because domain file forbids cross-domain edits without critical justification. | Estimated cost: 116000000us wall time

## Loop 2 - Tasks 6-10
- [x] Task 6: AUP Origin Shift Sync | Justification: `OnOriginShift` force-completes pending jobs and runs Burst rebase jobs over entity, foot, target, step, and body pose NativeArrays; rejected lazy next-frame fix because it can stretch legs during the shift frame. Prompt re-extracted after task 6. | Estimated cost: rare-path 0.18us/leg + 0.22us/entity
- [x] Task 7: Analytical 2-Bone IK | Justification: `ProceduralCrabAnalyticalTwoBoneIkJob` uses Law of Cosines to solve hip/knee/foot matrices; rejected FABRIK because prompt forbids iterative crab IK. | Estimated cost: 0.55us/leg high tier
- [x] Task 8: RSQRT/LUT Optimization | Justification: IK path avoids `math.acos` and `math.sqrt`, using `math.rsqrt` for distance and sine reconstruction; rejected trigonometric angle solve because it burns cycles and branches. | Estimated cost: saves roughly 0.2us/leg versus acos/sqrt solve
- [x] Task 9: Body Tilt | Justification: `ProceduralCrabBodyTiltJob` derives body normal with `math.cross(p1-p2, p3-p2)` and aligns body matrix to that normal; rejected Rigidbody-based tilt because visual fake is cheaper and controllable. Prompt re-extracted after task 9. | Estimated cost: 0.28us/entity
- [x] Task 10: S.O.A. to GPU Upload | Justification: solved joint matrices and body poses upload through `GraphicsBufferUploadUtility`, then submit via `Graphics.RenderMeshIndirect`; rejected crab GameObject renderers because prompt forbids them. | Estimated cost: one memcpy per buffer + one indirect draw
- [x] Compile/check after loop 2 | Justification: second `dotnet build Hecton8.Core.csproj --no-restore` reported only unrelated compile errors in survival, manta scooter, and tether domains; rejected foreign edits under domain boundary. | Estimated cost: 50700000us wall time

## Loop 3 - Tasks 11-15
- [x] Task 11: Death State | Justification: `SetEntityPose` latches `CorpseState`, raycasts stop, step job zeroes stepping and collapses foot Y to root height; rejected ragdoll/joint simulation because corpse pose only needs static visual settlement. | Estimated cost: 0.04us/leg while corpse is active
- [x] Task 12: Spatial Hash Avoidance | Justification: added `SetSpatialHashAvoidance` native snapshot input for Eco-Director spatial hash separation, applied to target feet in Burst; rejected direct managed spatial-hash calls inside jobs. Prompt re-extracted after task 12. | Estimated cost: 0.03us/raycasted leg
- [x] Task 13: Zero-GC IJobParallelFor | Justification: multi-leg solve path is persistent NativeArrays plus `IJobParallelFor` jobs for raycast build, target resolve, step, body tilt, IK, and AUP rebase; cold allocations are registered, hot path allocates 0 bytes by construction. | Estimated cost: 0 B/frame hot path
- [x] Task 14: Reconnaissance Protocol | Justification: ripgrep scan found no `Animator.SetIKPosition` or `OnAnimatorIK` callsites under `Assets/_Project/Scripts`; results logged to `RECON_ANIM_PROCEDURAL_BEHAVIOR.md`. | Estimated cost: 1700000us wall time
- [x] Task 15: Omega Compile Check | Justification: static scan found no `Transform`, `.transform`, `GetComponent`, `FindObject`, or `GameObject` in `ProceduralCrabLegIKRuntime.cs`, and no `math.acos`/`math.sqrt`/`Physics.Raycast`; prompt re-extracted after task 15. | Estimated cost: 1600000us wall time
- [x] Compile/check after loop 3 | Justification: third `dotnet build Hecton8.Core.csproj --no-restore` reported only unrelated `HectonSurvivalSystem` missing type; no crab runtime errors surfaced. | Estimated cost: 83400000us wall time

## Loop 4 - Self-Review
- [x] Re-read implementation for GC allocations, Transform access in jobs, raycast sync, NativeArray lifetime, and public API drift | Justification: scanned and re-read runtime; patched allocated-capacity drift and zero-vector IK direction fallback; cold `new` sites are allocation/init/dump only, no hot-path managed allocation. | Estimated cost: 9400000us wall time
- [x] Compile/check after loop 4 | Justification: first build attempt timed out at 120s; single-worker rerun completed and reported only pre-existing `HectonSurvivalSystem` missing `SurvivalPhysiologyScalarResult`; no crab runtime errors. | Estimated cost: 58280000us wall time

## Loop 5 - Omega Polish
- [x] Read POLISH_MANDATE only after all tasks are done or blocked | Justification: core tasks 1-15 and self-review were checked before reading `<POLISH_MANDATE id="OMEGA_POLISH">`; rejected early polish parsing because AGENTS forbids it. | Estimated cost: 1100000us wall time
- [x] Execute anti-bloat inquisition | Justification: replaced entity active/corpse boolean checks in Burst jobs with `StateFlags` bitmasks, confirmed no `foreach`, `string.Format`, interpolation, `ToString`, `math.sqrt`, `math.normalize`, `math.acos`, sync raycasts, Animator IK, or Transform access in runtime. | Estimated cost: 3400000us wall time
- [x] Append final report to Docs/AgentLogs/LOG_ANIM_PROCEDURAL_BEHAVIOR.md | Justification: final report appended with wrong/done/cheats/microseconds/build blocker/diff summary; status remains PENDING VERIFICATION because project compile is blocked by unrelated survival domain error. | Estimated cost: 2400000us wall time

## Loop 6 - Honest R&D AAA Upgrade
- [x] Fix body-relative ground probe drift | Justification: raycast origins now derive from rotated leg home positions plus a small velocity lead instead of stale target-foot XZ; rejected probing from old foot targets because moving crabs could drag/freeze feet behind the body. Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`. | Estimated cost: 0.04us/leg for home-vector rotate + velocity add; reduces visible slide under acceleration
- [x] Fix rear-leg scheduler starvation | Justification: added per-side round-robin gait cursors in `ProceduralCrabLegEntityState`; rejected first-match leg scans because front legs can monopolize each side lane under continuous movement. | Estimated cost: 0.03us/entity cursor math; improves six-leg coverage without random gait state
- [x] Unity script validation and static audit | Justification: Unity MCP `validate_script` returned 0 errors/0 warnings after the R&D patch; static audit found no Animator IK, sync raycasts, Transform access, managed search calls, string formatting, or forbidden math in the runtime. | Estimated cost: 6648800us validation wall time
- [x] Full build gate re-run | Justification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` reached project compile and failed on unrelated missing core/platform symbols (`HectonPersistentPathPolicy`, `HardwareTierDetector`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, `HapticWaveformLibrary`); rejected editing those domains because this task is fauna procedural IK. | Estimated cost: 148700000us wall time

## Loop 7 - Honest R&D Contact Safety
- [x] Clear stale grounded state on missed probes | Justification: `ProceduralCrabGroundTargetResolveJob` now clears `IsGrounded` when a budgeted raycast misses; rejected sticky last-known grounded state because terrain holes or SDF misses would let stale targets drive steps. Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`. | Estimated cost: 0.01us/budgeted leg
- [x] Suppress new steps into invalid targets | Justification: `ProceduralCrabStepSchedulerJob` now refuses to trigger a new step when `IsGrounded == 0`; rejected stepping toward old targets because it produces foot snaps at terrain edges. | Estimated cost: one byte branch per candidate leg
- [x] Clamp spatial-hash foot avoidance | Justification: added `_maxAvoidanceFootOffset` and `SpatialHashAvoidanceMaxOffset`; avoidance offsets now clamp with `rsqrt` in Burst so external separation spikes cannot teleport foot targets. | Estimated cost: 0.03us/raycasted leg when clamp is active
- [x] Validate patched script and audit console | Justification: Unity MCP `validate_script` returned 0 errors/0 warnings for the crab runtime; static audit found no forbidden IK/runtime patterns. Unity console remains red from unrelated `NativeArenaArrayEditTests`, `SaveBinaryStorage` Burst catch-filter, and MCP regex timeout errors; full build was not re-run because another dotnet build process is active and global compile is already blocked outside fauna IK. | Estimated cost: 8802600us validation wall time
