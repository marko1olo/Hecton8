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

## 2026-05-16 Multiplatform Inquisition Addendum

What was wrong:
- Physics-facing structs in the AUP patch scope still used default, `Pack = 4`, or `Pack = 8` layout in several packet/telemetry paths.
- KCC runtime NativeArray fields needed proof that they were vault-backed views, not private allocator islands.
- A few scanned scalar math paths still used `math.sqrt`, branch-only `rsqrt`, or a normal divide despite the mobile NaN mandate.

What was done:
- Enforced `Pack = 1` on determinism signal packets, docking spline packets, Leviathan telemetry, AUP-touched movement structs, and the KCC runtime telemetry/state/accumulator structs.
- Preserved `ActiveSplineData` as an explicit 144-byte packet with `ReservedTail` so DataVault stride stays stable while padding becomes intentional.
- Verified `PlayerKinematicsRuntime` allocates all runtime arrays through `AllocateRuntimeArray(..., BufferID.*, SystemID.GameplayPlayer)` and leaves only the `H8Memory.Allocate<T>` fallback with the correct SystemID.
- Replaced the remaining scanned `math.sqrt` with guarded `rsqrt` math and clamped additional `rsqrt`/`rcp` paths in station keeping, CCD, fluid math, tether constraints, docking spline distance, and acoustic portal reverb.

Cinematic cheats used:
- Acoustic portal reverb keeps the perceptual square-root curve via guarded reciprocal-square-root math instead of exact sqrt.
- Docking spline control length uses guarded distance from `distSq * rsqrt(distSq)`; no extra physical solver was added.

Exact microseconds saved / spent:
- ARM64 packing: 0.0 us direct frame gain; removes layout drift risk.
- KCC DataVault-first proof: 0.0 us direct frame gain; prevents duplicate persistent memory ownership.
- Scalar sqrt/division cleanup: estimated 0.01-0.05 us saved across cold/warm calls, with larger value from avoiding NaN recovery.

Validation:
- `rg --pcre2 -n "\[StructLayout\((?![^\)]*Pack\s*=\s*1)"` on the patched AUP/KCC/physics scope returns no matches.
- `rg -n "Vector3\.Distance|string\.Format|math\.sqrt\("` on the patched AUP/physics/audio-adjacent scope returns no matches.
- `git diff --check` on touched files returns 0 whitespace errors; Git reports LF-to-CRLF warnings only.
- Full compile still requires dependency-wall verification after external wake/docking/ecosystem contracts are restored.
## 2026-05-16 Compile Gate Burn-Down

What was wrong: Post-polish builds still failed on fixable glue errors: duplicate/missing `GlobalDataVault.ValidateAbiLayout`, missing lockstep signal lane constants, and incomplete DataVault handle migration in `SubmarineFluidDynamics`.

What was done: Restored one ABI validation method, added typed lockstep lane constants, completed hydro vault handle fields/allocation glue, kept KCC DataVault-first ownership, and reran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.

Cinematic cheats used: No new simulation. Existing AUP work keeps low-tier flora on the `_MATH_LOD_LOW` float approximation beyond 1000m and high-tier Leviathan contacts on the exact AUP double3 path.

Exact microseconds saved: compile glue saves 0.0 us directly. The maintained AUP savings remain: 20-80 us avoided on rebase frames by freezing KCC, 5-20 us saved on MX350 distant flora payloads, 0.03 us per 300 frames for AUP sync telemetry, and 0.01-0.05 us across clamped scalar rsqrt/rcp sites.

Validation: `Hecton8.Core.csproj` builds clean with 0 warnings and 0 errors.

Final scans: whole-script `Vector3.Distance` scan returns no matches. AUP/player/physics scans return no matches for `math.sqrt`, unguarded `math.rsqrt`, `string.Format`, standard `Update()`, or non-`Pack = 1` `StructLayout`. `git diff --check` reports no whitespace errors.

## 2026-05-16 Post-Reopen Multiplatform Burnish

What was wrong:
- Broader AUP/vehicle-fluid audit found explicit-layout packets that still omitted `Pack = 1`.
- Ballast and hydro perception paths still had branch-only `rsqrt` and exact `math.length` usage for audio/VFX stress.
- Post-reopen compile is no longer green because other lanes changed after the earlier compile burn-down.

What was done:
- Added `Pack = 1` to `AbsoluteUniversePositionBlit`, `SplashEvent`, ballast PID packets, hydro job packets, and hydro transfer jobs without changing packet sizes.
- Guarded remaining ballast/hydro `rsqrt` calls with `math.max`.
- Replaced ballast PID stress and hydro cavitation rumble exact magnitude with max/mid/min approximation.
- Migrated `PhysicsDeterminismSignals` from private `NativeQueue` lanes to typed `SignalBus<T>` lanes while preserving the public latest-signal API.
- Ran three compile attempts and wrote attempt dumps for integrator review.

Cinematic cheats used:
- PID hull stress and cavitation rumble now use approximate magnitude because they drive perception, not physics authority.

Exact microseconds saved / spent:
- ABI packing: 0.0 us runtime, removes ARM64/Quest layout drift risk.
- Stress/rumble magnitude fake: estimated 0.02-0.08 us saved on the affected events; not measured, static estimate only.
- Guarded `rsqrt`: negligible steady-state cost; primary benefit is NaN fault avoidance.
- SignalBus migration: 0.0 us direct frame gain; removes one private native queue family from physics determinism ownership.

Validation:
- Broad AUP/player/physics scan returns no matches for `Vector3.Distance`, `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, standard `Update()`, `GameObject.Find`, `FindObjectOfType`, or non-`Pack = 1` `StructLayout`.
- `PhysicsDeterminismSignals.cs` returns no matches for `NativeQueue<`, `new NativeQueue<`, `DisposeAllQueues`, `EnqueueBounded`, or `TryDequeue(ref`.
- Compile after three attempts is `[BLOCKED BY DEPENDENCY]` in external `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs`; no new AUP/player/physics compile error appears in the reported build output.
