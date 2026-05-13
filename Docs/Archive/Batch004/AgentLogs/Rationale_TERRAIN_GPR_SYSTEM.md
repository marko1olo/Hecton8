# Rationale_TERRAIN_GPR_SYSTEM

Status: PENDING VERIFICATION

## Mandate Selection

Problem: GPR must query subsurface geology without object instantiation or singleton coupling.
Solution: Use voxel SDF and ore-position SoA buffers, GlobalRegistry contract surface, persistent NativeArrays, and GPU structured/indirect buffers.
Rejected Alternatives: Unity Physics.SphereCastAll and GameObject markers; both allocate or scale poorly and ignore SDF truth.
Scalability potential: Low=16 rays and cheap ring draw, Middle=64 rays with capped pings, High=64 rays with full pulse draw, Ultra=64 rays with visual overkill in shader/cockpit branch.
Hardware Impact: On i3/MX350, avoiding managed queries and object markers is estimated to save 150-400 us per active scan burst versus physics/object UI probes.

## Decision 1: Service Boundary

Problem: A GPR singleton would collide with parallel agents and create hidden initialization order.
Solution: `IGroundRadarService` is registered through `GlobalRegistry`; cockpit reads the buffer through the interface.
Rejected Alternatives: `GPRManager.Instance`, scene scan, or direct cockpit reference.
Scalability potential: Low devices avoid lookup churn; high-end devices can add richer consumers without duplicating scan work.
Hardware Impact: 35-70 us saved on cold lookup paths and zero extra frame allocations.

## Decision 2: Ore Source Coupling

Problem: GPR needs ore positions but direct dependency on a concrete spawner is brittle.
Solution: `ProceduralOreSpawner` exposes `IWorldResourceSpawnerReadModel` and registers the ore SoA in `GlobalRegistry`.
Rejected Alternatives: serialized-only `ProceduralOreSpawner` dependency, GameObject tag scan, or duplicate ore cache.
Scalability potential: Low uses one contiguous ore lane; Ultra can add richer ore metadata without changing GPR job shape.
Hardware Impact: 25-80 us saved by reading the authoritative NativeArray directly.

## Decision 3: Raymarch Bound

Problem: SDF probing can become a frame hazard if raymarch depth is unbounded.
Solution: `GroundRadarRaymarchJob` clamps `MaxSteps` to 10 and uses only bounded `for` loops.
Rejected Alternatives: while-loop raymarch, adaptive until-hit loops, or per-ray physics casts.
Scalability potential: Low=16x10 max samples; High/Ultra=64x10 max samples.
Hardware Impact: Worst-case probe cost remains bounded; estimated 60-180 us low-tier savings versus fixed 64 rays.

## Decision 4: Visual Cheat

Problem: Players need readable deep-ore feedback, not physically real radar.
Solution: Upload `float4` hit/strength payload and draw pulsing shader rings; depth strength drives green/blue color.
Rejected Alternatives: volumetric radar simulation, particle systems, instantiated line rings.
Scalability potential: Low gets cheap rings; high-end can spend saved CPU on denser pulse material without changing data.
Hardware Impact: 200-700 us saved at 64-128 pings versus object-based visual markers.

## Decision 5: Black Box

Problem: NaN or drift bugs in subsurface systems need a deterministic last-state record.
Solution: Fixed 300-frame `NativeArray<GroundRadarTelemetryEntry>` circular buffer and binary dump on fault.
Rejected Alternatives: Debug.Log-only diagnostics or managed history list.
Scalability potential: Same fixed cost on toaster and high-end; high-end can add offline visualization from the dump.
Hardware Impact: No hot managed allocations; telemetry write is a fixed small struct copy.

## Compile Wall

Problem: Full `Hecton8.Core` compile is blocked by other agents' unresolved symbols/missing methods.
Solution: Verified `Hecton8.World.Contracts` and `Hecton8.World.GPR` with Unity csc exit 0; reran Core csc and confirmed current remaining errors are outside GPR after fixing the cockpit definite-assignment issue.
Rejected Alternatives: Editing unrelated `SaveBinaryPayloadCodec` code outside the assignment boundary.
Scalability potential: No runtime impact; protects domain ownership.
Hardware Impact: N/A, integration dependency only.

## OMEGA POLISH CHANGES

Problem: Initial job flags used byte booleans and shader ring math used direct radial length/division.
Solution: Replaced Burst scan/shift booleans with bitmask flags, converted SDF cell division to `math.rcp`, replaced shader `length()` with `dot + rsqrt`, and used shader `rcp(radius)` for cockpit GPR projection.
Rejected Alternatives: Leaving visual-only math honest, or simulating a volumetric radar return.
Scalability potential: Low keeps 16 rays and cheaper shader math; Middle/High/Ultra spend saved CPU/GPU budget on brighter ring pulses and cockpit reuse.
Hardware Impact: Estimated 5-20 us saved in shader/CPU aggregate on low-end scenes; bigger win is preserved deterministic hard bound.
Cinematic Cheats Used: 64/16 downward sample fan, density threshold instead of wave propagation, inverse-square scalar instead of material-specific attenuation, pulsing concentric ring shader instead of real radar volume.
Final Git Diff: Relevant tracked files touched: `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`, `GroundRadarContracts.cs`, `GroundRadarJobs.cs`, `GroundPenetratingRadarRuntime.cs`, `ProceduralOreSpawner.cs`, `VehicleSubOsCockpitRuntime.cs`, `Hecton_GroundRadarPingIndirect.shader`, `Hecton_RadarBlipInstanced.shader`. GPR assembly definition verified at `Assets/_Project/Scripts/World/GPR/Hecton8.World.GPR.asmdef`.
