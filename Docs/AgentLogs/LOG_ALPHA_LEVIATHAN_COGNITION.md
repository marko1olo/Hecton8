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
