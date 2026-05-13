# Rationale_FLORA_PROCEDURAL_SWAY

Status: PENDING VERIFICATION

## Decision 1 - Reuse FloraInteractionManager
Problem: Existing vegetation VFX authority already lives in `FloraInteractionManager`; it owns shader globals, wake trail RT, flow field binding, player/scooter/submarine interaction points, and origin-shift listener state.
Solution: Extend the existing owner with a native wake buffer and service contract instead of adding a parallel manager.
Rejected Alternatives: New `WindManager` or new scene singleton would duplicate state and violate GlobalRegistry/service-locator mandate. Moving all VFX scripts into a new asmdef would break existing dependencies in a dirty worktree.
Scalability potential: Low = flow-field only and wake loop off through `_MATH_LOD_LOW`; Middle = 32-slot scalar wake buffer; High = stronger shear and longer residency; Ultra = same cheap buffer buys more visual overkill in shader response.
Hardware Impact: MX350/i3 avoids GameObject wind zones and collider trigger churn; expected CPU hot-path gain is small but deterministic, roughly 20-60 us versus per-object interaction polling.

## Decision 2 - Native Signal Corridor
Problem: Prompt forbids direct environment pushes and asks for wake signals carrying AUP and velocity.
Solution: Add a bounded `WakeGeneratedSignal` lane to `GlobalSignals`; `FloraInteractionManager` consumes the native queue and builds the shader buffer.
Rejected Alternatives: String event names, UnityEvent, or concrete vehicle/player references from shader owner. Those allocate or create cross-domain coupling.
Scalability potential: Low = one or two active emitters still compressed into the same 32 slots; Ultra = more wake sources without changing shader ABI.
Hardware Impact: Fixed queue and fixed buffer avoid per-frame allocations; expected hot path cost under 0.05 ms if producers stay bounded.

## Decision 3 - Visual Fake For Kelp Wake
Problem: True water/flora physics around propwash is too expensive and not needed for gameplay authority.
Solution: Use a deterministic shader fake: packed wake spheres, squared-distance radial bend, root pinning through height mask, camera-biased normal cheat, and shear rim when intensity is high.
Rejected Alternatives: Unity WindZone, trigger-driven bending, per-blade forces, or physics volumes. They burn CPU for a presentation-only effect.
Scalability potential: Low = no wake loop; Middle = 32 cheap vector spheres; High = richer normal/shear response; Ultra = more visible bending range without new gameplay state.
Hardware Impact: Removes potential GameObject trigger and wind-zone overhead; shader loop is disabled on low tier and capped at 32 iterations elsewhere.

## Decision 4 - Service Registration Without New Singleton
Problem: Prompt requires `IProceduralSwayDirector` to be visible to bootstrap without adding another singleton owner.
Solution: `FloraInteractionManager` implements `IProceduralSwayDirector`, self-registers with `GlobalRegistry`, and `GameBootstrapper` has a recovery registration pass that binds the active flora owner if the slot is empty.
Rejected Alternatives: New `WindManager.Instance`, scene-wide `FindObjectOfType` in hot paths, or direct vehicle-to-flora references. Those violate the registry/service mandate and add coupling.
Scalability potential: Low = one sway service publishes empty or ambient buffers; Middle/High/Ultra = same service accepts more producers through signals without changing callers.
Hardware Impact: One registry pointer read and fixed queue drain; expected CPU cost under 10 us outside producer count, no per-frame scene search.

## Decision 5 - Packed Wake ABI
Problem: Shader global buffer budget is fixed at `Vector4[32]`, but each wake needs position, radius, and intensity.
Solution: Store runtime AUP-resolved position in xyz and pack radius/intensity into w as a finite quantized float: radius at 1/16m precision and intensity in 10 bits.
Rejected Alternatives: `Vector4` plus second array, structured buffer, or bit-cast float packing that can produce NaN/Inf bit patterns. Extra buffers increase binding churn; NaN payloads poison shader diagnostics.
Scalability potential: Low = shader loop skipped; Middle = 32 packed points; High = higher intensity shear and longer trails; Ultra = bigger visual radius without CPU allocation.
Hardware Impact: 32 `Vector4` upload stays bounded; MX350 avoids dynamic buffer allocation and keeps the wake path predictable.

