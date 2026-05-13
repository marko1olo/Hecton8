# LOG_UNASSIGNED_AUDIT

Date: 2026-05-13
Status: STATIC PROJECT STATE ASSESSMENT

What was wrong:
- The project state cannot be judged from build errors or old green logs.
- Current workspace is heavily dirty and under concurrent agent mutation.
- Stable docs repeatedly state that Unity Play Mode, profiler, GCMonitor, player build, scene wiring, memory, and visual proof are absent.
- Architecture is real but bloated: very large owner files, huge GlobalRegistry surface, many active status/log files, and broad system count.

What was done:
- Read authority docs and selected mandates.
- Inspected current evidence boundaries and active architecture map.
- Scanned current source count, scenes, prefabs, materials, shaders, images, status/log volume, and selected core systems.
- Did not run dotnet after user instruction.

Cinematic Cheats used:
- Assessment only. No runtime or visual cheat implemented.

Exact Microseconds saved:
- 0 us measured. No code changes and no profiler run.

Objective conclusion:
- The project is no longer a puddle. It is now a large infrastructure-heavy AA prototype with a real runtime spine, real scene/content footprint, and real technical doctrine.
- It is not yet a proven game. Runtime proof is missing; playable vertical-slice quality is unproven.
- Current risk is not "can it compile"; current risk is whether the system mass can collapse into a controllable, shippable player experience without drowning in agent-generated infrastructure.

Large-file x-ray addendum:
- Most large files are important. They are not dead filler.
- The bad pattern is not empty bloat; it is load-bearing code fused into god objects.
- Highest pileup risk: HectonPlayerMovement, HectonUnderwaterVisuals, HectonMapMagicVegetationBridge, SuitHUDV4CanvasOverlay, SargassumMicroFaunaBoids, FloraInteractionManager, FaunaBrain, BaseModule, GameBootstrapper, GlobalRegistry.
- More justified large technical kernels: SaveBinaryStorage, HectonVoxelEngine, PlayerCriticalProceduralAudioRenderer, WorldProceduralFieldSampler, PersistentWorldRegistry, SubmarineFluidDynamics, HectonFluidEngine, SubmarineAtmosphereSystem, PredatorCognitionDomain.
- Editor bootstrap authoring files are large but not runtime pileups.

HectonPlayerMovement x-ray addendum:
- The file is not 700 KB of walking. It is a fused player integration hub: locomotion, water state, environmental drag, sargassum/cable/parasite influence, crush/thermal/sonar reactions, transport/tow inheritance, inventory encumbrance, stamina/narcosis multipliers, VR comfort, wet lens, FOV, footstep/audio, AUP/no-clip recovery, origin shift, telemetry, and editor validation.
- Important load-bearing code exists: dispatcher Tick/FixedTick path, motor force queue, collision probe cache, water/voxel failsafes, black-box telemetry, render interpolation, origin-shift handling, and transport inheritance are not safe to delete blindly.
- Main defect is ownership. Domain-specific gameplay hazards and presentation feedback are embedded directly in the movement owner instead of arriving through narrow force/status interfaces and subscriber systems.
- Exact Microseconds saved: 0 us measured. Static x-ray only; no build, profiler, or runtime mutation.

Broader project audit addendum:
- Runtime spine is real. Static scan found only SystemDispatcher owning Unity Update/LateUpdate in first-party scripts; gameplay mostly routes through registry tick lanes.
- The system is infrastructure-heavy: GlobalRegistry exposes roughly 150 service slots and GlobalSignals owns dozens of NativeQueue lanes plus typed SignalBus lanes. This is functional architecture, but it is also a large global coupling surface.
- Scatter remains the strategic runtime bottleneck. `WorldProceduralScatterDirector` is still a 500 KB partial owner; docs correctly mark DOTS/live ownership as prototype or shadow-only until profiler parity exists.
- Addressables package is installed and runtime code uses Addressables, but no `AddressableAssetsData` directory was found in the project tree. Streaming code may exist ahead of asset-pipeline proof.
- Large audio risk exists: multiple 22-25 MB ambient WAVs use import settings equivalent to preloaded/decompressed ambience paths; only `Underwater Ambient.wav` was found referenced by Player prefab in the static GUID pass.
- Forbidden/legacy third-party packages are present in Assets: AstarPathfindingProject, Easy Save 3, DOTween, MasterAudio, Eazy Sound Manager. First-party code mostly does not call them, but they still contaminate import/compile/project surface.
- Tests are thin relative to system size: 10 test C# files under `Assets/_Project/Tests`; many smoke testers and validators exist under `Scripts`, but current docs do not provide fresh Play Mode/profiler/GC/player-build proof.
- Worktree is not stable: static `git status --short` count was 365 entries, including modified authority docs and mandates. That makes current truth branch-local and volatile.
- Exact Microseconds saved: 0 us measured. Static audit only.

