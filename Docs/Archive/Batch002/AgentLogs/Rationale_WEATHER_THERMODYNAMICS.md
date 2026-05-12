# WEATHER_THERMODYNAMICS Rationale

## Decision 1: Use Existing Thermodynamics Owner
Problem: Thermal vents already have registry ownership, AUP sampling, hazard publishing, and GPU plume data inside AbyssalThermalManager.
Solution: Extend AbyssalThermalManager instead of introducing a second thermodynamics service. DOD pattern: one authoritative owner through GlobalRegistry and narrow interfaces.
Rejected Alternatives: A new HeatMapManager was rejected because it would duplicate vent state, create ordering bugs with 20+ concurrent agents, and break the existing IThermodynamicsService boundary.
Scalability potential: Low/MX350 uses direct reciprocal distance heat without grid allocation; Middle/High/Ultra use the coarse grid and can spend saved CPU on stronger haze, bubbles, and audio.
Hardware Impact: Estimated i3/MX350 gain is 20-60 us/ColdTick versus duplicate vent scans and 0 B hot-path GC.

## Decision 2: Cinematic Heat Instead Of Fluid
Problem: The prompt asks for boiling, thrust, haze, and avoidance without volumetric simulation.
Solution: Use a 16x16 Jacobi heat map as gameplay truth, shader globals for presentation, and compute/VFX command data for bubbles. DOD pattern: fake-first thermal field.
Rejected Alternatives: Navier-Stokes, voxel water temperature, and per-particle CPU boiling were rejected as cost-prohibitive and not required for player-readable heat.
Scalability potential: Low uses direct inverse-square vent sampling; Middle uses 16x16 Jacobi; High/Ultra can raise presentation intensity without increasing gameplay resolution.
Hardware Impact: Estimated low-end saving versus even a 32x32 multi-iteration solve is 30-120 us/ColdTick plus avoided CPU particles.

## Decision 3: Convection As Routed Velocity Change
Problem: Hot vents must push the player and submarine upward without adding collider volumes or mass-query sweeps.
Solution: Sample only the player and submarine fixed-step positions, compute local heat, then queue `Vector3.up * Heat01 * math.rcp(mass)` through PhysicsForceRouter as a VelocityChange.
Rejected Alternatives: Trigger volumes, Rigidbody.AddForce direct writes, and fluid buoyancy simulation were rejected because they either allocate/scale badly or bypass the existing physics routing boundary.
Scalability potential: Low/MX350 gets two direct samples and no grid; Middle/High/Ultra can retain the same gameplay path while spending visuals on haze and bubbles.
Hardware Impact: Estimated i3/MX350 cost is 2-5 us/FixedTick; avoided trigger/collider path can save 40-120 us in dense caves.

## Decision 4: Burn Damage Through Combat Queue
Problem: Water over 80C needs to cook actors while preserving combat ownership.
Solution: Queue CombatDamageSignal with Thermal damage and Burning status; do not mutate player/submarine health directly.
Rejected Alternatives: Direct HectonPlayerHealth calls and HectonHazardManager-only heat were rejected because they lose temperature details and create hidden dependencies.
Scalability potential: Low/MX350 pays only when player/submarine are sampled; High/Ultra can add richer burn VFX without changing combat truth.
Hardware Impact: Estimated 3 us/FixedTick when hot; zero queue work outside boiling threshold.

## Decision 5: Fauna Coupling Through Read-Only Map
Problem: Predators and boids need to avoid >50C cells without direct dependency on thermal implementation details.
Solution: Extend IThermodynamicsService with a front-buffer NativeArray readback and map metadata. Ecosystem-side consumers can sample the buffer without owning vent state.
Rejected Alternatives: Direct writes into EcosystemDirector internals were rejected because they create cross-domain ownership and conflict with parallel agents.
Scalability potential: Low/MX350 returns false because no grid exists and consumers can fall back to direct vent checks; High/Ultra get stable 16x16 avoidance.
Hardware Impact: Expected 0 us until read by a consumer; avoids 16 vent scans per boid.