## Decision 6 - Compile Wall Record
Problem: `dotnet build Hecton8.Core.csproj` cannot reach a clean validation point because existing cross-domain dependencies fail first.
Solution: Record the build wall and continue with static verification of this task's shader loop, allocation path, prompt reread, and targeted grep checks.
Rejected Alternatives: Editing Cartography, Bootstrap.Contracts, or missing narrative signal types from another agent's domain to force a green build. That would be architectural sabotage.
Scalability potential: Low/Middle/High/Ultra unchanged; validation is blocked by unrelated assembly wiring, not by the wake buffer design.
Hardware Impact: No runtime hardware impact; integration risk remains compile-time until the dependency owner restores the baseline.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat audit required proof that the wake implementation does not use honest physics, runtime allocation, or expensive unconditional math.
Solution: Kept the wake as a finite packed `Vector4[32]`, squared-distance shader loop, `#if !defined(_MATH_LOD_LOW)` bypass, approximate camera-normal tilt, 2m conservative cull expansion without `sqrt`, and flow-led sway with sine reduced to low-amplitude organic noise.
Rejected Alternatives: Full water/flora simulation, true normal reconstruction, second global buffer, per-emitter GameObjects, or trigger volumes. Standard Unity WindZone/trigger paths were too slow and too coupled for the VFX-only requirement.
Scalability potential: Low = ambient abyssal flow only, wake loop compiled out; Middle = 32 wake points; High = stronger normal/shear response; Ultra = larger visual wake radius and longer perceived wash without more CPU structures.
Hardware Impact: i3/MX350 avoids physics volumes and dynamic allocations; expected saved CPU versus trigger/wind-zone approach is 20-60 us in common scenes, with shader cost capped at 32 iterations and zero on low tier.
Cinematic Cheats Used: finite packed scalar radius/intensity, squared distance radial bend, root-pinned height mask, camera-biased normal tilt, global shear rim tint, conservative cull-radius padding.
Cross-Domain Justification: Edited `GameBootstrapper` only to add a registry recovery hook for `IProceduralSwayDirector`; edited `GlobalRegistry`/`GlobalSignals` as required contracts/event-bus plumbing. No direct vehicle/player dependency was introduced.
Final Git Diff Summary: `FloraCulling.compute` +4; `Hecton_IndirectVegetation.shader` +79/-4; `GameBootstrapper.cs` +38; `GlobalRegistry.cs` +437/-7; `GlobalRegistryContracts.cs` +490; `GlobalSignals.cs` +894; `FloraInteractionManager.cs` +356/-1. Diff includes pre-existing dirty registry/signal expansion from other agents in the same files; this agent did not revert it.

## Decision 7 - Wake Upload Hardening
Problem: The wake path still had two avoidable costs/risks after the first implementation: `_ShearFoamAmount` appeared as a material property despite being driven as a global, and idle frames could keep uploading an unchanged empty `Vector4[32]` page.
Solution: Removed the material property entry while keeping the global uniform, added a forced upload path for OnEnable/Clear, skipped repeated empty global uploads once zeros are already published, and capped wake-signal draining to 64 packets per frame.
Rejected Alternatives: Leaving the material property in place risks SRP-batcher/material state confusion. A structured `GraphicsBuffer` rewrite would violate the prompt's explicit `Shader.SetGlobalVectorArray` requirement and expand the patch surface. Unbounded queue draining can create a one-frame CPU spike if vehicles/fauna flood the lane.
Scalability potential: Low = one forced empty upload then no idle wake PCIe/global churn; Middle = bounded 32-slot active response; High = same saved bandwidth can fund stronger shear/normal response; Ultra = bursty apex/vehicle wakes degrade over multiple frames instead of causing a single-frame drain spike.
Hardware Impact: On i3/MX350 idle flora scenes avoid repeated empty wake global writes and 32-entry tail zeroing; estimated gain is small but real, roughly 3-12 us on empty frames and more predictable worst-case drain cadence under producer bursts.
Verification Status: CODE-REVIEW ONLY. `git diff --check` returned only existing LF/CRLF warnings; targeted grep confirmed no `_ShearFoamAmount (` property line, retained `float _ShearFoamAmount`, retained squared-distance shader wake math, and confirmed `MaxWakeSignalsPerFrame`. `dotnet build` was not run because the user explicitly forbade it.

## Decision 8 - Cached Submarine Wake Source
Problem: The flora manager had three frame-path `GlobalRegistry.Submarine` reads across submarine wash globals, procedural wake publishing, and wake-trail stamping. That violated the direction of dependency injection even though similar legacy reads already existed in the file.
Solution: Cache `ISubmarineRuntimeContext` and its hull `Rigidbody` in `RefreshCachedSubmarineContext()` on enable and slow tick. Frame-path code now reads `_submarineHullRigidbody` only.
Rejected Alternatives: Direct references from vehicle code to flora would couple domains. Scene search would allocate and scale badly. Leaving registry reads in Tick would preserve architecture drift. A full injection-system rewrite is outside the flora VFX prompt and unsafe in the dirty multi-agent worktree.
Scalability potential: Low = no per-frame registry read for absent submarine; Middle = cached single hull read; High = same cached source drives stronger wake/shear without extra lookup; Ultra = bursty submarine wake visuals still use the same cached body and bounded wake buffer.
Hardware Impact: Small deterministic CPU gain, estimated 1-5 us/frame in active flora scenes, and lower architecture risk. MX350 benefit is mainly predictability rather than raw frame time.
Verification Status: CODE-REVIEW ONLY. Static grep found only one `GlobalRegistry.Submarine` read in `FloraInteractionManager`, inside `RefreshCachedSubmarineContext()`. `dotnet build` and Unity import were not run by instruction, so status remains PENDING VERIFICATION.

