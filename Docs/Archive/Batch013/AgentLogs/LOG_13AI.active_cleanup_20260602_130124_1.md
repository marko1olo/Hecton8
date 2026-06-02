# LOG_13AI

## 2026-05-27 AI Domain Audit/Fix

What was wrong:
- `AcousticEchoLocationRuntime` allowed `TryUpdatePredatorEcho()` and echo enqueue/hydration paths to bootstrap/refresh DataVault state from query/producer routes.
- `StressDrivenSpawnDirector` forced Ultra-like spawn work through a constant quality weight in budget/probe/cull paths.
- Phantom drone visual swarm computed a quality draw count but still dispatched and drew full `PhantomDroneCount`.
- Real headless drone renderer had GPU culling buffers and shader code, but `TryRenderGpuCulledFleet()` returned false and the draw path used full `HeadlessDroneCapacity`.
- `AmbientBiotaDirector` exposed read-only DataVault views while owner jobs could still be pending.

What was done:
- Added `AcousticEchoLocationRuntime.TickOwnerFrame()` and routed it through `FaunaDirector.Tick()` after acoustic signal drains.
- Made `TryUpdatePredatorEcho()` read cached owner state only; removed owner refresh and black-box writes from the predator query.
- Made `TryEnqueueEchoTap()` fail closed unless owner bootstrap already exists and resolve pending taps without DataVault acquire/grow.
- Deferred invalid echo producer faults to owner phase before black-box dump.
- Replaced stress spawn's constant quality use with continuous `input.GlobalQualityWeight` for candidate score, budget, probability bias, hidden radius/probes, cull radius, and debug.
- Kept fixed behavior truth under `AuthoritativeBehaviorWeight` for cognition values that must not change with device quality.
- Wired phantom drone indirect args, dispatch groups, and compute capacity to `phantomDrawCount`.
- Activated real-drone GPU append culling and indirect count copy with fallback to the previous full-capacity path when compute/camera resources are absent.
- Gated ambient biota public read views while `_jobPending` is true.

Cinematic cheats used:
- Acoustic echo remains a single owner-updated breadcrumb trail, not continuous sound-wave simulation.
- Stress spawn scales hidden-probe count and probability continuously instead of simulating exact ecosystem populations.
- Phantom drones remain GPU-authored procedural visuals; quality controls visible swarm count instead of physical drone truth.
- Real drone culling uses frustum/distance append compaction, not CPU object-level render decisions.

Exact microseconds saved:
- Verified measured savings: 0 us. No profiler/GCMonitor/player run was available.
- Acoustic static estimate: removes 5-30 us cold-spike risk from predator query/producer paths by moving DataVault ensure/refresh to owner phase.
- Stress spawn static estimate: minimum quality uses 3 hidden probes instead of up to 19, removing 16 probe evaluations per spawn attempt.
- Phantom swarm static estimate: quality 0 dispatches/draws 0 instead of 500 instances; quality 0.5 dispatches/draws about 250 instead of 500.
- Real drone render static estimate: visible count replaces 512-capacity draw; if 40 are visible, 472 instances are skipped, 92.2% fewer instances for that draw.

Verification:
- `git diff --check` on touched source files passed with CRLF warnings only.
- `dotnet build` not launched. Latest gate sample: CPU 100%, dotnet/csc count 8. AGENTS forbids build when CPU >50% or dotnet/csc is running.
- Unity import, Play Mode, shader import, RenderDoc, profiler, and GCMonitor proof remain PENDING VERIFICATION.

Residual domain debt:
- `HectonBoidController` still lacks a DataVault-backed 300-frame NativeArray black box.
- `PathFunnelNavmeshRuntime.TryReadVoxelPathResult()` still performs a bounded scan over result capacity.
- `RepairDroneHub` still keeps active hubs in a static managed list.
- GPU culling activation needs Unity/RenderDoc proof before it can be called runtime-ready.

## 2026-05-27 AI Domain Follow-Up: Boid Black Box

What was wrong:
- `HectonBoidController` was a critical fish AI/visual flock controller without a 300-frame native Black Box.
- The initial boid Black Box buffer id `71990` collided with `ShinobuParasiteProfileCount`.
- Acoustic and boid fault dumps did not use the literal `Docs/AgentLogs/Dump_13AI.bin` proof path.