## Decision 6: Deterministic Eruption As Triangle Wave
Problem: Vent heat cannot be static, but nondeterministic timers make replay and fauna behavior unstable.
Solution: Use `TriangleWave01(_simulationTime / cycle + hash)` with a duty gate. DOD pattern: deterministic cinematic cheat.
Rejected Alternatives: Random.Range timers and coroutine eruption loops were rejected for replay drift and managed scheduling.
Scalability potential: Low/MX350 still gets inverse-square direct heat scaled by the same wave; High/Ultra can use the scalar for denser VFX.
Hardware Impact: Estimated 1 us/vent per 0.25s GPU refresh.

## Decision 7: Fabricator Heat Through Existing BaseModule Temperature
Problem: Running fabricators must add +20C to the local room without a new habitat thermal graph.
Solution: Inject +20C once when powered crafting actually advances, using the existing BaseModule host-room temperature API.
Rejected Alternatives: Continuous per-SlowTick heat accumulation was rejected because it would runaway without a paired cooling model; a new crafting thermodynamics service was rejected as cross-domain bloat.
Scalability potential: Same behavior on all tiers; High/Ultra visuals can respond through existing room temperature presentation later.
Hardware Impact: Estimated 1 us/craft start; no per-frame cost.

## Decision 8: Compile Boundary
Problem: Full project compile remains red after thermal fixes, but reported errors are in unrelated Visor, Combat, Construction, and SaveBinaryStorage code.
Solution: Fix the one thermal namespace error, then validate touched thermal scripts directly. Mark compile as dependency-blocked instead of editing other agents' systems.
Rejected Alternatives: Patching DeflectSignalWriter, DroneFleetTask, RenderGraphBuilder, or SaveBinaryStorage was rejected as out-of-domain and high-risk.
Scalability potential: No runtime scalability impact; prevents thermal work from expanding into unrelated architecture.
Hardware Impact: 0 us runtime. Avoids churn and potential regressions in other systems.

## Decision 9: Black Box Ring
Problem: Thermal gameplay is now critical: NaN or bad heat values must be postmortem-debuggable.
Solution: Added a fixed 300-entry NativeArray telemetry ring and binary dump on non-finite thermal state.
Rejected Alternatives: Debug.Log in FixedTick was rejected for allocation/noise; dynamic List history was rejected for GC.
Scalability potential: Same fixed memory on all tiers; High/Ultra can add richer debug decode later without touching hot path.
Hardware Impact: Normal path is one ring write for sampled targets, estimated under 1 us; dump path only triggers on fault.

## OMEGA POLISH CHANGES
Problem: Final audit found avoidable divisions in the newly added thermal-map/direct-sample path.
Solution: Replaced those divisions with `math.rcp` multiplications in cell sizing, map sampling, direct inverse-distance heat, height gates, heat normalization, radial map writes, and deterministic eruption phase/duty math.
Rejected Alternatives: Left pre-existing hydrothermal/cable math untouched because it predates this prompt and expanding the patch would be a refactoring loop outside WEATHER_THERMODYNAMICS.
Scalability potential: Low/MX350 remains gridless direct reciprocal heat; Middle/High/Ultra keep the 16x16 ColdTick grid and spend presentation budget on haze, bubbles, roar, and condensation.
Hardware Impact: Estimated i3/MX350 gain is 2-8 us across thermal sampling spikes versus raw division path, with no gameplay change.

Exact cinematic cheats used:
- 16x16 Celsius NativeArray heat map, one Jacobi diffusion step on ColdTick, not volumetric water.
- Low/MX350 direct inverse-distance-squared heat, no grid allocation.
- Deterministic `TriangleWave01(hash + time)` vent eruption, no random timers or coroutines.
- Shader scalar globals for heat haze and condensation, no new thermal render pass.
- Fixed `Vector4[16]` GPU bubble commands, no standard CPU ParticleSystem boiling.
- Read-only front-buffer thermal map for fauna avoidance over 50C, no fauna owner dependency.
- 300-frame fixed NativeArray black-box ring with binary dump on non-finite thermal state.

