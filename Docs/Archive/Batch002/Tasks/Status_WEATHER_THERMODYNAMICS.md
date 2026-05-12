# WEATHER_THERMODYNAMICS Status

Prompt ID: WEATHER_THERMODYNAMICS  
Identity: THERMAL_ENGINEER  
Domain: ATMOSPHERE & CELESTIAL / Thermodynamics  
Status: PENDING VERIFICATION

## Mandates Loaded
- CORE_Weather_Abyssal_FlowField_Currents.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt

## Loop 1: Tasks 1-5
- [x] 1. Thermal map generation: Added 16x16 persistent NativeArray Celsius front/back/source buffers inside AbyssalThermalManager. DOD practice: SoA NativeArray, owner-registered sentinel, no managed hot-path allocation. Alternative rejected: per-fauna direct vent scan. Estimate: 28 us/ColdTick.
- [x] 2. Jacobi diffusion: Added ThermalMapJacobiJob with `[BurstCompile(FloatMode = FloatMode.Fast)]`, scheduled at 1Hz ColdTick and completed in LateFrame swap. DOD practice: no Complete mid-Tick, front/back buffer swap. Alternative rejected: volumetric heat solve. Estimate: 9 us/ColdTick.
- [x] 3. Upward thrust: Added FixedTick player/submarine convection through PhysicsForceRouter using `heat01 * math.rcp(mass)` velocity change. DOD practice: fixed-step force routing, no broad trigger volumes. Alternative rejected: buoyancy collider volumes. Estimate: 2 us/FixedTick.
- [x] 4. Boiling damage: Added CombatDamageRuntime thermal burn signal above 80C with local temperature detail. DOD practice: decoupled queue signal. Alternative rejected: direct health mutation or hazard trigger spam. Estimate: 3 us/FixedTick.
- [x] 5. Screen haze distortion: Added local thermal heat, Celsius, and condensation shader globals. DOD practice: scalar presentation bridge. Alternative rejected: volumetric distortion pass. Estimate: 1 us/update.

## Loop 2: Tasks 6-10
- [x] 6. Audio roar: Added throttled low-pass ProceduralAudioEvents ping plus ImpactSignal when local heat is near a vent. DOD practice: event queue, no AudioSource creation. Alternative rejected: per-vent looping emitters. Estimate: 4 us on trigger.
- [x] 7. Fauna flee map: Exposed read-only front-buffer thermal map through IThermodynamicsService with 50C avoidance metadata in shader/world-rect payload. DOD practice: front-buffer only, no per-boid owner coupling. Alternative rejected: direct EcosystemDirector dependency write. Estimate: 0 us unless consumer samples.
- [x] 8. Geyser eruption cycle: Added deterministic TriangleWave01 vent cycle mixed with seismic eruptions. DOD practice: hash-seeded phase, predictable sleep/eruption. Alternative rejected: random timers. Estimate: 1 us/vent on refresh.
- [x] 9. GPU boiling bubbles: Added fixed Vector4 command staging and shader global command count for compute/VFX consumers. DOD practice: GPU-facing command buffer, no CPU ParticleSystem. Alternative rejected: standard ParticleSystem emission. Estimate: 2 us/refresh.
- [x] 10. Exothermic crafting: Fabricator injects +20C once when powered crafting actually runs. DOD practice: existing BaseModule thermal cell injection. Alternative rejected: new thermodynamics-to-crafting hard dependency. Estimate: 1 us/craft start.

