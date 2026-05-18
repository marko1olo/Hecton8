# HECTON-8 Project State Static X-Ray

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK / CLI_COMPILE_ARCHIVED

This file is a durable project-state risk register. It is not an AgentLog and must not be treated as runtime proof.

## Scope

User request: ignore easy build bugs, audit deeper project health, and keep documentation current under concurrent agent churn.

Most commands were static filesystem and source scans. DOC_AUDIT R29 reported one Unity `6000.4.1f1` batchmode import/script-compilation artifact at `Library/Codex_DOC_AUDIT_UnityBatchCompile.log`, but the R10 filesystem check did not find that path; treat it as dated report text unless restored or replaced. R40/R41 added controlled external CLI compile evidence through a source-backed generated-project bridge and a serial root `Hecton8*.csproj` sweep; R42 propagated that compile boundary into active reference docs that still carried the older May 13 missing-artifact-only override. The latest same-day Core boundary is archived at `Docs/Archive/Batch007/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log` (`EXIT=0`, `0 Warning(s)`, `0 Error(s)`) plus archived H-Phi full static budget artifact `Docs/Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json` (`EXIT=0`, `MemoryAlignment=0.506309148`, `RuntimeHPhiRisk=0.000636091`, `GlobalRegistrySurface=5060/5060`). DOC_HONEST_ANALYSIS R3 remains a historical archived same-day Core graph prune slice for transient `Hecton8.World.GPR` asmdef drift; CurrentDisk53/BudgetGate22 supersede R49/R52/R53/R54 only as archived Core build/H-Phi evidence. No Play Mode, profiler, GCMonitor, player build, Memory Profiler, RenderDoc, save/load roundtrip, visual capture, or runtime benchmark was run. DOC_AUDIT R5/R6 added package-lock, BuildSettings, URP asset, PlayerSettings, and script-local docs checks; R8 added the world/scatter/streaming wiring addendum; R17 added the renderer/visor/shader proof boundary; R21 closed the static resource-node catalog/worldPrefab gaps; R22 added the PDA fail-closed guard; R23 hardened editor validation for duplicate item identity/catalog ambiguity.

Mandates used:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

## Executive Verdict

The project is no longer a puddle. It is an infrastructure-heavy AA pre-alpha / vertical-slice candidate with a real runtime spine, real content footprint, and a serious technical doctrine.

It is not yet a proven playable game. The missing proof is not compile status. The missing proof is current Unity runtime evidence: scene wiring, Play Mode, profiler, GC, player build, memory, VRAM, frame time, and visual inspection.

Current static estimate: roughly 70% engineering infrastructure, 30% proven game. This ratio is not measured runtime truth; it is an architecture/readiness assessment from static evidence.

## Non-Claims

- No Unity Console / Play Mode / player-build clean compile is claimed. Current clean compile evidence is limited to the root `Hecton8*.csproj` external CLI surface.
- No 0 GC/frame is claimed.
- No frame-time budget compliance is claimed.
- No MX350 compliance is claimed.
- No Addressables residency correctness is claimed.
- No scene wiring correctness is claimed.
- No visual quality is claimed.
- No player-build readiness is claimed.

## 2026-05-15 Build / H-Phi Static Addendum

Evidence class: `CLI_COMPILE` plus `STATIC_SOURCE_FULL_SCAN`.

- `Docs/Archive/Batch007/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log` reports `Hecton8.Core.csproj` CLI compile `EXIT=0`, `Build succeeded`, `0 Warning(s)`, and `0 Error(s)` after the later 22:38 source write.
- Failed or superseded same-session artifacts include stale generated-CLI visibility of `MacroDatabasePayloadFlags`, the transient `ScalabilityTierBindingBridge` typo, R50 missing `PlayerKinematicsRuntime` storage helpers, R51 duplicate helper definitions, R52/R53 stale boundaries under later source writes, the historical R49 clean slice, and R54 dirtied by a later 22:38 source write. The later clean CurrentDisk53 artifact supersedes them for archived Core CLI status only.
- `Docs/Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json` reports H-Phi static budget `EXIT=0`, `MemoryAlignment=0.506309148`, `DataSovereignty=0.021306032`, `RuntimeHPhiRisk=0.000636091`, `GlobalRegistrySurface=5060/5060`, `ManagedFormatSurface=534/534`, `PrimaryManagedRuntimeRisk=147/147`, `DuplicateSignalNames=0`, `UnityUpdateMethods=0`, and Core graph debt `25/10/14/8/6`.
- `Docs/Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.json` is the archived equivalent of the previously cited active-path summary. It found static scores still at `HPhiStaticRisk=0.000636091`, `DataSovereignty=0.021306032`, and `MemoryAlignment=0.506309148`, but Core asmdef debt had drifted to `26` because unused `Hecton8.World.GPR` was present in transient Core asmdef state. The active-path `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.json` is absent in the R11 filesystem check.
- `Docs/Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json` reports Core graph debt back at `25/10/14/8/6` and no unused Core asmdef reference candidates after file/index alignment; CurrentDisk53/BudgetGate22 are newer archived Core compile/H-Phi boundary evidence than that prune slice, not current active-workspace compile proof.
- This addendum does not change the core verdict: runtime playability and scalability remain unproven until Unity import/Console, Play Mode, profiler, GCMonitor, player build, memory, scene wiring, save/load, and visual captures exist.

## Static Inventory Findings

- 2026-05-19 R24 source-scale spot check: `Assets/_Project/**/*.cs` = `1814`, `Assets/_Project/Scripts/**/*.cs` = `1758`, non-test C# files excluding `Assets/_Project/Tests*` = `1794`, project physical lines = `1198173`, script physical lines = `1178627`, non-test physical lines = `1193454`, direct public interfaces in `GlobalRegistryContracts.cs` = `62`, first-party asmdefs = `119`. Evidence class: `STATIC_SOURCE`; this is not compile or runtime proof and must be rerun under concurrent source churn.
- `Assets/_Project/Scripts/**/*.cs`: `1758` first-party script C# files in the R24 static PowerShell pass.
- `Assets/_Project/**/*.cs`: `1814` C# files in the R24 broader extension inventory.
- BuildSettings contains the normative scene chain:
  - `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
  - `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `02_HECTON_WORLD.unity`: about 32.2 MB.
- `_Project` extension inventory included 428 prefabs, 171 materials, 99 shader files, 30 compute shaders, 4 hlsl files, 97 png files, 89 ogg files, and 45 wav files.
- Scene/prefab/asset scan did not find obvious `m_Script: {fileID: 0}` missing-script markers in `_Project`. Evidence class: STATIC_SOURCE only.
- Worktree was not stable: DOC_AUDIT R3 readback saw `git status --short` return 378 entries: 261 modified-like, 116 untracked, 1 deleted.

## Runtime Spine Findings

Good:

- Static scan found only `SystemDispatcher` owning first-party Unity `Update` / `LateUpdate` methods.
- Gameplay is mostly routed through dispatcher tick interfaces and `GlobalRegistry` lanes.
- `SystemDispatcher` has explicit cadence lanes, profiler markers, fixed/slow/cold/frost ticks, late-frame circuit breakers, and GC-pressure awareness.
- `GlobalSignals` uses NativeQueue-backed lanes and typed SignalBus infrastructure.
- There are many watchdog/profiler/smoke-test tools in the codebase.

Risk:

- `GlobalRegistry.cs` is about 279 KB / 5763 lines and exposes roughly 150 service slots. This is a functional project OS, but also a massive coupling surface.
- `GlobalSignals.cs` owns many signal lanes. This reduces direct dependencies, but the global event surface is now broad enough that ownership drift becomes easy.
- Bootstrap, registry, dispatcher, scene runtime, save, audio, scatter, and player systems are highly interdependent. Runtime proof is mandatory before claiming stability.

## Large-File X-Ray

Largest first-party runtime files are mostly real code, not empty filler. The problem is load-bearing code concentrated into huge owners.

Highest pileup risk:

- `Assets/_Project/Scripts/HectonPlayerMovement.cs` - 740,426 bytes / 13,240 lines.
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` - 539,165 bytes / 11,907 lines.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` - 355.3 KB.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` - 336,953 bytes / 7,041 lines.
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` - 315 KB.
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` - 308.6 KB.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` - 277.6 KB.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs` - 279.3 KB.
- `Assets/_Project/Scripts/BaseModule.cs` - 234.2 KB.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` - 200 KB.

Large but more justified technical kernels:

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`

Editor authoring files are large but lower runtime risk:

- `Assets/_Project/Scripts/Editor/BiomeMatrixBootstrapAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs`

## HectonPlayerMovement Finding

`HectonPlayerMovement.cs` is not 700 KB of walking. It is a fused player integration hub.

It contains:

- core locomotion and KCC/motor force queue
- water state and surface swimming
- environmental drag/currents/thermal/crush response
- sargassum, cable, parasite, tow, grapple, and transport influence
- inventory mass, stamina, narcosis, emergency movement multipliers
- VR comfort, wet lens, FOV, camera/presentation signals
- footstep/audio/acoustic/sonar/splash warning signals
- AUP/origin-shift/no-clip recovery
- collision probe/cache/render interpolation
- black-box/telemetry/editor validation

Good signs:

- no Unity `Update` / `LateUpdate` / `FixedUpdate`
- no `FindObjectOfType` / `GameObject.Find`
- dispatcher registration exists
- many caches and finite/failsafe paths exist
- R34 adds a small fixed-path ladder snap cache, avoiding repeated `ClimbableLadder` component resolution for the same collider.

Bad sign:

- ownership is wrong. Player movement owns too many environmental hazards and presentation effects.
- The file is still too large for safe human reasoning without targeted slices; current state is 740,426 bytes / 13,240 lines.

Decision:

- Do not split this first. It is load-bearing. Cutting it before runtime baseline would create weeks of regression risk.

## Scatter / World Runtime Finding

`WorldProceduralScatterDirector.cs` is the more strategic runtime risk than `HectonPlayerMovement.cs`.

Evidence:

- R46 static scan: `539165` bytes / `11907` lines.
- Still the live owner for scatter.
- Contains backend shadow mode, rescue placement, reconciliation, candidate maps, pool warmup, sampling, diagnostics, MapMagic/geology/flora hooks.
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` already states the Entities/DOTS scatter backend is prototype/shadow-only and not proven as optimization.

Risk:

- Scatter/streaming/content is where MX350 and scene playability will likely fail first, not in ordinary C# compile errors.
- Candidate count parity is not enough; runtime parity needs placement correctness, memory, GC, frame-time, hitch profile, and visual proof.

Decision:

- Next serious audit should target scatter + streaming + boot + asset memory, not class aesthetics.

## World / Scatter / Streaming Wiring Addendum

Evidence class: STATIC_SOURCE / FILESYSTEM. No Unity scene load, inspector readback, validator execution, profiler, or player build was run.

World runtime code inventory:

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`: `539165` bytes / `11907` lines.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`: `336953` bytes / `7041` lines.
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`: `263836` bytes / `5133` lines.
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: `187902` bytes / `4368` lines.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`: `273872` bytes / `6159` lines.
- Supporting runtime directors/helpers exist for sampling pipeline, working memory, vegetation residency, slice/streaming, scatter budget, state registry, backend integration, diagnostics, heuristics, and streaming profiles.

Good evidence:

- The world stack is not empty theater. It has Burst jobs, NativeArrays/NativeLists/NativeHashMaps, fixed telemetry rings, Addressables/load-handle paths, additive-scene paths, GPUI/indirect vegetation seams, cold allocation annotations, profiler markers, tier budgets, predictive loading, residency hysteresis, proxy/final variant concepts, and editor validators/authoring tools.
- `WorldChunkStreamingProfile.asset` exists and describes a real 15 km world model: `15000 m` world, `192 m` chunks, `64 m` cells, `768 m` macro zones, and radius bands `180/420/900/1800`.
- The profile has per-layer policy: Terrain/Flora/Debris/Resources/Fauna/Construction/LargeThreats, including low activation budgets and a special LargeThreats layer with chunk residency disabled but full simulation enabled.
- `WorldProceduralScatterDirector` registers as `IWorldGenService`, registers dispatcher update/slow/late-frame lanes, defers on bootstrap where configured, uses bootstrap prime passes, and supports cheap proxy vs final variants.
- R46 source recheck found partial scatter refactor pieces already present: `SamplingSnapshot.cs`, `WorldProceduralScatterWorkingMemory.cs`, `ScatterHeuristicsUtility.cs`, and `ScatterDiagnosticsTracker.cs`. `ScatterRescueContext` exists only as a private readonly struct in a director partial, `GetGridPlacements()` returns bucketed placement lists, and no `ScatterSpawningService` source/class was found.
- `WorldProceduralFieldSampler` registers into `GlobalRegistry.ProceduralFieldSampler`, owns Burst sampling data, graphics-buffer output, biome/event integration, and fallback synthetic sampling.
- `WorldChunkResidencyManager` is a real data-driven residency manager: authoring `ChunkDefinition[]`, native chunk tables, predictive load/unload jobs, memory guard, Addressables handle release, additive scene loading, and `Docs/AgentLogs/Dump_ASSET_STREAMING_PREDICTIVE.bin` telemetry dump path.
- `HectonMapMagicVegetationBridge` is not a trivial bridge. It owns resident vegetation chunk selection, predictive forward radius, native vegetation pools, threat/flow/thermal/abyssal/HLOD jobs, late-frame job recovery, and renderer-source binding.
- `Assets/_Project/Data/World` contains `285` `.asset` files, about `1.34 MB` serialized text data. Notable counts: `78` family profiles, `37` procedural placement rules, `35` flora templates, `33` procedural families, `13` procedural biome contexts, and `9` pattern profiles.
- Procedural family assets show actual proxy/final data: static scan found `62` `proxyOnly: 1` entries and `179` `finalReady: 1` entries across procedural families.

Wiring risk:

- `GameBootstrapper` creates a missing `PersistentWorldRegistry`, but it does not create `WorldProceduralScatterDirector`, `WorldProceduralFieldSampler`, `WorldChunkResidencyManager`, `MapMagicRuntimeBridge`, `HectonMapMagicVegetationBridge`, `WorldStreamingDirector`, `WorldSliceDirector`, or `ScatterBudgetController`.
- `GameBootstrapper.TryEnsureWorldGenRegistryCoverage()` only registers an already active `WorldProceduralScatterDirector`; it does not instantiate one.
- `GameBootstrapper.PrimeRuntimeWorldAsync()` prewarms scatter only if `TryResolveProductionScatterDirector()` finds an already registered non-temporary scatter director.
- Static text scene/prefab/data scans did not find serialized references for the main world runtime components or `WorldChunkStreamingProfile.asset`. This is not absolute proof of absence because `02_HECTON_WORLD.unity` is a large binary-like scene, but it means the current static audit cannot prove the production world scene is wired.
- `WorldRuntimeBootstrapAuthoring` can create/configure much of the world manager stack in the editor menu `Hecton/Authoring/Rebuild World Runtime Stack`, and `WorldStreamingWiringValidator` can validate/fix profile references if components exist. These are editor authoring guarantees, not runtime guarantees.
- `WorldRuntimeBootstrapAuthoring` does not appear to create `WorldChunkResidencyManager` or `HectonMapMagicVegetationBridge`; separate scene/authoring proof is needed for those paths.
- `WorldChunkResidencyManager` can compile Addressables load/release paths through `UNITY_ADDRESSABLES_EXIST`, but the project still has no `Assets/AddressableAssetsData` directory in the static filesystem scan. That keeps chunk-asset streaming readiness unproven.

Memory / toaster risk:

- `HectonMapMagicVegetationBridge` defaults to a `256 MB` native vegetation pool budget with a `64 MB` minimum and preallocates fixed threat sampling hash buffers of `65536` entries. This may be justified by visual density, but it is not a toaster-safe claim until Memory Profiler evidence exists.
- `WorldChunkResidencyManager` has tier dispatch budgets (`1/2/3/4`) and predictive VRAM abort logic, but the authored chunk definition count and live Addressables payload are not statically proven.
- `WorldProceduralScatterDirector` still has heavy managed working memory around lists/dictionaries/hashsets plus NativeCollections. `ScatterWorkingMemory` is real and prewarms some pools/buckets, but the static audit cannot certify zero-GC runtime behavior without profiler/GCMonitor proof.

Decision:

- World/scatter is one of the strongest signs the project has become a real game architecture, not a random prototype pile.
- It is also the highest risk area for fake readiness: a serious editor-authored pipeline can still produce no runtime world if the scene was not rebuilt, the profile is unassigned, Addressables data is missing, or the binary scene differs from source assumptions.
- Do not call world streaming production-ready until there is an artifact-backed scene ownership capture showing the active `02_HECTON_WORLD` has the scatter/field/streaming/vegetation/MapMagic/chunk managers, the streaming profile assigned, Addressables or fallback payloads present, and a profiler/memory snapshot on the low tier.

## Asset / Addressables Finding

Package evidence:

- `Packages/manifest.json` includes `com.unity.addressables` version `2.7.6`.
- Runtime code uses `Addressables` in `GameBootstrapper`, `WorldChunkResidencyManager`, `ItemCatalog`, and `AssetLifecycleGovernor`.

Filesystem evidence:

- No `Assets/AddressableAssetsData` directory was found in the static scan.

Risk:

- Streaming architecture may exist ahead of asset-pipeline proof.
- If no Addressables groups/settings are present, runtime load paths can compile but fail as a product pipeline.

Decision:

- Treat all Addressables readiness claims as PENDING VERIFICATION until groups, labels, load modes, release ledger, and Memory Profiler evidence exist.

## Boot / Streaming Wiring Addendum

Evidence class: STATIC_SOURCE / FILESYSTEM. This is not scene-runtime proof.

Boot wiring:

- `00_BOOTSTRAP.unity` contains `[BOOTSTRAPPER]` with `BootstrapController`.
- `BootstrapController` is a compatibility shim. It delegates to `GameBootstrapper.EnsureRuntimeInstance(gameObject)?.BeginBootstrap()` from `Awake`, `Start`, and `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`.
- Direct `GameBootstrapper` script GUID was not found serialized in `_Project` scenes/prefabs/assets. That is not automatically broken because the shim creates or attaches it at runtime, but it means boot authority is code-created and must be proven in Play Mode.

Chunk residency:

- `WorldChunkResidencyManager.cs` is real code, not a stub: it owns chunk definitions, NativeArray/NativeQueue state, Burst residency scan data, profiler markers, Addressables load/release handles, additive-scene activation gates, 300-frame telemetry, and black-box dump code.
- Direct `WorldChunkResidencyManager` script GUID was not found serialized in `_Project` scenes/prefabs/assets.
- Scoped source search found references to chunk residency data and validators, but no obvious scene/prefab project wiring for `WorldChunkResidencyManager` itself.
- Its dump path currently targets `Docs/AgentLogs/Dump_ASSET_STREAMING_PREDICTIVE.bin`; if `AgentLogs` is treated as disposable, crash evidence for this system is disposable too.

Streaming profile:

- `Assets/_Project/Data/World/Streaming/WorldChunkStreamingProfile.asset` exists.
- Current profile data: world size 15000m, chunk size 192m, cell size 64m, macro-zone size 768m, full/mid/visual/data radii 180m/420m/900m/1800m.
- Layer profiles exist for terrain, flora, debris, resources, fauna, construction, and large threats. Large threats explicitly disable chunk residency and use visual proxy/full-simulation near-player settings.
- Static GUID search found the profile asset itself, but did not prove serialized assignment into the runtime scene. Editor authoring/validator code is responsible for assigning `chunkStreamingProfile` to directors.

Item catalog:

- `Assets/_Project/Data/Items/ItemCatalog.asset` exists and references `ItemCatalog`.
- The hardcoded world-prefab fallback GUIDs inside `ItemCatalog.cs` resolve to existing `Assets/_Project/Prefabs/Items/Tools/*_World.prefab.meta` files in the static scan.
- This proves the fallback GUID table is not random garbage. It does not prove Addressables load success, because no `AddressableAssetsData` settings/groups were found.

Scene-search limitation:

- `02_HECTON_WORLD.unity` and `03_HECTON_WORLD_CREST5.unity` are about 32 MB each and produced binary-like output during text search.
- Plain `rg`/`Select-String` against those scenes is therefore partial evidence only. Scene wiring for world directors needs Unity-side inspection or a purpose-built YAML/binary scene report.

Decision:

- Boot and streaming architecture are materially present.
- Runtime wiring remains PENDING VERIFICATION: `00_BOOTSTRAP -> GameBootstrapper -> world scene -> streaming profile -> scatter/residency -> Addressables groups` has not been proven by the static pass.

## Audio Memory Finding

Evidence class: STATIC_SOURCE / FILESYSTEM. No Memory Profiler or runtime mixer capture was run.

Audio inventory under `Assets/_Project/Audio`:

- 45 `.wav` files, about 291.9 MB source total.
- 89 `.ogg` files, about 171.57 MB source total.
- 3 `.mp3` files, about 2.22 MB source total.
- 1 `.flac` file, about 0.06 MB source total.

Large ambient audio files exist under `_Project`:

- `Assets/_Project/Audio/Underwater Ambient.wav` - 32.5 MB, referenced by `Player.prefab`, import settings showed loadType `1`, compression `0`, preload `0`, forceToMono `0`.
- `Assets/_Project/Audio/Atmos 1.wav` - 25.3 MB, loadType `0`, compression `1`, preload `1`, forceToMono `0`, sample override `44100`.
- `Assets/_Project/Audio/Atmos 2.wav` - 24.9 MB, loadType `0`, compression `1`, preload `1`, forceToMono `0`, sample override `44100`.
- `Assets/_Project/Audio/Atmos 3.wav` - 23.8 MB, loadType `0`, compression `1`, preload `1`, forceToMono `0`, sample override `44100`.
- `Assets/_Project/Audio/Atmos 4.wav` - 23.6 MB, loadType `0`, compression `1`, preload `1`, forceToMono `0`, sample override `44100`.
- Several `Atmos * Loop.wav` files are 22-23.4 MB with the same preload/decompress-style risk pattern.
- `Assets/_Project/Audio/Breathing/inside suit sounds (too loud).wav` - 15.1 MB, import settings showed loadType `1`, compression `1`, preload `1`, forceToMono `1`, sample override `22050`.

Reference scan:

- `Underwater Ambient.wav` is directly wired into `Assets/_Project/Prefabs/Player.prefab` on an enabled looping `AudioSource` with `m_PlayOnAwake: 1`.
- The ten `Atmos *.wav` / `Atmos * Loop.wav` files above had no serialized hits in `_Project` scenes/prefabs/assets in the scoped static GUID scan.
- The 15.1 MB breathing WAV had no serialized hits in the same scoped scan.

Importer policy:

- `Assets/_Project/Scripts/Editor/HectonAudioPostprocessor.cs` exists and attempts to enforce first-party import rules.
- Managed SFX roots are specific folders: `SFX`, `Footsteps`, `Hit (Damage)`, `Impact`, `Movement`, `Creatures`, `Thruster`, `Breathing`.
- Managed ambient roots are `Audio/Ambient`, `Audio/Music for Game`, plus paths under `Audio` whose path contains `ambient`.
- The root-level `Atmos *.wav` files do not match those managed roots. They are large unmanaged audio assets.
- `Underwater Ambient.wav` should be managed by the substring rule, but its current meta does not match the postprocessor's ambient target fields.

Music system:

- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` references ten music profiles and is serialized in `01_MAIN_MENU.unity`.
- The music profiles contain about 150 direct AudioClip references across biome/fallback/combat/menu pools.
- The 84 files under `Assets/_Project/Audio/Music for Game` totaled about 171.51 MB and currently showed `loadType: 2`, `compressionFormat: 1`, `preloadAudioData: 0`, `loadInBackground: 1`, `sampleRateOverride: 44100`.
- That is memory-safer than preloaded long-form audio, but it conflicts with `HectonAudioPostprocessor.ApplyAmbientImporterPolicy`, which currently targets `AudioClipLoadType.CompressedInMemory`.

Risk:

- Long ambience as large WAV with preload/decompress-like import settings is a RAM/startup-hitch risk on the 8 GB / MX350 target.
- Audio rules in `AGENTS.md` require ambient/music Vorbis Q70, compressed in memory; SFX rules differ by length.
- Blindly running the managed audio reimport/validator may not be safe: it can convert currently streaming music imports toward the editor policy instead of preserving the lower-memory current import state.

Decision:

- Audio import settings need an explicit pass before runtime memory claims.
- Do not bulk-reimport audio until the import policy is reconciled: root-level `Atmos` assets, `Underwater Ambient`, SFX length handling, and long-form music streaming policy need one written rule set.

## Render / Scene Memory Addendum

Evidence level: STATIC_SOURCE / FILESYSTEM only. No Frame Debugger capture, Memory Profiler snapshot, RenderGraph viewer proof, player build, or runtime frame capture was executed.

Quality / URP state:

- `ProjectSettings/QualitySettings.asset` defines three quality levels: `Surface (Medium)`, `Abyss (Low)`, and `Orbit (High)`.
- Current quality index is `0`, which maps to `Surface (Medium)` and `URP_Medium (PC_RPAsset).asset`.
- `Abyss (Low)` maps to `URP_Low (PC_RPAsset).asset`.
- R5 renderer GUID readback: `URP_Medium` -> `PC_Renderer`, `URP_Low` -> `Mobile_Renderer`, `URP_High` -> `PC_High_Renderer`; older wording that says Low uses `PC_Renderer` is stale against current assets.
- All scanned quality levels have streaming mipmaps enabled.
- Low/Medium streaming mipmap budget is `512 MB`; High is `1536 MB`.
- All scanned quality levels use `asyncUploadTimeSlice: 2`, `asyncUploadBufferSize: 16`, and `asyncUploadPersistentBuffer: 1`.

Risk:

- The project has real URP tiering, but the low tier is not a hard-minimal MX350 fallback.
- `URP_Low` still requires depth texture, opaque texture, HDR, 0.85 render scale, main light shadows, additional light shadows, two additional lights, reflection probe blending/box projection, soft shadows, and 30m shadow distance.
- This may be acceptable for the intended look, but it is not proven inside the low-end budget.
- The async upload buffer is `16 MB`. This saves memory but is below the 64 MB low-tier target in the current performance mandate and can cause texture upload resize/churn risk unless runtime captures prove otherwise.

Renderer-feature state:

- `PC_Renderer.asset` has active custom features for abyssal SSDO, visor uber post, half-res particles, noir depth fog, scooter volumetric shafts, VR brownout, save-thumbnail capture, and atmosphere soot.
- `PC_High_Renderer.asset` adds or enables heavier features including Shapes, screen-space shadows, decals, plus the same broad post/underwater stack.
- `Mobile_Renderer.asset` still has active VR brownout, volumetric shafts, half-res particles, abyssal SSDO, Shapes, noir depth fog, visor uber post, and atmosphere soot.
- Several active feature scripts implement `RecordRenderGraph`, which is a positive sign for URP 17 compatibility.
- The same active feature family also uses `AddUnsafePass` and `Blitter.BlitCameraTexture` paths. This is not automatically wrong, but it requires Frame Debugger / RenderGraph verification before claiming the pass graph is optimal or low-risk.

R17 renderer / visor / shader static refresh:

- URP asset scan: `URP_Low`, `URP_Medium`, `URP_High`, and `Mobile_RPAsset` have SRP Batcher enabled and GPU Resident Drawer / GPU occlusion disabled (`m_GPUResidentDrawerMode: 0`, `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0`). Do not claim GPU Resident Drawer or GPU occlusion is active.
- Renderer asset count: `Mobile_Renderer.asset` has `8` active and `2` inactive features, `PC_Renderer.asset` has `8` active and `5` inactive features, and `PC_High_Renderer.asset` has `10` active and `2` inactive features.
- Visor source count: `21` first-party `ScriptableRendererFeature` files under `Assets/_Project/Scripts/Visor` implement `RecordRenderGraph`; `16` of those files still use `AddUnsafePass`, `4` use `AddComputePass`, and `1` uses obsolete `AddRenderPass<T>`. That is partial RenderGraph adoption, not Frame Debugger proof of an optimal graph.
- Shader-like inventory under `Assets/_Project`: `136` files (`101` `.shader`, `31` `.compute`, `4` `.hlsl`), with `191` `#pragma multi_compile` lines, `13` `#pragma shader_feature` lines, and `66` `numthreads` declarations. The heaviest visible variant surfaces include `TerrainMaster.shader` (`16` variant pragma lines), `HectonBiolumMaster.shader` (`15`), `Hecton_AbyssalVoxelRock.shader` (`11`), and the coral/kelp master shaders (`9-10` each).
- `HectonScooterVolumetricShaftsFeature` is a real fake-first screen-space stack: source tooltip says shaft generation performs zero world raymarch steps, material upload forces `_HectonShaftRaymarchSteps` to `0`, and the renderer YAML `raymarchSteps: 8` is a legacy serialized value, not proof of world volumetric raymarching.
- The same scooter/noir stack still has multiple active full-screen passes plus GPU histogram auto-exposure and contact-shadow globals. It may be justified by the look, but it is not a measured MX350 pass budget until Frame Debugger / Profiler / Memory Profiler captures exist.
- `HectonSonarPointCloudFeature` contains screen-space and world-space sonar history textures, but `enableFullscreenSonarHistory` defaults to `false`; do not claim fullscreen sonar history is active without renderer YAML/runtime proof.
- `ScreenSpaceLightShaftRuntime`, `GroundPenetratingRadarRuntime`, and `InstanceCullingService` contain serious bounded architecture: fixed-size or persistent buffers, 300-frame black-box telemetry rings, low-tier gates, and dump paths. Static GUID scans did not prove any of those runtime components are serialized in `_Project` scenes/prefabs/assets, so treat them as source-ready but scene-wiring `PENDING VERIFICATION`.
- `GroundPenetratingRadarRuntime` specifically caps GPR to `64` rays, `16` low-tier rays, `10` raymarch steps, and `128` pings, and renders pings through `Graphics.RenderMeshIndirect` plus `Hecton_GroundRadarPingIndirect.shader`. This is a bounded sensor fake, not proof that the player can see/use it in scene.