Cross-domain justification:
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: WEATHER-authored change is the read-only `IThermodynamicsService` Celsius sample/map readback contract. Other visible diff hunks in this file are concurrent/pre-existing and were not reverted.
- `Assets/_Project/Scripts/Fabricator.cs`: Task 10 required exothermic crafting. The implementation uses existing `BaseModule.TryInjectHostRoomTemperatureDeltaCelsius` and avoids a new thermodynamics-crafting dependency.
- `Assets/_Project/Scripts/ThermalGeyser.cs`: Task 13 required no CPU boiling ParticleSystem. The removed field/play-stop path is replaced by thermal manager GPU command globals.

Final Git diff summary:
```
git diff --numstat -- Assets/_Project/Scripts/World/AbyssalThermalManager.cs Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs Assets/_Project/Scripts/ThermalGeyser.cs Assets/_Project/Scripts/Fabricator.cs
36      4       Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs
15      0       Assets/_Project/Scripts/Fabricator.cs
0       21      Assets/_Project/Scripts/ThermalGeyser.cs
862     20      Assets/_Project/Scripts/World/AbyssalThermalManager.cs
```

Verification after polish:
- `validate_script Assets/_Project/Scripts/World/AbyssalThermalManager.cs`: 0 errors, 0 warnings.
- Hot-path text scan over touched thermal files: no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` results.
- `dotnet build Hecton8.Core.csproj`: [BLOCKED BY DEPENDENCY]. Current failures are unrelated missing core/native symbols including `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, and unrelated Combat helpers. Existing warnings are in PlayerCriticalProceduralAudioRenderer and WorldSpatialHashGrid.

## Decision 10: GPU-Visible Thermal Map Texture
Problem: The thermal map existed as CPU/Burst gameplay truth and read-only NativeArray metadata, but shader/VFX consumers still had only scalar heat globals and command vectors. That leaves the "heat map" invisible to high-tier presentation.
Solution: Added `_HectonThermalMapTexture`, a persistent 16x16 RFloat Texture2D created only on grid-enabled tiers, updated from the front-buffer NativeArray via `SetPixelData` only when `_thermalMapVersion` changes. Upload is deferred to Tick start after LateFrame job swap.
Rejected Alternatives: A 2D RenderTexture diffusion pass was rejected because the CPU map is already the gameplay truth. An RGBA fallback was rejected because it would require managed conversion or extra staging. A public interface expansion was rejected because shader globals are enough for presentation consumers.
Scalability potential: Low/MX350 remains direct-distance heat and allocates no texture. Middle gets a tiny bilinear heat-map signal for haze/distortion. High/Ultra can sample the same map for richer shimmer, bubble density, predator avoidance debug, and visor overlays without changing gameplay resolution.
Hardware Impact: RFloat 16x16 is 1024 bytes plus Unity object overhead. Dirty-gated upload is one 1 KB upload per active ColdTick instead of 60 uploads/sec. Estimated saved bandwidth versus per-frame upload is about 59 KB/sec and avoids repeated driver work.
REGRESSION MODEL: CPU cost limited to 1Hz active MED+ map upload; GC remains 0 B in Tick because no managed arrays are created; memory adds one tiny owned Texture2D; correctness risk is stale shader map if the job swap fails, mitigated by version gate and inactive flag.
HOT PATH IMPACT: Tick adds one branch and early return unless `_thermalMapTextureDirty` is set. Dirty path runs only after map version changes.
FAILURE MODES: RFloat unsupported -> shader active flag set to 0 and no fallback allocation; Low/MX350 -> no map texture; manager disable -> texture destroyed and global binding reset to black if previously active.
WHY KEPT: This is visual currency bought by the earlier coarse-map optimization. It upgrades perception without introducing fluid truth.

Verification:
- Text scan: only one `new Texture2D` in `AbyssalThermalManager.cs`, in cold allocation path with canonical COLD ALLOC comment.
- Text scan: no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` in `AbyssalThermalManager.cs`.
- Unity MCP `validate_script`: inconclusive; first call disconnected, second timed out.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`: blocked by unrelated missing core/native symbols and unrelated GPUScatter telemetry methods; no `AbyssalThermalManager.cs` errors were reported.