What was done:
- Added DataVault-backed `BoidBlackBoxEntry[300]` at `(BufferID)71979`, owned by `SystemID.AIEcology`.
- Wrote bounded owner-frame telemetry from `LateFrameTick()`/`RunBoidVisualSync()` with fault flags and state hash.
- Added one-shot fault dump to `Docs/AgentLogs/Dump_13AI.bin`.
- Moved acoustic echo fault dump to the same 13AI proof path.

Cinematic cheats used:
- Fish flock forensics records high-level state only: target, bounds, grid, quality weights, acoustic ping vectors, and counts. It does not read back GPU boid particles or simulate exact fish truth on CPU.

Exact microseconds saved:
- Verified measured savings: 0 us. No profiler/GCMonitor/player run was available.
- Added cost: one bounded 128B DataVault write plus primitive hash math per owner frame; runtime cost is unverified and must be profiled against the 0.1 ms suspicion line.
- Avoided risk: `71990` DataVault aliasing with parasite VFX, which could have produced undefined state corruption rather than a measurable frame-time regression.

Verification:
- Static source scan confirms boid Black Box uses `(BufferID)71979`; `71990` remains owned by parasite VFX.
- Static source scan confirms acoustic and boid fault dump paths are `Docs/AgentLogs/Dump_13AI.bin`.
- Source `git diff --check` for acoustic and boid files passed with CRLF warnings only.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on external `Assets/Candice AI for Games/.../CandiceSQLiteProvider.cs`: missing `Mono.Data` namespace and `SqliteDataReader`. No 13AI-owned file appeared in the reported errors before the external failure.
- Unity import, Play Mode, fault injection, dump generation, profiler, and GCMonitor proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Repair Drone Hub Cleanup

What was wrong:
- `RepairDroneHub` used a static managed `List<RepairDroneHub>` for active hub identity in a route read by drone fleet and pollution telemetry.
- `RepairDroneHub.DockingAirlock` could search components from a read accessor when the cache was empty.
- Repair supply hash lookup recomputed default/legacy `LocHash` values, and sortie diagnostics touched `task.Module.name` on the runtime path.
- `PathFunnelNavmeshRuntime.TryReadVoxelPathResult` was audited as suspected pathfinding debt.

What was done:
- Replaced the active hub list with fixed `RepairDroneHub[32]` and explicit count.
- Used stable bounded removal to preserve previous hub ordering because fallback consumers read `GetActiveHubAt(0)`.
- Moved airlock component search to cold lifecycle cache calls in `Awake`, `OnEnable`, and `OnSpawn`.
- Made `DockingAirlock` a pure cached read.
- Added cached repair supply hash ids and per-item hash cache.
- Added `_debugCurrentTargetId` for runtime and kept `_debugCurrentTargetName` assignment editor-only.
- Left `PathFunnelNavmeshRuntime.TryReadVoxelPathResult` unchanged after audit: bounded 1024 scan, read-only, allocation-free, no hidden job completion.

Cinematic cheats used:
- Repair drones still use bounded hub/task scans and headless fleet state. No physical drone docking simulation or exact logistics search was added.
- Hub identity stays gameplay truth; visual richness remains in fleet/phantom rendering, not in object registry complexity.

Exact microseconds saved:
- Verified measured savings: 0 us. No profiler/GCMonitor/player run was available.
- Static estimate: removes player-runtime `module.name` string access during sortie launch.
- Static estimate: removes repeated default/legacy repair `LocHash.Compute` calls from logistics availability checks.
- Static estimate: removes possible first-read `GetComponentInParent<BaseAirlock>()` miss from `DockingAirlock`; remaining cost is cold lifecycle only.
- Static estimate: fixed hub unregister performs at most 31 reference moves and no managed list growth/capacity behavior.

Verification:
- `cmd /c "git diff --check -- Assets/_Project/Scripts/Construction/RepairDroneHub.cs"` returned only CRLF normalization warning.
- `cmd /c "git diff --check -- [touched 13AI files]"` returned only CRLF normalization warnings.
- `RepairDroneHub.cs` brace count is 97/97 and `#if/#endif` count is 3/3.
- Static scan found no `s_ActiveHubs.Count`, `.Add`, `.Remove`, `.Clear`, `new List`, or `using System.Collections.Generic` residue in `RepairDroneHub.cs`.
- Build was not launched after this patch. Gate sample was CPU 100% with 8 active dotnet/csc processes, so AGENTS forbids `dotnet build`.
- Unity import, Play Mode, profiler, GCMonitor, and runtime drone behavior proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Drone Fleet Repair Signal Contract