Texture-memory state:

- `_Project` static image source inventory found about `357.51 MB` PNG, `100.72 MB` JPG, and `0.21 MB` EXR.
- 42 textures over 5 MB total about `296.36 MB` source.
- 33 of those over-5 MB textures total about `232.91 MB` source and currently have `streamingMipmaps: 0` in meta reads.
- Large non-streaming candidates include planet/cloud textures, sky textures, coral/kelp/flora detail maps, and some 2K environment textures.

Risk:

- Source texture size is not VRAM size. This is not a memory-profiler result.
- Still, large 2K+ non-streaming art under `_Project` is a real low-end residency risk until a Memory Profiler snapshot proves the active scene budget.

Player prefab scene-cost state:

- `Assets/_Project/Prefabs/Player.prefab` is about 169 KB / 5601 text lines.
- Static prefab scan found 43 `MonoBehaviour` components, 4 cameras, 3 audio sources, 2 lights, 1 rigidbody, 1 sphere collider, and 1 capsule collider.
- Main camera and HUD camera data require post-processing plus depth/color textures.
- `Underwater Ambient.wav` is wired to an enabled looping play-on-awake AudioSource on the Player prefab.
- Multiple `RuntimeSmokeTester` components are serialized on the Player prefab. They may be useful harnesses, but their production/runtime guard status needs a separate source audit.

Scene wiring boundary:

- Static script-GUID search did not find direct scene/prefab hits for several important world systems, including `WorldProceduralScatterDirector`, `WorldStreamingDirector`, `WorldSliceDirector`, `WorldProceduralFieldSampler`, `HectonMapMagicVegetationBridge`, `FaunaDirector`, `ScavengePopulator`, `ScatterBudgetController`, `WorldGenerativeGeologyIntegrationDirector`, `HectonPlayerSpawner`, `HectonVoxelEngine`, and `HectonFluidEngine`.
- This is not proof that they never run. The main world scenes are large and produce binary-like search output, so plain text search is partial evidence.
- It is proof that scene/runtime wiring remains PENDING VERIFICATION until an artifact-backed scene ownership capture exists.

Decision:

- Render architecture is real and modern enough to be worth preserving.
- Current low tier is still visually ambitious, not a verified toaster profile.
- Before any MX350 claim, the project needs one authoritative minimal URP profile, a render-feature gate table per quality tier, an explicit texture streaming policy, Player camera ownership cleanup, and Frame Debugger / Memory Profiler proof.

## Dev Smoke Harness Contamination Addendum

Evidence level: STATIC_SOURCE / FILESYSTEM only. No Play Mode smoke pass, build stripping check, or player build inspection was executed.

Serialized smoke harness footprint:

- Scoped `_Project` scene/prefab/asset scan found smoke-test serialization in two places:
- `Assets/_Project/Prefabs/Player.prefab` serializes eight runtime smoke tester components.
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity` serializes one `ShellVerificationRuntimeSmokeTester` object named `ShellVerificationSmokeTester_SCENE_TEMP`.
- The scoped scan did not find `runOnStart: 1` in `_Project` scenes/prefabs/assets.

Player prefab smoke components:

- `ToolRuntimeSmokeTester` at `Player.prefab:1629`, enabled, `runOnStart: 0`.
- `BuilderRuntimeSmokeTester` at `Player.prefab:1743`, enabled, `runOnStart: 0`.
- `UIRuntimeSmokeTester` at `Player.prefab:1769`, enabled, `runOnStart: 0`.
- `ScanRuntimeSmokeTester` at `Player.prefab:1804`, enabled, `runOnStart: 0`.
- `FieldToolRuntimeSmokeTester` at `Player.prefab:1825`, enabled, `runOnStart: 0`.
- `BarterRuntimeSmokeTester` at `Player.prefab:1873`, enabled, `runOnStart: 0`.
- `ToolTrialRangeRuntimeSmokeTester` at `Player.prefab:1926`, enabled, `runOnStart: 0`.
- `FabricationRuntimeSmokeTester` at `Player.prefab:1946`, enabled, `runOnStart: 0`.

Release/build guard quality:

- `UIRuntimeSmokeTester`, `ScanRuntimeSmokeTester`, and `ToolTrialRangeRuntimeSmokeTester` wrap most runtime logic in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `ShellVerificationRuntimeSmokeTester` has an `IsAutoStartSupported()` guard that returns false outside editor/development builds; the scene instance also serializes `runOnStart: 0`.
- `ToolRuntimeSmokeTester`, `BuilderRuntimeSmokeTester`, `FieldToolRuntimeSmokeTester`, `BarterRuntimeSmokeTester`, and `FabricationRuntimeSmokeTester` are not fully wrapped in editor/development guards. They compile runtime code into release assemblies.
- Their current prefab values prevent auto-run, but their `Awake`/`Start`/manual run paths and smoke logic still exist in the component code.

Asset dependency risk:

- `ToolRuntimeSmokeTester` on `Player.prefab` serializes direct references to 12 held tool prefabs:
- `Tool_Scanner_Held.prefab`
- `Tool_Repair_Held.prefab`
- `Tool_Builder_Held.prefab`
- `Tool_LaserCutter_Held.prefab`
- `Tool_Flashlight_Held.prefab`
- `Tool_Propulsion_Held.prefab`
- `Tool_SalvageSampler_Held.prefab`
- `Tool_BeaconDeployer_Held.prefab`
- `Tool_EnvAnalyzer_Held.prefab`
- `Tool_Knife_Held.prefab`
- `Tool_StunPistol_Held.prefab`
- `Tool_HarpoonLauncher_Held.prefab`
- `FieldToolRuntimeSmokeTester` also serializes a direct item asset reference.
- `Player.prefab` contains 111 unique GUID references in the static scan. This number includes legitimate gameplay dependencies, but dev harness references make the production dependency graph harder to reason about.

Validator blind spot:

- `Assets/_Project/Scripts/Editor/PerformanceHotPathValidator.cs` explicitly excludes `SmokeTester`, `RuntimeSmoke`, and `FabricationRuntimeSmokeTester` from hot-path validation.
- That is reasonable for isolated dev harnesses.
- It is dangerous when those harnesses are serialized on canonical runtime prefabs, because they bypass the same static hot-path audit used to discipline normal production scripts.

Risk:

- This is not an immediate proven frame-time failure because all serialized `runOnStart` values are currently `0`.
- It is still production-boundary contamination: dev validation components are enabled on the canonical Player prefab and a temporary smoke object is serialized into the bootstrap scene.
- The low-end cost may be small in the no-run path, but build inclusion, asset dependency closure, accidental inspector toggles, manual invocation paths, and audit blind spots are real risks.

Decision:

- Keep the smoke testers as tools, but quarantine them.
- Production `Player.prefab` should not serialize dev smoke tester components.
- Dev smoke coverage should live in dedicated test scenes, editor menu runners, or explicitly stripped development-only prefab variants.
- Before any shippable/runtime-memory claim, verify that release builds strip or exclude these harnesses and that player prefab dependency closure is intentional.

## Build Scene Serialization / Debug Overlay Addendum

Evidence level: STATIC_SOURCE / FILESYSTEM only. Scene counts below are text/YAML counts, not Unity runtime hierarchy captures.

Build settings:

- `ProjectSettings/EditorBuildSettings.asset` enables three scenes:
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Scene serialization state:

- `00_BOOTSTRAP.unity` is YAML, about `0.02 MB`.
- `01_MAIN_MENU.unity` is YAML, about `0.62 MB`.
- `02_HECTON_WORLD.unity` is binary/non-YAML, about `32.2 MB`.
- `03_HECTON_WORLD_CREST5.unity` is also binary/non-YAML, about `32.1 MB`.
- `GeminiSandbox.unity` is binary/non-YAML, about `9.17 MB`.
- `ProjectSettings/EditorSettings.asset` reports `m_SerializationMode: 2`, but the core build world scene is still binary on disk.

Static YAML counts:

- `00_BOOTSTRAP.unity`: 10 GameObjects, 7 MonoBehaviours, 0 cameras, 0 AudioSources, 0 lights.
- `01_MAIN_MENU.unity`: 202 GameObjects, 305 MonoBehaviours, 1 camera, 0 AudioSources, 1 light.
- `02_HECTON_WORLD.unity`: text/YAML counts are unavailable because the file is binary. Prior scene GUID and component searches against this file are partial at best.

Risk:

- The core playable world scene is not text-auditable in the current filesystem state.
- This blocks reliable static proof of scene wiring, component ownership, active lights/cameras/audio, active debug objects, and serialized references.
- It also creates a high merge/conflict risk under concurrent agent work.

Bootstrap debug overlay:

- `00_BOOTSTRAP.unity` serializes an active object named `SubnauticaSystemsDebugUI_Root`.
- It has enabled script `Hecton8.UI.SubnauticaSystemsDebugUI`.
- Source comment identifies it as a temporary runtime overlay.
- The script has `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)` and auto-creates `SubnauticaSystemsDebugUI_Auto` when the active scene is `02_HECTON_WORLD`.
- That auto-create path is not wrapped in `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- The component implements `ITickable`, `IUpdatable`, and `ISlowTickable`, then registers with `GlobalRegistry.TryRegisterUpdatable(... PriorityLayer.UI)` and `TryRegisterSlowTickable(... PriorityLayer.UI)`.
- It creates runtime `GameObject`, `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `Image`, `CanvasGroup`, and TextMeshProUGUI objects for a visible debug panel.
- Refresh interval is serialized as `0.2`.
- Stress harness is serialized off, but the visible diagnostics overlay itself is production-bound by source unless stripped elsewhere.

Risk:

- This is stronger than a cosmetic naming issue. Source code indicates a debug UI can create itself in the build world scene.
- No runtime capture was executed, so visibility in an actual player build is PENDING VERIFICATION.
- Still, a production-bound temporary debug overlay is incompatible with a clean vertical-slice/player-facing claim.

Decision:

- Convert or regenerate the core world scene into text/YAML, or provide an authoritative scene inventory artifact.
- Dev/debug overlays must be gated behind editor/development build symbols, a dev-only bootstrap scene, or explicit build stripping.
- Until that happens, scene wiring and player-facing cleanliness remain unproven even if code architecture looks strong.

## Runtime Auto-Init / Hidden Bootstrap Surface Addendum

Evidence level: STATIC_SOURCE / STATIC_DOC only. No player build, Play Mode trace, boot timeline, or profiler capture was executed.

Runtime init inventory:

- Scoped runtime script scan, excluding `Scripts/Editor`, found 267 `RuntimeInitializeOnLoadMethod` lines across 224 files.
- `RuntimeInitializeLoadType` token count in the same scope:
- `SubsystemRegistration`: 239
- `AfterSceneLoad`: 18
- `BeforeSceneLoad`: 7
- `AfterAssembliesLoaded`: 3

Positive signal:

- Heavy `SubsystemRegistration` usage means many systems are at least aware of domain-reload-disabled reset risk.
- This is better than a codebase that relies only on `OnDestroy` or editor domain reload.

Risk:

- The non-`SubsystemRegistration` surface is large enough that runtime authority is not only `00_BOOTSTRAP` and scene wiring.
- Some systems install hooks, mutate quality/URP settings, create GameObjects, attach components, or scan disk before/after scene load.
- Static code proves these paths exist. It does not prove how often they run in the current player route.

Mod loader surface:

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs` runs `Bootstrap()` at `BeforeSceneLoad`.
- It installs SaveEvents, HectonEventBus native queue bindings, ModCommandDispatcher, ModResourceRegistry, GameBootstrapper listener, `Application.quitting`, and `SceneManager.sceneLoaded`.
- It scans a `Mods` directory next to the project/player root via `Application.dataPath` parent + `Mods`.
- It reads `mod.json` files with `Directory.GetFiles(... SearchOption.AllDirectories)` and `File.ReadAllText`.
- Managed runtime assembly reflection loading is not active in the inspected code path; managed mods require explicit registered factories.
- Content-only mod metadata, localization files, and bundle paths can still be discovered and registered.
- No `mod.json` file was found in the current repo filesystem scan.

