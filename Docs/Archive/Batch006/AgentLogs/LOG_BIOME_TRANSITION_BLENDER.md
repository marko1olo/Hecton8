# BIOME_TRANSITION_BLENDER Log

Status: PENDING VERIFICATION
Agent: ENVIRONMENT_ENGINEER
Domain: World Generation & Terrain / Biome Transition Manager

## 2026-05-14 - SDF Biome Boundaries

What was wrong:
Hard biome cuts were still architecturally possible because the project had no mathematical biome boundary signal. Static searches found no `BiomeManager.Instance` and no biome-owned `BoxCollider` trigger code to delete, so the defect was missing deterministic boundary blending, not removable trigger code in this slice.

What was done:
Added `BiomeGradientSignal` in `Hecton8.Core.Signals`.
Added `World/Contracts/Biomes/BiomeBoundarySdfContracts.cs`.
Added `World/Biomes/BiomeBoundarySdfJobs.cs` with Burst IDW sampling and exact-center div-zero guard.
Added `World/Biomes/BiomeBoundarySdfRuntime.cs` with persistent `NativeArray<byte> GlobalBiomeMap`, DataMonolith heatmap hydration, AUP sampling, low-tier 3x3 LOD, origin-shift telemetry, and 300-entry black-box dump path.
Updated `HectonGIRelaySystem` to consume the gradient for fog/SH tint and `_HectonBiomeGradientState`.
Updated `EcosystemDirector` to pass blend into spawn credit recovery, spawn selection, and biomass carrying capacity.
Updated `HectonMusicDirector` to crossfade the atmosphere mixer layer by `BlendFactor01`.

Cinematic cheats used:
IDW over 2D byte heatmap instead of physical biome volumes.
Fog/SH scalar tint instead of full atmospheric simulation.
Mixer-layer crossfade instead of dynamic ambient source spawning.
Scalar spawn/capacity bias instead of simulating mixed biome food webs.

Exact microseconds saved:
Trigger broadphase removal target: estimated 20-60 us avoided per transition-heavy scene by not using biome trigger volumes.
Low-tier 3x3 kernel: 16 fewer samples than 5x5; estimated 3-8 us saved per SlowTick sample on i3/MX350.
Reciprocal OMEGA pass: estimated 1-4 us saved per sample job on low-end CPUs.
Zero-GC sampler: 0 bytes allocated in the grid sampling path; avoided managed collection churn and later GC spikes.
SignalBus consumer path: estimated 1-5 us saved versus managed multicast/direct lookup chains.

Verification:
`rg` found no `BiomeManager` / `BiomeManager.Instance`.
`rg` found no biome `BoxCollider` trigger implementation.
Direct Roslyn probe compiled `BiomeBoundarySdfContracts.cs` + `BiomeBoundarySdfJobs.cs` with Unity references.
`dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` remains red from unrelated global dependency errors (`Hecton8.AI.Ecology`, `Hecton8.Environment.Fluids`, `MacroSwarm`, audio virtualization, etc.). This is a dependency wall outside BIOME_TRANSITION_BLENDER.
Unity MCP validation could not run: no Unity session available.

Final Git Diff:
Modified:
- `Assets/_Project/Scripts/Core/GlobalSignals.cs` (BIOME_TRANSITION_BLENDER-owned hunk: `BiomeGradientSignal`; file also has pre-existing unrelated dirty hunks)
- `Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`

Added:
- `Assets/_Project/Scripts/World/Contracts/Biomes/BiomeBoundarySdfContracts.cs`
- `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfJobs.cs`
- `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs`
- `Docs/Tasks/Status_BIOME_TRANSITION_BLENDER.md`
- `Docs/AgentLogs/Rationale_BIOME_TRANSITION_BLENDER.md`
- `Docs/AgentLogs/LOG_BIOME_TRANSITION_BLENDER.md`

## 2026-05-14 - Second Pass Upgrade

What was wrong:
The first sampler pass was correct for normal in-map boundaries but weak at heatmap edges because it clamped the cell index while leaving the sample coordinate outside the map. It also aggregated by compact byte ID only, which is not safe when many biome records clamp/fold into the same byte. Runtime used schedule/complete for an immediately consumed scalar sample. The consumer-side `EcosystemDirector` also had brine-height double/float conversion errors from a neighboring AUP precision edit.