What was wrong:
- `DroneFleetManager` wrote `HectonQualityTier` enum values into `HullRepairedSignal.QualityTier`.
- `RepairTool` already writes the same field as a continuous 0..255 quality byte from `GlobalQualityWeight`.
- `DroneFleetManager.ResolveAuthoritativeQualityWeight()` was a misleading name for cadence, budget, heuristic, probe, and metadata Math LOD routes.

What was done:
- Renamed the local helper to `ResolveDroneSimulationQualityWeight()`.
- Changed drone repair signal publication so `QualityTier` is `round(GlobalQualityWeight * 255)`, clamped to byte range.
- Left `HullRepairedSignal` layout, flags, AUP route, room route, source hash, dent data, and completion authority unchanged.

Cinematic cheats used:
- Drone repair presentation metadata now scales continuously with quality while hull repair truth stays fixed.
- No physical docking, repair beam, or damage propagation simulation was added. The repair completion signal remains the cheap first-party route.

Exact microseconds saved:
- Verified measured savings: 0 us.
- No runtime saving is claimed. This was a contract-correctness fix, not a frame-time optimization.
- Avoided risk: future consumers reading mixed enum/continuous quality metadata from the same signal lane.

Verification:
- Targeted scan found no `ResolveAuthoritativeQualityWeight`, no `ResolveDroneRepairQualityTier`, and no `HectonQualityTier` residue in `DroneFleetManager.cs`.
- `DroneFleetManager.cs` brace count is 729/729.
- `git diff --check` for `DroneFleetManager.cs` returned only CRLF normalization warning.
- Build gate sample before compile: CPU 8%, dotnet/csc count 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on external `Assets/Candice AI for Games/.../CandiceSQLiteProvider.cs`: missing `Mono.Data` namespace and `SqliteDataReader`. No 13AI-owned file appeared in the reported errors before that dependency wall.
- Unity import, Play Mode, profiler, GCMonitor, and runtime drone signal consumer proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Fauna Registry Cache

What was wrong:
- `FaunaSensorSuite.TrySampleClosedNavGridCell()` polled `GlobalRegistry.ResourceDistribution` from obstacle avoidance and player line-of-sight sensory sampling.
- That path is reachable from active fauna brain ticks and can run line-of-sight plus three obstacle probes.
- `FaunaDirector.SlowTick()` helper methods also polled `GlobalRegistry.DepthZoneReadModel` and `GlobalRegistry.VegetationThreat` when cached services were missing.

What was done:
- Added a cached `IBrineFluidDensityReadModel` route to `FaunaSensorSuite`.
- Bound that read model from `FaunaBrain.RefreshColdRegistryDependencies()` through `GlobalRegistry.BrineFluidDensity`.
- Updated the cache from `FaunaBrain.OnGlobalRegistryServiceReplaced(ResourceDistributionRuntime)`.
- Removed direct `GlobalRegistry.ResourceDistribution` reads from fauna sensory sampling.
- Removed slow-tick fallback polling of `GlobalRegistry.DepthZoneReadModel` and `GlobalRegistry.VegetationThreat`; fauna director now relies on cold refresh, hot-swap notification, and serialized local fallback.
- Removed the dead no-op vegetation resolver and all remaining calls to it, including the `SlowTick()` call.

Cinematic cheats used:
- No new physical perception simulation was added.
- Brine/nav-grid perception remains a cheap read-model sample, not a raycast-heavy or scene-query route.
- Visual/behavior richness should be bought with existing continuous quality/cadence controls, not by making sensory probes discover services at runtime.

Exact microseconds saved:
- Verified measured savings: 0 us.
- Static estimate: removes up to four `GlobalRegistry.ResourceDistribution` reads per active fauna sensory tick in the inspected path.
- Static estimate: removes two recurring fauna director slow-tick registry reads when depth/vegetation services are absent.
- Avoided risk: hidden dependency lookup jitter in AI sensory and director cadence work on i3/MX350-class hardware.

