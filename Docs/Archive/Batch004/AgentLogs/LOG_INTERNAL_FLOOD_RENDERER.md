# INTERNAL_FLOOD_RENDERER Log

## 2026-05-13 - Camera Waterline Mask

What was wrong:
- Habitat flood state could exist as scalar gameplay data while the visor presentation stayed visually dry until full submersion.
- Singleton and mesh-water paths were forbidden; static scans found no `FloodVfxManager.Instance`, `WaterPlaneManager.Instance`, `WaterMeshPrefab`, or `Instantiate(WaterMeshPrefab)` to preserve.
- Low-tier waterline presentation risked paying for a useless refracted scene sample before correction.
- Compile proof is blocked by unrelated missing assemblies and types outside the habitat/visor waterline ownership.

What was done:
- Integrated the internal flood mask into the existing `HectonVisorUberPostFeature` and `HectonVisorUberPost.shader`; no second fullscreen pass was added.
- Added shader globals/material params for `_InternalWaterlineY`, `_InternalWaterColor`, `_InternalWaterlineRuntime`, `_InternalWaterlineParams`, and `_InternalWaterlineDistortion`.
- Verified runtime readback through `GlobalRegistry.HabitatGraph` / `IHabitatGraphService.RoomWaterLevels` and room waterline snapshots instead of direct concrete habitat dependencies.
- Registered `InternalFloodWaterlineRuntime` through bootstrap service setup so it can publish waterline globals, droplet timing, exhale bubble signals, acoustic crossing signals, gas submerged fraction, and origin-shift-safe AUP data.
- Kept the low-tier path tint-only by forcing refraction strength to zero and branching around the extra scene sample.
- Added fixed-size blackbox telemetry coverage through the waterline runtime and preserved dump path `Docs/AgentLogs/Dump_INTERNAL_FLOOD_RENDERER.bin` on non-finite detection.
- Corrected runtime telemetry header sizing from 48 bytes to 40 bytes and kept the 300-entry ring at 12,000 bytes.

Cinematic cheats used:
- Screen-space split from camera pitch and room water surface height instead of interior water meshes.
- Conditional one-sample refraction inside the existing Uber Post instead of a second fullscreen pass.
- Triangle-wave procedural water offset instead of sine waves or physical wave simulation.
- Procedural hash droplets over timed scalar state instead of particle decals, coroutines, or spawned wet-glass objects.
- Acoustic and bubble feedback routed through existing signal lanes instead of managed scene object churn.
- Gas coupling uses one submerged scalar per room, not per-voxel water/gas simulation.

Exact microseconds saved, static estimates only:
- No second fullscreen pass: 180-450us saved on MX350-class render scale.
- No per-room water mesh spawn/render path: 35-120us saved per room event plus draw and GC avoidance.
- Low-tier branch skips refracted scene sample: 90-220us saved when the mask covers a large screen region.
- Triangle-wave replacement for added internal-water sine offset: 8-24us saved on large-mask frames.
- Cached room lookup versus scene/module scan: 4-12us saved in populated bases.
- Signal-based acoustic crossing versus `AudioSource.PlayOneShot` object path: 20-60us saved per crossing.
- Scalar gas submerged fraction versus richer gas/water simulation: preserves deterministic cost at roughly 0.5-2us per gas solve room.

Verification:
- Unity MCP script validation is blocked: `Unity session not available; reason no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false` is blocked by 113 unrelated missing dependency/type errors, including `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, resource spawner read models, acoustic portal contracts, inventory algorithms/corrosion, and binary layout attributes.
- Static scans passed for banned singleton/mesh-water symbols.
- Final status remains `PENDING VERIFICATION` until Unity import, console, and profiler data exist.

## 2026-05-13 - Patient Upgrade Pass

What was wrong:
- Inactive waterline state could still call shader global uploads every FastTick after context loss.
- Gas submerged-fraction writes could be lost while the gas solver job was running.
- Surfacing droplets were tied to the underwater mask, so they failed the requested visor-wide 2-second wet-lens behavior.
- Telemetry flags did not expose submerged/low-tier/pending-gas state.

What was done:
- Added dirty-checked shader global writes and inactive early-out.
- Cached habitat graph, gas solver, and quality tier on a 30-tick cadence.
- Added a zero-GC pending retry slot for gas submerged-fraction pushes.
- Moved droplets to a full-visor procedural mask; low tier stays additive-only, higher tiers get transient one-sample droplet refraction.
- Preserved the droplet timer after the waterline clears so surfacing remains visible for the full 2 seconds.
- Expanded telemetry flags without changing the 40-byte telemetry entry size.

Cinematic cheats used:
- Full-visor procedural droplet mask instead of particles, decals, or spawned visor objects.
- Triangle-wave droplet offset instead of physical water beads.
- One transient high-tier-only sample instead of a second fullscreen pass.
- Dirty globals and scalar retry instead of heavier cross-domain synchronization.

Exact microseconds saved, static estimates only:
- Dirty shader globals on inactive frames: 3-12us saved.
- No second droplet pass: 180-450us still avoided.
- Pending gas retry avoids correctness loss at roughly one scalar call per retry frame.
- Low-tier droplet path avoids extra scene sample: 20-80us saved during the 2-second surfacing window versus high-tier droplets.

Verification:
- Scoped `git diff --check` passed with CRLF warnings only.
- Unity MCP validation remains blocked: `no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false` remains blocked by the same 113 unrelated missing dependency/type errors.
- Final status remains `PENDING VERIFICATION`.