What was done:
Added `OutOfBounds` to biome SDF flags and `BiomeGradientSignal`.
Clamped out-of-domain sample coordinates before IDW math.
Changed weight aggregation to compare biome hash when hashes are available, preserving compact byte map memory while avoiding false biome merges.
Changed `BiomeBoundarySdfRuntime` to use `job.Run()` for synchronous Burst execution.
Added `BiomeBoundarySdfSmokeTester` for boundary blend, low-tier diameter, out-of-bounds clamp, and hash-collision cases.
Removed `EcosystemDirector` brine-height conversion errors while preserving double AUP shift math.

Cinematic cheats used:
Kept the 2D byte heatmap + hash mirror instead of widening all hot biome storage to 32-bit IDs.
Kept a scalar `OutOfBounds` flag for consumer-side visuals instead of edge-volume simulation.
Kept synchronous slow-tick Burst sampling instead of scheduling a tiny one-result worker job.

Exact microseconds saved:
Synchronous `job.Run()`: estimated 2-6 us saved per slow-tick sample versus schedule/complete overhead.
Hash-aware byte map: retained 64 KB hot map instead of 256 KB uint map; estimated cache gain 1-3 us on low-tier memory pressure.
Out-of-bounds clamp: estimated 1-3 us saved on invalid/edge samples by avoiding huge-distance decay and recovery paths.
Brine conversion fix: compile-risk removal, no frame-time claim.

Verification:
Direct Roslyn probe still passes for `BiomeBoundarySdfContracts.cs` + `BiomeBoundarySdfJobs.cs`.
`dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` no longer reports biome-gradient, `BiomeBoundarySdf`, or `EcosystemDirector` brine conversion errors. It remains red from unrelated missing `HectonBlueprintPreviewBatch` and `PlayerLookTargetPromptCache` symbols.

## 2026-05-14 - Third Pass Runtime Presence

What was wrong:
The SDF producer could still be absent from a scene. The math and signal were present, but no authored or cold-start path guaranteed that `BiomeBoundarySdfRuntime` existed on `[MANAGERS]`. Generated project files also lagged Unity source import and did not list the new SDF source files, so a direct editor authoring reference created a stale-csproj compile hazard.

What was done:
Added guarded `ActiveRuntimeInstance` ownership to `BiomeBoundarySdfRuntime`.
Added duplicate suppression and subsystem-registration reset for the runtime producer.
Added `BiomeBoundarySdfRuntimeBootstrap` as a cold fail-safe that attaches one runtime producer only when no active instance exists.
Updated `WorldRuntimeBootstrapAuthoring` to author the runtime onto `[MANAGERS]` using type-name resolution, avoiding generated `.csproj` surgery.
Removed a stale unsafe-collections using from the runtime file.

Cinematic cheats used:
Kept one SlowTick SDF producer instead of scene-wide biome trigger volumes.
Kept authoring/runtime fail-safe ownership instead of per-frame searches.
Kept editor-only reflection for stale project-file tolerance instead of adding runtime indirection.

Exact microseconds saved:
Authored steady state: 0 us added versus the previous runtime sampler path.
Runtime fail-safe: one cold GameObject/component attach only in mis-authored scenes; no hot-path cost.
Duplicate suppression: prevents a second producer from double-publishing signals and doubling slow-tick sampler work, saving the full duplicate sampler cost when a scene is incorrectly authored.
Generated-project fix: build hygiene only, no runtime frame-time claim.

Verification:
Re-extracted the exact `BIOME_TRANSITION_BLENDER` prompt from `Docs/Tasks/CURRENT_BATCH.md` using CLI.
`dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` passed.
`dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` passed.
Direct Roslyn probe passed for `BiomeBoundarySdfContracts.cs` + `BiomeBoundarySdfJobs.cs`.
`git diff --check` passed for the touched biome/editor files.
Source scan found no `Update()`, `BiomeManager.Instance`, biome trigger collider, `new List<`, or `new Dictionary<` in the SDF slice.
Unity MCP validation remains unavailable in this session; status stays `PENDING VERIFICATION`.
