# Rationale_ACOUSTIC_PORTAL_PROPAGATION

## 2026-05-13 - Bootstrap
Problem: The batch protocol requires exact isolation of the acoustic portal prompt before any architectural decision.
Solution: Extracted only `<AGENT_PROMPT id="ACOUSTIC_PORTAL_PROPAGATION" role="DSP_ACOUSTIC_LEAD">` from `Docs/Tasks/CURRENT_BATCH.md` with a PowerShell raw-read regex.
Rejected Alternatives: Chat-memory parsing and MCP/basic file reads were rejected because neighboring prompt bleed or truncation would contaminate ownership decisions.
Scalability potential: Low tier must be allowed to stay on straight-line SDF. Middle/High/Ultra can buy better belief through portal diffraction, true path delay, and stronger reverb coupling.
Hardware Impact: No runtime impact; process guard only. Estimated cold CLI cost under 1 ms on i3/MX350 class hardware.

Problem: Acoustic portal propagation can become a fake physics trap if it simulates wave truth instead of perceived travel.
Solution: Use deterministic graph pathing as an audio presentation fake: shortest portal corridor, corner count, last-portal projection, low-pass bands, and distance delay. No wave equation, no volumetric pressure solve.
Rejected Alternatives: Continuous acoustic wave propagation and per-surface diffraction were rejected because they exceed the 0.1 ms suspicion budget and provide little gameplay authority.
Scalability potential: Low: SDF occlusion only. Middle: capped 30-node portal BFS. High: richer corner filtering and room RT60. Ultra: more portal metadata and stronger binaural/reverb detail if measured budget allows.
Hardware Impact: Target is bounded SlowTick/Burst work with zero GC; expected MX350 gain versus wave simulation is entire orders of magnitude, pending profiler proof.

## 2026-05-13 - Inventory Boundary
Problem: The propagation kernel needs voxel and habitat topology without hard-coding against private implementation details or spawning a refactor loop across construction/world domains.
Solution: Keep the Burst acoustic path kernel in a nested `Hecton8.Audio.Propagation` assembly with pure blittable contracts. Add only read-only accessors where the root assembly already owns internals, then let `SpatialAudioManager` adapt existing voxel macro portal routes and habitat CSR arrays into a capped acoustic graph.
Rejected Alternatives: Moving `SpatialAudioManager` into a new asmdef was rejected because it would create cyclic references against existing core contracts. Rewriting voxel portal generation was rejected because `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc` already supplies the correct non-mutating topology surface.
Scalability potential: Low/MX350 stays on straight SDF. Middle uses a 30-node route. High/Ultra can spend saved cycles on richer per-corner filters and room-volume reverb without changing the contract.
Hardware Impact: Adapter work is bounded by 30 acoustic nodes and existing graph buffers. Expected low-end cost is under the current PlayAtPoint setup budget; exact microseconds require profiler capture after compile.

## 2026-05-13 - Phase 1/2 Implementation
Problem: Existing audio culls and muffles by straight listener-source SDF, so a cave roar behind a bend can die before the listener hears the corridor path.
Solution: Resolve a graph path first, then present the AudioSource from the last portal AUP before the listener. Existing SDF still runs for open-water or no-graph cases. Habitat data is read through CSR arrays; voxel data is read through the already generated macro portal route.
Rejected Alternatives: Removing SDF was rejected because open water has no portal topology. Using the true source transform was rejected because it preserves impossible wall panning.
Scalability potential: Low/MX350 never invokes the job. Middle gets portal projection and corner filters. High/Ultra can expand metadata quality without changing playback callers.
Hardware Impact: Route expansion is capped at 30 nodes and 60 edges. The child `Hecton8.Audio.Propagation` compile passes; full core compile is blocked by unrelated fauna/modding/visor errors, not by the acoustic assembly.

## 2026-05-13 - Safety, LOD, Telemetry
Problem: A graph route can become another invisible failure mode if it stalls, loses origin precision, or cannot explain bad audio on crash.
Solution: Use AUP-native query/cache keys, hard 30-node expansion, persistent native buffers, low-tier bypass, and a 300-frame blackbox ring that dumps on non-finite output.
Rejected Alternatives: Raw `Vector3` path caches were rejected because floating origin shifts invalidate them. Managed dictionaries/lists were rejected because emissions can occur in bursts.
Scalability potential: Low/MX350 buys stability by refusing portal pathing. Middle uses the capped fake. High/Ultra can spend more authored metadata through the same node/edge structs.
Hardware Impact: Low-end hardware avoids the route entirely. Middle/high path is fixed-size and compiled in the isolated assembly; measured child compile is clean, full runtime measurement is blocked by current Unity session/global compile blockers.

## 2026-05-13 - Compile Wall
Problem: Full project compile cannot reach a clean state from the acoustic domain because `Hecton8.Core.rsp` currently stops on unrelated fauna/modding/inventory symbols and Unity batchmode refuses to open while another Unity instance owns the project lock.
Solution: Verified the isolated acoustic propagation assembly directly with Unity Roslyn using `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.rsp`. Ran `Hecton8.Core.rsp` twice to confirm the remaining blockers are outside audio propagation.
Rejected Alternatives: Killing the user's Unity process was rejected. Editing fauna/modding/inventory dependencies was rejected as domain breach.
Scalability potential: No runtime change. The isolated compile path proves the route kernel is syntactically valid for the intended asmdef once the global project blockers are cleared.
Hardware Impact: Compile-only. Acoustic child compile cost observed around 12.2 ms wall-clock command time; core compile wall around 59.4 ms before unrelated failures report.

## 2026-05-13 - Post-Completion Re-Audit
Problem: The portal route calculated a virtual audible position but normal `PlayAtPoint` still placed the `AudioSource` at the true source and did not apply the `Transmission01` corner/bulkhead loss. The no-eviction helper also contained portal-only locals from a bad merge path.
Solution: Make normal pooled playback assign `source.transform.position = audiblePosition` and multiply base volume by the Burst result transmission when a portal route is used. Restore the no-eviction helper to raw `position`/source AUP presentation. Add `AcousticPathJob` guards for uncreated result arrays, invalid AUP input, and zero-capacity open/closed scratch lists before touching no-resize writes.
Rejected Alternatives: A larger playback refactor was rejected because the defect was local and the project is running with many concurrent agents. Leaving the source at true position was rejected because it violates the virtual-source objective and produces wall-panning artifacts.
Scalability potential: Low/MX350 still bypasses portal A*. Middle now pays the same fixed route cost but gets the intended positional illusion. High/Ultra can spend the saved deterministic path on richer room metadata without changing callers.
Hardware Impact: Runtime cost is unchanged except one branch and one multiply only after a valid portal path. Expected cost is below measurement noise on i3/MX350; qualitative gain is correct portal panning and attenuation. Recompile evidence: `Hecton8.Audio.Propagation.rsp` clean; full core blocked by unrelated `GasDynamicsSolver.TrySetRoomSubmergedFraction`.