Risk:

- If modding is a deliberate shipping pillar, this needs a first-class security/performance/product policy.
- If modding is not a current vertical-slice goal, it is a large early-boot surface running too soon.
- Even with no repo mods, external runtime `Mods` content can alter boot behavior; static repo audit cannot prove player-build cleanliness.

Runtime fail-safe creation surface:

- `WorldReadabilityRuntimeBootstrap` creates `WorldReadabilityDirector_Root` and attaches readability/relay directors if authored scene components are missing.
- `RelayHUDRuntimeBootstrap` creates HUD route marker layers and marker UI under the active suit HUD if missing.
- `SubtitleManager` creates a `SubtitleManager` owner under the active HUD canvas if missing.
- `SuitHUDV4CanvasOverlay` attaches `HectonUIScaler` and `SuitHUDV4CanvasOverlay` to a scene canvas named `Suit_HUD_Canvas` if missing.
- `HectonSubmarineOS` attaches `HectonSubmarineOS` and `SubmarineStationKeepingController` to registered submarine roots after scene load.
- `PlayerStressMetricsRuntime` creates a `[PlayerStressMetricsRuntime]` object and calls `DontDestroyOnLoad`.
- `HectonMusicDirector` can instantiate a configured runtime director prefab through `ObjectPoolManager` after scene load.
- `QAEnduranceWatchdogBot` can create a DDOL QA bot if command line args, environment variable `H8_QA_ENDURANCE_10KM=1`, or `Temp/H8_QA_ENDURANCE_10KM.flag` are present.
- `DodReplayRecorder` auto-starts after scene load, but the inspected file is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so it is a dev/development-build surface, not release-player proof.

Risk:

- Runtime fail-safes are useful for resilience, but they also hide missing authored scene setup.
- A scene can look "working" because many components self-inject, while the actual scene authoring contract remains broken.
- This makes build-scene truth and prefab dependency closure harder to audit, especially because `02_HECTON_WORLD.unity` is binary.

Quality / scalability mutation surface:

- `PlatformBatteryWatchdog` runs after scene load and can call `QualitySettings.SetQualityLevel(0, true)` on critical battery.
- In current `QualitySettings.asset`, index `0` is `Surface (Medium)`, while `Abyss (Low)` is index `1`.
- Therefore the critical-battery path does not select the named Low quality profile by index; it selects Medium while separately registering `ScalabilityTierProfiles.LowMx350`.
- `HardwareTierDetector` registers `LowMx350` for constrained/legacy/shared-memory conditions, but that tier override is not the same thing as swapping the URP quality profile.
- `HectonUrpShadowBudgetGuard` runs before scene load and mutates the active URP asset shadow settings at runtime. It forces shadow distance to `40m`, atlas resolution by tier, and cascade count by quality index.
- This means static URP asset fields such as `shadowDistance: 30` are not the full runtime truth.

Risk:

- Current low-end behavior is split between QualitySettings index, GlobalRegistry scalability tier override, URP asset mutation, and renderer feature assets.
- That is too many authorities for a hard MX350 claim without a boot trace and Frame Debugger/Profiler proof.
- The specific `SetQualityLevel(0)` path is likely semantically wrong if `Abyss (Low)` is intended to be the low preset.

Decision:

- Keep `SubsystemRegistration` reset culture; it is a strength.
- Build one explicit runtime-init ledger: every `BeforeSceneLoad`/`AfterSceneLoad` method must say whether it is production, development-only, fail-safe, or forbidden in player builds.
- ModLoader needs an explicit shipping policy and a build flag if it is not part of the current vertical slice.
- Runtime fail-safes should emit artifact-backed "authored setup missing" reports, not silently normalize missing scene wiring.
- Low-end quality must have one authoritative path: quality index, URP asset, scalability tier, texture budget, shadow budget, and feature gates must converge.

## Modding Boundary / Internal Event Coupling Addendum

Evidence type: STATIC_SOURCE.

Static inventory:

- First-party runtime scripts outside `ModdingAPI` and `Editor` contain 41 direct `HectonEventBus.Publish/Subscribe` call sites.
- Split observed in the static scan: 16 publish call sites and 25 subscribe call sites.
- Publishers include weather shock, logistics pipe leak, random meteor event, recycling, harvest/item collection, celestial eclipse, survival death/damage, player building, achievement unlock, advisory issuance, inventory discard, and player inventory payloads.
- Subscribers include meta difficulty, run modifiers, global profile, achievements, PDA advisory/logbook, UI boot sequence, death memory dump, and environmental strain.
- `SystemDispatcher.LateUpdate()` always calls `ModCommandDispatcher.DrainLateFrame()` and includes `ModRegistryEvents.PendingCount` / `FlushPending()` in the core late-frame artery.
- `ModLoader.Bootstrap()` runs `BeforeSceneLoad`, installs save/event/bootstrap/application/scene hooks, initializes `ModCommandDispatcher` and `ModResourceRegistry`, and scans external `Mods` content when present.

Positive architecture:

- The bus is not naive C# event soup.
- `HectonEventBus` has a dispatch-depth cap of 5, recursive cascade reporting, callback watchdog, exception isolation, subscriber disable, and managed allocation tracking around callbacks.
- Typed managed event publishing/subscribing is `internal` and forbidden from active mod execution scope; public mod-facing event lanes use unmanaged payloads/native payload copies.
- `ModCommandDispatcher` uses persistent `NativeQueue` lanes, `NativeHashMap` lookup tables, command quotas, per-mod command accounting, AUP rebasing, and queue prewarm.
- Mod command capacities are explicit: 4096 standard commands, 4096 AUP commands, 1024 render instances, 128 raycasts, 256 reject/AUP response lanes, 32 mod states.
- `ModRegistryEvents` coalesces registry invalidations into small native queues and drains them through `SystemDispatcher`.

Risk:

- Despite the name and API surface, this is not a purely optional external mod layer anymore. It is part of the first-party gameplay/meta/PDA/progression/event graph.
- If `ModLoader`, `HectonEventBus`, or mod command drain behavior regresses, first-party systems can be affected even when no external mods exist.
- The regular late-frame path pays at least a structural check/drain surface for mod command and registry events; static source cannot prove the per-frame cost is negligible.
- Managed event callbacks still execute through `List<SubscriptionEntry>` channels, `Stopwatch.GetTimestamp()`, `GC.GetAllocatedBytesForCurrentThread()`, `try/catch`, and optional mod scope checks. This is engineered safety, not free hot-path math.
- Any first-party gameplay event using `new SomeEvent(...)` before `Publish()` is not zero-allocation by construction unless the event type is pooled or the path is cold. Static source found several such call sites.
- Because mod content can be discovered outside the repo at runtime, repository-only static audit cannot prove shipping-player mod cleanliness.

Decision:

- Treat `HectonEventBus` as a first-party event spine with mod projection, not as an optional mod API.
- Hot gameplay lanes should prefer existing NativeQueue/static buses or narrow domain signals. `HectonEventBus` is acceptable for cold/meta/progression hooks, but suspicious for frequent frame gameplay.
- Add a shipping policy: mod layer enabled/disabled per build target, external `Mods` scan policy, IL2CPP policy, and artifact-backed boot trace.
- Add a small static ledger of first-party `HectonEventBus` use: cold allowed, warm reviewed, hot forbidden unless measured.
- Do not delete this layer blindly. It contains real safety work. The correction is boundary discipline and measured runtime cost, not panic removal.

## Black Box / Crash Forensics Addendum

Evidence type: STATIC_SOURCE / FILESYSTEM.

Static inventory:

- `CrashTelemetryBuffer` exists as a runtime `MonoBehaviour` implementing `ITickable`, `IUpdatable`, and `IFixedTickable`.
- Central crash telemetry uses a `NativeArray<TelemetryEntry>` ring of 1024 entries, a 1000-entry export snapshot, 64-byte entries, and a fixed 64016-byte export scratch buffer.
- It records physics NaN recovery, memory pressure/spikes, physiology NaN, time dilation, bus congestion, signal lanes, origin shift, kinetic anomaly, late-frame load shedding, critical performance spike, latency crime, native memory faults, audio overflow/DSP stats, bootstrap safe halt, runtime watchdog stall, AUP jitter correction, and Unity log faults.
- It writes `runtime_telemetry.bin` and `BLACKBOX_CRASH.h8dump` through `HectonPersistentPathPolicy`, which resolves to `Application.persistentDataPath`, not `Docs/AgentLogs`.
- Static source scan found 48 runtime C# files with `Dump_*.bin` paths.
- Static source scan found 50 runtime/editor C# files matching a 300-frame telemetry/blackbox capacity pattern.
- Critical covered domains include voxel mesh pipeline, voxel caving, player kinematics, physics determinism, physics culling, AI encounter director, drone fleet, tethers, habitat, pipe logistics, submarine systems, audio DSP/VWS, radiation, suit upgrades, scatter, chunk residency, bioluminescence, thermal/weather, ore/wreck generation, flora growth, and food-chain telemetry.
- `Docs/AgentLogs` currently contains no `Dump_*.bin` or `crash_*.h8dump` files in the filesystem scan. This is not a failure by itself; no runtime crash/export was executed in this audit.

Positive architecture:

- The black-box mandate is not just paperwork. Many systems own fixed native telemetry rings and binary dump paths.
- Central crash telemetry is more mature than the local mandate minimum: 1024-entry ring, 1000-entry snapshot, background export worker, fixed binary header, live telemetry record, export cooldown, and failure/dropped/suppressed counters.
- Several domain systems use one-shot or throttled dump flags to prevent fault-frame spam.
- Editor tooling includes a `BlackBoxBinaryReader`, so the project has at least a source-level path to inspect binary dumps.

Risk:

- The project has two crash-forensics destinations: central `persistentDataPath` files and many domain `Docs/AgentLogs/Dump_*.bin` files. That is a policy split, not a single authoritative postmortem contract.
- `Docs/AgentLogs` was explicitly called temporary by the user; many dump paths still target it. If logs are cleaned, postmortem artifacts can be erased with operational chatter.
- `Gameplay/DataArchaeologyRuntime.cs` builds `Path.Combine(Application.dataPath, "../../Docs/AgentLogs/Dump_DATA_ARCHAEOLOGY.bin")`. From a normal Unity project `Application.dataPath == <project>/Assets`, this resolves toward `<project parent>/Docs`, not `<project>/Docs`, so this dump path is likely wrong.
- Many domain dump writers hand-build paths from `Application.dataPath` instead of a shared dump-path policy. That creates drift and silent wrong-directory risk.
- Static source proves telemetry code exists. It does not prove that all critical systems are registered, ticking, dumping, or readable in the current build route.
- No current binary dumps were found, so no postmortem reader/artifact validation was performed.

Decision:

- Treat black-box culture as a real project strength.
- Do not claim forensics readiness until one controlled crash/NaN route produces a dump and the dump is read back by tooling.
- Move dump root policy into one shared API: project docs dump for editor/dev, persistent data dump for player, never ad hoc `Application.dataPath` arithmetic.
- Keep `Docs/AgentLogs` operational logs separate from durable crash artifacts, or document a preservation rule before log cleanup.
- Fix the `DataArchaeologyRuntime` `../../Docs` path before trusting that system's crash evidence.

## Assembly / Domain Boundary Addendum

Evidence type: STATIC_SOURCE / FILESYSTEM.

Static inventory:

- 2026-05-17 R6 scan: total `*.asmdef` files under `Assets`: 141.
- 2026-05-17 R6 scan: first-party `*.asmdef` files under `_Project`: 95.
- 2026-05-17 R6 scan: first-party `*.asmdef` files under `_Project/Scripts`: 91.
- 2026-05-17 R6 nearest-asmdef static count for all C# files under `_Project/Scripts`: `Hecton8.Core` owns about 1203 C# files; `Hecton8.Editor` owns about 212; all other first-party script assemblies are still small by comparison.
- 2026-05-17 R6 nearest-asmdef static count excluding `Editor` folders: `Hecton8.Core` owns about 1198 runtime C# files.
- `Hecton8.Core.asmdef` references `Hecton8.Core.Memory`, bootstrap/world/contracts, physics determinism, logistics, cartography, SpaceEngine terrain, input, Unity InputSystem, Mathematics, Burst, Collections, Addressables, ResourceManager, Profiling.Core, TextMeshPro, UnityEngine.UI, URP/Core RP, `GPUInstancer`, and `VolumetricLightBeam`.
- `Hecton8.Plugins.asmdef` references `Hecton8.Core`, SpaceEngine terrain, Burst/Collections/Mathematics, `Den.Tools`, `MapMagic`, `Crest`, and WaveHarmonic Crest assemblies.
- `Hecton8.World.Dots.asmdef` is `autoReferenced: false` and gated by define constraints `HECTON8_ENABLE_ENTITIES_DOTS`, `HECTON8_HAS_ENTITIES_PACKAGE`, and `HECTON8_ENABLE_OPTIONAL_ASSEMBLIES`.
- `Hecton8.QA.asmdef` is runtime-included (`includePlatforms: []`, `autoReferenced: true`) and references `Hecton8.Core`, Mathematics, and Collections.
- Scoped `using UnityEditor` scan outside `Editor` folders found many editor-conditioned sections; targeted check found the obvious top-level editor-only files were wrapped in `#if UNITY_EDITOR`. This is not a compile proof.

Positive architecture:

