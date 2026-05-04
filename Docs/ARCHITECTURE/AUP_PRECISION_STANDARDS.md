# AUP Precision Standards

Status: REFERENCE
Verification: PENDING VERIFICATION

2026-05-04 current-state boundary:

- This is the AUP/floating-origin standards contract, not proof that every current system obeys it.
- Current project-state orientation starts at `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Known risk classes remain raw `Vector3` caches across origin shifts, presentation-derived gameplay state, and async/job ownership around voxel/physics publication.

Mandates followed:
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Authority

This document is the active standards entry point for Absolute Universe Position precision and floating-origin rebasing. Runtime ownership remains mapped in `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md`.

## Coordinate Contract

- All simulation-scale positions are represented as `AbsoluteUniversePosition`: integer grid sector plus local offset.
- `Transform.position` is presentation/runtime space only.
- Sector deltas are computed as integer values before any floating conversion.
- Local offsets may be accumulated in double precision, but the final runtime handoff narrows to `float3`.
- Cached world-space positions are invalid across a rebase.

## Floating-Origin Rebase Contract

- `HectonFloatingOrigin` is the sole runtime authority for origin shifts.
- Rebase is atomic from the simulation perspective: physics integration is paused before transform/body mutation and resumes only after every origin-shift listener has completed.
- Physics queries across a shift boundary are forbidden.
- Listener discovery must cover all loaded additive scenes before shift completion is acknowledged.
- Async rebase loops must consume the owner cancellation token and abort if the owner is destroyed.

## Physics Contract

- Project physics timestep remains `Time.fixedDeltaTime = 0.02f` (50Hz). Do not mutate it to satisfy headless/server targets.
- `PhysicsApplySystem` owns queued force packet application.
- Origin-shift cleanup must drain or finalize physics packet/body state before simulation resumes.
- Safe teleport uses the floating-origin pause protocol, not ad hoc transform writes during active physics.

## Rendering Boundary

- Headless simulation must not initialize cameras, canvases, render dispatchers, shader warmup, or URP features.
- Renderers may subscribe to origin-shift events, but they cannot own AUP truth.
- GPU/camera-relative matrices are presentation artifacts derived from AUP data.

## Verification Hooks

- `HectonFloatingOrigin.WaitForShiftStabilityAsync(...)` is the read barrier for code that must wait until a rebase is fully stable.
- `HectonFloatingOrigin.IsPhysicsPausedForShift` and `IsShiftInProgress` are diagnostics only; game logic should not poll them as a substitute for the barrier.
- Bootstrap cycle failure must enter BIOS/error reporting rather than continuing with a partial dependency graph.