## Decision 9 - Finite Origin Shift Guard
Problem: The wake/origin-shift safety path checked `ShiftOffset.sqrMagnitude <= epsilon`, but a NaN offset makes that comparison false and can poison cached player, wake, trail, flow-field, parasite, and shader positions.
Solution: Copy `shiftData.ShiftOffset` once, reject non-finite vectors before the magnitude branch, and add a second finite guard at `ApplyRuntimeOffsetToCachedState` so future callers cannot mutate cached state with NaN/Inf offsets.
Rejected Alternatives: Clearing the whole wake buffer on any bad shift would hide corruption but erase valid visuals. Relying on Unity `Vector3.sqrMagnitude` was insufficient because NaN does not trip the small-offset branch. Adding exception/log allocation in the shift callback would violate the zero-GC frame path.
Scalability potential: Low = invalid shift is ignored and low-tier ambient sway remains stable; Middle = 32 wake points stay finite through ordinary origin shifts; High = stronger shear/trail visuals avoid shader NaN flicker; Ultra = larger wake radii and longer residency still retain deterministic bounds.
Hardware Impact: The added three-axis finite check is sub-microsecond on i3/MX350 and only runs on origin-shift events, not per vegetation vertex. It prevents the high-cost failure mode: NaN shader globals causing broad visual corruption or forced scene recovery.
Verification Status: CODE-REVIEW ONLY. `git diff --check` on `FloraInteractionManager.cs` returned only the existing LF/CRLF warning. Static grep confirmed the finite shift guard, the single cached `GlobalRegistry.Submarine` access, the `WakeGeneratedSignal` queue lane, `_MATH_LOD_LOW`, and the shader squared-distance wake loop. `dotnet build` was not run by explicit user instruction.

## Decision 10 - Procedural Wake Native Stride
Problem: `ProceduralWakePoint` declared three `float3` values, three floats, two bytes, and one ushort, but the explicit struct size was pinned to 48 bytes. The field payload is at least 52 bytes with 4-byte packing, so the declared native stride was unsafe.
Solution: Pad the explicit layout to 64 bytes. The wake buffer remains fixed at 32 elements, so the total persistent NativeArray footprint is 2048 bytes, still trivial and cache-predictable.
Rejected Alternatives: Removing explicit size would leave the stride to runtime layout rules. Keeping 48 bytes risks invalid field layout or memory interpretation bugs. Compressing fields to recover 384 bytes would add packing complexity for no meaningful hardware win.
Scalability potential: Low = 2 KB fixed native state with shader wake loop disabled; Middle = stable 32 wake slots; High = same safe stride supports stronger wake/shear response; Ultra = longer visual residency without widening the CPU data structure.
Hardware Impact: i3/MX350 pays 384 extra persistent bytes versus a hypothetical 52-byte stride, not per-frame CPU. Avoided risk is a runtime layout fault or corrupted wake state, which is more expensive than the padding.
Verification Status: CODE-REVIEW ONLY. Static grep confirmed `[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]`, `MaxProceduralWakePoints = 32`, and the persistent `NativeArray<ProceduralWakePoint>` allocation. `git diff --check` returned only the existing LF/CRLF warning. `dotnet build` was not run by explicit user instruction.