- There is a real attempt to carve out contracts, memory, time, input, logistics, lighting, physiology, cartography, world contracts, DOTS, plugins, QA, editor, and tests.
- Optional DOTS is correctly shaped as non-auto-referenced and define-gated at the asmdef level.
- Plugin-specific MapMagic/Crest references are mostly isolated into `Hecton8.Plugins` rather than injected into the first-party core assembly.
- Tests are non-auto-referenced and define-gated by `UNITY_INCLUDE_TESTS`.

Risk:

- The actual runtime compile unit is still dominated by `Hecton8.Core`. With about 1198 non-editor script C# files nearest to that assembly in the R6 static scan, the assembly boundary does not yet enforce most domain boundaries.
- `Hecton8.Core` directly references UI, TMP, URP, Addressables, InputSystem, Burst, Collections, GPUInstancer, and VolumetricLightBeam. That makes the central gameplay assembly sensitive to render/UI/input/asset/plugin churn.
- The small domain assemblies are currently more like islands than the main architecture. Most gameplay still lives in the core sea.
- `Hecton8.QA` is runtime auto-referenced. Even if QA code is gated at runtime, it is still part of player compilation unless the build pipeline strips it by symbols/asmdef changes.
- Third-party packages remain auto-referenced in their own asmdefs across `Assets`. Even when first-party code avoids them, they stay in the compile/import surface.
- R5 package-lock scan found forbidden UPM package IDs absent from `Packages/manifest.json`: DOTween, MasterAudio, Easy Save, and Astar are not UPM dependencies.
- R5/R6 filesystem scan still found physical legacy contamination: `Assets/AstarPathfindingProject` (`605` files), `Assets/Plugins/Easy Save 3` (`422` files), `Assets/Plugins/Demigiant` (`357` files, including `DOTween`/`DOTweenPro`), and `Assets/Plugins/DarkTonic/MasterAudio` (`346` files, about `51 MB`).
- R5 first-party `.cs` scan found no active `DG.Tweening`, `DOTween`, `ES3`, `Easy Save`, `MasterAudio`, or `DarkTonic` usage. Astar appears only as dormant archetype/authoring labels, `useAstarPathing: 0` in scanned archetype assets, and `ThirdPartyStrippingGuard` text.
- `ThirdPartyStrippingGuard` currently audits `Crest`, `MapMagic`, `Steamworks`, `GPUInstancer`, `AstarPathfindingProject`, and `Feel`; it does not name `Easy Save 3`, `Demigiant`, `DOTween`, `DarkTonic`, or `MasterAudio`.
- Many runtime files contain editor-only helper code guarded by preprocessor directives. This is common in Unity, but it means one bad guard can break player builds from a core file.

Decision:

- Do not treat namespace/domain docs as hard boundaries until the assembly shape matches them.
- Short-term: keep using the core monolith, but stop adding new plugin/render/editor dependencies to `Hecton8.Core`.
- Medium-term: extract only stable leaf domains with low cross-talk: save contracts, player kinematics contracts, audio contracts, scan/PDA contracts, construction contracts, world streaming contracts.
- Keep DOTS optional and non-auto-referenced until a measured path proves parity.
- Move QA runtime code behind development-only asmdefs or build symbols if it is not an intentional shipping feature.

## Package / Player Settings Drift Addendum

Evidence type: `STATIC_SOURCE`, `FILESYSTEM`, `PACKAGE_LOCK`.

Static inventory:

- `ProjectSettings/ProjectVersion.txt` pins Unity `6000.4.1f1`.
- `Packages/manifest.json` pins URP `17.4.0`, Addressables `2.7.6`, Input System `1.19.0`, AI Navigation `2.0.11`, Memory Profiler `1.1.12`, and `com.coplaydev.unity-mcp` from a Git URL.
- `Packages/manifest.json` does not list `com.waveharmonic.crest`, `com.jbooth.microsplat.core`, `com.jbooth.microsplat.urp2022`, or `com.unity.shadergraph`.
- `Packages/packages-lock.json` does list embedded package entries for `com.waveharmonic.crest`, `com.jbooth.microsplat.core`, `com.jbooth.microsplat.urp2022`, and `com.unity.shadergraph`.
- Physical embedded package folders exist under `Packages/`: `com.waveharmonic.crest`, `com.jbooth.microsplat.core`, `com.jbooth.microsplat.urp2022`, and `com.unity.shadergraph`.
- `Packages/com.waveharmonic.crest/package.json`: Crest `5.4.1`, declared Unity compatibility `2022.3`.
- `Packages/packages-lock.json` records Crest dependency expectations for `com.unity.render-pipelines.core` `14.0.11` and `com.unity.shadergraph` `14.0.11`, while the project uses URP/Core/ShaderGraph `17.4.0`.
- `Packages/com.jbooth.microsplat.core/package.json`: MicroSplat Core `3.9.0`, declared Unity compatibility `2019.4`.
- `Packages/com.jbooth.microsplat.urp2022/package.json`: MicroSplat URP2022 `3.9.0`, declared Unity compatibility `2022.2`.
- `ProjectSettings/ProjectSettings.asset` scripting defines include `DOTWEEN` on multiple platforms and Standalone additionally includes `CREST_OCEAN`, `CREST_URP`, `__MICROSPLAT__`, `MAPMAGIC2`, `GPU_INSTANCER`, `ODIN_INSPECTOR`, `AMPLIFY_SHADER_EDITOR`, `SHAPES_URP`, `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED`, `BAKERY_INCLUDED`, and `VLB_URP`.
- `ProjectSettings/ProjectSettings.asset` still has template app identifiers: Android `com.UnityTechnologies.com.unity.template.urpblank`, Standalone/iPhone `com.Unity-Technologies.com.unity.template.urp-blank`.
- Product metadata is still early: `productName: Submerge`, `bundleVersion: 0.1.0`.
- `ProjectSettings/XRSettings.asset` exists as a tiny legacy XR settings file, but `Packages/manifest.json` and `packages-lock.json` have no `com.unity.xr.management` or `com.unity.xr.openxr` entries.
- Useful hardening settings are present: `allowUnsafeCode: 1`, `useDeterministicCompilation: 1`, `gcIncremental: 1`, and `activeInputHandler: 1`.

Positive architecture:

- The forbidden tools are not UPM dependencies in `manifest.json`; the project is not currently pulling DOTween/Easy Save/MasterAudio/Astar from Package Manager.
- Standalone scripting defines honestly reflect the actual heavy integration surface: Crest, MicroSplat, MapMagic, GPUInstancer, Odin, Shapes, NiceVibrations, Bakery, and VLB are not imaginary.
- Deterministic compilation, unsafe code, incremental GC, and new Input System are explicitly enabled at the ProjectSettings level.

Risk:

- `manifest.json` alone under-reports the real package surface. `packages-lock.json` and the physical `Packages/` folder reveal embedded Crest, MicroSplat, and ShaderGraph entries that must be treated as current risk until Unity import/build proves otherwise.
- Crest and MicroSplat package metadata target older Unity/URP generations than the current Unity 6000.4 / URP 17.4 project. This does not prove breakage, but it is a compatibility-risk surface.
- The `DOTWEEN` scripting define survives even though first-party source currently shows no active DOTween runtime usage. Defines can keep dead integration branches alive and can hide contamination behind compile symbols.
- Standalone has many vendor defines turned on at once. That increases import/compile and shader-variant surface even when first-party usage is quarantined.
- XR/VR code and architecture domains exist, but source-of-truth package settings do not show OpenXR or XR Management installed. Treat VR as code-level aspiration/presentation scaffolding, not platform-ready configuration.
- Template application identifiers are not production metadata. This is not gameplay-breaking, but it is objective evidence the project is not productized for release.

Decision:

- Do not say "package config is clean" without qualifying the statement. `manifest.json` is cleaner; the real project surface is not.
- Future package audits must read `manifest.json`, `packages-lock.json`, physical `Packages/`, physical `Assets/Plugins`, asmdefs, and PlayerSettings scripting defines together.
- Strip stale define symbols or document why they remain. A forbidden-package folder plus a live define is a stronger risk than a folder alone.
- Require a Unity import/build artifact before claiming Crest 5.4.1 + URP 17.4 + Unity 6000 compatibility.
- Require explicit XR package/config proof before claiming VR platform readiness.

## Asset Loading / Data Residency Addendum

Evidence type: STATIC_SOURCE / FILESYSTEM.

Static inventory:

- `Assets/AddressableAssetsData` is absent in the current tree.
- `Assets/StreamingAssets` is absent in the current tree.
- Therefore `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is also absent.
- `H8DataLayoutConstants.DefaultStreamingAssetsRelativePath` points to `Hecton8/DataMonolith/static_data.h8bin`.
- `GameBootstrapper.InitializeBootstrapDataMonolith()` calls `H8StaticDataArena.TryInitializeFromStreamingAssets(..., failIfMissing: false, ...)` and treats `Missing` as acceptable.
- `H8StaticDataArena` has a 256 MB blob cap, header/directory validation, persistent native arena residency, write-locking after ready, and a boot-only `File.ReadAllBytes` staging copy before native blit.
- `AsyncLoadHelper` is a deliberate compatibility tombstone: `Instance => null`, load calls return null/failure, and editor builds log that runtime Resources/Addressables loading through that helper is unsupported.
- Direct `Resources.Load` use under first-party `_Project/Scripts` runtime files excluding `Editor` folders: 0 files in the static scan.
- All `Assets` runtime C# files with `Resources.Load`: 30 files, mostly third-party packages: Crest, GPUInstancer, MapMagic, Dynamic Decals, Shapes, VolumetricLightBeam, Easy Save 3, MasterAudio, Feel/NiceVibrations, Astar helpers, and Mantis LOD.
- `Resources` folders under `Assets`: 40 directories, about 13.12 MB of source files. First-party `_Project` Resources footprint is small; the larger buckets are mostly MapMagic editor resources, Shapes, VolumetricLightBeam, GPUInstancer, MapMagic runtime resources, and Dynamic Decals.
- First-party runtime files outside `Editor` folders contain 117 `AssetDatabase.LoadAssetAtPath` call lines across 69 files. These are likely guard-heavy editor fallback paths, but this audit did not compile a player build to prove every guard.
- `GameBootstrapper` uses `Addressables.DownloadDependenciesAsync` and releases dependency handles.
- `ItemCatalog` uses `AssetReferenceGameObject`, dispatch tickets, fallback GUID construction, `LoadAssetAsync<GameObject>()`, and `Addressables.Release`.
- `WorldChunkResidencyManager` uses addressable chunk prefab loading, handle release, and `Addressables.ClearDependencyCacheAsync`.
- `AssetLifecycleGovernor` has Addressables release/cache-cleanup paths.
- `ModAssetManager` can load external mod content with `AssetBundle.LoadFromFile(bundlePath)` and cache loaded bundles for the process lifetime.

Positive architecture:

- The old first-party runtime `Resources.Load` path appears intentionally killed, not accidentally forgotten.
- The first-party Resources folder footprint is not the major current residency risk.
- Addressable handle discipline exists in several important systems; this is not a naive "load and leak forever" codebase.
- The data monolith loader is shaped like a serious boot-time static-data pipeline: bounded size, native resident arena, validation, write lock, explicit status reporting.
- Item world prefab loading has a queued/dispatcher-aware path instead of only immediate async loads.

Risk:

- Addressables code exists, but the project tree does not currently contain Addressables settings/groups. Static source cannot prove any label, address, dependency chain, or chunk prefab address resolves.
- The static-data monolith loader exists, but the expected StreamingAssets blob is missing. Because missing is accepted at boot, the game can silently run without the intended monolith unless another path populates it.
- Third-party runtime `Resources.Load` is still present in packages that first-party core references or can activate indirectly, especially GPUInstancer, VolumetricLightBeam, Crest, MapMagic, Shapes, Easy Save, and MasterAudio.
- `Hecton8.Core` references GPUInstancer and VolumetricLightBeam, so those Resources-backed package systems are close to the central runtime surface even if first-party code avoids direct Resources calls.
- The 117 `AssetDatabase.LoadAssetAtPath` call lines in non-Editor-folder first-party files are a compile/build fragility surface. One missing `#if UNITY_EDITOR` can turn an otherwise clean player build into a failure.
- Editor fallback asset loading is spread across player, UI, world, weather, fluid, scanner, render-feature, construction, and smoke-test files. That makes asset-authoring convenience leak into runtime-file ownership.
- Mod bundle loading is valid as a cold extensibility path, but it is another asset residency authority outside Addressables/StreamingAssets and needs a shipping policy.

Decision:

- Do not claim content-streaming readiness from Addressables API calls. Require `AddressableAssetsData`, group/label inventory, and a resolved boot trace.
- Do not claim static-data readiness until `static_data.h8bin` exists or the missing-monolith fallback is explicitly documented as the intended vertical-slice mode.
- Keep the first-party Resources tombstone. Do not revive `AsyncLoadHelper` as a quick fix.
- Build an asset residency ledger with four columns: scene-owned reference, Addressables, StreamingAssets monolith, external mod bundle. Anything outside those columns is suspicious.
- Centralize editor fallback loading behind editor-only helpers or partial files instead of scattering `AssetDatabase.LoadAssetAtPath` through runtime files.
- Third-party Resources usage needs package-by-package policy: used and measured, quarantined, or removed.

## Gameplay Economy / Inventory / Logistics Addendum

Evidence type: STATIC_SOURCE / FILESYSTEM. No Unity, Play Mode, profiler, or dotnet verification was run.

Static inventory:

- `Assets/_Project/Data/Items` contains `73` ItemData assets excluding `ItemCatalog.asset`.
- `Assets/_Project/Data/Items/ItemCatalog.asset` references `73` unique item assets; all catalog GUIDs resolve to files under `Data/Items`.
- `1` ItemData asset is intentionally still outside `ItemCatalog`: legacy root `Assets/_Project/Data/Items/Data_Copper.asset`. The R21 catalog pass added `Resources/Raw/Data_CarbonGraphite.asset`, `Resources/Raw/Data_PressureDiamond.asset`, and `Resources/Raw/Data_VoidGlassMeteorite.asset`.
- `Assets/_Project/Data/Crafting/Recipes` contains `41` recipe assets. Static parse found `149` non-script recipe GUID references, and all resolve to current item assets.
- Recipe groups are populated: group `1` has `5`, group `2` has `7`, group `3` has `6`, group `4` has `3`, group `5` has `11`, group `6` has `9`.
- `6` recipes are scan-locked: `Recipe_EnvAnalyzer`, `Recipe_FieldBeacon`, `Recipe_Flashlight`, `Recipe_RepairTool`, `Recipe_SalvageSampler`, and `Recipe_Scanner`. No biome-locked recipe was found in this static parse.
- Recipe ingredient count is currently small and bounded: max `3` ingredients per recipe asset.
- R27 static craft-route check: `Recipe_Scanner.asset` outputs `Item_Tool_Scanner` and has two authored ingredient entries; `ContentSanityValidator` now validates recipe result/ingredient/catalog/group integrity and cross-checks `QuestData.OnCraftCompleted` IDs against valid recipe outputs. This is still not runtime fabricator proof.
- R28 scan-gate check: `scan.resource_node` has an obvious generic runtime source in `ScanLogSystem` / `ScannerTool`; `scan.expedition_contact`, `scan.resource_cache`, and `scan.structure_relay` are visible in recipe assets and editor authoring scripts, but no current `_Project` prefab/scene/data route was found by static grep. `ContentSanityValidator` now warns via `RecipeScanGateWarnings` when a scan-locked recipe has no known generic or prefab `ScannableTarget` route.
- `Assets/_Project/Data/Scavenging/ResourceNodes` contains `27` `ResourceNodeTemplate` assets; all `27` harvest item refs resolve to current ItemData assets.
- R14 snapshot found `23 / 27` ResourceNodeTemplate harvest items with `worldPrefab: {fileID: 0}`. R19 reduced the current primary-harvest gap to `16 / 27`; R21 reduced the current resource-node primary-harvest gap to `0 / 27` by assigning existing pickup shells to the remaining harvest ItemData.
- R14 snapshot found `4 / 27` ResourceNodeTemplate harvest refs pointing to ItemData assets not present in `ItemCatalog`. R19 moved `ResourceNodeTemplate_CopperVein` to cataloged raw copper; R21 added the remaining three raw resources to `ItemCatalog`, so the current resource-node harvest non-catalog count is `0 / 27`.
- There are still two `Data_Copper` ItemData assets with the same `stableId: Data_Copper`: root `Assets/_Project/Data/Items/Data_Copper.asset` and cataloged raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`. Recipes, the catalog, `ResourceNodeTemplate_CopperVein`, R18 provisioner starter material, and the checked barter offers now reference the raw asset; the root asset remains legacy contamination, not current first-hour copper authority. R23 adds an editor validator tripwire for duplicate `ItemData.PersistentId` so this contamination is no longer only documented manually.
- `PlayerInventory.cs` is about `166.9 KB` and is load-bearing: it owns a grid plus native SOA mirrors, stack counts, condition, craft locks, genetics, quality, mass/volume/radiation caches, degradation, pressure crush, reactive chemistry, and save shadow payloads.
- `CraftingSystem.cs` has bounded native evaluators: recipe ingredient cap `32`, complex recipe depth cap `5`, graph node cap `64`, graph edge cap `128`, bitmask fast-fail, `NativeParallelHashMap` counts, and a topological raw-cost expansion path.
- `Fabricator.cs` is present in the binary world scenes: static scene string scan found `Trial_Fabricator`, `Forward_Fabricator`, `Hecton8.Crafting.Fabricator`, and `HectonFabricatorUI`; `Player.prefab` targets `Forward_Fabricator`.
- R26/R27 static first-hour quest string check: `Data_TitaniumScrap`, `Item_Tool_Scanner`, and cataloged raw `Data_Copper` are present in `ItemCatalog`; checked first-hour prerequisites resolve to existing quest IDs; `OnCraftCompleted` IDs are now required to resolve to valid recipe outputs. The legacy root `Data_Copper` remains outside catalog and is covered by R23 duplicate identity validation.
- `ResourceNode` is present in the binary world scenes: static scene string scan found `14` `Hecton8.Scavenging.ResourceNode` hits and named starter nodes including `Node_Copper_A`, `Node_Copper_B`, `Trial_Node_Active`, and `Trial_Node_Depleted`.
- `ResourceScarcityDirector` is not scene-placed by static scene string scan, but `GameBootstrapper.PublishPlayerRuntimeReference()` calls `EconomyRuntimeInstaller.EnsureRuntimeSystems()`, which creates `ResourceScarcityDirector` under `__HECTON_ECONOMY_RUNTIME`.
- `ResourceDistributionDirector` and `ProceduralOreSpawner` contain serious deterministic spawning code, but static scene/prefab string scans found `0` placements for them. `ResourceDistributionBootstrapAuthoring` can install the director through an editor menu, which is authoring proof, not runtime placement proof.
- `ConstructionManager` is present in world scenes and is the `ILogisticsService` owner. `PowerGridManager` is boot-created by `GameBootstrapper` if no runtime instance exists.
- `BaseLogisticsNetwork` is a real cold-path logistics layer: fixed reservation pool `64`, storage/fabricator/recycler endpoint registries, power-grid scoped access counts, route CSR scratch, and BFS routing through `LogisticsPipeRoutingKernel`.
- `FluidPipeGraphRuntime`, `LogisticsPipeNode`, and `LogisticsPipeTransportScheduler` code exists, including native SOA pipe pressure jobs and a `300` frame black box, but static scene/prefab scans found `0` placements for the pipe runtime and `GameBootstrapper` does not create it.
- `ContentSanityValidator` now checks `ResourceNodeTemplate.harvestYield` and `rarityDrops` against the active `ItemCatalog`, `ItemData.worldPrefab`, and world-prefab pickup contract (`PickupItem`/`HectonItem`, `Collider`, `Rigidbody`), with explicit `ResourceNodeYieldMissingWorldPrefab`, `ResourceNodeYieldNotCataloged`, and `ResourceNodeYieldInvalidWorldPrefabContract` summary counters. R23 extends it with item/catalog identity counters: `ItemDataDuplicatePersistentId`, `ItemCatalogNullEntries`, `ItemCatalogDuplicateHashes`, `ItemCatalogMissingRuntimeDescriptors`, and `ItemCatalogLookupAmbiguities`. R26 extends it with quest route counters: `Quests` and `QuestRouteErrors`, validating `QuestData.questId`, `prerequisiteQuestIds`, item/craft `triggerId` and `completionId`, and `criticalItemId` against active quest/catalog data. R27 extends it with recipe route counters: `Recipes` and `RecipeRouteErrors`, validating `RecipeData` result/ingredient/catalog/group state and craft quest recipe-output routes. R28 adds `RecipeScanGateWarnings` for scan-locked recipes with no known generic or authored prefab `ScannableTarget` route. This is editor tooling proof only; the validator was not run in Unity during this pass.

Positive architecture:

- The project has a real resource/crafting spine: item data, catalog, recipes, recipe unlocks, fabricator UI, player inventory, resource nodes, first-hour quest hooks, and logistics storage queries are not empty scaffolding.
- The inventory implementation is not a plain managed list. It is a native SOA-backed grid with explicit caches, non-alloc item count copy, mass/volume/radiation recompute jobs, and save shadow state.
- Crafting evaluation is bounded and data-oriented enough for a vertical-slice scale. The current `41` recipes and max `3` ingredients per recipe are small enough that synchronous `IJob.Execute()` use is not automatically a frame disaster, though it still needs UI-call-cadence proof.
- The first-hour loop is designed around actual product beats: orientation, first copper resource quest, first breath/depth quest, first craft, route/lore/module guidance, and scan/craft/interaction event listeners.
- Scene static strings prove at least two fabricator stations and starter resource nodes exist in world scenes. This is stronger than "systems only in code".
- Scarcity and construction/logistics services have plausible bootstrap ownership. Resource scarcity is runtime-installed; construction/logistics is scene-owned and registry-backed.
- Pipe/pressure logistics code is materially serious: native arrays, flow vectors, rupture signals, room exchange, oxygen/water kinds, cadence tiers, and dump path exist.

Risk:

- The resource-node pickup path no longer has the specific static catalog/worldPrefab break candidate found in R14/R19. Current static recount is `0 / 27` missing primary-harvest `worldPrefab` and `0 / 27` non-catalog primary-harvest refs. This still is not runtime proof: hydration, pooling, interaction, inventory add, quest completion, and Addressables/direct fallback behavior were not run in Unity.
- The starter copper path is materially cleaner after R19: `ResourceNodeTemplate_CopperVein`, recipes/catalog, R18 provisioner starter material, and three checked barter offers now use cataloged raw `Resources/Raw/Data_Copper.asset`. The root duplicate `Data_Copper.asset` still exists and should be quarantined or removed only after Unity/editor validation.
- `BarterBootstrapAuthoring` now also loads raw cataloged copper, so the checked authoring rebuild path should not reintroduce root copper. This has not been validated by running the editor menu.
- `ItemCatalog` now falls back to direct serialized `ItemData.worldPrefab` when Addressables world-prefab entries are missing or fail. This is a small pickup-prefab escape hatch for the current missing `Assets/AddressableAssetsData` state, not proof that Addressables streaming is configured.
- PlayerInventory cannot accept arbitrary hashes; `TryAddItemWithStateInternal()` requires `TryGetRuntimeDescriptor()` from `ItemCatalog`. Any collectible whose hash is not present in the catalog is not inventory-addable by that path.
- `ItemAcquiredSignal` is not an inventory grant. Static scan found it consumed by radiation hazard logic, while first-hour resource quest completion listens to `InteractionEvents.ItemCollected` and runtime inventory state. Emitting `ItemAcquiredSignal` from ore/resource systems does not itself prove player progression.
- `ResourceDistributionDirector` and `ProceduralOreSpawner` are serious systems, but not statically proven live in the production scene. The active scene evidence mostly proves placed starter nodes, not the full procedural resource economy.
- `ResourceScarcityDirector` is runtime-created with default serialized `directiveResources = Array.Empty<DirectiveResourceDefinition>()`. That means the base scarcity counters/inflation can exist, but authored Atlas scarcity directives are not proven populated unless another authoring path fills them.
- `FluidPipeGraphRuntime` has strong code and black-box telemetry, but static evidence does not prove any active runtime pipe graph in world scenes. Treat pipe logistics as implemented subsystem code, not proven gameplay.
- Fabricator scene presence is not enough to prove successful crafting. It still depends on player inventory resources, recipe unlock state, output capacity, power/emergency lock state, and the resource pickup path above.

Decision:

- Classify gameplay economy as "real but currently fragile at the resource acquisition seam".
- The biggest immediate product risk is now runtime route proof, not the previously visible static ItemData/Catalog/worldPrefab hole. The route can still fail at scene wiring, pooling, pickup interaction, inventory add, quest signal, or save/load.
- Fix direction should stay data-first and narrow: replace reused low-tier pickup shells with dedicated per-resource pickup art after runtime route proof, not before. The current shells are acceptable static route closures, not final AAA presentation.
- Keep the R21/R23 editor validator strict for `ResourceNodeTemplate.harvestYield[*].item` / `rarityDrops[*].item` active catalog membership, `worldPrefab` availability, pickup prefab contract, duplicate `ItemData.PersistentId`, and catalog lookup ambiguity. The root duplicate `Data_Copper` is now a validator failure candidate, but the validator was not run in Unity.
- Keep the R26/R27/R28 quest/recipe validators strict for item/craft quest strings, prerequisite quest IDs, `RecipeData` integrity, craft-completion recipe-output routes, and scan-locked recipe unlock routes. Static clean string/result/unlock-warning resolution is not runtime proof of `InteractionEvents.ItemCollected`, `CraftingEvents.CraftCompleted`, scan log unlock, fabricator delivery, quest completion, PDA display, or save/load.
- Do not spend time hand-polishing pipe GameObjects before the resource acquisition chain is clean. Pipes are deeper vertical-slice content; copper/resource pickup is first-hour progression.
- Require one bounded runtime route later: mine/collect copper, observe `InteractionEvents.ItemCollected`, inventory contains `Data_Copper`, `quest_copper_sample` completes, craft `Copper Wire`, then save/load that state. Until that exists, gameplay loop readiness remains `PENDING VERIFICATION`.

## Tools / PDA / First-Hour Interface Addendum

Evidence type: `STATIC_SOURCE`, `FILESYSTEM`, `PREFAB_YAML`, `STATIC_SCENE_STRING`. No Unity, Play Mode, profiler, or dotnet verification was run for this addendum.

Static inventory:

- `Assets/_Project/Data/Items/Tools` contains `12` tool ItemData assets.
- `Assets/_Project/Prefabs/Tools/Held` contains `12` held tool prefabs.
- `Assets/_Project/Prefabs/Items/Tools` contains `12` tool world prefabs.
- `Assets/_Project/Data/Tools` contains `13` `ToolMetadata_*.asset` files. The extra metadata is `ToolMetadata_LogicSpanner.asset`.
- All `12` tool ItemData assets have non-null `worldPrefab` refs.
- `LogicSpannerTool.cs` and `ToolMetadata_LogicSpanner.asset` exist, but static scan found no `Item_Tool_LogicSpanner.asset`, no held prefab, no world prefab, no catalog ref, and no recipe ref.
- R24 static metadata reference recount: `12 / 13` `ToolMetadata_*.asset` files are referenced by held prefabs; `ToolMetadata_LogicSpanner.asset` has only the asset/meta self-reference route.
- `Player.prefab` owns `PlayerToolManager`, `PlayerPDA`, `ToolLoadoutProvisioner`, `ScanLogSystem`, `PDAExchangeSystem`, and `PlayerInteraction`.
- `Player.prefab` `ToolLoadoutProvisioner` remains present, but startup grants are now disabled: `provisionInventoryOnStart: 0`, `assignCoreLoadoutOnStart: 0`, and `provisionConstructionMaterialsOnStart: 0`.
- `Player.prefab` provisioner still carries four core quick-slot prefabs and `12` `allToolItems` for explicit editor/development validation.
- `Player.prefab` starter construction material now points at cataloged raw `Data_Copper` GUID `7a9f752461931354e865d30b319c0f35`, not the non-catalog root copper GUID `84877e24023afe648a6682f49f11defa`.
- `ToolLoadoutProvisioner` provisioning, construction-material grants, quick-slot assignment, and startup preset application now no-op outside `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `Player.prefab` `PlayerPDA` has `pdaPanel: {fileID: 0}`, `pdaCanvasGroup: {fileID: 0}`, and all eight `tabs` entries `{fileID: 0}`.
- Binary scene string scans found PDA tab component strings in `02_HECTON_WORLD.unity` and `03_HECTON_WORLD_CREST5.unity`.
- `DiegeticPDAController.cs` calls `PlayerPDA.ConfigureUI(...)`, but static scene/prefab scans did not find the `DiegeticPDAController` class string or MonoScript GUID `8f05da9f4a7a4158a04d6cc0e0f9d8c2` in `_Project` scenes/prefabs.
- `ProgressionRuntimeInstaller` boot-adds `HectonOSBootManager`; `PDARuntimeInstaller` boot-adds exploration/logbook/marker/intrusion systems. Neither installer adds `DiegeticPDAController`.
- R22 source guard: `PlayerPDA.Open()` now refuses to set `IsOpen` or switch input unless a PDA panel and at least one tab are configured. `ContentSanityValidator` now reports `PlayerPdaHeadlessOpenRisk` when `Player.prefab` has a `PlayerPDA` with no serialized panel and no `DiegeticPDAController` bridge.
- R24 validator hardening: `ContentSanityValidator` now loads held tool prefabs under `Assets/_Project/Prefabs/Tools/Held`, validates `PlayerTool.Metadata`, `PlayerTool.ToolData`, `ItemCategory.Tool`, `ItemCatalog` runtime descriptors, non-null tool `worldPrefab`, duplicate/empty `ToolMetadata.toolID`, and orphan active tool metadata. The validator summary now reports `ToolMetadata`, `ToolHeldPrefabs`, `ToolMetadataOrphans`, and `ToolRouteErrors`.
- R25 validator hardening: `ContentSanityValidator` now loads canonical `Player.prefab` `ToolLoadoutProvisioner` and reports `PlayerDevProvisionerStartupRisk` if `provisionInventoryOnStart`, `assignCoreLoadoutOnStart`, or `provisionConstructionMaterialsOnStart` is re-enabled.
- `Player.prefab` still contains multiple dev/smoke components with `runOnStart: 0`. That is lower risk than active provisioning, but it is production-prefab contamination.
- `WorldShippingContentFilter` suppresses trial/staging scene hierarchies such as `Tool_Staging`, `Fabrication_Trial`, and `Tool_TrialRange`; static source does not show it stripping player-attached `ToolLoadoutProvisioner` or smoke components.