## Loop 3: Tasks 11-15
- [x] 11. Condensation UI: Added cold-to-hot delta detection and `_HectonThermalCondensation01` shader scalar for RenderGraph/full-screen consumers. DOD practice: scalar trigger, no new pass allocation. Alternative rejected: new full-screen feature in thermal owner. Estimate: 1 us/update.
- [x] 12. Math LOD: Low/MX350 disables grid allocation and samples direct inverse-square vent heat. DOD practice: tier gate through GlobalRegistry.ScalabilityTier/SystemInfo VRAM. Alternative rejected: always-on Jacobi. Estimate: saves 9-28 us/ColdTick and NativeArray map allocation on MX350.
- [x] 13. No CPU ParticleSystems for boiling: Removed ThermalGeyser eruption ParticleSystem field and play/stop logic; boiling now emits GPU command globals. DOD practice: compute-driven presentation hook. Alternative rejected: standard ParticleSystem emission. Estimate: avoids CPU emitter/collision overhead.
- [x] 14. Recon scan: Wrote `Docs/AgentLogs/RECON_WEATHER_THERMODYNAMICS.md`. DOD practice: rg scan of scripts and art fallback because `Assets/_Project/Art/VFX/` does not exist. Alternative rejected: manual IDE browse. Estimate: 0 runtime us.
- [x] 15. Omega compile check: `AbyssalThermalManager.cs`, `ThermalGeyser.cs`, and `GlobalRegistryContracts.cs` validate with 0 diagnostics; post-polish `AbyssalThermalManager.cs` validates with 0 diagnostics. Unity compile shows no WEATHER_THERMODYNAMICS errors. `dotnet build Hecton8.Core.csproj` is [BLOCKED BY DEPENDENCY] on unrelated missing core/native symbols and existing warnings. DOD practice: fail-fast fix of our namespace error; stop at dependency wall. Estimate: 0 runtime us.

## Iteration Log
- Iteration 1: Prompt extracted from CURRENT_BATCH.md by CLI. Domain file read. Existing owner selected: AbyssalThermalManager.
- Iteration 2: Prompt re-extracted after task 3. Loop 1 code patched; compile check pending.
- Iteration 3: Compile attempt 1 found stale/unrelated SurvivalPhysiologyScalarResult csproj issue. Unity compile showed one thermal namespace error, fixed.
- Iteration 4: Prompt re-extracted after task 6/9. Unity compile now has no WEATHER_THERMODYNAMICS errors; remaining errors are unrelated dependency wall in Visor, Combat, Construction, SaveBinaryStorage.
- Iteration 5: Self-review found and fixed condensation previous-temperature reset when leaving heat field. Recon file written. Local script validation passed for AbyssalThermalManager, ThermalGeyser, GlobalRegistryContracts; Fabricator validator timed out on file size but Unity produced no Fabricator error.
- Iteration 6: POLISH_MANDATE parsed only after all core tasks were checked. Replaced avoidable thermal-path floating divisions with `math.rcp` multiplications. Scan found no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` in touched thermal files. `dotnet build Hecton8.Core.csproj` failed on unrelated core/native missing symbols: `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, plus unrelated Combat helpers.
- Iteration 7: Honest R&D continuation added GPU-visible thermal-map upload. `AbyssalThermalManager` now publishes `_HectonThermalMapTexture` as a 16x16 RFloat texture only when the ColdTick map version changes. DOD practice: cold texture allocation, NativeArray `SetPixelData`, dirty/version gate, inactive bind to black. Alternative rejected: per-frame texture upload, RenderTexture simulation, public interface expansion. Estimate: 1 upload/sec on active MED+ thermal fields; Low/MX350 remains gridless and texture-free.

## R&D Continuation
- [x] AAA heat-map bridge: Added a shader/VFX-readable RFloat thermal map texture without new physics truth. DOD practice: presentation bridge consumes existing 16x16 gameplay truth. Alternative rejected: volumetric heat texture or compute thermal simulation. Estimate: about 1-3 us CPU on dirty 1Hz upload plus 1 KB VRAM.
- [x] Bandwidth guard: Texture upload is dirty/version gated and happens at Tick start after LateFrame job swap. DOD practice: no upload when the map has not changed. Alternative rejected: calling `SetPixelData` every frame. Estimate: saves 59 redundant uploads/sec at 60 FPS.
- [x] Degrade path: RFloat unsupported path disables shader map active flag and binds inactive/black only after a prior active texture. DOD practice: fail closed, no fake active texture. Alternative rejected: allocating RGBA fallback or managed conversion array. Estimate: 0 B hot-path GC.
- [x] Verification: Unity MCP validation timed out/disconnected. `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` still fails on unrelated missing core/native symbols and unrelated GPUScatter telemetry methods, with no `AbyssalThermalManager.cs` errors reported. Status remains PENDING VERIFICATION.