Verification:
- Targeted scan confirms `FaunaSensorSuite.cs` no longer contains `GlobalRegistry.ResourceDistribution`; `FaunaBrain` cold bind uses the interface accessor `GlobalRegistry.BrineFluidDensity`.
- Targeted scan confirms `GlobalRegistry.DepthZoneReadModel` and `GlobalRegistry.VegetationThreat` in `FaunaDirector.cs` are limited to cold registry refresh.
- Brace counts matched for `FaunaSensorSuite.cs`, `FaunaBrain.cs`, and `FaunaDirector.cs`.
- `git diff --check` for touched fauna source and 13AI log/status files returned only CRLF normalization warnings.
- Build gate later passed: CPU 37%, active `dotnet/csc` count 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on external `Assets/Candice AI for Games/.../CandiceSQLiteProvider.cs`: missing `Mono.Data` namespace and `SqliteDataReader`. No 13AI-owned file appeared in the observed error set.
- Unity import, Play Mode, profiler, GCMonitor, and runtime fauna behavior proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Encounter Director Dependency Cache

What was wrong:
- `HectonDirectorAI.Tick()` read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` every director frame before computing acoustic vegetation threat.
- Predator sight contact processing called `GlobalRegistry.MapMagic` inside `IsPredatorSightTerrainBlocked()` before terrain height samples.
- This violates the project route rule: `GlobalRegistry` is cold identity/dependency injection, not a hot polling path.

What was done:
- Added cached `_terrainProvider` and `_vegetationBridge` fields to `HectonDirectorAI`.
- Bound them from `RefreshColdRegistryReferences()` using `GlobalRegistry.Terrain` and `GlobalRegistry.MapMagicVegetation`.
- Updated the same caches from `OnGlobalRegistryServiceReplaced()` on `TerrainProviderRuntime` and `MapMagicVegetationRuntime`.
- Rewired director acoustic vegetation pressure to read `_vegetationBridge`.
- Rewired predator LOS terrain occlusion to read cached `ITerrainProvider`.

Cinematic cheats used:
- No expensive physical sight simulation, raycast fan, or terrain query system was added.
- Predator visibility still uses three cheap terrain-height samples.
- Vegetation pressure remains a cheap threat-level read. The patch fixes dependency routing, not perception truth.

Exact microseconds saved:
- Verified measured savings: 0 us.
- Static estimate: removes one vegetation singleton read per director tick.
- Static estimate: removes up to three terrain registry reads per processed predator sight contact.
- Avoided risk: hidden dependency lookup jitter in encounter pressure and predator sight loops on i3/MX350-class hardware.

Verification:
- Targeted `rg` found no `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` and no `GlobalRegistry.MapMagic` in `HectonDirectorAI.cs`.
- Remaining `GlobalRegistry.Terrain` and `GlobalRegistry.MapMagicVegetation` reads are in cold registry refresh only.
- `HectonDirectorAI.cs` brace count is 187/187 and paren count is 718/718.
- `git diff --check` for `HectonDirectorAI.cs` returned only CRLF normalization warning.
- Build was not launched after this patch. Gate sample was CPU 100% with 8 active `dotnet/csc` processes, so AGENTS forbids `dotnet build`.
- Unity import, Play Mode, profiler, GCMonitor, and runtime encounter behavior proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Hot Dependency Pass 2

What was wrong:
- `HectonDirectorAI.RefreshRuntimeReferences(false)` was still reachable from `Tick()` and could fall through into player/camera scene or component fallback discovery.
- Predator sight tick processing could still call the DataVault allocation/acquisition route for spatial hash buffers.
- Predator sight cone orientation used `contact.Transform.forward`, making AI truth depend on scene transform state instead of the spatial-contact logic pose.
- Predator spatial hash insertion scheduled a 64-row helper job, which is not justified without profiler proof.
- `FaunaBrain` and `FaunaBrain.Compatibility` still read vegetation/voxel `ActiveRuntimeInstance` singletons in inspected cognition routes.

What was done:
- `HectonDirectorAI` non-force runtime refresh now only applies cached `IPlayerRuntimeContext` references and returns before scene/component fallback discovery.
- Player position/snapshot reads in the encounter director now use cached `_playerRuntimeContext.TryGetMovementRuntimeState()` / `TryGetLookRuntimeState()`.
- Predator sight tick now opens existing DataVault views only; acquisition remains in `OnEnable()` and DataVault hot-swap handling.
- Predator spatial cell coordinates are filled inline under the existing lock/finally path.
- Predator sight forward now uses `IFaunaSpatialContact.ResolveContactForward()` and dominant-axis quantization.
- `FaunaBrain` now caches `HectonMapMagicVegetationBridge` and `HectonVoxelEngine` from cold registry refresh and hot-swap callbacks.
- Inspected fauna fear pressure, voxel guidance, burrow ambush, director hunt, corpse floor, and compatibility chemical-pack logic now use those caches.

Cinematic cheats used:
- No new physical AI perception simulation was added.
- Predator terrain occlusion remains three cheap height samples.
- Spatial hash cell fill stays primitive integer math for 64 contacts instead of scheduling a tiny job.
- Fauna vegetation/voxel reads remain cheap cached owner calls; richer presentation should be bought with existing continuous quality/cadence controls.

Exact microseconds saved:
- Verified measured savings: 0 us.
- Static estimate: removes hidden scene/component fallback from the encounter director non-force tick refresh path.
- Static estimate: removes DataVault generation-handle acquisition risk from predator sight tick.
- Static estimate: removes one 64-row job schedule/completion/unlock route.
- Static estimate: removes six vegetation/voxel active singleton reads from inspected fauna cognition/compatibility routes.
- Avoided risk: transform-state jitter or mismatch feeding predator sight truth.

Verification:
- Targeted `rg` on edited files found no `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, no `HectonVoxelEngine.ActiveRuntimeInstance`, no `GlobalRegistry.MapMagic`, no `contact.Transform.forward`, and no `Schedule(PredatorSpatialHashContactCapacity` residue.
- Remaining targeted hits are cold `GlobalRegistry.MapMagicVegetation` binds and the recorded residual `FaunaBrain.TryResolveCachedPlayerRuntimeContext()` static player fallback.
- Brace/paren counts matched: `HectonDirectorAI.cs` 191/191 braces and 736/736 parens; `FaunaBrain.cs` 643/643 braces and 3377/3377 parens; `FaunaBrain.Compatibility.cs` 67/67 braces and 449/449 parens.
- Latest build gate sample after this pass: CPU 95%, active `dotnet/csc` count 3. `dotnet build` was not launched under AGENTS rules.
- Unity import, Play Mode, profiler, GCMonitor, runtime AI behavior proof, and crash/fault dump proof remain PENDING VERIFICATION.

