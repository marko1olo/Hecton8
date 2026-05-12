# SURVIVAL_ATMOSPHERE Rationale

Status: PENDING VERIFICATION

## Decision 1 - Use Existing Atmosphere Owner

Problem: The prompt asks for Dalton-law fake gas dynamics, and the repository already contains an untracked `Hecton8.Atmosphere` implementation matching that ownership. A second implementation would create direct dependency conflicts with other agents.
Solution: Treat `BaseAtmosphereMath` as the math kernel and `BaseAtmosphereEngine` as the runtime owner. Keep cross-domain interaction through `GlobalRegistry.PowerGrid`, `SystemDispatcher`, `GlobalTelemetryBus`, and flags consumed by presentation owners.
Rejected Alternatives: A new MonoBehaviour life-support manager was rejected because it would duplicate tick ownership, likely double-drain O2, and violate registry discipline. Direct references to render/VFX/player controllers were rejected because the batch protocol demands decoupling.
Scalability potential: Low updates only the active player compartment at 1Hz; Middle can process more active rooms at the same scalar cost; High/Ultra runs the full compartment array at 5Hz and exposes fog/bubble/glitch flags for presentation overkill.
Hardware Impact: On i3/MX350, scalar per-room addition and `NativeArray` linear access avoids diffusion graph traversal; estimated save is 80-250 microseconds per cold tick against a naive 50-room diffusion step, pending Unity profiler proof.

## Decision 2 - Dalton Fake Over Diffusion

Problem: True gas diffusion across a room graph would burn CPU on invisible causes and produce unstable tuning.
Solution: Total pressure is `O2 + CO2 + N2`, gas fractions use reciprocal total pressure, and airlock equalization is a fixed timer/audio fake.
Rejected Alternatives: Per-particle gas, raycast diffusion, and volumetric smoke/gas simulation were rejected under the Cinematic Cheat Protocol because the player needs believable gauges, alarms, fog, bubbles, and control penalties, not molecular truth.
Scalability potential: Low keeps scalar safety; High/Ultra spends saved cost on local volumetric fog and richer bubble/glitch presentation via flags.
Hardware Impact: Eliminates graph diffusion and physics probes; estimated save is 100-400 microseconds per update on cheap silicon, pending profiler proof.

## Decision 3 - Tasks 1-5 Cadence And Side Effects

Problem: Life support needs to feel dangerous without turning a 50-room base into a per-frame simulation island.
Solution: Math LOD uses 5Hz full-array updates only on High/Ultra; Low/MX350 runs a 1Hz active-compartment solve. CO2 toxicity is emitted as flags and stamina multiplier, not a coupled stamina dependency. Airlock is a 5s timer fake with presentation hooks.
Rejected Alternatives: Full-room diffusion on Low was rejected because rooms the player cannot inspect do not need truth. Direct stamina or shader mutation inside the Burst job was rejected because Burst jobs must stay unmanaged and presentation owners consume flags.
Scalability potential: Low updates one room. Middle can keep 1Hz but widen active-room windows. High/Ultra can run 5Hz and spend the saved frame time on volumetric fog, richer audio, and bubble VFX driven by flags.
Hardware Impact: Expected gain is 80-250us per low-tier cold tick from active-room-only processing plus 50-150us per airlock event by removing pressure transfer math; PENDING VERIFICATION until Unity profiler capture.

## Decision 4 - Same-Domain Compile Blocker In Celestial

Problem: `Hecton8.Core.csproj` failed before verifying the atmosphere work because `HectonCelestialEngine` referenced private helper methods that were absent: reciprocal caching, orbit reciprocal resolution, year reciprocal resolution, and snapshot frame gating.
Solution: Added private helper methods only. Reciprocals use finite guards plus `math.rcp`; snapshot evaluation is gated by frame interval, High/Ultra at 60 frames and Low/MX350 at 300 frames. No public API changed.
Rejected Alternatives: Ignoring the error was rejected because the batch protocol requires compile verification. Reverting the file was rejected because the modifications appear to be another agent's in-flight work. Expanding architecture was rejected because the missing calls only needed private helpers.
Scalability potential: Low/MX350 updates analytical celestial snapshots less often; High/Ultra keeps more frequent visual state for tide, GI relay, and moon/fog presentation.
Hardware Impact: Gating snapshot evaluation saves repeated analytical orbit math on cheap devices; estimated 20-80us per skipped slow-tick slice, pending profiler proof.

## Decision 5 - Compile Checkpoint Discipline

