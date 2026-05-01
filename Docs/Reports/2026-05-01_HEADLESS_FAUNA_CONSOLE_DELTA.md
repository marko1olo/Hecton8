# Headless Fauna / Console Delta - 2026-05-01

Status: `PENDING VERIFICATION`

## Mandates Followed

- `.agents-skills/AI_Creature_Cognition_States.txt`
- `.agents-skills/AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

## What Was Wrong

Two active truth problems existed in the reports:

- `DOOMSDAY_FLAW_REPORT.md` still claimed `FaunaBrain.UpdateBioluminescentHypnosis()` used `runtimeContext.PlayerCamera` as gameplay truth.
- Current `Editor.log` after the latest compile success contained two `CS0414` warnings for `HectonMapMagicVegetationBridge.scatterSnapRaycastElevationMeters` and `scatterSnapRaycastDistanceMeters`.

## Source Recheck

Current `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` no longer matches the original camera-dependent finding.

Current `UpdateBioluminescentHypnosis()` source path:

- gets `PlayerRuntimeContext` through `PlayerRuntimeContextService.TryGetActiveRuntimeContext(...)`;
- requires `PlayerMovement`, not `PlayerCamera`;
- reads `runtimeContext.LookState`;
- checks `PlayerRuntimeSnapshotFlags.HasPlayerRoot`;
- computes dazzle direction from `PlayerLookState.EyePosition` and `PlayerLookState.AimForward`;
- falls back to `runtimeContext.MovementState.Forward` for normalization;
- applies gameplay pull through `PlayerMovement.ApplyFaunaHypnosisPull(...)`.

Current `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs` defines `PlayerLookState` as a blittable headless-safe gaze snapshot:

- `float3 EyePosition`
- `float3 AimForward`
- `uint Flags`

This fixes the specific "no camera means no dazzle gameplay" source defect. It does not prove headless runtime correctness because no Play Mode/headless test was executed.

## Console Hygiene Change

`HectonMapMagicVegetationBridge` keeps two serialized scatter snap budget fields that were no longer referenced after the placement path moved to resident terrain-cache sampling.

Change applied:

- normalize `scatterSnapRaycastElevationMeters` in `Awake`;
- normalize `scatterSnapRaycastDistanceMeters` against the elevation value in `Awake`;
- do not add a physics raycast fallback;
- do not delete serialized fields.

Reason: deleting serialized fields would churn scene/prefab data, while reintroducing per-placement physics raycasts would violate the current terrain-cache placement direction.

## Remaining Headless Risk

Do not claim fauna headless is complete.

Still open by source review:

- `FaunaSensorSuite` still uses player `Transform` and Rigidbody references for perception and distance gating.
- `FaunaBrain` still has presentation-adjacent paths such as camera shake through `CameraJuiceSystem`.
- No headless test exists proving dazzle-capable fauna applies the gameplay pull with no `Camera` component.

## Regression Model

CPU: no hot-path work added. Awake-time scalar clamps only.

GC: no managed allocations added in hot paths.

Memory: no native memory, scene object, prefab, or asset lifecycle changed.

Cadence: no tick order or dispatcher lane changed.

Correctness: stale camera-specific report finding is downgraded by current source evidence. Runtime correctness remains pending until a headless/Play Mode test exists.

STATUS: PENDING VERIFICATION
