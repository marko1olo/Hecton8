# AUP Precision Standards
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current R28 static/tool boundary: AtlasCheck fails `57` RealtimeCSG refs; Mod API static validation now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`). Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the AUP/floating-origin standards contract, not proof that every current system obeys it.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
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
