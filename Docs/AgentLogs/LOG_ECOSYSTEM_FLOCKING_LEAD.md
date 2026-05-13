# LOG_ECOSYSTEM_FLOCKING_LEAD

## 2026-05-13 - Boid Compute Evasion
What was wrong: GPU fish had no predator/player AUP scatter contract in the Sargassum compute path. Existing CPU-side fish avoidance was forbidden, and `PlayerMovedSignal` does not exist as a concrete type in this tree.
What was done: Routed swarm threat awareness through existing `IEncounterDirectorService.TryGetPredatorAupGpuBuffer`; slot 0 is player/sub AUP, slots 1-15 are predators. `SargassumMicroFaunaBoids.compute` consumes `_PredatorAUPBuffer` as `StructuredBuffer<float4>`, applies tier-capped GPU flee, breaks cohesion to 0.1 while fleeing, and keeps fluid advection after panic acceleration. `AcousticPingSignal` snapshots inject short massive threats and publish `SwarmDispersedSignal`; `FoodChainTelemetryFlagBoidsScattered` writes into the fixed 300-frame ring.
Cinematic cheats used: squared radius gates instead of physics overlap, `rsqrt` scatter normalization, squared-speed movement acoustic radius gate, low-tier 4-threat cap, acoustic ping as a one-frame massive-threat fake, and retained flow drift as a visual layering cheat.
Exact microseconds saved: CPU boid flee avoided O(5000) iteration, estimated 100-400 us on i3/MX350 versus CPU avoidance. Low-tier GPU saves up to 12 predator checks per boid, about 60k threat comparisons at 5000 boids. Movement acoustic sqrt removal saves <5 us per event burst. Exact profiler capture is blocked.
Verification: `validate_script` passed for `EncounterDirector.cs`; MCP validation for huge `GlobalSignals.cs`/`SargassumMicroFaunaBoids.cs` timed out. Unity MCP session became unavailable. `dotnet build Hecton8.Core.csproj --no-restore` failed on unrelated missing scheduling/layout/audio/CCD/crafting/tether dependencies and interface drift, not on the new boid scatter surface. Status remains PENDING VERIFICATION due global compile dependency.
Integrator note: `Hecton8.AI.Boids` asmdef does not exist. Creating it inside `Scripts/World` would drag unrelated world scripts into a new assembly; task 3 is blocked for an asmdef owner.

---

Agent: ECOSYSTEM_FLOCKING_LEAD
Timestamp: 2026-05-13
Status: FOLLOW-UP HARDENING

What was wrong: `_PredatorAUPBuffer` is declared by the compute kernel, but Sargassum only bound the published director buffer when `EncounterDirector` was initialized and non-empty. Unity can reject dispatches with an unset structured buffer even when shader loop count is zero.
What was done: Added a zeroed 16-slot fallback predator AUP buffer in Sargassum, bound it in static compute setup, and kept the active threat path unchanged: `EncounterDirector`'s published buffer overrides the fallback when valid.
Cinematic cheats used: Null threat presentation is a zeroed buffer, not a simulated predator state. Startup/service gaps show calm fish rather than blocking the GPU boid pass.
Exact microseconds saved: Runtime cost after initialization is expected 0 us because the fallback is only a bound safety target when count is zero. Failure cost avoided: one compute dispatch abort/stall during director registration gaps. VRAM cost paid: 256 B fixed.
Verification: `git diff --check` returned only CRLF normalization warnings. Unity MCP `validate_script` for Sargassum returned `no_unity_session`. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /clp:ErrorsOnly` timed out after 124s; the remaining `Hecton8.Core.csproj` build process was stopped.