## 2026-05-27 AI Domain Follow-Up: Residual Debt Pass 3

What was wrong:
- `FaunaBrain` still had a static player runtime fallback through `PlayerRuntimeContextService.TryGetActiveRuntimeContext()`.
- `PredatorCognitionDomain.RefreshThreatVoxelSnapshot()` still read vegetation and cave voxel-lighting active singletons.
- `DroneFleetManager` still read flora and MapMagic vegetation active singletons in inspected environment routes.
- Macro swarm hydration compressed continuous quality into a 0..3 byte and decoded it as `0, 1/3, 2/3, 1` before feeding the spawn job.

What was done:
- Replaced fauna player fallback with a frame-local snapshot sourced only from cached `IPlayerRuntimeContext`.
- Added owner-published vegetation/cave source caches for predator threat-voxel refresh.
- Added owner-published flora/vegetation bridge caches for inspected drone assignment, parasite, headless task-map, and abyssal flow payload routes.
- Changed ecosystem macro quality encoding to 0..255 and ambient macro hydration decoding to continuous `qualityByte / 255`.
- Kept shared service/signal layouts unchanged.

Cinematic cheats used:
- No new physical sight, water, or flora simulation was added.
- Predator threat voxel work remains cheap cached read-model sampling.
- Drone environment queries remain cached owner calls.
- Macro quality now scales existing spawn presentation inputs continuously instead of adding more simulation.

Exact microseconds saved:
- Verified measured savings: 0 us.
- Static estimate: removes one static player fallback route from inspected `FaunaBrain`.
- Static estimate: removes two active singleton reads from predator threat-voxel refresh.
- Static estimate: removes four active singleton reads from inspected drone environment routes.
- Static estimate: removes 4-step quality stair-stepping from macro hydration; no runtime allocation or new job added.

