# SUBMARINE OS MANUAL
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
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this manual as current runtime truth.
- This document is a submarine diagnostic/display contract, not proof that power, atmosphere, audio alarms, brownout visuals, or UI terminals are fully scene-wired.
- Re-open `HectonSubmarineOS`, display owners, power telemetry, and atmosphere owners before surgery.

## Scope

`HectonSubmarineOS` is the submarine-wide diagnostic owner.

Responsibilities:
- Consume aggregate power telemetry from `PowerGridTelemetryEvents`.
- Sample room oxygen and pressure from `SubmarineAtmosphereSystem`.
- Resolve one emergency level for the vessel.
- Push diegetic HUD log requests to `HectonSubmarineOsDisplay`.
- Apply physical brownout visuals during sustained power starvation.
- Trigger direct audio alarms for danger and evacuate states.

`HectonSubmarineOsDisplay` is the diegetic operator-facing terminal.

Responsibilities:
- Render emergency status.
- Render subsystem bit icons.
- Render power / oxygen / pressure metrics.
- Maintain a zero-alloc 16-line circular terminal log.

`SubmarineStationKeepingController` is the fixed-step AUP hold controller.

Responsibilities:
- Hold target position in Absolute Universe Position space.
- Hold target attitude.
- Compensate for sampled abyssal current before drift accumulates.
- Queue acceleration packets through the gameplay physics router.

## Emergency Levels

Level `0` `Nominal`
- No low-power lockout.
- No pressure-high state.
- Oxygen above critical thresholds.
- No fatal implosion latch.

Level `1` `Caution`
- Entered when low-power mode is active.
- Entered when compartment pressure exceeds the caution threshold.

Level `2` `Danger`
- Entered when life support is critical.
- Entered when normalized power falls to `<= 0.10`.
- Entered when room pressure reaches the danger threshold.
- Triggers direct diegetic danger alarm behavior.

Level `3` `Evacuate`
- Entered when oxygen falls to `<= 0.05`.
- Entered when fatal implosion is latched.
- Triggers `ABANDON SHIP` through `GlobalRegistry.Audio`.

## Brownout Triggers

Primary low-power mode:
- Engage threshold: `PowerNormalized < 0.20`
- Release threshold: `PowerNormalized >= 0.24`
- Effects:
- `Fabricator.SetEmergencyPowerLockAll(true)`
- `BaseModule.SetAmbientLightsBrownout(true)`

Cascading brownout:
- Engage when aggregate `SupplyRatio < 0.40`
- Holds only while the grid brownout tier is `EssentialOnly` or worse, or normalized power remains below `0.40`
- Effects:
- Interior point lights forced to `15%` of cached base intensity
- Emissive materials pulsed toward warning red with `Mathf.Sin(Time.time * 8f)`

Visual pulse equation:

```csharp
float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 8f));
Color pulsedEmission = Color.Lerp(baseEmissionColor, BrownoutEmissiveColor, pulse);
```

## Cinematic Station Keeping

Control space:
- Position target is stored in AUP (`double3`).
- Runtime hull pose is converted from `worldCenterOfMass` into AUP every fixed step.
- Hold mode is a deterministic cinematic lock, not a physical controller.

Linear lock:

```csharp
double3 currentAbsolutePosition = AbsoluteUniversePosition
    .FromRuntimePosition(_hullRigidbody.worldCenterOfMass)
    .ToAbsoluteDouble3();

float3 offsetToTarget = (float3)(_targetAbsolutePosition - currentAbsolutePosition);
Vector3 targetRuntimePosition = _hullRigidbody.position + (Vector3)offsetToTarget;

_hullRigidbody.linearVelocity = Vector3.zero;
_hullRigidbody.angularVelocity = Vector3.zero;
_hullRigidbody.MovePosition(Vector3.MoveTowards(
    _hullRigidbody.position,
    targetRuntimePosition,
    positionLockSpeedMetersPerSecond * fixedDeltaTime));
```

Angular lock:
- `Quaternion.RotateTowards` moves toward the stored hold rotation.
- No integral, derivative, water-current feed-forward, or force routing is used.

## Zero-Alloc BIOS Terminal

Storage layout:
- `char[16][64]` committed history ring
- `char[64]` active typing buffer
- `char[1104]` flattened TMP render buffer

Behavior:
- New log requests are priority-sorted.
- The active line types out character-by-character.
- When finished, the line is copied into the current ring slot.
- The write index wraps modulo `16`.
- TMP receives only cached char buffers through `SetCharArray`.

Dynamic message formatting:
- Power warnings append formatted percentages via `Span<char>` and `TryFormat`.
- Oxygen warnings append formatted percentages via `Span<char>` and `TryFormat`.
- Hull pressure warnings append formatted `kPa` integers via `Span<char>` and `TryFormat`.

## Failure Modes

- Shared-material emissive pulsing can affect every renderer using the same material asset, not just submarine hull visuals.
- If aggregate telemetry is stale, the OS falls back to service-level normalized power and atmosphere sampling.
- If `GlobalRegistry.Audio` is unavailable, the OS cannot satisfy the evacuate bypass requirement.

## Verification Notes

- Script-level validation must pass for:
- `HectonSubmarineOS.cs`
- `HectonSubmarineOsDisplay.cs`
- `SubmarineStationKeepingController.cs`

- Unity console must still be checked separately.
- Script validation alone is not evidence for a green editor console.
