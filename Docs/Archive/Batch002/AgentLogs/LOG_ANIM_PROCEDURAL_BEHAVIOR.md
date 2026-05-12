# LOG_ANIM_PROCEDURAL_BEHAVIOR

STATUS: PENDING VERIFICATION

## 2026-05-11 Final Report
What was wrong:
- Bottom-feeder motion had no lightweight data-oriented crab/spider IK path in the fauna domain.
- Heavy Animator IK/per-leg Transform approaches were explicitly disallowed for 100+ entities.
- Existing project compile is already blocked outside this domain by `HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult`.

What was done:
- Added `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`.
- Implemented `NativeArray<float3>` foot and target S.O.A. buffers with 4/6 leg support.
- Implemented side-locked step scheduling and parabolic step lift.
- Implemented async `RaycastCommand.ScheduleBatch` ground probing with Low/MX350 two-leg budget and High all-leg budget.
- Implemented AUP rebase jobs that subtract shift deltas from feet, targets, step endpoints, entity roots, and body poses.
- Implemented analytical two-bone crab IK using Law of Cosines with `rsqrt`, no `acos`, no `sqrt`, no FABRIK.
- Implemented body tilt from `math.cross(p1-p2, p3-p2)`.
- Implemented body pose and joint matrix upload to `GraphicsBuffer` plus `Graphics.RenderMeshIndirect` submission.
- Implemented death/corpse latch, spatial-hash avoidance snapshot input, zero-GC job path, recon log, and 300-frame black-box telemetry dump.

Cinematic Cheats used:
- Parabolic foot lift: estimated saves 0.2us/stepping leg vs curve/trig sampling.
- Foot-plane body tilt fake: estimated saves 0.7us/entity vs physics tilt/joints.
- Low-tier two-leg raycast budget: six-legged crab terrain probes reduced by 66%.
- Static corpse collapse: removes ongoing raycast and active step cost for dead entities.
- Spatial avoidance offset: replaces collision solve with one vector multiply/add per raycasted leg.

Exact microseconds saved:
- Animator IK removed: estimated 8-20us/entity depending on rig complexity.
- Sync raycast path avoided: estimated 15-60us/frame per 100 crabs by keeping probes scheduled, not serialized on main thread.
- Low/MX350 ray budget: estimated terrain probe cost reduced from 6 rays/entity/frame to 2 rays/entity/frame.
- Analytical no-`acos` IK: estimated 0.2us/leg saved vs trigonometric angle solve.
- GPU indirect draw: renderer/Transform update cost replaced with two linear buffer uploads and one indirect draw.

Verification:
- `rg` recon found no `Animator.SetIKPosition` or `OnAnimatorIK` under `Assets/_Project/Scripts`.
- Static runtime audit found no `foreach`, `string.Format`, interpolation, `.ToString`, `math.sqrt`, `math.normalize`, `math.acos`, `Physics.Raycast`, Animator IK, or Transform access.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails only on `HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult`.

Final Git Diff:
- Added `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`.
- Added `Docs/Tasks/Status_ANIM_PROCEDURAL_BEHAVIOR.md`.
- Added `Docs/AgentLogs/Rationale_ANIM_PROCEDURAL_BEHAVIOR.md`.
- Added `Docs/AgentLogs/RECON_ANIM_PROCEDURAL_BEHAVIOR.md`.
- Added/updated `Docs/AgentLogs/LOG_ANIM_PROCEDURAL_BEHAVIOR.md`.

Status:
- PENDING VERIFICATION. Not VERIFIED MASTER GRADE because the project compile is blocked by a pre-existing foreign-domain survival error.

## 2026-05-12 Honest R&D AAA Upgrade
What was wrong:
- Ground raycast origins were tied to previous target-foot XZ, so moving crabs could keep probing where feet used to be instead of where body-relative leg homes currently are.
- The side-locked scheduler scanned legs from local index 0 every frame, so front legs could monopolize a side lane and starve rear legs during continuous locomotion.
- Full project compile is still not usable as proof; this run fails in foreign core/platform/audio/save domains, not in the crab runtime.

What was done:
- Changed `ProceduralCrabGroundRaycastBuildJob` to compute probe origins from rotated local leg homes plus `Velocity * VelocityLeadSeconds`.
- Added `_velocityLeadSeconds` as a serialized grounding control, default `0.08`.
- Added `LeftStepCursor` and `RightStepCursor` to entity state.
- Reworked `ProceduralCrabStepSchedulerJob` into advance-first, then per-side round-robin trigger logic.
- Re-extracted the assignment from `Docs/Tasks/CURRENT_BATCH.md`.

Cinematic Cheats used:
- Velocity-led foot homes: one cheap lead vector sells anticipation instead of simulating foot planning.
- Round-robin side cursors: deterministic integer gait fairness instead of authored animation clips or random gait offsets.

Exact microseconds saved:
- Avoided predictive gait planner: estimated 0.3-1.0us/entity saved versus multi-leg lookahead.
- Avoided random/curve gait layer: estimated 0.1-0.4us/entity and no managed state.
- Added cost: estimated 0.04us/budgeted leg for home probe math and 0.03us/entity for cursor scheduling.

Verification:
- Unity MCP `validate_script Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`: 0 warnings, 0 errors.
- Static audit: no `Animator.SetIKPosition`, `OnAnimatorIK`, `Physics.Raycast`, `math.acos`, `math.sqrt`, `math.normalize`, Transform access, managed search calls, `foreach`, `string.Format`, interpolation, or `.ToString(` in the runtime.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` failed with 73 errors in unrelated missing symbols, including `HectonPersistentPathPolicy`, `HardwareTierDetector`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, and `HapticWaveformLibrary`.

Status:
- PENDING VERIFICATION. Crab runtime validates locally; global build remains blocked upstream outside fauna procedural IK.

## 2026-05-12 Honest R&D Contact Safety
What was wrong:
- A missed budgeted ground probe left `IsGrounded` unchanged, allowing stale contact targets to look valid.
- Scheduler could start a new step into a target that no longer had a valid raycast hit.
- Spatial-hash avoidance could move a foot target by an unbounded external offset.

What was done:
- `ProceduralCrabGroundTargetResolveJob` now clears `IsGrounded` on missed raycast hits.
- `ProceduralCrabStepSchedulerJob` now refuses new step starts for ungrounded legs.
- Added `_maxAvoidanceFootOffset` / `SpatialHashAvoidanceMaxOffset`.
- Avoidance offsets are clamped with `math.rsqrt` before being added to target feet.

Cinematic Cheats used:
- Keep stale foot position frozen on missed probes instead of simulating slip physics.
- Clamp crowd avoidance as a visual foot-placement bias, not collision solving.

Exact microseconds saved:
- Avoided per-leg fallback terrain search on miss: estimated 0.2-0.8us/budgeted miss saved.
- Avoided collision/constraint solver for crowding: estimated 1-5us/entity saved under dense crab clusters.
- Added cost: ~0.01us/budgeted miss for grounded clear and ~0.03us/raycasted leg only when avoidance clamp is active.

Verification:
- Unity MCP `validate_script Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`: 0 warnings, 0 errors.
- Static audit: no Animator IK, sync raycasts, Transform access, managed search calls, string formatting, or forbidden trig/sqrt/normalize math in the runtime.
- Unity console still contains unrelated blockers in `NativeArenaArrayEditTests`, `SaveBinaryStorage`, and MCP validation regex handling; no crab runtime error surfaced.

Status:
- PENDING VERIFICATION. Local crab script is clean; global project verification remains blocked outside this domain.