Verification:
- Targeted `rg` found no inspected residue for `FloraInteractionManager.ActiveRuntimeInstance`, `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, `HectonCaveVoxelLightingVolume.ActiveRuntimeInstance`, `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, or `TryResolveCachedPlayerRuntimeContext` in the patched AI/drone hot files.
- Remaining targeted hits are two `PlayerRuntimeContextService.TryGetActiveRuntimeContext` uses inside `HectonMapMagicVegetationBridge`, recorded as world-domain residual debt.
- Targeted `rg` found no `ResolveMacroVisualQualityByte`; ambient macro hydration now uses `ResolveMacroVisualQualityWeight01` plus 0..255 signal-byte encoding.
- Brace counts matched for all touched files. `PredatorCognitionDomain.cs` naive paren count remains 3432/3433, treated as non-code imbalance until compiler proof is available.
- `git diff --check` returned only CRLF normalization warnings.
- Build gate sample later passed: CPU 45%, active `dotnet/csc` count 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on external `Assets/Candice AI for Games/.../CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`. No 13AI-owned error was visible in the observed error set.
- Unity import, Play Mode, profiler, GCMonitor, runtime AI behavior proof, and crash/fault dump proof remain PENDING VERIFICATION.

## 2026-05-27 - 13AI residual predator cadence/dump pass

What was wrong:
- Mesofauna behavior quality and mesofauna cadence quality shared the same authoritative `1.0` value. That preserved gameplay truth but disabled continuous quality-based cadence scaling.
- Predator retinal low-cadence mode was dead because the helper returned false.
- Predator cognition forced high-tier smooth steering regardless of the existing `CognitionInputFlags.HighTierSmoothSteering` input.
- Retinal, alpha, and mesofauna blackbox fault dumps wrote subsystem-specific files instead of `Docs/AgentLogs/Dump_13AI.bin`.
- Foveated combat damage-lock notification read `GlobalRegistry.FoveatedSimulationDirector` directly from runtime feedback code.

What was done:
- Split mesofauna behavior quality from cadence quality. Behavior/tuning remains authoritative; slice modulo now consumes continuous `HomeostasisBrain.GlobalQualityWeight`.
- Replaced disabled retinal low mode with continuous predator interval lerp from 1.0s to 0.5s; alpha cadence remains 0.1s.
- Changed predator steering to honor `CognitionInputFlags.HighTierSmoothSteering`.
- Routed retinal, alpha, and mesofauna fault dumps to `Docs/AgentLogs/Dump_13AI.bin`.
- Cached `IFoveatedSimulationDirector` through cold refresh and hot-swap callback, then used the cached interface in damage-lock notification.

Cinematic cheats used:
- Cadence LOD and continuity instead of changing behavior truth.
- Dominant/basic steering remains available for low fidelity; smooth steering is only used when flagged.
- Single mandated fault artifact with writer-specific header/layout instead of normal-frame disk diagnostics.

Exact Microseconds saved:
- 0 verified us; no profiler proof.
- Static estimate only: minimum-quality mesofauna can skip full FSM work for up to nine of ten slice slots.
- Static estimate only: non-alpha predator utility cadence can run at 1.0s instead of the previous forced 0.5s.
- Static estimate only: non-high-tier inputs avoid the smooth-steering math branch.

Verification:
- `rg` confirmed removal of `ResolveRetinalLowCadenceMode`, forced `useHighTierSmoothSteering = true`, old subsystem dump filenames, and direct foveated registry read from the edited route.
- Brace counts are balanced for `PredatorCognitionDomain.cs`, `FaunaBrain.cs`, and `FaunaBrain.Foveated.cs`.
- `git diff --check` on touched source returned CRLF warnings only.
- Build gate allowed one compile attempt at CPU 21% with 0 active `dotnet/csc`; build remains `[BLOCKED BY DEPENDENCY]` on external Candice SQLite missing `Mono.Data` / `SqliteDataReader`. No 13AI-owned compile error was visible.

## 2026-05-27 - 13AI fauna/ambient residual route pass