Positive architecture:

- `PlayerToolManager.cs` is load-bearing, not filler: inventory-gated quick slots, GlobalRegistry input, object-pool equip/despawn, pool warmup, break/replacement handling, runtime context publication, battery siphon, and module-status handling are present.
- `PlayerTool.cs` and the equipment stack carry real metadata, durability, modular equipment, heat, battery, haptics, and operational summaries.
- `ModularEquipmentEngine` owns native arrays for tool state/stats/type ids/heat/battery/status masks and boot-creates a runtime instance.
- `EquipmentInteractionHandler` is boot-created by `GameBootstrapper`.
- `PlayerInteraction` on `Player.prefab` routes non-alloc look queries through dispatcher/query caches and checks `IInventoryPickupSource` before physical/interactable fallback.
- `HectonItem.TryHandleInventoryPickup(...)` and `PickupItem` raise `InteractionEvents.ItemCollected` and publish `ItemCollectedEvent`, which is the correct event family for first-hour/quest/resource progression.
- `ScannerTool.cs` is serious code: cooldown/radius, `WorldSpatialHashGrid.CollectContactsNonAlloc(...)`, scan result aggregation, resource-node/pickup/module/scannable discoveries, focused dispatcher raycast path, scan audio/feedback, and `GlobalSignals.ScannerToolActiveSignal`.
- `ScanEvents` has NativeQueue-backed pending and next-frame event lanes. `ScanLogSystem` is on `Player.prefab`, implements save/listener ownership, and registers through `GlobalRegistry.ScanLog`.
- `QuestStateManager` and `QuestGraphEvaluator` contain a real packed/native quest signal path for item/scan/craft/narrative events.

Risk:

- R18 removed the immediate hidden-startup-grant defect from `Player.prefab`; static source/prefab scan observed startup flags `0` and a release-build provisioning guard. Runtime route proof remains absent.
- Remaining provisioning risk is boundary hygiene, not current startup mutation: `ToolLoadoutProvisioner` is still serialized on the canonical player prefab, still carries direct tool/material references, and still allows explicit provisioning in editor/development builds. After R25, accidentally re-enabling its startup grant flags should become a content-validator error.
- Shipping cleanup does not strip player-attached `ToolLoadoutProvisioner`; it suppresses named scene hierarchies. That is acceptable after R18 for release mutation risk, but prefab dependency pollution remains.
- PDA backend and tab content exist, but the bridge is not proven. Before R22, `PlayerPDA.Open()` could switch to UI input, cursor, depth of field, sound, and events even when `pdaPanel`/tabs were null. R22 now fails closed instead of opening headless, but visible PDA UX is still not proven.
- Scene tab strings are positive evidence for authored PDA UI objects, but they do not prove that `PlayerPDA` is connected to them.
- `LogicSpanner` is partial content: source and metadata exist, but no player acquisition or prefab path was found. After R24 this is no longer only a manual audit finding; the active content validator should emit `ToolMetadataOrphans=1` / `ToolRouteErrors>=1` until the full route is authored or the metadata is quarantined.
- Dev/smoke components with `runOnStart: 0` are not hot by default, but they still pollute production-prefab verification and increase the chance of false positives in future smoke/readiness claims.

Decision:

- Classify the tool/scan/interaction stack as real, material architecture.
- Do not classify the first-hour route as proven from the static fix. R18 removes hidden dev startup grants, but runtime route proof is still absent.
- Do not call PDA absent; call PDA shell wiring `PENDING VERIFICATION`. The backend, tabs, and installers exist, but the `DiegeticPDAController` bridge placement was not proven statically.
- Next fix direction should be route proof, not GameObject polish: prove one clean route from empty-ish start -> acquire resource -> unlock/craft/equip scanner -> visible PDA/scan/log/quest update.
- Keep editor validators strict for player-prefab dev provisioning, tool metadata routes, recipe output/unlock routes, duplicate/cross-catalog starter materials, and low-tier shared pickup shells that still need high-tier per-resource presentation. R21 covers resource-node yield catalog/worldPrefab/contract gaps in `ContentSanityValidator`; R22 covers the headless `PlayerPDA` shell/bridge tripwire; R24 covers orphan active tool metadata and held tool item/catalog/world-prefab routes; R25 covers dev provisioning startup flag regression; R27 covers recipe result/ingredient/catalog/group integrity plus craft quest recipe-output routes; R28 covers scan-locked recipe routes as warnings until production unlock proof exists.

## AI / Fauna Data vs Runtime Wiring Addendum

Evidence type: STATIC_SOURCE / FILESYSTEM / STATIC_DOC. No Unity, Play Mode, profiler, GCMonitor, player build, or scene load was run.

Static inventory:

- `Assets/_Project/Data/AI/CreatureArchetypes` contains `22` recursive creature archetype `.asset` files.
- `Assets/_Project/Data/Fauna` contains `22` fauna data template `.asset` files.
- `Assets/_Project/Data/AI/FaunaBiomes` contains `108` fauna biome datasets.
- `Assets/_Project/Data/Biomes/FaunaFamilies` contains `13` fauna family profiles.
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs` contains `6` generated proxy prefabs: small passive, territorial, hunter, heavy hunter, leviathan, and drone.
- The `108` fauna biome datasets currently contain `432` `possibleCreatures` entries with non-null prefab references and `0` `possibleCreatures` prefab-null hits by static field scan.
- The same biome datasets contain `17` large-threat macro-zone archetype references and `17` `useLargeThreatMacroZone: 1` flags.
- `FaunaRuntimeSmokeTesterRunner.RunOmegaHeadlessSmoke()` exists and prints a `FAUNA_OMEGA_SMOKE_RESULT` JSON line on pass/fail when the runner reaches that point.

Positive architecture:

- The fauna data surface is real. It is not just concept prose: archetypes, fauna templates, biome spawn entries, family profiles, generated proxy prefabs, and large-threat macro-zone metadata exist on disk.
- `FaunaDirector` is substantial runtime code: registry-backed `IFaunaSim` service registration, adaptive density budgets, spawn ring and culling controls, biome/depth/zone weighting, spawn registry resolution, pool warmup, acoustic panic commands, resident data-only simulation, dispatcher registration, and late-frame completion.
- `WorldFaunaSpawnRegistry` is a real anchor registry: ordinary anchors, large-threat zones, runtime reef anchors, chunk/macro-zone buckets, procedural-state availability checks, and pooled anchor buckets.
- `EcosystemRuntimeInstaller` does create ecosystem-adjacent runtime managers: `FaunaGeneticsManager`, `EcosystemHealthDirector`, and `MigrationDirector`.
- `WorldRuntimeBootstrapAuthoring` can add/configure `WorldFaunaSpawnRegistry` and can wire an existing `FaunaDirector` to the chunk streaming profile, spawn registry, and procedural state registry.

Risk:

- Current static script-GUID search did not find serialized `FaunaDirector`, `WorldFaunaSpawnRegistry`, `FaunaRuntimeSmokeTester`, or `EcosystemRuntimeInstaller` hits in `Assets` scenes/prefabs/assets. This is not proof they are absent at runtime, but it is enough to reject any documentation claim that scene wiring is currently proven.
- `EcosystemRuntimeInstaller.EnsureRuntimeSystems()` does not instantiate `FaunaDirector` or `WorldFaunaSpawnRegistry`; it only creates genetics/health/migration managers.
- `GameBootstrapper.EnsureFaunaSimulationRegistered()` calls `FaunaDirector.InitializeService()` only if `FaunaDirector.ActiveRuntimeInstance` already exists. If no real fauna director registers `IFaunaSim`, bootstrap registers `DemiurgeFaunaSimulationService.Shared`, which reports ready but has `ResidentSlotCapacity = 0`.
- The headless fallback is useful for service-slot safety, but it can make `GlobalRegistry.FaunaSimulation.IsReady` true while no visible fauna director, spawn registry, resident slots, or active creatures are proven.
- `WorldRuntimeBootstrapAuthoring.ConfigureFaunaDirector()` returns immediately when no `FaunaDirector` exists. Therefore the editor tool can wire an existing director but does not prove one is present in production content.
- `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` is not usable PASS evidence. It reports `.codex-artifacts is not a valid directory name` and ends with Unity return code `1` without a visible `FAUNA_OMEGA_SMOKE_RESULT` PASS line.
- `Docs/AI_Fauna/AI_CREATURE_ROSTER_ENTERPRISE.md` remains encoding-damaged. Stable IDs and family links are useful pointers; prose in that file is not final writing or runtime truth.

Decision:

- Classify AI/Fauna as "real authored data and real runtime code, unproven visible production runtime".
- Keep `Docs/AI_Fauna` as an active coverage/reference pack, but never use it as scene-wiring, Play Mode, profiler, or spawn-readiness proof.
- Highest-value runtime proof later: load production world, prove active `FaunaDirector`, active `WorldFaunaSpawnRegistry`, nonzero biome entries loaded into runtime, nonzero resident slot capacity from the real `IFaunaSim`, at least one ordinary spawn and one large-threat macro-zone path, and a fresh `FAUNA_OMEGA_SMOKE_RESULT` PASS artifact.
- Low tier direction: use proxy-prefab and data-only fauna first; visible spawn caps and adaptive budget must shed density before frame time spikes. High/Ultra direction: spend saved cycles on richer visible ecology, longer large-threat residency, denser near-field schools, and stronger audio/biolum cues only after the real director path is proven.

## Save / Persistence Addendum

Evidence type: `STATIC_SOURCE`, `FILESYSTEM`, `STATIC_DOC`, plus DOC_AUDIT R29 Unity batchmode import/script-compilation evidence. No Play Mode, profiler, GCMonitor, Memory Profiler, player build, or save/load roundtrip was run for this addendum.

Static inventory:

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: 334 KB.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`: 263.6 KB / 5120 static lines.
- `Assets/_Project/Scripts/SaveManager.cs`: 131 KB.
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`: 110.4 KB.
- `Assets/_Project/Scripts/SaveDataMigration.cs`: 56.5 KB.
- `Assets/_Project/Scripts/SaveData.cs`: 53.1 KB.
- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs`: 37 KB / 838 static lines.
- `Assets/_Project/Scripts/SaveEvents.cs`: 24.8 KB.
- `Assets/_Project/Scripts/SaveSidecarStorage.cs`: 23.3 KB.
- `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs`: 22.1 KB / 559 static lines.
- `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs`: 19.8 KB / 481 static lines.
- `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs`: 19.6 KB.
- `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs`: 10.4 KB.
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity` contains an active, enabled `SaveManager` object using script GUID `ab6eaa84afd9c784ca6a618ab192b550`.
- `SaveManager` inspector values in bootstrap: manual backups 3, auto backups 2, quick backups 2, verbose logging off.
- `SaveSystemRuntimeSmokeTester`, `SaveRecoverySmokeTester`, and `Interaction/SaveStation` GUIDs were found only in their `.meta` files in the scoped `_Project` scan, not in text-serialized `_Project` scenes/prefabs/assets. Binary world scene contents remain unproven by text search.
- Static scan found 47 `ISaveable` class/interface match lines under `_Project/Scripts`; after obvious comparer/comment false positives, the project is already around the mid-40s for runtime save-owner classes.

Positive architecture:

- This is not a toy save system. It has a binary container, magic/version/checksum validation, header checksum version `0x0009`, minimum supported version `0x0003`, tokenized payloads, indexed block storage, mod payload sidecars, backup generations, migration, slot-name guards, and explicit load-candidate fallback.
- `SaveBinaryStorage` defines a 64 MB raw payload budget and a 68 MB compressed payload budget, with native LZ4 entry points and a managed Deflate fallback path.
- Windows native `liblz4.dll` exists at `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`.
- `SaveManager.SaveGameAsync()` blocks saves during floating-origin shifts, captures save thumbnails, snapshots registered `ISaveable` owners, routes full-save voxel deltas through a native snapshot path, stamps runtime world seed, captures persistent-world/ecosystem/quest snapshots, and moves binary write work to background-thread stages.
- `SaveManager.LoadGameAsync()` builds primary/backup candidates, migrates save data, validates runtime world seed, preloads tombstones, loads mod MMF payloads, stages packed quest state, applies saveables in load-priority order, restores voxel native deltas, and falls back from indexed persistent-world directory restore to loaded record arrays when needed.
- DOC_AUDIT R29 added a `SaveManager` async world-pager bridge for chunk page writes/reads/copy/retire/telemetry/flush. DOC_AUDIT R30/R31 corrected the overclaim: chunk dehydration is now bounded to at most `2` signals per tick and stages inventory shadow plus chunk metadata only. It no longer captures the entire global `VoxelDeltaProcessor` snapshot per dehydrated chunk.
- `H8BinaryWorldPager` now fail-closes on `IOException` / `UnauthorizedAccessException` while opening `world_data.h8bin`: it records an initialization fault and rejects pager IO instead of throwing through bootstrap. R30 also changes page-file sharing to `FileShare.Read` for single-writer semantics, adds a bounded worker-stop handshake before native disposal, releases invalid read results, and treats sparse/collided page headers as `Missing` rather than `Corrupt`. R31 removes pager initialization from `SaveManager.InitializeNativeBuffers()`, so the sidecar page file is opened only on first actual chunk page IO.
- DOC_AUDIT R32 splits `SaveManager` boot buffers from persistence working buffers: boot now keeps the save telemetry ring and tiny load-candidate scratch only, while the 64 MB raw payload, about 68 MB compressed payload, and 10 MB staging arenas allocate on first save/load/chunk-sidecar use. R33 tightens the fault path so a faulted/uninitialized pager does not allocate the 10 MB staging arena, and load first-use allocation sits inside the normal failure/cleanup envelope. R36 re-removed a concurrent regression where chunk dehydration captured the global voxel snapshot as `worldPagerVoxelDeltaSnapshot`; R37 rechecked the latest churn, restored a joinable pager worker thread, and locally compiled `Hecton8.Core.Memory` plus `Hecton8.Core` through Unity Bee/Roslyn temp response files with exit code `0`. R38 hardens unexpected pager worker command faults so dequeued pending counters decrement in `finally`, records current WFC outpost MacroDB bitmask persist/restore contract coverage in `SaveManager`, and demoted the old full-Core success as stale under then-current churn; R43 later superseded that compile-blocked note with a clean external root `Hecton8*.csproj` no-restore CLI recheck.
- DOC_AUDIT R39 identified the first blocker for external Core builds as generated-project drift: `Hecton8.Core.asmdef` references `23` first-party assemblies that were absent from generated `Hecton8.Core.csproj`. `HectonComplianceValidator` now has an editor-only `CSPROJ001` tripwire for this exact mismatch. R40 attempted a non-destructive Unity batchmode project refresh, found the root generated projects still stale, and added a source-backed `Directory.Build.targets` bridge instead of editing generated `.csproj` files. R41 serially rechecked the root `Hecton8*.csproj` compile surface after restoring missing MSBuild assets; R43 rechecked the same root surface under current churn. Final single-project no-restore CLI builds now pass for `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, `Hecton8.World.Contracts.csproj`, `Hecton8.World.Dots.csproj`, `Hecton8.Bootstrap.Contracts.csproj`, `Hecton8.Input.Generated.csproj`, and `Hecton8.Input.csproj` at `0 Warning(s)` / `0 Error(s)` with `LASTEXITCODE=0`. Fresh no-restore attempts can still fail before source compilation on missing `Temp\obj` restore assets, missing referenced `Temp\bin\Debug` DLLs, or shared `Temp\obj` locks; restore/build plus build-server cleanup clears those evidence hazards. Full restore graphs still emit vendor/package warnings from URP/GPUInstancer/Crest/ShaderGraph and MapMagic/Den.Tools. This is external compile evidence only, not Unity Console, Play Mode, profiler, GCMonitor, player build, or scene wiring proof.
- `PersistentWorldRegistry` is a real persistence authority, not a thin list dump. It owns save snapshots, tombstone preload, indexed-sector restore, paging disable/fallback, sector override temp files, and corruption/quarantine surfaces.
- `ISaveable` documents ownership rules: each owner writes only its own DTO section, validates on load, and should avoid new array allocation in the contract.
- PlayMode and smoke surfaces exist: `SmokeTests_SaveLoad.cs`, `InquisitionStabilityPlayModeTests.cs`, `SavePersistenceOmegaSmokeTester`, `SaveRecoverySmokeTester`, and `SaveSystemRuntimeSmokeTester`.