Problem: Atmosphere verification was blocked by a core project compile failure, and stale follow-on diagnostics appeared in unrelated movement/fluid files during the next incremental pass.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` after the celestial helper patch before touching unrelated systems. The fresh build passed with 0 warnings and 0 errors, so no cross-domain patch was justified.
Rejected Alternatives: Patching movement or fluid files from stale diagnostics was rejected because those files already contained the referenced symbols and the fresh compiler result proved no source edit was needed.
Scalability potential: Keeping the fix to private celestial cadence helpers preserves the atmosphere ownership boundary and avoids adding direct dependencies for other agents to unwind.
Hardware Impact: No new runtime cost. Avoided unnecessary code churn in physics and movement hot paths.

## Decision 6 - Physiology Batch Verification

Problem: Tasks 6-10 were implemented in the atmosphere math/runtime layer, but the editor tests did not pin the highest-risk scalar cheats: bends thresholding, deterministic narcosis, powered scrubber byte reduction, and bulk tank ID movement.
Solution: Added focused editor tests in `BaseAtmosphereMathEditTests`. The runtime stays scalar: nitrogen tissue load is a cheap lerp, bends is threshold damage, narcosis is LCG plus triangle wave, crush damage is squared times `math.rsqrt`, scrubber work is one byte-lane reduction, and tank swaps use guarded `UnsafeUtility.MemCpy`.
Rejected Alternatives: Full decompression tables, sine-driven steering wobble, managed inventory copy loops, and gas-filter graph simulation were rejected because they add cost without improving the player-facing survival signal.
Scalability potential: Low keeps one active compartment and deterministic hazard scalars. Middle can widen active-room windows. High/Ultra can use the same hazard flags to drive heavier steering postprocess, audio bends cues, and richer tank-swap presentation without changing the math kernel.
Hardware Impact: Estimated low-end savings are 30-120us for bends versus decompression tables, 5-20us for triangle narcosis versus sine/noise, 20-80us for scrubber byte-lane work versus graph filtering, and 5-25us per tank swap versus managed per-item dispatch. Unity profiler proof is still pending.

## Decision 7 - Presentation Flags Over Simulation

Problem: Tasks 11-15 need fog, rupture, smoke, struct alignment, and raw iteration without dragging render, VFX, or fire systems into the atmosphere job.
Solution: Keep atmosphere output as flags and bytes. Humidity over 90 on high scalability sets `RenderFogRequested`; suit rupture drains oxygen with a rational exponential-style factor and sets `BubbleVfxRequested`; smoke raises the `Toxicity` byte and sets `SmokeParticlesRequested`; `CompartmentState` remains exactly 32 bytes; hot loops are raw `for` loops over `NativeArray`.
Rejected Alternatives: Local volumetric fog simulation, bubble physics, volumetric smoke propagation, managed room objects, and foreach/enumerator traversal were rejected because presentation owners can spend the saved cost after reading flags.
Scalability potential: Low keeps invisible scalar hazards. Middle can use cheaper particles. High/Ultra can render dense local fog, bubble trails, and GPU smoke from the same flags without changing the deterministic life-support state.
Hardware Impact: Estimated low-end savings are 40-120us per fog-relevant room, 30-100us per rupture update, 80-300us per smoke event, plus small cache savings from 32-byte compartments and raw `for` iteration. Checkpoint compile is blocked by `ProceduralWreckGenerator`, not atmosphere.

## Decision 8 - UI And Burst Boundary

Problem: Tasks 16-20 require cheap seal checks, division-free gauges, delayed UI, deterministic narcosis, and a clean Burst job boundary.
Solution: Seal state uses bitwise masks, max pressure reciprocal is stored in `CompartmentState`, O2 UI formatting uses caller-owned `Span<char>` and only fires on integer changes, narcosis uses LCG hashing plus triangle wave, and `BaseAtmosphereColdTickJob` has no UnityEngine import or Unity API usage.
Rejected Alternatives: Managed seal state objects, per-frame gauge division, string interpolation UI updates, `UnityEngine.Random`, `math.sin`, and MonoBehaviour callbacks inside Burst were rejected because each creates either avoidable CPU cost, GC risk, or Burst incompatibility.
Scalability potential: Low avoids allocations and divisions. Middle can add more visible gauges. High/Ultra can spend the saved cost on richer UI animation and postprocess while the math remains deterministic.
Hardware Impact: Estimated low-end savings are 2-10us per seal query burst, 2-8us per gauge update, 10-60us plus GC avoidance per delayed UI refresh, and 5-20us per narcosis input update. Final compile verification is blocked by another agent's world wreckage implementation, not atmosphere.

## Decision 9 - Omega Polish Result

Problem: The batch requires reading `<POLISH_MANDATE>` after core completion, but the current batch file contains no such tag.
Solution: Recorded the absent tag and ran the anti-bloat checks directly: no `foreach`, `math.pow`, `UnityEngine.Random`, string formatting, or Unity API usage inside the atmosphere math/Burst job; whitespace scan passed for touched atmosphere tests, status, rationale, and log files.
Rejected Alternatives: Waiting for a missing tag or inventing a polish mandate was rejected because the protocol requires evidence from disk, not assumed text.
Scalability potential: The final state keeps the cheap scalar math kernel intact and leaves presentation overkill to consumers of flags/bytes.
Hardware Impact: No added runtime cost. The scan prevents accidental allocation/math regressions before integration.