What was wrong:
- `FaunaPresentationService.ConfigureSpawnedCreature()` read `GlobalRegistry.FaunaGenetics` and `GlobalRegistry.EcosystemHealth` per spawn.
- `FaunaDirector` still had static player runtime fallback and active dispatcher singleton time reads in spawn/player pose logic.
- `FaunaBrain` still read `SystemDispatcher.ActiveRuntimeInstance` from its dispatcher-time helper.
- `StressDrivenSpawnDirector` and `AmbientBiotaDirector` wrote blackbox dumps to legacy/subsystem filenames instead of `Docs/AgentLogs/Dump_13AI.bin`.

What was done:
- Bound fauna genetics and ecosystem health into `FaunaPresentationService` from `FaunaDirector` cold refresh and hot-swap callbacks.
- Added cached `IPlayerRuntimeContext` and `SystemDispatcher` to `FaunaDirector`, with a per-frame primitive player snapshot for transform/look/AUP reads.
- Changed `FaunaBrain.ReadDispatcherTimeSeconds()` to use cached dispatcher dependency.
- Normalized stress spawn and ambient biota dump paths to `Docs/AgentLogs/Dump_13AI.bin`.

Cinematic cheats used:
- No new physical simulation was added.
- Dependency-route cleanup buys determinism/jitter reduction without changing AI truth.
- Fault diagnostics remain one bounded 300-frame ring plus fault-only disk dump, not recurring I/O.

Exact Microseconds saved:
- 0 verified us; no profiler proof.
- Static estimate only: removes two global registry reads per spawned creature from presentation wiring.
- Static estimate only: removes active dispatcher singleton reads from inspected fauna director/brain time routes.
- Static estimate only: removes static player runtime fallback from inspected fauna director pose/view reads.
- 0 normal-frame us for dump-path changes; fault path writes the same kind of file to the mandated artifact.

