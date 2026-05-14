# LOG_ALPHA_LEVIATHAN_COGNITION

Status: PENDING VERIFICATION

What was wrong:
- Standard predator cognition could collapse into direct chase/bite behavior.
- Alpha Leviathan had no explicit fog-stalking phase lane, no false-charge/no-hit authority, and no dedicated black-box phase telemetry.
- Local generated build path is not trustworthy: `Hecton8.Core.csproj` fails with 131 missing cross-asmdef/generated references before a useful domain compile.

What was done:
- Added `Hecton8.AI.Cognition` contract asmdef with Alpha phase constants and 64-byte telemetry entry.
- Added Alpha SoA state to `PredatorCognitionDomain`: phase byte, phase start time, 300-entry telemetry ring, and binary dump on invalid state.
- Consumed `AcousticPingSignal` in `CreatureUtilityBrain` as an AUP target; ignored Leviathan roar echo.
- Added fog-ring circling at `FogEnd - 10m`, gaze/headlight dive behavior, low-tier radial fallback, 30 m/s false charge, roar signal, and <15m veer-off with `ShouldAttack` cleared.
- Bypassed biomass/ecology overrides for apex predators.
- Kept Alpha evaluation on 10Hz slow-tick cadence.

Cinematic Cheats used:
- Fog silhouette ring, not honest orbit physics.
- One-gradient SDF dive fake on high tier; radial break on low tier.
- Dear-lie false charge: Feint + roar + no hit.
- Binary black box instead of per-frame text logs.

Microseconds saved:
- Exact measured microseconds: unavailable; Unity session unavailable and local generated build is dependency-red.
- Static hot-path estimate: radial low-tier dive saves the SDF-gradient branch, estimated ~0.09 us per active Alpha slow tick versus high tier.
- Static hot-path estimate: no NavMesh/path spline saves an unbounded main-thread query; Alpha branch remains scalar Burst math, estimated <0.1 us per active Alpha eval.
- Static allocation saving: 0 B/frame in Alpha hot path; telemetry is fixed 19.2 KB session memory.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: BLOCKED BY DEPENDENCY, 131 global missing-reference errors.
- `dotnet build-server shutdown`: completed.
- Unity MCP refresh: timed out after 60s; console unavailable because no Unity session was attached.
- Static scans: Alpha direction/distance paths use `math.rsqrt`; no new managed collection/LINQ allocation in Alpha hot branch.

---

Second-pass upgrade: 2026-05-14

What was wrong:
- Acoustic ping AUP conversion used the implicit current-origin helper before cognition input packed its own floating-origin offset. That allowed a mixed-origin target if rebasing happened inside the evaluation window.
- Phase byte `3` behaved correctly as the no-hit veer-off but was not explicitly exposed as `Strike`, which weakened the batch contract for telemetry consumers.
- The false charge relied on acoustic/physiology ordering for the stress spike, while prompt DOD requires immediate `PlayerStress01` pressure.

What was done:
- Re-extracted `<AGENT_PROMPT id="ALPHA_LEVIATHAN_COGNITION" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-aware CLI regex.
- Captured `HectonFloatingOrigin.CurrentTotalOffset` once per `CreatureUtilityBrain.Evaluate`, used that same `float3` for acoustic `AUPMath.ToRuntimeFloat3(...)` and `CognitionInput.FloatingOriginOffset`.
- Added `AlphaLeviathanPhase.Strike = 3` and kept `VeerOff = Strike` so byte phase 3 satisfies the prompt while still enforcing the no-hit escape.
- Added a one-shot `PlayerStressSignal` on Alpha roar emission via `GlobalSignals`, with `Stress01 = 1`, apex/acoustic flags, and no direct physiology mutation.

Cinematic Cheats used:
- Coordinate lock: one committed-origin snapshot for the full cognition evaluation instead of chasing live Transform state.
- Phase-3 lie: telemetry can call it Strike; runtime still veers off and strips attack.
- Stress spike: one 32-byte global signal, not a sustained simulation or direct player-system coupling.

Exact Microseconds saved:
- Measured microseconds unavailable; Unity editor MCP transport is offline and local generated build remains dependency-red.
- AUP fix cost: one `Vector3` read + `float3` pack per slow evaluation; expected <0.01 us on i3/MX350-class hardware, 0 B/frame.
- Strike alias cost: compile-time constant only, 0 us.
- Stress signal cost: one 32-byte queue write on false-charge transition only, 0 B/frame hot path.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: still blocked by generated/cross-asmdef dependency wall, now 127 errors. Primary examples: missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Virtualization`, stale generated project reference for `Hecton8.AI.Cognition`.
- Unity MCP `refresh_unity` and `read_console`: failed at HTTP transport to `127.0.0.1:8088/mcp`; no Unity console proof available.
- `git diff --check`: no whitespace errors; only repository LF-to-CRLF warnings.
- Static scans: no remaining implicit `PositionAup.ToRuntimeFloat3()` acoustic conversion in `FaunaBrain.Compatibility.cs`; Alpha hot-path allocation scan only reports pre-existing instance scratch lists in `FaunaBrain`.

---

Third-pass upgrade: 2026-05-14

What was wrong:
- Hidden/dive phase could collapse back to circling after one 10Hz evaluation once gaze or retinal exposure stopped.
- Alpha black-box telemetry could claim SDF dive on low tier even though low tier intentionally uses radial fake steering.
- Telemetry did not recompute and record the player-gaze break bit, reducing postmortem value.

What was done:
- Added `AlphaHiddenHoldSeconds = 1.15f` to keep phase 0 readable after gaze/headlight break without adding another state lane.
- Added `ResolveAlphaTelemetryDirection(...)` using `math.rsqrt` and zero-allocation scalar math.
- Changed Alpha telemetry flags so `PlayerGazeBreak` reflects the actual dot test and `SdfDiveRequested` only appears for high-tier Hidden with a player target.

Cinematic Cheats used:
- Long-enough vanish: a 1.15s fake hide, not pathfinding or burrow simulation.
- Truthful telemetry: low tier records the cheap radial fake; high tier records the SDF visual fake.

Exact Microseconds saved:
- Measured microseconds unavailable; Unity editor transport remains offline.
- Hidden hold costs one scalar comparison per 10Hz Alpha evaluation, estimated <0.01 us on i3/MX350.
- Telemetry direction recompute costs two `math.rsqrt` paths per active Alpha post-eval write; no hot-frame heap pressure.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with attribute-aware CLI regex.
- Static math scan found no sqrt/normalize/length calls in Alpha-scoped files.
- Allocation scan found no new managed allocation pattern in `PredatorCognitionDomain`; matches are pre-existing `FaunaBrain` scratch lists and guarded/fault logs.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: still blocked by generated/cross-asmdef reference wall, now 132 errors.
- Unity MCP `refresh_unity` and `read_console`: still offline at `127.0.0.1:8088/mcp`.
