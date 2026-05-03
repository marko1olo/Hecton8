# Reports

Date: `2026-05-03`
Status: `PENDING VERIFICATION`

Purpose: canonical drop zone for new reports, audits, and validation writeups that are still active.

## Naming

- single-file report: `YYYY-MM-DD_TaskName.md`
- multi-file report bundle: `YYYY-MM-DD_TaskName/`

## Rule

- do not create new report files in repo root
- do not drop one-shot reports loose in `Docs/`
- when a report is older than `5` days and no longer drives current work, archive it

## Current High-Authority Reports

- `2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md`
- `2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`
- `2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
- `2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`
- `2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `2026-05-01_CURRENT_PROJECT_STATE.md`
- `2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`
- `2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
- `2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`
- `2026-05-01_EVENT_CASCADE_RECHECK.md`
- `2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md`
- `TOTAL_CODEBASE_AUDIT_V2.md`
- `OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `DOOMSDAY_FLAW_REPORT.md`

`2026-05-01_CURRENT_PROJECT_STATE.md` is the current conceptual entry point.
It keeps its stable path but now includes May 3 evidence. It does not replace source files or runtime verification logs; it defines which active reports should be read first.

`2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` is the latest foundation-hardening addendum.
It records removal of a forced PhysX-bake completion path in `HectonWorldGenerator`, async mesh-build watchdogs in `ProceduralWreckGenerator`, the reduced floating-origin watchdog, `UserOptionsPersistence` ownership via bootstrap/GlobalRegistry, the `BaseAirlock` dispatcher-registration retry plus safe-teleport and pre-cycle spawn-validation hardening, `BaseAirlockEvents` NativeQueue runtime lane, `SealedDoor` empty-dispatcher-registration removal and progress-cadence coalescing, `LoreDatabaseManager` save-registration ownership/edit-mode guard hardening, `HabitatGraphManager` traversal-scratch separation and native-disposal defaulting, bounded organic/drop durability drains, dispatcher-raycast sidecar tail clearing, tether visual `.Run()` removal, `PlayerInventory` inline-kernel `.Run()` removal, `CraftingSystem` inline-kernel `.Run()` removal, `QuestStateManager` direct-kernel `.Run()` removal, `SargassumMicroFaunaBoids` one-element prime `.Run()` removal, `FaunaSimulationEngine` hibernation catch-up `.Run()` removal, `FloraInteractionManager` scheduled cascade phase seed publication, fallback beacon material/despawn ownership, interaction/fabricator cold-allocation comment compliance, `SubtitleManager` singleton-removal compile drift, the generated Entities-package warning/error gate, stale `Hecton8.Editor.csproj` audit-helper includes, and generated-output pruning for PlayMode test compile recovery. Evidence includes latest Unity batchmode success with strict C# failure scan `0`, latest local Core build `0 Error(s)` / `0 Warning(s)`, latest local Editor/Input/optional DOTS builds `0 Error(s)` / `0 Warning(s)`, scoped PlayMode test compile `0 Error(s)` / `0 Warning(s)`, and Unity EditMode spatial-hash self-test `3/3` passed. Earlier full verbose builds showed third-party/package warnings from URP, GPUInstancer, Crest, Den.Tools, WaveHarmonic.Crest, and ShaderGraph. Unity batch also logged a licensing access-token update error. Local `dotnet build` evidence must be serial because shared `Temp\obj` can create false `CS2012` locks under parallel builds. It is not Play Mode, GCMonitor, MCP runtime-console, profiler, save/load roundtrip, construction graph teardown soak, malformed-airlock-prefab proof, moving-base airlock transition proof, airlock event-listener churn proof, sealed-door laser-cut progress/VFX proof, tether visual scene proof, inventory degradation/reactive-chemistry scene proof, crafting/deconstruction gameplay proof, quest progression gameplay proof, fauna hibernation restore proof, flora cascade visual/profiler proof, fallback beacon deploy/retract/save-load color or prefab-pool-failure despawn proof, PlayMode test execution, foveated/raycast burst, subtitle HUD scene proof, tech-art audit menu execution, vendor-warning cleanup, licensing repair, or memory-retention proof.
Latest addendum inside that report removes the `QuestStateManager.EvaluateSignal()`, `SargassumMicroFaunaBoids.PrimeFoveatedSimulationDecision()`, and `FaunaSimulationEngine.RunHibernationCatchUp()` synchronous `IJob*.Run()` sites, fixes fallback beacon material/despawn ownership so later fallback deployments cannot recolor older fallback beacons through a shared mutable material or route non-pooled fallback objects into pool despawn after prefab-spawn failure, moves `PlayerFlashlight`, `HectonFabricatorUI`, interaction prompt UIs, `DiegeticTooltipSystem`, and `PDAIntrusionManager` input subscription/action ownership to lifecycle/hot-swap paths, caches `PlayerInteraction.ActiveInteractKey`, and moves `EndingTerminalInteractable` prompt localization to lifecycle/language-change caches; `2026-05-03_FOUNDATION_GUARD_SCAN.md` now reports `495` global registry self-registration inventory sites, `0` remaining `.Run(` sites as a hard gate, `0` hot-path `.Run(` review sites, `1` completion `.Complete(` text hit, `0` direct `InputManager.Instance` sites, `0` hot-path direct input-owner review sites, release-reachable direct hot-path debug-log sites `0`, release-reachable one-hop debug-log review sites `0`, `Optimization singleton residue = 0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, and broad physics layer masks outside Editor `0`. The guard script now also has cheap prefilters for loop/coroutine/API/physics scans, comment-stripped call classification, and explicit success `exit 0`; `HUDSaveNotificationLink` was repaired for the current `GameLanguage` localization contract. Latest serial Core, Editor, Input, optional DOTS, PlayMode test, and post-terminal-cache Core builds report `0 Warning(s)` / `0 Error(s)`.

Latest verification note: `Fabricator.cs` release-log preprocessor drift was repaired, the stale `Hecton8.Core.csproj` include for the existing NativeQueue-backed `BaseAirlockEvents.cs` was restored, controlled scoped Core build reports `0 Warning(s)` / `0 Error(s)`, and Unity batchmode fallback strict C# failure scan reports `0`.

`2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md` records guard-signal cleanup around asset dispatch.
`AssetLoadDispatcher.Complete(int, bool)` was renamed to `AcknowledgeDispatchRequest(int, bool)` so the `.Complete(` guard no longer mixes asset ticket acknowledgement with real `JobHandle` fences. Current guard state: `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `1`, guarded dispatcher completion sites `1`. Evidence is source scan, scoped and full `Hecton8.Core` compile `0 Warning(s)` / `0 Error(s)`, plus Unity batchmode import/compile strict failure scan `0`. It is not Play Mode, GCMonitor, Addressables streaming, or memory-retention proof.

`2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md` records the Optimization/VRAM ownership cleanup.
`AssetLifecycleGovernor`, `AssetLoadDispatcher`, `VRAMMonitor`, `VRAMPressureMonitor`, `RenderTextureLifecycleTracker`, `RenderTexturePool`, `VisorRTManager`, `CameraRTManager`, `PostFXRTManager`, and `UIRTManager` no longer retain private static `_instance` or internal `Instance` residue. Duplicate authority now resolves through `GlobalRegistry` slots. Evidence is local static scan, full local `Hecton8.Core` compile `0 Warning(s)` / `0 Error(s)`, Unity batchmode script/import compile with strict failure scan `0`, and foundation guard `Optimization singleton residue = 0`. It is not Play Mode, MCP console, GCMonitor, VRAM residency, duplicate-scene-object, or memory-retention proof.

`2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md` records the latest settings/UserOptions persistence-order hardening.
`SettingsManager` now prefers `GlobalRegistry.Settings` for safe access, registers as an `IGlobalRegistryHotSwapListener`, refreshes `GlobalRegistry.UserOptions` before load/save helpers, and receives a bootstrap UI-phase `RefreshPersistenceFromRegistry()` call after settings runtime registration. Evidence is local `Hecton8.Core` compile only: `0 Warning(s)`, `0 Error(s)`, plus scoped diff check. It is not settings-panel scene flow, PlayerPrefs persistence, save/reload roundtrip, Play Mode, GCMonitor, profiler, or MCP console proof.

`2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md` records the habitat graph anchor-state correction.
`HabitatGraphManager` now separates authoritative `_anchorReachability` from BFS traversal scratch via a persistent `_traversalVisited` buffer, guards the global siege-target snapshot with a publishing owner token, and resets that static snapshot at subsystem registration. `LoreDatabaseManager` now flushes its deferred native unlock-word disposal job without calling `.Complete()`. Evidence is local dotnet compile only: latest scoped Input and Core commands returned `0 Error(s)` / `0 Warning(s)`. It is not Play Mode, GCMonitor, scene/prefab, logistics gameplay, predator siege cognition, graph replacement/disposal, lore save/load, domain reload leak detection, or memory-retention proof.

`2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` is the latest documentation-sweep addendum.
It records the active-doc inventory boundary, dirty-worktree risk, conflicting old logs, fresh local `dotnet build .\Hecton8.Core.csproj` result: `0 Error(s)`, `136 Warning(s)`, elapsed `00:01:24.05`, and latest post-restore `--no-restore` rerun result: `0 Error(s)`, `73 Warning(s)`, elapsed `00:00:23.95`.

`2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` is the current blunt project-level verdict.
It is source/doc-backed, but still not Play Mode proof.

`2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md` is the current local `Editor.log` evidence for console-spam mitigation.
It supersedes older same-day statements that the editor console had known C# warnings or `SetResource` spam in the latest reachable local log, but it is not Play Mode or profiler proof.

`2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md` records the current compile-clean source migration around listener-backed Sargassum/Emergency relay events, the Burst spatial-hash `in` argument fix, and the latest MCP console zero-entry check.

`2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md` supersedes the older compile-evidence line numbers after the `VegetationJobRecovery.cs.meta` restoration. It records the Bee file-lock/internal-error recovery, current `Editor.log` compile/reload success, and final MCP console zero-entry check.

`2026-05-01_EVENT_CASCADE_RECHECK.md` corrects stale event-bus audit claims.
It confirms the source-present `HectonEventBus` depth cap and keeps NativeQueue generation split as the remaining event-cascade risk. As of 2026-05-03, `ModRegistryEvents`, `BootstrapEvents`, `LocalizationEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AudioLogEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `PhysicsEventBus`, `CelestialEvents`, `FluidFeedbackEvents`, `RepairDroneTorchAcousticEvents`, `ElectrolysisAcousticEvents`, `AudioCaptionEvents`, `SpectrumEvents`, `ProceduralAudioEvents`, `HectonSubmarineOsEvents`, `LaserCutterEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `DirectorAIEvents`, `HectonDroneFleetEvents`, `FlashlightEvents`, `PlayerSignalEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `ModuleStatusEvents`, `BaseAirlockEvents`, `DepthZoneEvents`, `SoundscapeEvents`, `EmergencyServiceRelayEvents`, `SargassumGlobalDragManager`, `AtlasSignalEvents`, `PlayerExpressionEvents`, `BaseIntegrityEvents`, `PDAIntrusionEvents`, `PDAEvents`, `SceneBootstrap`, `ObjectPoolDiagnostics`, `PerformanceEvents`, `RandomEventEvents`, `Atlas6Events`, and `GlobalRegistry` service rebound events have source-level generation split; remaining lanes still require review and runtime proof.

## Active Secondary Reports

These are useful, but not first-read project-state authority:

- `CI_VALIDATION_HOOKS_SURGERY_LOG.md`
- `2026-05-03_FOUNDATION_GUARD_SCAN.md`
- `NAVGRID_LEAK_PURGE_SURGERY_LOG.md`
- `OMEGA_PURGE_SURGERY_LOG.md`
- `GC_SINGLETON_KILL_LIST.md`

## Evidence Artifacts

Patch files are evidence artifacts, not narrative authority:

- `2026-04-29_Habitat_Logistics_Graph_Diff.patch`
- `NAVGRID_LEAK_PURGE_DIFF.patch`

For full doc importance sorting, read:

- `../ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`

## Deprecated

Historical static snapshots moved out of the active report root:

- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/ILLEGAL_SINGLETONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/GC_HOTPATH_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/BOOT_ORDER_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/NATIVE_ALLOCATION_AUDIT.md`