Durable-doc promotion addendum:
- User warned that AgentLogs are temporary and may be deleted.
- Findings were promoted into `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- `Docs/README.md` now links `PROJECT_STATE_STATIC_XRAY.md` from both the stable authority spine and current evidence snapshot sections.
- Exact Microseconds saved: 0 us measured. Documentation-only preservation.

Boot/streaming wiring addendum:
- `00_BOOTSTRAP.unity` serializes `BootstrapController`, not direct `GameBootstrapper`. `BootstrapController` deliberately delegates to `GameBootstrapper.EnsureRuntimeInstance(...).BeginBootstrap()`, so absence of a direct serialized `GameBootstrapper` component is not by itself a fault.
- Direct `WorldChunkResidencyManager` serialized wiring was not found in `_Project` scenes/prefabs/assets by script GUID. The code is substantial, but runtime ownership/wiring remains unproven by static evidence.
- `WorldChunkStreamingProfile.asset` exists and contains concrete 15000m world / 192m chunk / 180-420-900-1800m radius data plus layer budgets.
- Static GUID search did not prove profile assignment into runtime scene directors; editor authoring/validator code appears responsible for wiring `chunkStreamingProfile`.
- `ItemCatalog.asset` exists. Hardcoded fallback GUIDs in `ItemCatalog.cs` resolve to real tool world prefabs.
- No `AddressableAssetsData` directory was found. Addressables runtime calls remain pipeline-PENDING until groups/settings/labels/release ledger/memory evidence exist.
- `02_HECTON_WORLD.unity` and `03_HECTON_WORLD_CREST5.unity` are ~32 MB and produce binary-like search output, so plain text search is partial evidence only.
- Exact Microseconds saved: 0 us measured. Static audit only.

Audio memory/import addendum:
- `Assets/_Project/Audio` contains 45 WAV files totaling ~291.9 MB and 89 OGG files totaling ~171.57 MB.
- `Underwater Ambient.wav` is 32.47 MB and is wired into `Player.prefab` on an enabled looping play-on-awake AudioSource.
- Ten root-level `Atmos *.wav` / `Atmos * Loop.wav` files total ~233.6 MB source and currently show loadType 0 / compression 1 / preload 1 / stereo / 44100 in static meta reads.
- Those root-level `Atmos` files are not under the managed ambient/SFX roots used by `HectonAudioPostprocessor`; scoped GUID search found no serialized `_Project` references, so they are likely unmanaged asset debt unless loaded indirectly.
- `Music for Game` currently looks memory-safer: 84 files, ~171.51 MB source, loadType 2, compression 1, preload 0, loadInBackground 1, 44100.
- `MusicDirectorConfig_Global.asset` references ten music profiles with ~150 direct AudioClip references and is serialized in `01_MAIN_MENU.unity`.
- `HectonAudioPostprocessor` currently targets CompressedInMemory for managed ambient, which conflicts with the current streaming import state of music. Blind reimport can regress memory behavior.
- Exact Microseconds saved: 0 us measured. Static audit only.

Render/scene memory addendum:
- Current quality index is 0: `Surface (Medium)` using `URP_Medium (PC_RPAsset).asset`; `Abyss (Low)` and `Orbit (High)` exist as separate URP tiers.
- All scanned quality levels use streaming mipmaps, `asyncUploadTimeSlice: 2`, `asyncUploadBufferSize: 16`, and persistent upload buffer.
- `URP_Low` is not a hard-minimal fallback: depth texture, opaque texture, HDR, 0.85 render scale, shadows, additional lights, reflections, soft shadows, and 30m shadow distance are still enabled.
- `PC_Renderer`, `PC_High_Renderer`, and `Mobile_Renderer` all keep multiple active custom visual features. Low/mobile still carries SSDO/shafts/fog/post/brownout/soot-style visual cost.
- Active feature scripts show `RecordRenderGraph`, but also use `AddUnsafePass` and `Blitter.BlitCameraTexture`; this needs Frame Debugger / RenderGraph proof before any render-hotpath claim.
- `_Project` image source inventory is about 357.51 MB PNG plus 100.72 MB JPG. 42 textures over 5 MB total about 296.36 MB source; 33 of those total about 232.91 MB source and currently show `streamingMipmaps: 0`.
- `Player.prefab` is about 169 KB / 5601 lines and statically contains 43 MonoBehaviours, 4 cameras, 3 audio sources, 2 lights, and looping play-on-awake underwater ambience.
- Static script-GUID search did not prove scene/prefab wiring for several major world systems, but large binary-like scene files make this partial evidence only.
- Exact Microseconds saved: 0 us measured. Static audit only.

Dev smoke harness contamination addendum:
- Scoped scene/prefab/asset scan found smoke-test serialization only in `Player.prefab` and `00_BOOTSTRAP.unity`.
- `Player.prefab` serializes eight enabled runtime smoke tester components: Tool, Builder, UI, Scan, FieldTool, Barter, ToolTrialRange, and Fabrication.
- All eight Player smoke testers serialize `runOnStart: 0`.
- `00_BOOTSTRAP.unity` serializes `ShellVerificationSmokeTester_SCENE_TEMP`, enabled, with `runOnStart: 0`.
- The scoped scan found no `runOnStart: 1` under `_Project` scenes/prefabs/assets.
- Guard quality is mixed: UI/Scan/ToolTrialRange are mostly `UNITY_EDITOR || DEVELOPMENT_BUILD`; Shell has release auto-start disabled; Tool/Builder/FieldTool/Barter/Fabrication are not fully development-gated and compile runtime smoke code into release assemblies.
- `ToolRuntimeSmokeTester` on `Player.prefab` hard-references 12 held tool prefabs. `FieldToolRuntimeSmokeTester` hard-references a tool item asset.
- `PerformanceHotPathValidator` excludes SmokeTester/RuntimeSmoke/FabricationRuntimeSmokeTester files, so canonical prefab smoke components bypass the normal static hot-path validator.
- Exact Microseconds saved: 0 us measured. Static audit only.

Build scene serialization/debug overlay addendum:
- Enabled build scenes are `00_BOOTSTRAP.unity`, `01_MAIN_MENU.unity`, and `02_HECTON_WORLD.unity`.
- `00_BOOTSTRAP.unity` and `01_MAIN_MENU.unity` are YAML. `02_HECTON_WORLD.unity` is binary/non-YAML and about 32.2 MB.
- Additional non-build binary scenes exist: `03_HECTON_WORLD_CREST5.unity` about 32.1 MB and `GeminiSandbox.unity` about 9.17 MB.
- `EditorSettings.asset` reports serialization mode 2, but the core build world scene is still binary on disk.
- YAML counts: bootstrap has 10 GameObjects / 7 MonoBehaviours; main menu has 202 GameObjects / 305 MonoBehaviours / 1 camera / 1 light.
- Text/YAML scene counts and reliable GUID scans are unavailable for `02_HECTON_WORLD.unity`; previous static scene-wiring gaps are therefore partial evidence, not absence proof.
- `00_BOOTSTRAP.unity` includes active `SubnauticaSystemsDebugUI_Root` with enabled `Hecton8.UI.SubnauticaSystemsDebugUI`.
- `SubnauticaSystemsDebugUI` source auto-creates `SubnauticaSystemsDebugUI_Auto` in `02_HECTON_WORLD` via `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` without a development-build guard around that auto-create path.
- The overlay registers as UI updatable/slow-tickable and creates runtime Canvas/TextMeshPro debug UI objects with a 0.2s refresh interval.
- Exact Microseconds saved: 0 us measured. Static audit only.

Runtime auto-init surface addendum:
- Runtime-script scan excluding `Scripts/Editor` found 267 `RuntimeInitializeOnLoadMethod` lines across 224 files.
- RuntimeInitializeLoadType token counts: 239 `SubsystemRegistration`, 18 `AfterSceneLoad`, 7 `BeforeSceneLoad`, 3 `AfterAssembliesLoaded`.
- Heavy `SubsystemRegistration` usage is a real strength for domain-reload-disabled reset discipline.
- Non-subsystem hooks still form a hidden bootstrap surface: some install hooks, create GameObjects/components, or mutate quality/URP settings outside static scene wiring.
- `ModLoader` runs at `BeforeSceneLoad`, installs save/event/bootstrap/application/scene hooks, scans `Application.dataPath` parent + `Mods`, reads `mod.json`, and registers content/localization/bundle paths. Managed assembly reflection loading is disabled in the inspected path; no repo `mod.json` was found.
- Runtime fail-safe creators include `WorldReadabilityRuntimeBootstrap`, `RelayHUDRuntimeBootstrap`, `SubtitleManager`, `SuitHUDV4CanvasOverlay`, `HectonSubmarineOS`, `PlayerStressMetricsRuntime`, and `HectonMusicDirector`.
- QA/dev surfaces include command/env/file-gated `QAEnduranceWatchdogBot` and development-build-only `DodReplayRecorder`.
- `PlatformBatteryWatchdog` calls `QualitySettings.SetQualityLevel(0, true)` on critical battery, but current quality index 0 is `Surface (Medium)` and `Abyss (Low)` is index 1.
- `HectonUrpShadowBudgetGuard` mutates active URP shadow settings at runtime, including forcing shadow distance to 40m, so static URP asset fields are not full runtime truth.
- Exact Microseconds saved: 0 us measured. Static audit only.

Modding boundary/internal event coupling addendum:
- First-party runtime code outside `ModdingAPI` and `Editor` contains 41 direct `HectonEventBus.Publish/Subscribe` call sites: 16 publish, 25 subscribe.
- This means `HectonEventBus` is not just optional mod support; it is part of first-party gameplay/meta/PDA/progression wiring.
- Positive: `HectonEventBus` has depth cap, cascade reporting, exception isolation, stall watchdog, subscriber disable, and managed allocation tracking. `ModCommandDispatcher` uses persistent/prewarmed NativeQueue lanes, NativeHashMap lookups, quotas, AUP rebasing, and explicit capacities.
- Risk: `SystemDispatcher` always drains mod command/registry lanes in late frame, and `ModLoader` always runs `BeforeSceneLoad` to install hooks and scan external `Mods` content when present.
- Risk: managed callback safety uses `List<SubscriptionEntry>`, `Stopwatch`, `GC.GetAllocatedBytesForCurrentThread`, try/catch, and optional mod-scope checks. That is robust, not free.
- Decision: keep the layer, but classify it as first-party event spine with mod projection. Cold/meta hooks are plausible; hot gameplay needs NativeQueue/static buses or profiler proof.
- Exact Microseconds saved: 0 us measured. Static audit only.

Black box / crash forensics addendum:
- `CrashTelemetryBuffer` owns a 1024-entry `NativeArray<TelemetryEntry>` ring, 1000-entry export snapshot, 64-byte entries, and fixed 64016-byte export scratch.
- It records many central fault classes: physics NaN recovery, memory pressure/spikes, physiology NaN, bus/signal congestion, origin shift, kinetic anomaly, late-frame shedding, performance spikes, latency crimes, native memory faults, audio stats, bootstrap safe halt, runtime watchdog stall, AUP jitter, and Unity log faults.
- Static scan found 48 runtime C# files with `Dump_*.bin` paths and 50 files matching 300-frame telemetry/blackbox capacity patterns.
- Positive: black-box culture is real; many critical domains have native rings and binary dump paths.
- Risk: central crash telemetry writes `BLACKBOX_CRASH.h8dump` and `runtime_telemetry.bin` to `Application.persistentDataPath`, while many domain systems write to `Docs/AgentLogs/Dump_*.bin`.
- Risk: `Docs/AgentLogs` is temporary operational memory by user policy, yet many crash artifacts target it.
- Risk: `Gameplay/DataArchaeologyRuntime.cs` uses `Application.dataPath + "../../Docs/AgentLogs/Dump_DATA_ARCHAEOLOGY.bin"`, which likely resolves outside the Unity project root.
- Current filesystem scan found no `Dump_*.bin` or `crash_*.h8dump` artifacts under `Docs/AgentLogs`; no runtime crash/export/readback was executed.
- Decision: unify dump path policy and prove one controlled dump/readback before calling crash forensics production-ready.
- Exact Microseconds saved: 0 us measured. Static audit only.

Assembly/domain boundary addendum:
- `Assets` contains 72 asmdefs; `_Project` contains 24; `_Project/Scripts` contains 21.
- Nearest-asmdef source count shows the real runtime mass is still `Hecton8.Core`: about 1111 non-editor C# files in one assembly.
- `Hecton8.Core` directly references UI/TMP, URP/Core RP, Addressables/ResourceManager, InputSystem, Burst/Collections/Mathematics, GPUInstancer, and VolumetricLightBeam.
- `Hecton8.Plugins` isolates MapMagic/Crest better, but it still references `Hecton8.Core`.
- `Hecton8.World.Dots` is non-auto-referenced and define-gated, which is the correct shape for optional DOTS.
- `Hecton8.QA` is runtime auto-referenced, so QA code is compile-surface unless stripped by build symbols/asmdef changes.
- Decision: project has asmdef islands, not full domain isolation. Stop adding dependencies to `Hecton8.Core`; extract stable leaf contracts only when measured need exists.
- Exact Microseconds saved: 0 us measured. Static audit only.

Asset loading / data residency addendum:
- `Assets/AddressableAssetsData` is absent in the current tree.
- `Assets/StreamingAssets` is absent in the current tree, so `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent too.
- `GameBootstrapper.InitializeBootstrapDataMonolith()` accepts `H8DataBlobLoadStatus.Missing`, so the boot path can proceed without the intended static-data monolith.
- `H8StaticDataArena` is still serious code: 256 MB cap, validation, persistent native arena, write lock, and boot-only file staging before native blit.
- `AsyncLoadHelper` is intentionally disabled: `Instance => null`, null/failure callbacks, and an editor error saying runtime Resources/Addressables loading through that helper is unsupported.
- Scoped first-party runtime scan found 0 non-editor `_Project/Scripts` files with direct `Resources.Load`.
- Whole-Assets runtime scan found 30 C# files with `Resources.Load`, mostly third-party packages: Crest, GPUInstancer, MapMagic, Dynamic Decals, Shapes, VolumetricLightBeam, Easy Save 3, MasterAudio, Feel/NiceVibrations, Astar helpers, and Mantis LOD.
- `Assets` has 40 `Resources` directories totaling about 13.12 MB source. First-party Resources are small; the larger buckets are mostly third-party.
- First-party runtime files outside `Editor` folders contain 117 `AssetDatabase.LoadAssetAtPath` call lines across 69 files. Likely editor-guarded, but this is not player-build proof.
- Addressable discipline exists in code: `GameBootstrapper`, `ItemCatalog`, `WorldChunkResidencyManager`, and `AssetLifecycleGovernor` load/release/clear dependency handles.
- `ModAssetManager` adds another content authority via cold-path `AssetBundle.LoadFromFile`.
- Decision: do not claim streaming readiness until Addressables settings/groups and static data blob exist or the fallback mode is explicitly documented. Keep Resources tombstoned. Build one asset residency ledger.
- Exact Microseconds saved: 0 us measured. Static audit only.
