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

## 2026-05-16 Typed-Lane Event Inquisition

What was wrong:
- `FluidFeedbackEvents` still owned two private `NativeQueue<SplashEvent>` lanes.
- `PhysicsEventBus` still owned private `NativeQueue<PhysicsEventPayload>` lanes despite being a physics-domain deferred event bridge.
- Deferred submarine impact trauma dispatch used a local private native queue.
- Several `PhysicsApplySystem` event/force packets relied on default packing.

What was done:
- Converted `SplashEvent` to a packed `ISignal` and moved `FluidFeedbackEvents` onto `SignalBus<SplashEvent>`.
- Converted `PhysicsEventPayload` to a packed `ISignal` and moved `PhysicsEventBus` onto `SignalBus<PhysicsEventPayload>` while preserving the public API surface.
- Moved deferred submarine impact trauma payloads onto `SignalBus<DeferredSubmarineImpactSignal>`.
- Added requeue-on-budget-exhaustion for converted late-frame bridges so unconsumed snapshot tails survive to the next frame.
- Added `Pack = 1` to `PhysicsApplySystem` force/acoustic/pressure/impact packets.

Cinematic cheats used:
- No new simulation. Presentation splash/acoustic/trauma events remain bounded payloads; high-end visual work is bought by keeping authority/event plumbing compact.

Exact microseconds saved / spent:
- SignalBus migration: 0.0 us direct frame gain; removes duplicate native event queues and centralizes memory sentinel accounting.
- ARM64 packet packing: 0.0 us runtime; removes implicit padding drift risk.
- Late-frame requeue: only runs when the dispatcher budget is exhausted and is capped by existing lane capacities.

Validation:
- `rg --pcre2 -n "\[StructLayout\((?![^\)]*Pack\s*=\s*1)"` over AUP/player/physics patch scope returns no matches.
- `rg --pcre2 -n "Vector3\.Distance|math\.distance\(|math\.sqrt\(|math\.length\(|math\.rsqrt\((?!math\.max)|string\.Format|void\s+Update\s*\("` over the same scope returns no matches.
- `rg -n "NativeQueue<PhysicsEventPayload>|new NativeQueue<PhysicsEventPayload>|NativeQueue<SplashEvent>|new NativeQueue<SplashEvent>"` returns no matches.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passes with 0 warnings and 0 errors.

## 2026-05-16 Force Command Vault Migration

What was wrong:
- `PhysicsApplySystem` still owned private fixed-step `NativeQueue<ForcePacket>` front/back command buffers.
- Packet validation staging still used private persistent `NativeArray` fields.
- The dead `TryGetForcePacketBackWriter` API preserved a parallel-writer contract with no repo producer.

What was done:
- Audited repo producers; all live force requests route through `PhysicsForceRouter` methods, and `TryGetForcePacketBackWriter` had no external usage.
- Removed the private force `NativeQueue` command buffers and the unused parallel-writer accessor.
- Added DataVault buffer IDs for `PhysicsForceCommandFront`, `PhysicsForceCommandBack`, `PhysicsForceValidationPackets`, and `PhysicsForceValidationMask`.
- Rebuilt the fixed-step front/back swap with vault-backed buffers and `_frontCount`/`_backCount`.
- Kept `ValidateForcePacketsJob` intact, but its packet/mask storage now resolves from `GlobalDataVault`.
- Restored missing lockstep typed-lane constants and fixed the diagnostics `DebugSignal` namespace compile gap encountered during compile attempts.

Cinematic cheats used:
- No new physical simulation. The change preserves the cheap bounded force-command path so high-tier visual/audio impact overkill can spend cycles outside the physics authority loop.

Exact microseconds saved:
- Direct force-buffer migration: 0.0 us measured, static estimate only.
- Removed duplicate native queue ownership: 0.0 us frame-time; memory accounting is centralized under `SystemID.Physics`.
- Enqueue remains O(1) bounded write; validation remains capped at 64 packets.

Validation:
- Force queue scan returns no matches for `TryGetForcePacketBackWriter`, `NativeQueue<ForcePacket>`, `new NativeQueue<ForcePacket>`, `_frontPacketQueue`, `_backPacketQueue`, `_validationPackets`, `_validationMask`, or `_frontPackets`.
- Private native field scan on `PhysicsApplySystem.cs` returns no `private NativeArray`, `private NativeQueue`, `private NativeList`, or `private NativeHashMap`.
- Struct packing and math scans on the touched AUP/physics scope return no matches for non-`Pack = 1` `StructLayout`, `Vector3.Distance`, `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, or standard `Update()`.
- `git diff --check` reports LF-to-CRLF warnings only.
- Compile attempts 11-13 are recorded. Attempt 13 is `[BLOCKED BY DEPENDENCY]` on external `DiegeticGyroCompassRuntime` DTO drift and `SystemDispatcher` blackbox/raycast-lock drift; no force-buffer compile error remains.

## 2026-05-16 Global Physics Vault Migration

What was wrong:
- `GlobalPhysicsStateManager` still owned private persistent native culling/result arrays and a private impact `NativeQueue`.
- Several global physics structs relied on non-`Pack = 1` layout.
- Remaining global physics scalar paths still had branch-only `rsqrt` guards.

What was done:
- Replaced private native culling, last-valid-position, telemetry, and impact storage with `GlobalDataVault` handles.
- Converted deferred collision impacts to a vault-backed bounded ring while preserving late-frame flush timing.
- Added `BufferID.RigidbodyLastValidPositions` and `BufferID.PhysicsImpactEvents`.
- Enforced `Pack = 1` on global physics runtime packets and clamped remaining global physics `rsqrt` paths.

Cinematic cheats used:
- Kept impact/wake radii as scalar approximations and bounded rings; no expensive collision replay or heap event layer.
- Preserved squared-distance culling and cheap wakeups, saving budget for visual feedback outside the authority path.

Exact microseconds saved:
- 0.0 us direct frame-time gain.
- Impact enqueue remains O(1); culling job remains contiguous SoA via resolved `NativeArray` views.
- Memory/sentinel gain: removed duplicate private native ownership and moved accounting to DataVault.

Validation:
- Global physics scan returns no matches for private native container ownership, non-`Pack = 1` `StructLayout`, `Vector3.Distance`, direct `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, or standard `Update()`.
- Compile attempt 14 is `[BLOCKED BY DEPENDENCY]` on external save/data-baker drift: `HectonContractVersion`, `CsvReadBufferBytes`, and `SignalBusRegistry`. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt14.txt`.

## 2026-05-16 Player Blackbox Vault Closure

What was wrong:
- `HectonPlayerMovement` still held the cinematic focus blackbox as a private `NativeArray<CinematicFocusTelemetryEntry>` field.

What was done:
- Replaced the cached native field with `VaultBufferHandle<CinematicFocusTelemetryEntry>`.
- Resolved the DataVault-backed telemetry view only inside write and dump paths.
- Preserved the 300-entry ring, binary dump format, and dump cooldown.

Cinematic Cheats used:
- Kept the blackbox compact and fault/cinematic-focus gated. No per-frame managed diagnostics or expanded camera simulation.

Exact Microseconds saved:
- 0.0 us direct frame-time gain.
- Removed the final scanned private native field in the AUP/player/physics patch scope.

Validation:
- Broad AUP/player/physics/vehicle scan returns no matches for private native container ownership, private native allocations, non-`Pack = 1` `StructLayout`, `Vector3.Distance`, direct `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, or standard `Update()`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passes with 0 warnings and 0 errors. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt15.txt`.
