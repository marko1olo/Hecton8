# LOG_VERLET_TOW_WINCH

## 2026-05-16 - Verlet Tow Winch Implementation

What was wrong:
- Tow cable authority was still exposed as a procedural tether path without the full VERLET_TOW_WINCH contract: no published 10-segment SOA cable state, no explicit tension signal, no local-offset AUP-safe Verlet authority, no High/Ultra indirect render path, and no `PeakCableTension` blackbox field.
- Full compile validation is currently blocked by unrelated cross-domain errors in player kinematics, visor, spatial audio, AI/fauna contracts, and registry contract types.

What was done:
- Added fixed DataVault BufferID lanes for canonical cable positions, previous positions, velocities, masses, segment tensions, and blackbox ownership.
- Implemented `VerletCableSolverJob` with pinned endpoints, segment stretch correction, `math.rsqrt` normalization, finite guards, and per-segment tension output.
- Converted active Verlet authority to local offset space relative to the tow anchor, with visual upload converting back to runtime world coordinates.
- Added DataVault/fallback SOA publication for 11 canonical cable points and 10 canonical cable segments.
- Added `TetherTensionSignal` publication with AUP endpoints, force, snap threshold, normalized tension, reactive VFX scalar, and node count.
- Added Low/MX350 3-segment authority and high-tension straight-line visual fake.
- Added High/Ultra `Graphics.RenderMeshIndirect` rendering through a persistent six-vertex cylindrical impostor segment mesh and shader `SV_InstanceID` segment mapping.
- Added reactive stress shader scalar path plus high-threshold creak/snap impact signals.
- Added velocity clamping to `MaxCableVelocity`, `PeakCableTension` telemetry, and `Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin` dump paths.
- Moved tether fixed tick registration to `PriorityLayer.Environment`, ahead of `PlayerKinematicsRuntime` in `PriorityLayer.Player`.
- Routed equal/opposite endpoint force packets through `PhysicsForceRouter`, scaled by `MassSub / (MassSub + MassWreck)`.
- Snap now clears the cable DataVault slot and publishes an `ImpactSignal` with snap material hash.

Cinematic Cheats used:
- Low tier uses 3 authority segments and a taut straight-line visual fake above high tension instead of spending 10-segment solve cost on MX350.
- High/Ultra spend saved CPU on indirect cylindrical impostor rendering and stress pulse instead of increasing physics realism.
- Motion vectors use camera-valid render mode to avoid invalid per-object history from procedural cable deformation.

Exact Microseconds saved:
- Singleton/joint purge: 0 us direct runtime, avoids unbounded PhysX spring-solver spikes.
- Low tier 3-segment solve vs 10-segment high path: estimated 6-12 us saved per active tether step on i3/MX350.
- Local-offset rebase: estimated <1 us for 11 nodes; prevents precision-failure recovery cost.
- Velocity clamp: estimated <1 us per 11-node integration pass.
- DataVault SOA publish: estimated +3-6 us per active tether; accepted to buy zero-GC cross-system visibility.
- Tension signal publish: estimated <2 us when active.
- Snap cleanup: estimated <3 us for the fixed DataVault slot.
- High/Ultra indirect draw: estimated 0-5 us CPU saved versus repeated procedural segment submission; profiler evidence is pending.

Validation:
- `rg` verified no first-party tether singleton/joint path in touched tether files.
- `rg` verified `math.rsqrt` in `VerletCableSolverJob`.
- `git diff --check` returned no whitespace errors for touched files; line-ending warnings are repository-normal CRLF conversion warnings.
- Compile attempt 1: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` failed on unrelated cross-domain errors, with no tether errors in the reported set.
- Compile attempt 2: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed before tether validation on missing AI/animation/registry contract types.
- Compile attempt 3: full `Assembly-CSharp.csproj` errors-only build timed out after 306 seconds with no final compiler result.
- Unity runtime and profiler validation were not executed.
