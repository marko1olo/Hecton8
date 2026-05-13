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
