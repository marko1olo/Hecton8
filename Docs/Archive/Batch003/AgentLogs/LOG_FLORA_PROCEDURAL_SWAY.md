# LOG_FLORA_PROCEDURAL_SWAY

## 2026-05-12 - Flora Procedural Sway / Wake Buffer
What was wrong:
- Flora wake was not exposed through a decoupled contract; no `IProceduralSwayDirector` slot existed.
- No bounded global wake buffer existed for vertex-driven kelp/player/submarine/apex disturbance.
- Existing vegetation displacement was flow/sine led and lacked a producer-agnostic wake signal path.
- GPU culling bounds did not reserve space for procedural wake displacement.

What was done:
- Added `WakeGeneratedSignal(AUP, velocity)` to `GlobalSignals` and `IProceduralSwayDirector` to registry contracts.
- Registered procedural sway through `GlobalRegistry`, `FloraInteractionManager`, and a `GameBootstrapper` recovery hook.
- Built a preallocated `NativeArray<ProceduralWakePoint>` plus `Vector4[32]` shader upload path.
- Published player, submarine rear-propeller, and leviathan Rigidbody wake signals; flora consumes the native queue.
- Added smooth trail/decay, origin-shift correction, finite radius/intensity packing, and `_ShearFoamAmount`.
- Updated `Hecton_IndirectVegetation.shader` with `_HectonFloraWakeBuffer`, squared-distance radial bend, height/root pinning, camera-normal cheat, low-tier wake-loop bypass, and flow-led sway.
- Expanded `FloraCulling.compute` bounds by a conservative 2m radius pad.
- Updated `Status_FLORA_PROCEDURAL_SWAY.md` and `Rationale_FLORA_PROCEDURAL_SWAY.md`.

Cinematic cheats used:
- Packed scalar wake metadata in one float instead of a second buffer.
- Squared-distance radial wake bend, no true fluid simulation.
- Root-pinned vertex-height multiplier instead of skeletal/physics deformation.
- Camera-biased normal tilt instead of normal reconstruction.
- Global shear rim tint instead of particles or foam simulation.
- Low-tier `_MATH_LOD_LOW` shader loop bypass.

Exact microseconds saved:
- Removed expected WindZone/trigger/per-object wake approach: estimated 20-60 us CPU saved in active flora scenes.
- Registry recovery avoids scene search in hot path: estimated 5-10 us saved per frame versus lookup.
- NativeQueue + fixed 32-slot buffer avoids managed allocation churn: estimated 0 B/frame and under 50 us for bounded drain/update/upload.
- Low-tier shader bypass saves up to 32 wake iterations per vegetation vertex on MX350 path.