Verification:
- Targeted `rg` found no `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, no `SystemDispatcher.ActiveRuntimeInstance`, no `Dump_SHINOBU_253`, and no `Dump_AMBIENT_BIOTA_DIRECTOR` in the touched files.
- Remaining `GlobalRegistry.FaunaGenetics` and `GlobalRegistry.EcosystemHealth` hits are cold refresh binds in `FaunaDirector`.
- Brace/paren counts matched for `FaunaDirector.cs`, `FaunaPresentationService.cs`, `FaunaBrain.cs`, `StressDrivenSpawnDirector.cs`, and `AmbientBiotaDirector.cs`.
- `git diff --check` on touched files returned CRLF normalization warnings only.
- Build gate sampled CPU 36% with one active `dotnet` process. `dotnet build` was not launched under AGENTS rules.
- Unity import, Play Mode, profiler, GCMonitor, runtime AI behavior proof, and crash/fault dump proof remain PENDING VERIFICATION.

## 2026-05-27 - 13AI AI blackbox dump consolidation pass

What was wrong:
- Active 13AI-domain fault writers still used legacy or subsystem dump artifacts: `Dump_SHINOBU_303.bin`, `Dump_SHINOBU_311.bin`, `Dump_SHINOBU_304.bin`, `Dump_SHINOBU_305.bin`, `Dump_PATH_FUNNEL_NAVMESH_FIXER.bin`, `Dump_FAUNA_BITE_IK_SOLVER.bin`, `Dump_LEVIATHAN_TENTACLE_IK.bin`, and `Dump_ANIM_PROCEDURAL_BEHAVIOR.bin`.
- Predator steering named a relative dump file directly through `FileStream`, so the forensic artifact could depend on Unity process working directory.
- SHINOBU 303 and 311 architecture route docs still documented old dump paths after runtime route alignment.

What was done:
- Normalized predator steering, predator acoustic SDF, path funnel, voxel A*, fauna kinematics, bite IK, Leviathan tentacle IK, and procedural crab leg IK fault paths to `Docs/AgentLogs/Dump_13AI.bin`.
- Anchored predator steering dump creation to project root via `Application.dataPath` before opening the file.
- Updated `Docs/ARCHITECTURE/SHINOBU_303_LEVIATHAN_STEERING_ROUTE.md` and `Docs/ARCHITECTURE/SHINOBU_311_ACOUSTIC_SENSORY_ROUTE.md` to document the 13AI dump artifact.

Cinematic cheats used:
- No physical simulation was added.
- Fault diagnostics remain a bounded 300-frame blackbox plus fault-only file output, not recurring per-frame managed logging.
- One agent-level artifact keeps crash forensics deterministic without changing AI behavior, pathfinding truth, or creature IK authority.

Exact Microseconds saved:
- 0 verified us; no profiler proof.
- 0 normal-frame us expected from dump-path changes because disk I/O remains fault-only.
- Static correctness gain: removes artifact ambiguity during postmortem and avoids cwd-dependent steering dump placement.

Verification:
- Targeted `rg` found no old targeted dump names in inspected AI/fauna/pathfinding sources or route docs.
- Targeted scan confirmed all changed writers/docs now reference `Docs/AgentLogs/Dump_13AI.bin`.
- Brace counts matched for `PredatorCognitionDomain_Steering.cs`, `PredatorCognitionDomain.AcousticSdf.cs`, `PathFunnelNavmeshRuntime.cs`, `PathFunnelNavmeshRuntime_VoxelAStar.cs`, `FaunaKinematicsRuntime.cs`, `LeviathanTentacleVerletSolver.cs`, and `ProceduralCrabLegIKRuntime.cs`.
- `PredatorCognitionDomain_Steering.cs` still has a naive parenthesis count mismatch; braces are balanced and full compiler proof is still required.
- `git diff --check` returned only CRLF normalization warnings.
- Build gate sampled CPU 51%, 65%, then 63% with no active `dotnet/csc`; `dotnet build` was not launched under AGENTS rules.
- Unity import, Play Mode, profiler, GCMonitor, runtime AI behavior proof, and crash/fault dump proof remain PENDING VERIFICATION.

## 2026-05-27 - 13AI telemetry truth and symbiosis Math LOD pass

What was wrong:
- Alpha Leviathan telemetry hardcoded `highTierSmoothSteering = true`, so blackbox flags could claim SDF/high-tier steering even when the evaluation input did not authorize that path.
- Fish/flora symbiosis AI had continuous helpers for micro-exchange gating, but `runMicroExchangeFrame` was hardcoded to `true`.
- Symbiosis fault dumps wrote `Dump_SHINOBU_62.bin` and duplicate `Dump_SYMBIOSIS.bin` instead of the mandated 13AI artifact.

What was done:
- Alpha telemetry now reads `CognitionInputFlags.HighTierSmoothSteering`, matching the actual predator steering route.
- `ShinobuFloraFaunaSymbiosisSolver` now computes `microExchangeWeight = ResolveMicroExchangeWeight(quality, MacroThreshold)` and gates micro exchange through deterministic sector/frame dithering.
- Low-quality symbiosis frames now use the existing macro biomass approximation; high-quality frames still run dense micro exchange.
- Symbiosis blackbox dump now writes only `Docs/AgentLogs/Dump_13AI.bin`.

Cinematic cheats used:
- Macro average biomass transfer is used as the cheap fish/flora interaction fake on weak devices.
- Deterministic stochastic decimation blends macro and micro exchange over time instead of a binary quality switch.
- Telemetry truth was corrected without adding any new simulation.

Exact Microseconds saved:
- 0 verified us; no profiler proof.
- Static estimate only: below `MacroThreshold`, one flora spatial hash job is skipped and micro neighbor sampling over mock/ambient fish is replaced by macro stride loops.
- Static fault-path estimate: symbiosis dump writes one file instead of two when a fault occurs.
- Alpha telemetry fix is correctness only; no runtime performance claim.

Verification:
- Targeted `rg` found no `highTierSmoothSteering = true`, no `bool runMicroExchangeFrame = true`, no `Dump_SHINOBU_62`, and no `Dump_SYMBIOSIS` in touched files.
- Brace counts matched for `PredatorCognitionDomain.cs` and `ShinobuFloraFaunaSymbiosisSolver.cs`.
- `PredatorCognitionDomain.cs` still has the known naive parenthesis counter mismatch; compiler proof is still required.
- `git diff --check` returned only CRLF normalization warnings.
- Build gate sampled CPU 100%, waited 30 seconds, then CPU 100% again with no active `dotnet/csc`; `dotnet build` was not launched under AGENTS rules.
- Unity import, Play Mode, profiler, GCMonitor, runtime AI behavior proof, and crash/fault dump proof remain PENDING VERIFICATION.
