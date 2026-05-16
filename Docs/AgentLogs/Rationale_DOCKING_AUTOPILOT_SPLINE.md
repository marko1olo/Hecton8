# Rationale_DOCKING_AUTOPILOT_SPLINE

## Decision 1 - Accept Injected Batch Prompt

Problem: Earlier disk state lacked `DOCKING_AUTOPILOT_SPLINE`, but the user supplied the prompt and `Docs/Tasks/CURRENT_BATCH.md` now contains the exact XML block.

Solution: Supersede the previous missing-prompt blocker, re-extract the XML by CLI, and use `DOCKING_AUTOPILOT_SPLINE | PHYSICS/VEHICLES | 18` as the active assignment.

Rejected Alternatives: Continuing with the stale blocker was rejected because it no longer matches disk evidence. Synthesizing beyond the XML was rejected; Phase 1 stays constrained to tasks 1-3.

Scalability potential: Low/Middle/High/Ultra behavior is deferred to later tasks, but Phase 1 must preserve the boundary for math LOD and current compensation.

Hardware Impact: 0 us/frame so far. This decision only corrects task authority.

## Decision 2 - Domain Bridge Is Necessary

Problem: The XML domain names `Assets/_Project/Scripts/Physics/Vehicles/Automation/`, while the current repo already has vehicle automation contracts in `Assets/_Project/Scripts/Vehicles/Automation/` and docking handlers in `Assets/_Project/Scripts/Construction/`.

Solution: Place the new docking autopilot source in the XML path `Assets/_Project/Scripts/Physics/Vehicles/Automation/` while keeping the namespace `Hecton8.Vehicles.Automation` compatible with existing docking signals. Touch `GlobalRegistry`, `GlobalDataVault` enum definitions, `Hecton8.Core.csproj`, and `VehicleDockingModule` only as cross-domain integration points required by Phase 1 and dotnet verification.

Rejected Alternatives: Creating a parallel orphan automation tree was rejected because it would split namespaces and leave existing handlers on the old path. Moving existing files was rejected because it is out-of-scope and unsafe with 20+ parallel agents.

Scalability potential: A central service/data buffer lets low-tier 10 Hz solve and high-tier overkill smoothing be layered without rewriting handler ownership.

Hardware Impact: Expected Phase 1 runtime cost is neutral-to-lower on i3/MX350 by replacing repeated object-space interpolation with cached spline state and service snapshots. Static estimate: ~2-4 us saved per active dock versus old linear/nlerp path, 0 B/frame.

## Decision 3 - Mandate Set For Phase 1

Problem: Docking spans movement authority, physics scheduling, service lifetime, data ownership, and future telemetry. Editing only the visible Lerp call would preserve architectural rot.

Solution: Apply these mandates: `CORE_Submarine_Vehicles_Kinematics_AUP`, `PHYS_Physics_Integrity_Determinism_ForceMode`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `MATH_AUP_Determinism_Sync`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `ARCH_Execution_Phases`, and `CORE_Weather_Abyssal_FlowField_Currents`.

Rejected Alternatives: Unity `AnimationCurve`, `Vector3.Lerp`, `Quaternion.Slerp`, direct `Transform.position` authority, and singleton polling were rejected by prompt and mandates.

Scalability potential: Low: fixed cheap Bezier sample; Middle: service-buffered spline state; High: current-aware tangent and motion vectors; Ultra: later Hermite/visual overkill without changing authority data.

Hardware Impact: Target is 0 B/frame and sub-0.1 ms. Any additional cost must stay in cold registration or job-friendly value math.

## Decision 4 - Phase 1 Spline Authority

Problem: `VehicleDockingModule` advanced docking by linearly interpolating AUP position and nlerping rotation. That produced game-object motion and kept docking authority local to the handler.

Solution: Build `ActiveSplineData` once at capture using double3 P0-P3 control points. During `FixedTick`, evaluate cubic Bernstein position and derivative tangent, convert the double-precision result back through AUP, and rotate with guarded `Quaternion.LookRotation` from tangent/up. Cache `IDockingAutopilotService` outside the hot path and mirror active data into a `GlobalDataVault` buffer when the service exists.

Rejected Alternatives: `Vector3.Lerp`, `Quaternion.Slerp`, local nlerp, `AnimationCurve`, per-frame `GlobalRegistry.TryGet`, and local `NativeArray` ownership were rejected. The standard Unity approach is too easy to desync across origin shifts and too opaque for later Burst/job telemetry.

Scalability potential: Low/MX350: one cubic sample per fixed docking step and vault slot writes only while active. Middle: cached service buffer for AI/drone consumers. High: later current compensation and STP vectors can read the same spline state. Ultra: Hermite/zero-jerk polishing can swap progress shaping without changing P0-P3 authority.

Hardware Impact: Estimated gain on i3/MX350 is ~2-4 us per active dock and 0 B/frame. The main win is not raw math; it is removing singleton/search risk and keeping spline state blittable for jobs.

## Decision 5 - Compile Result Classification

Problem: Full `dotnet build Assembly-CSharp.csproj` and focused `dotnet build Hecton8.Core.csproj --no-restore` cannot finish green in the current shared worktree.

Solution: Fix the docking-specific compile surface by adding the new source to `Hecton8.Core.csproj`, then classify remaining errors as external dependencies: `Hecton8.VFX.Wakes` contracts missing, screen-space light shaft contracts missing, and `EcosystemDirector` not matching new `IEcosystemDirectorService` methods.

Rejected Alternatives: Reverting unrelated agent work or editing VFX/ecosystem domains was rejected as out-of-scope sabotage. Reporting build success was rejected because the compiler is objectively red.

Scalability potential: Not impacted by the external compile blockers. Docking code remains value-type and vault-backed.

Hardware Impact: 0 us/frame. This is integration evidence only.

## Decision 6 - Burst Kernel Without Scheduling Policy

Problem: Phase 2 requires a Burst cubic solver and tangent math, but the physics mandate forbids adding a same-frame `Schedule().Complete()` trap or inventing a dispatcher policy inside the docking service.

Solution: Add `CubicBezierJob : IJobParallelFor` as a pure kernel over `NativeArray<ActiveSplineData>`, `NativeArray<float>` progress lanes, and `NativeArray<DockingSplineSample>` output. The job calls the same Bernstein evaluator used by the scalar path and emits derivative tangent for LookRotation consumers. Scheduling remains the responsibility of a future dispatcher/autopilot system.

Rejected Alternatives: Running all spline batches on the main thread only, adding a hidden immediate-complete job path, or using Unity `AnimationCurve` were rejected. Float3 control points were rejected because AUP-scale docking would warp far from origin.

Scalability potential: Low/MX350: scalar or small batched solve at low active counts. Middle: jobified 64-slot vault lane. High: downstream current compensation can add a second vector lane. Ultra: later Hermite smoothing can write a different progress lane while the solver remains unchanged.

Hardware Impact: Estimated cost target is under 0.1 ms for the 64-slot batch on i3/MX350 and 0 B/frame. The job avoids managed dispatch decisions and only carries blittable data.