## Decision 11 - Wake Scalar Finite Clamp
Problem: Serialized `Range` fields are not a runtime safety contract. Bad scene/prefab values for wake radius, fade, trail length, strength, or submarine thresholds could still produce NaN/Inf shader globals, packed wake values, or compute wake-trail stamps.
Solution: Reuse the local `ClampFinite` pattern on submarine wash, procedural wake radius/intensity, wake-trail fade/length/radius/strength, and authored wake simulation scalars. Add finite rejection at the wake queue, update, publish, pack, stamp, and wake-trail rect boundaries.
Rejected Alternatives: Relying on Inspector ranges is insufficient because serialized data, prefab merges, and external edits can bypass them. Clearing all wakes on any invalid scalar would hide the source and destroy valid visual state. Adding logging in the hot path would violate zero-GC discipline.
Scalability potential: Low = invalid authored values degrade to safe ambient/no-stamp behavior instead of breaking low-tier shaders; Middle = 32 wake points remain finite; High = stronger wash visuals stay bounded; Ultra = longer wake trails and larger submarine stamps retain predictable compute inputs.
Hardware Impact: Added scalar finite checks are branch-only and estimated under 2 us/frame on i3/MX350 in active wake scenes. The avoided failure case is expensive: NaN shader globals or compute UVs causing full-screen artifacts, invalid dispatch inputs, or forced scene recovery.
Verification Status: CODE-REVIEW ONLY. Static grep confirmed finite gates for radius/intensity, `ClampFinite` use on wake-trail scalars, `IsFiniteVector4` wake rect guards, single flora `GlobalRegistry.Submarine` access in cache refresh, retained `_MATH_LOD_LOW`, and retained shader `dot(worldPos - wake.xyz, worldPos - wake.xyz)`. `git diff --check` returned only LF/CRLF warnings. `dotnet build` was not run by explicit user instruction.

## Decision 12 - Wake AUP Publication Boundary
Problem: The wake queue drained invalid AUPs safely, but player/apex fallback paths could still call `AbsoluteUniversePosition.FromRuntimePosition` before proving the runtime position was finite, and the shared publisher did not reject non-finite AUP locals before enqueue.
Solution: Gate player fallback runtime position, apex fallback runtime position, and every `PublishWakeGeneratedSignal` call with finite checks. Add a local `IsFiniteAup` helper for AUP locals and harden `ClampFinite` so a non-finite fallback cannot leak through future scalar clamps.
Rejected Alternatives: Letting `QueueProceduralWake` reject invalid `ToRuntimeFloat3()` results still permits bad signal packets into the shared native queue. Expanding this into a global AUP API change would cross domain boundaries and risk other agents' work.
Scalability potential: Low = bad producers are rejected before consuming queue budget; Middle = 32 wake slots receive only finite coordinates; High = stronger wash visuals avoid NaN shader globals; Ultra = bursty external wake producers cannot contaminate the shared flora wake lane with bad AUP locals.
Hardware Impact: Three local finite checks are sub-microsecond on i3/MX350 and avoid wasting bounded wake drain capacity on invalid packets. No allocation and no additional containers.
Verification Status: CODE-REVIEW ONLY. Static grep confirmed `IsFiniteAup`, finite player/apex runtime gates before `FromRuntimePosition`, hardened `ClampFinite`, retained `WakeGeneratedSignal` queue, single flora `GlobalRegistry.Submarine` cache read, and shader squared-distance wake loop. `git diff --check` returned only LF/CRLF warning. `dotnet build` was not run by explicit user instruction.

## Decision 13 - Flora AUP Ingress Clamp
Problem: Some flora-side runtime ingress points still accepted runtime `Vector3` input before proving finiteness: external interaction could publish invalid shader points, external wake drain converted `signal.PositionAup` to runtime space after only checking velocity, kelp pushback accepted arbitrary public positions/radii, and cascade spatial-hash rebuild/query paths converted extracted matrix positions directly.
Solution: Add finite guards at the external interaction API, external wake queue drain, kelp pushback API, cascade registration, cascade query, nearest-payload selection, and cascade propagation source. Reorder apex wake velocity validation so approximation math never sees NaN/Inf velocity.
Rejected Alternatives: Relying on shader clamps or `HectonSpatialHash` to reject already-published/converted invalid data is late; `AbsoluteUniversePosition.FromRuntimePosition` can still be called with poisoned runtime input. Adding logs in these hot paths would allocate or spam. A global AUP API rewrite crosses ownership boundaries.
Scalability potential: Low = invalid producer/public inputs collapse to no-stamp/no-query and low-tier flow sway remains stable; Middle = shader interaction points, spatial hash, and 32 wake slots stay finite; High = stronger cascade/wake visuals have clean inputs; Ultra = bursty external producers and dense flora matrices cannot spend wake/query budget on invalid state.
Hardware Impact: Branch-only guards are estimated under 2 us/frame on i3/MX350 when these paths are active. The avoided cost is high: invalid AUP conversion, corrupted spatial hash registration, or NaN wake globals causing shader artifacts and manual recovery.
Verification Status: CODE-REVIEW ONLY. Static grep confirmed finite gates for `RegisterExternalInteraction`, `QueueProceduralWake`, `TryResolveKelpPushback`, cascade registration/query/propagation, and apex velocity. Signal lane, registry slot, shader `_MATH_LOD_LOW`, and `dot(worldPos - wake.xyz, worldPos - wake.xyz)` remain intact. `git diff --check` returned only LF/CRLF warning. `dotnet build` was not run by explicit user instruction.