Risk:

- Bootstrap `SaveManager.Awake()` calls `InitializeNativeBuffers()`, but after R32 that method initializes only the save telemetry ring and load-candidate scratch. The large raw/compressed/staging save buffers are now first-use allocations, not boot allocations.
- The remaining risk moved from boot residency to first-use latency, after-first-use residency, and runtime behavior under real IO/memory faults. On the low-end target class this still needs a Memory Profiler capture and a save/load timing artifact.
- The normal primary promotion path rotates backups, then uses `File.Move(temp, final)` for the primary `.sav`. There is temp verification and a backup chain, so it is not naive, but the normal commit path is not the same as an atomic `File.Replace` primary swap.
- `File.Replace` exists in critical backup/self-repair paths, which means the code already knows the safer primitive. The inconsistency needs a deliberate policy, not accidental drift.
- The release-build behavior for save registry overflow is weak. Capacity is 256; editor/development logs the first capacity error, while non-development builds return without registering the extra saveable.
- Save ownership is scattered across many domains. `SaveManager` is central, but long-lived state also exists in `SaveSidecarStorage`, `UserOptionsPersistence` (`options.h8cfg`), `Core/RebindingManager` (`controls.json`), `GlobalProfileManager` (`profile.json`), `ModWorldPersistenceManager`, mod payload stores, and third-party package surfaces.
- `SaveData` remains a managed DTO graph with lists/strings/arrays. That is acceptable for cold save/load work, but this system should not be described as zero-GC in the literal sense.
- If native LZ4 is unavailable on a target, the managed Deflate fallback is correctness-friendly but has different CPU/GC behavior that needs platform proof.
- Current `.codex-artifacts/unity-save-persistence-omega-smoke-2026-05-05.log` only shows Unity startup/licensing and stops at `Library Redirect Path: Library/`; it does not contain a visible PASS/FAIL save result. Treat it as stale/incomplete, not proof.
- Fresh DOC_AUDIT R29 Unity Console evidence before the final batch run showed a real bootstrap fault from `world_data.h8bin` sharing violation. The source now degrades that case to a disabled pager, but no Play Mode rerun proved bootstrap recovery in a live scene.
- R30/R31 source review found the async voxel chunk-paging route is not ready for correctness claims: residency prefetch can retire pager tickets, but there is still no chunk-local voxel payload apply path. `WorldChunkResidencyManager.RequestLoad()` no longer starts orphaned `VoxelDeltaRle` prefetch from static source, chunk dehydration no longer writes global voxel snapshots, and chunk-local voxel persistence remains `PENDING DESIGN/PROOF`.
- Save-station runtime access is not proven from text-serialized `_Project` scenes/prefabs. The class exists, but static YAML evidence did not prove a placed station in active content.
- The indexed persistent-world path is load-bearing and complex. Static review cannot prove restore order, sector quarantine, override temp cleanup, AUP math, or backup promotion under real corrupted-sector conditions.

Decision:

- Classify persistence as one of the more serious subsystems in the project, but still `PENDING VERIFICATION`.
- Do not refactor this blindly; the code is load-bearing and has real recovery logic.
- Require one fresh artifact-backed save/load test that covers: clean save, clean load, backup recovery, corrupted indexed sector, missing/corrupt mod payload, migration path, and memory capture before/after `SaveManager.Awake()`.
- Create a persistence authority ledger. Minimum columns: file/artifact, owning system, path under `Application.persistentDataPath`, temp/backup policy, atomicity primitive, load order, and failure behavior.
- Re-evaluate the post-first-use allocation policy. R32 removes the large boot allocation, but low tier may still need release-after-use, pooled arena reuse, or tier-based reduced staging budget if Memory Profiler/save-load timing proves the first-use/resident cost too high.
- Do not claim shipping-grade persistence until the current dirty workspace has a fresh Play Mode/player artifact with PASS lines and a captured save directory diff.
- Treat `Library/Codex_DOC_AUDIT_UnityBatchCompile.log` as dated report text unless the artifact is restored or replaced; the R10 filesystem check did not find it. Earlier DOC_AUDIT text said it contained no `error CS`, no bootstrap dependency exception, no `BIOS ERROR`, and ended in successful batchmode exit, but that statement is not current filesystem proof. Later read-only Unity MCP Console readback first returned `0` errors and `7` non-C# warnings, then final readback returned `0` log entries. This does not prove runtime save/load behavior.

## World Streaming / HLOD PDA Upload Addendum

Evidence type: `STATIC_SOURCE`. No Unity import, Play Mode, PDA route, profiler, GCMonitor, Frame Debugger, or player build was run for this addendum.

Current source facts:

- `WorldChunkResidencyManager` owns HLOD active impostor SOA and exposes the read model through `IStreamingBackpressureService`.
- `PDAMapTab` consumes active HLOD impostor points into a fixed `16 x float4` GPU buffer for the sonar/cartography overlay.
- Before R35, `TryResolveHlodImpostorAupBuffer()` uploaded that fixed buffer on every map build whenever active HLOD points existed, even if the streaming point data had not changed.
- R35 adds `IStreamingBackpressureService.ActiveImpostorVersion` and backs it with a separate `_activeImpostorPointVersion` in `WorldChunkResidencyManager`.
- `PDAMapTab` now caches uploaded HLOD version/count, clamps count to the native point array length, clears trailing fixed slots only when data changes, and skips unchanged HLOD POI uploads.
- Renderer matrix dirty state remains separate from PDA point dirty state, so PDA fade progress can advance without forcing HLOD matrix re-upload.

Decision:

- Treat this as a small but real bandwidth-discipline fix.
- Do not claim measured savings until Frame Debugger/profiler proof captures PDA map builds with active HLOD points.

## Third-Party Contamination Finding

Forbidden or replaced packages are present in `Assets`:

- `Assets/AstarPathfindingProject`
- `Assets/Plugins/Easy Save 3`
- `Assets/Plugins/Demigiant/DOTween`
- `Assets/Plugins/DarkTonic/MasterAudio`
- `Assets/Eazy Sound Manager`

First-party code scan found little direct production usage:

- Astar appears mostly as `useAstarPathing`, and current archetype assets scanned had `useAstarPathing: 0`.
- DOTween/EasySave/MasterAudio direct first-party code usage was not found in active runtime code in the scoped static scan, except settings/assets/editor references.

Risk:

- Package presence still expands import/compile/project surface and can mislead future agents into using forbidden tools.

Decision:

- Treat them as contamination until stripped, isolated, or explicitly quarantined by docs/build guards.

## Test / Verification Finding

Static inventory:

- `Assets/_Project/Tests`: 10 C# test files.
- Many smoke testers, validators, profilers, watchdogs, and QA utilities exist under `Assets/_Project/Scripts`.

Risk:

- Tools exist, but current docs do not provide fresh Play Mode, profiler, GCMonitor, player-build, memory, or visual proof for the current dirty workspace.
- Smoke-test classes are not equivalent to artifact-backed test runs.

Decision:

- Status remains PENDING VERIFICATION until fresh artifact-backed runtime captures exist.

## Documentation / Truth Stability Finding

Current docs repeatedly state that May 11 compile artifacts are absent from the current filesystem and that Unity/profiler/runtime proof is missing.

Risk:

- Many agents are concurrently mutating authority docs, mandates, source, and task logs.
- Agent logs are temporary and should not be the sole place where project-state conclusions live.

Decision:

- This file is the durable anchor for the static x-ray findings.
- If future evidence contradicts this file, update this file with a dated addendum and artifact path, not a chat-only claim.

## Highest-Value Next Audit Path

Priority order:

1. Scatter + world streaming runtime: `WorldProceduralScatterDirector`, `WorldChunkResidencyManager`, `WorldProceduralFieldSampler`, `HectonMapMagicVegetationBridge`, `PersistentWorldRegistry`.
2. Boot + asset lifecycle: `GameBootstrapper`, `SceneRuntimeService`, `AssetLifecycleGovernor`, Addressables groups/settings.
3. Audio memory/imports: large ambient WAV import settings, active references, mixer/runtime loading path.
4. Scene memory/static content: `02_HECTON_WORLD`, Player prefab, active cameras/volumes/HUD/audio services.
5. Runtime proof plan: one constrained Play Mode route, profiler markers, GCMonitor, memory snapshot, and player build on a known branch.

## Product State Classification

Current label:

Infrastructure-heavy AA pre-alpha / vertical-slice candidate.

Not acceptable labels:

- not "production-ready"
- not "ship-ready"
- not "verified master grade"
- not "still a puddle"

The project has a real spine. It still needs hard runtime proof.
