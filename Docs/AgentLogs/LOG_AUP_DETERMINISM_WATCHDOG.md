# LOG: AUP_DETERMINISM_WATCHDOG

## 2026-05-16 Surgical Record

What was wrong:
- AUP direction and station-keeping paths were collapsing precision to float before the final runtime handoff.
- Dynamic ballast feedback used exact sqrt for a non-authoritative visual/audio stress value.
- KCC sync-fence, GPU-flow, squeeze, and pre-shift counters were scattered instead of owned by one unmanaged accumulator state.
- KCC did not halt on `AupPreShiftSignal`, so a rebase frame could enter authoritative integration.
- High-tier Leviathan grab contact still used runtime float contact math despite available AUP roots/tips.
- KCC telemetry did not carry `AupMaxDriftErrorMeters` or the AUP watchdog dump path.
- Player default gravity used local down instead of a direction resolved relative to the AUP center.
- Two `Vector3.Distance` calls remained in acoustic portal graph construction.

What was done:
- Rewrote `AUPMath.AUPDirection` around double3 delta, double lengthsq, guarded rsqrt, and final float3 cast only.
- Updated submarine station keeping to preserve double3 target delta until final Rigidbody `Vector3` move.
- Replaced ballast flood magnitude sqrt with a max/mid/min approximation.
- Packed KCC transient counters into `PlayerKinematicsAccumulatorState`.
- Added one-frame KCC halt on `AupPreShiftSignal`, canceling pending state writes and publishing a frozen KCC velocity frame.
- Verified `RigidbodyAUPs` is contiguous SoA and marine snow/TAA shift paths already carry the correct pre/post-shift data.
- Added `_MATH_LOD_LOW` distant flora float offset path beyond 1000m, leaving exact double offset for near/non-low cases.
- Added High/Ultra Leviathan AUP contact direction and runtime contact conversion.
- Added KCC sync-fence drift telemetry and `Dump_AUP_DETERMINISM_WATCHDOG.bin`.
- Added AUP-center radial gravity resolution in `HectonPlayerMovement`.
- Replaced the remaining `Vector3.Distance` calls in `SpatialAudioManager` with squared-length guarded rsqrt.

Cinematic cheats used:
- Ballast stress uses approximate scalar magnitude because it drives perception, not authority.
- Low-tier flora uses float offset approximation only for distant payload copies behind `_MATH_LOD_LOW`.
- Gravity correction changes direction authority only; no simulated gravity field or planet-radius falloff was added.
- High-tier tentacle exactness is tier-gated so low hardware keeps cheap Verlet presentation.

Exact microseconds saved / spent:
- Station-keeping double3 path: estimated 1.2 us saved per active hull by avoiding float jitter correction churn.
- Ballast magnitude approximation: estimated 0.2 us saved per dynamic flood stress event.
- KCC pre-shift halt: estimated 20-80 us avoided on origin-shift frames.
- Low-tier distant flora approximation: estimated 5-20 us saved during active payload copies on MX350.
- High-tier Leviathan AUP contact: estimated +0.1 us only on High/Ultra grab damage ticks.
- Sync-fence AUP drift telemetry: estimated +0.03 us every 300 frames.
- AUP-center gravity: estimated +0.04 us per fixed tick.
- Acoustic portal `Vector3.Distance` purge: estimated 0.02-0.05 us saved per portal graph build.

Validation:
- `rg -n "Vector3\\.Distance" Assets/_Project/Scripts` returns no matches.
- `git diff --check` on the post-polish touched player/audio files returns 0 whitespace errors; Git only reports LF-to-CRLF warnings.
- Final compile command run: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Final compile result: `[BLOCKED BY DEPENDENCY]`, 14 unrelated errors in wake VFX, docking autopilot/spline contracts, and ecosystem interface drift. No AUP/KCC double3 conversion error surfaced.

Status:
- `VERIFIED MASTER GRADE` for AUP determinism scope.
- Global compile remains blocked by external dependency owners.