Verification:
- Prompt reread completed after core task pass.
- Shader loop verified: `dot(worldPos - wake.xyz, worldPos - wake.xyz)`.
- Kelp/Flora prefab `OnTriggerEnter` scan returned no hits.
- `git diff --check` returned only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` blocked by unrelated `Hecton8.Bootstrap.Contracts` errors.
- Narrow `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false` blocked by unrelated Cartography/Submarine/progression signal dependencies.

Status:
- PENDING VERIFICATION due global compile dependency wall, not due known wake implementation failure.

## 2026-05-13 - Flora Wake Hardening / No-Build Pass
What was wrong:
- `_ShearFoamAmount` was exposed as a material property while the runtime drives it as a global wake value.
- The wake publisher could still upload an unchanged empty `Vector4[32]` page every frame after all wakes expired.
- The native wake drain loop was unbounded, so a producer burst could concentrate work into one frame.

What was done:
- Removed `_ShearFoamAmount` from shader `Properties`; kept the global uniform and global C# upload.
- Added forced zero uploads for OnEnable/Clear, then skipped repeated empty `Shader.SetGlobalVectorArray` calls.
- Moved tail-zeroing behind the idle guard so empty frames avoid the 32-slot clear loop after the first published zero state.
- Added `MaxWakeSignalsPerFrame = 64` to bound wake signal draining.
- Re-extracted the active `FLORA_PROCEDURAL_SWAY` XML prompt from `Docs/Tasks/CURRENT_BATCH.md` and re-read the relevant mandates.

Cinematic cheats used:
- No new physics. The effect remains a bounded shader fake: packed wake radius/intensity, squared-distance bend, root pinning, camera-normal bias, and global shear tint.

Exact microseconds saved:
- Repeated idle global upload skipped: estimated 3-12 us on i3/MX350 empty flora frames.
- Bounded wake drain prevents worst-case burst spikes; expected saved spike time depends on producer count, with overflow amortized over later frames.
- No visual fidelity removed. High/Ultra keeps the same wake visuals; Low still bypasses the wake loop.

Verification:
- `git diff --check` on touched C#/shader files returned only existing LF/CRLF warnings.
- Grep confirmed no `_ShearFoamAmount (` material property remains.
- Grep confirmed `float _ShearFoamAmount`, `#if !defined(_MATH_LOD_LOW)`, and `dot(worldPos - wake.xyz, worldPos - wake.xyz)` remain in shader.
- Grep confirmed `MaxWakeSignalsPerFrame` and forced upload calls in `FloraInteractionManager`.
- Registry/contracts/signals were rechecked by static scan.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. This is code-review-only until Unity import/console/profiler logs exist.

## 2026-05-13 - Submarine Wake Cache Pass / No-Build
What was wrong:
- Flora wake code still read `GlobalRegistry.Submarine` on the frame path for wash globals, procedural wake signals, and wake-trail stamping.

What was done:
- Added cached `_submarineRuntimeContext` and `_submarineHullRigidbody`.
- Refreshed the cache on OnEnable and SlowTick.
- Converted frame-path submarine wake reads to `_submarineHullRigidbody`.

Cinematic cheats used:
- No simulation change. Cached hull velocity still feeds the same shader fake: packed wake sphere, squared-distance bend, wake-trail stamp, and shear tint.

Exact microseconds saved:
- Removed three per-frame registry reads from flora wake paths: estimated 1-5 us/frame and less dependency drift.

Verification:
- `rg` confirms only one `GlobalRegistry.Submarine` read remains in `FloraInteractionManager`, isolated to `RefreshCachedSubmarineContext()`.
- Hot-path grep found no new LINQ/coroutine/find/camera-main patterns; the only `new List` hit is pre-existing cold parasite state.
- `git diff --check` returned only existing LF/CRLF warning.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity console/profiler evidence still absent.

## 2026-05-13 - Origin Shift Finite Guard / No-Build
What was wrong:
- Origin-shift handling trusted `ShiftOffset.sqrMagnitude`. A NaN shift offset makes the comparison false, so cached wake, trail, flow-field, parasite, and shader positions could be poisoned.

What was done:
- Copied `shiftData.ShiftOffset` once in `OnOriginShift`.
- Rejected non-finite shift offsets before the magnitude branch.
- Added a second non-finite guard at `ApplyRuntimeOffsetToCachedState` so future callers cannot mutate cached flora state with NaN/Inf offsets.

Cinematic cheats used:
- No new simulation. The wake remains a visual fake: packed wake spheres, squared-distance bend, root pinning, camera-normal bias, and global shear tint.

Exact microseconds saved:
- No measurable hot-frame saving; the finite check is sub-microsecond and event-only.
- Avoided worst-case recovery cost from NaN shader globals and corrupted cached wake pages, which can otherwise force scene reload or manual state cleanup.

Verification:
- Re-extracted the complete `FLORA_PROCEDURAL_SWAY` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- `git diff --check -- Assets/_Project/Scripts/World/FloraInteractionManager.cs` returned only the existing LF/CRLF warning.
- `rg` confirmed the finite origin-shift guard, a single cached `GlobalRegistry.Submarine` access, the `WakeGeneratedSignal` queue lane, `_MATH_LOD_LOW`, and `dot(worldPos - wake.xyz, worldPos - wake.xyz)`.
- Targeted flora/script purge scan found no `OnTriggerEnter`, `WindManager.Instance`, `FindObjectOfType`, `Camera.main`, LINQ, or `.ToArray()` hits in the checked paths.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity import, console, and profiler evidence still absent.

## 2026-05-13 - Flora AUP Ingress Clamp / No-Build
What was wrong:
- External wake drain still converted `signal.PositionAup` to runtime space before checking AUP local finiteness.
- External interaction bursts could publish non-finite position/velocity/radius into the shader interaction point buffer.
- Public kelp pushback and cascade spatial-hash paths could call `AbsoluteUniversePosition.FromRuntimePosition` with non-finite runtime input from vehicles, player state, or vegetation matrices.
- Apex wake code estimated speed before rejecting non-finite velocity.

What was done:
- Added `IsFiniteAup` gate in `QueueProceduralWake` before `ToRuntimeFloat3`.
- Added finite position/velocity/radius guards to `RegisterExternalInteraction`.
- Added finite position/radius guards to `TryResolveKelpPushback`.
- Added finite position and half-extents guards before reactive flora spatial-hash registration.
- Added finite player, candidate, and source position guards before cascade query/propagation AUP conversions.
- Reordered apex velocity validation before speed approximation.

Cinematic cheats used:
- No new physics. This protects the existing cheap visual stack: packed wake spheres, squared-distance vertex displacement, root-pinned bending, flow-field sway, and shear rim tint.

Exact microseconds saved:
- Estimated under 2 us/frame during active cascade/wake paths from rejecting bad inputs before conversion/query work.
- Main gain is failure prevention: invalid AUP data cannot poison the wake buffer or reactive flora spatial hash.

Verification:
- Re-read prompt/status/rationale, AGENTS, domain map, zero-GC, AUP, and flora mandates from disk.
- `git diff --check` returned only the existing LF/CRLF warning.
- `rg` confirmed finite guards for external interaction, external wake drain, kelp pushback, cascade registration/query/propagation, and apex velocity.
- `rg` confirmed `WakeGeneratedSignal` lane, `IProceduralSwayDirector` registry/bootstrap slot, shader `_MATH_LOD_LOW`, and `dot(worldPos - wake.xyz, worldPos - wake.xyz)`.
- Targeted purge scan found no `WindManager.Instance`, `WindZone`, or `OnTriggerEnter` in flora target paths.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity import, console, and profiler evidence still absent.

## 2026-05-13 - Wake AUP Boundary / No-Build
What was wrong:
- Player and apex wake fallback paths could call `AbsoluteUniversePosition.FromRuntimePosition` before proving the runtime position was finite.
- The shared wake publisher checked velocity but not AUP local finiteness before enqueue.
- `ClampFinite` trusted its fallback, which was safe for current calls but weak for future reuse.

What was done:
- Added player fallback finite position gate before `FromRuntimePosition`.
- Added apex fallback finite position gate before `FromRuntimePosition`.
- Added `IsFiniteAup` and used it in `PublishWakeGeneratedSignal`.
- Hardened `ClampFinite` so non-finite fallback values collapse to the finite minimum.

Cinematic cheats used:
- No simulation change. The same packed wake sphere fake is protected earlier in the signal lane.

Exact microseconds saved:
- Prevents invalid packets from consuming the 64-signal wake drain budget; estimated under 1 us/frame saved only during bad-producer frames.
- Main value is failure avoidance: bad AUP locals no longer enter the shared wake queue.

Verification:
- `git diff --check` returned only the existing LF/CRLF warning.
- `rg` confirmed `IsFiniteAup`, player/apex finite runtime gates, hardened `ClampFinite`, the `WakeGeneratedSignal` lane, the single flora submarine cache read, and shader squared-distance wake math.
- Targeted purge scan still found no `WindManager.Instance`, `WindZone`, or `OnTriggerEnter`.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity import, console, and profiler evidence still absent.

## 2026-05-13 - Procedural Wake Stride Fix / No-Build
What was wrong:
- `ProceduralWakePoint` had an explicit 48-byte layout while its declared fields require at least 52 bytes under 4-byte packing.

What was done:
- Changed the explicit native stride to 64 bytes.
- Kept the wake buffer fixed at 32 slots, so persistent state is 2048 bytes total.

Cinematic cheats used:
- No simulation change. The fixed-stride state still feeds the same 32-point global shader fake.

Exact microseconds saved:
- No direct frame-time saving. This is a correctness fix.
- Cost is 384 extra persistent bytes versus a 52-byte stride. Benefit is removing a native layout hazard before Unity import/runtime.

Verification:
- `rg` confirmed `[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]`.
- `rg` confirmed `MaxProceduralWakePoints = 32` and the persistent `NativeArray<ProceduralWakePoint>` allocation.
- `git diff --check` returned only the existing LF/CRLF warning.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity import, console, and profiler evidence still absent.

## 2026-05-13 - Wake Scalar Clamp / No-Build
What was wrong:
- Wake radius/intensity and wake-trail stamp inputs were finite at velocity/AUP entry points, but authored scalar fields could still become NaN/Inf through serialized data or prefab merges.
- A previously poisoned `_wakeTrailWorldRect` could survive into later frames if the center did not move enough to force a rebuild.

What was done:
- Clamped wake-trail world size, fade, diffusion, wave strength, and damping in `Awake`.
- Clamped active wake-trail radius, length, velocity-to-length, and source strengths before stamp creation.
- Clamped submarine wash radius/min-speed/whip/strength before shader globals and wake stamps.
- Added finite radius/intensity gates in procedural wake queue, update, publish, and pack.
- Added `IsFiniteVector4` and wake-trail rect invalidation so bad UV rect state is rebuilt/cleared.

Cinematic cheats used:
- No physical simulation. The fix protects the existing visual fake: packed wake spheres, squared-distance vertex bend, root pinning, wake-trail compute stamp, and global shear tint.

Exact microseconds saved:
- No direct hot-frame saving target; branch cost estimated under 2 us/frame in active wake scenes.
- Prevents worst-case NaN shader/compute state that would cost far more through visual corruption, dispatch invalidation, or manual scene recovery.

Verification:
- Re-read prompt/status/rationale and relevant mandates from disk.
- `git diff --check` returned only LF/CRLF warnings.
- `rg` confirmed finite scalar gates, `ClampFinite`, `IsFiniteVector4`, a single flora `GlobalRegistry.Submarine` cache read, the `WakeGeneratedSignal` lane, `_MATH_LOD_LOW`, and shader squared-distance wake math.
- Targeted purge scan found no `WindManager.Instance`, `WindZone`, or `OnTriggerEnter` in flora target paths.
- Hot-path grep found no new LINQ, `.ToArray()`, scene search, `Camera.main`, or `goto`; the only `new List` hit remains pre-existing cold parasite state.
- `dotnet build` was not run by explicit user instruction.

Status:
- PENDING VERIFICATION. Static review only; Unity import, console, and profiler evidence still absent.
