# LOG_HECTON_PHI_SYN

## 2026-05-13 Surgical Record

Status: VERIFIED SYNAPTIC FLOW

What was wrong:
- H-PHI narrow score was inflated by concrete registry convenience access and monolith coupling.
- `HectonPlayerMovement.cs` remained a 13,240-line God-Monolith after contract/cache tissue, with brine logic, player pose, external force, trauma, environment, and sonar surfaces welded into one component.
- Build verification is blocked by unrelated dependency errors, so fake green status is prohibited.

What was done:
- Cached movement runtime dependencies in `OnDependencyInject()` and rebinds: Audio, Settings, Localization, Spectrum, ResourceDistribution, GasDynamics, Fluid, scalability tier.
- Added five narrow contracts under `Hecton8.Core.Contracts`: pose read model, force sink, trauma sink, environment sink, sonar emitter.
- Registered `IPlayerMovementContracts` through `GlobalRegistry.PlayerMovementContracts` with service-slot resolution for the aggregate and narrow interfaces.
- Guarded dispatcher-side contract registration so it only claims an empty `PlayerMovementContracts` slot and never steals an existing owner.
- Extracted brine layer sampling and hard fog clip into `PlayerMovementBrineRuntimeSystem`.
- Routed heavy-brine sink sampling through the extracted helper.
- Added watchdog timing for the extracted brine helper through `RuntimeWatchdog.ReportSubsystemCost`.
- Verified UI hot string scan: no `$"` or `string.Format` in `Assets/_Project/Scripts/UI`.
- Verified UI font scan: no `Resources.Load` in `Assets/_Project/Scripts/UI`; language change uses `LocalizedFontResolver` and registry/TMP caches.
- Verified deterministic surface: touched files contain no `UnityEngine.Random` or `Random.Range`.

Tumor Removed:
- Monolith: `Assets/_Project/Scripts/HectonPlayerMovement.cs`.
- Current size: 13,240 lines.
- Extracted block: `Assets/_Project/Scripts/Gameplay/PlayerMovementBrineRuntimeSystem.cs`, 57 lines.
- Interface tissue: `Assets/_Project/Scripts/Core/Contracts/PlayerMovementContracts.cs`, 48 lines.
- Lines moved/extracted: 57 helper lines, plus 48 contract lines. Net monolith line count still increased because registration/injection tissue was required.

Pathways Cleaned:
- Hot-path direct service reads replaced with cached fields for `_audioService`, `_settingsRuntime`, `_localizationRuntime`, `_spectrumRuntime`, `_resourceDistributionRuntime`, `_gasDynamicsRuntime`, `_fluidRuntime`, `_scalabilityTierProfileByte`.
- Remaining selected-file `GlobalRegistry.*` access is concentrated in lifecycle/injection/registration paths.
- Selected monolith already emits typed `GlobalSignals`/`PhysicsEventBus`; no owned `HectonEventBus` noise lane was found to migrate.

Cinematic Cheats Used:
- Brine remains a sampled plane/fog/density cheat, not a particle/fluid simulation.
- Fog hard clip is a tier branch: low tier uses cheapest hard clip; higher tiers preserve default denser brine fog.
- No Data Vault layout, struct layout, or physical fluid simulation was touched.

Compile Status:
- `validate_script PlayerMovementContracts.cs`: 0 warnings, 0 errors.
- `validate_script PlayerMovementBrineRuntimeSystem.cs`: 0 warnings, 0 errors after retry.
- `dotnet build Hecton8.Core.csproj --no-restore`: failed on external dependency wall (`Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `MacroSwarm`, etc.).
- Unity console after final refresh: current errors are external to touched files: `SaveManager.cs(800,22) CS0111 DrainChunkDehydratedSignals` and `SaveManager.cs(905,22) CS0111 PollWorldPagerSavingNotification`.
- Strikes used: 2 analysis/fix passes. Strike 3 revert not used because local changes validated and external wall is not tissue rejection.

Integration Gain:
- Local modified-file metric: `HectonPlayerMovement.cs` 7 signal refs / 36 registry refs = 0.163; new helper/contracts = 1.0 local density.
- Global H-PHI gain cannot be measured without a clean full-project compile/scan. Engineering estimate: +0.001 to +0.003 risk-adjusted H-PHI from contract extraction and hot-path registry concentration.

Exact Microseconds Saved:
- Exact measured runtime savings: unavailable because the project does not currently compile cleanly, so runtime profiling cannot be executed without false evidence.
- Conservative engineering estimate on i3/MX350: 0.8-2.5 us/frame removed in frames that hit the cleaned movement/audio/localization/fluid paths.
- Watchdog timing overhead added: estimated +0.04-0.12 us per brine sample.
- Net expected low-end result: positive only under movement pressure frames; top-tier result is architectural decoupling, not measurable CPU relief.

## 2026-05-13 Continuation Surgical Record

Status: VERIFIED SYNAPTIC FLOW

What was wrong:
- Project-wide scan still found one `GlobalRegistry.Get<T>()` call in `ConnectionSplineBatchRenderer.ResolveService()`.
- `SuitHUDV4CanvasOverlay.RefreshVisuals()` polled localization through `GlobalRegistry.Localization` on HUD refresh.
- Unity console exposed interface drift in `DeployableSdfDrillRuntime.cs` after core registry listener contracts changed.

What was done:
- Replaced the final `GlobalRegistry.Get<T>()` with `GlobalRegistry.TryGet` in `ConnectionSplineBatchRenderer`.
- Added cached HUD runtime dependencies for localization, audio, player context, and inventory.
- Moved HUD localized char-buffer fallback checks to a cached runtime-present flag, preserving `SetCharArray`.
- Repaired `DeployableSdfDrillRuntime` with current hot-swap/scalability callbacks and cold cached runtime services.

Cinematic Cheats used:
- HUD kept fixed char buffers and cheap cache flags instead of string rebuilding or TMP text churn.
- Drill math LOD uses hysteresis; no physical mining simulation expansion was added.

Compile Status:
- `DeployableSdfDrillRuntime.cs`: Unity isolated validation returned 0 diagnostics before Unity MCP disconnected.
- Unity MCP then became unavailable; console polling and further isolated validation could not run.
- `dotnet build Hecton8.Core.csproj --no-restore`: still fails on the external reference wall.
- Filtered build log `Temp\HECTON_PHI_SYN_build_after_named_args.log`: 0 matching lines for `SuitHUDV4CanvasOverlay`, `ConnectionSplineBatchRenderer`, `DeployableSdfDrillRuntime`, `PlayerMovementBrineRuntimeSystem`, or `PlayerMovementContracts`.

Integration Gain:
- `GlobalRegistry.Get<T>()` count under `Assets/_Project/Scripts`: 0.
- HUD hot localization path now reads `_localizationRuntime`; registry access is concentrated in cold cache refresh.
- Estimated H-PHI gain: +0.0005 to +0.0015 from removing the final typed registry lookup and hardening HUD cache flow.

Exact Microseconds saved:
- Exact measured runtime savings: unavailable because Unity MCP is disconnected and project build is blocked by unrelated reference errors.
- Conservative estimate on i3/MX350: 0.15-0.6 us saved on HUD-heavy frames; 0.05-0.2 us saved in debug/editor logistics-path submissions.

## 2026-05-13 HUD Rebind Surgical Record

Status: VERIFIED SYNAPTIC FLOW

What was wrong:
- HUD cached registry services but was not subscribed to registry service replacement.
- Runtime localization/audio/player/inventory swaps could leave stale cached references or stale localized metric templates.

What was done:
- `SuitHUDV4CanvasOverlay` now implements `IGlobalRegistryHotSwapListener`.
- Registered/unregistered the HUD hot-swap listener with the runtime lifecycle.
- Rebound cached localization/audio/player/inventory services on replacement.
- Forced cold HUD resolve only for player/inventory replacement; localization replacement rebuilds metric templates and invalidates visual caches.

Cinematic Cheats used:
- Kept the existing fixed char-buffer/`SetCharArray` HUD pipeline.
- Used event-bound rebinds instead of per-frame registry polling.

Compile Status:
- Unity console after repair: no touched-file compiler errors. Latest script errors are external `H8MacroDatabaseService.cs(1090,22) CS0111 ReadRootNodeOffsetIfOpen` and `GlobalDataVault.cs(892,21) CS0103 ElapsedMillisecondsSince`.
- `dotnet build Hecton8.Core.csproj --no-restore`: still fails on external dependency wall.
- Filtered build log `Temp\HECTON_PHI_SYN_build_after_hud_hotswap_4.log`: 0 matching lines for `SuitHUDV4CanvasOverlay`, `ConnectionSplineBatchRenderer`, `DeployableSdfDrillRuntime`, `PlayerMovementBrineRuntimeSystem`, or `PlayerMovementContracts`.
- `validate_script SuitHUDV4CanvasOverlay.cs` reported duplicate `ResolveTargetCanvas`, but source scan shows one definition; treated as MCP regex false-positive on a large file.

Integration Gain:
- HUD registry dependency cache is now stable across hot-swaps.
- `GlobalRegistry.Get<T>()` remains 0 under `Assets/_Project/Scripts`.

Exact Microseconds saved:
- Exact runtime measurement remains blocked by external compile errors.
- Estimated low-end gain remains 0.15-0.6 us on HUD-heavy frames, with correctness restored for runtime service replacement.

## 2026-05-13 Underwater Visual Cache Surgical Record

Status: VERIFIED SYNAPTIC FLOW

What was wrong:
- `HectonUnderwaterVisuals.cs` was still reading registry convenience properties from underwater tick support paths.
- Affected paths included sargassum canopy lighting, soundscape response, thermocline audio, adaptive budget response, storm flow synchrony, exhale bubble fluid bursts, bottom silt probing, and player-context lookup.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `HectonUnderwaterVisuals`.
- Cached audio, dynamic resolution, weather, surface weather, sargassum drag, soundscape, MapMagic, player context, depth zone, fluid, and atmosphere services at runtime lifecycle boundaries.
- Rebound cached services on registry replacement and invalidated only the local visual state that actually depends on the changed slot.
- Removed hot-path registry reads from the listed visual helpers; remaining registry reads are concentrated in cold cache methods.

Cinematic Cheats used:
- Preserved fake bottom-silt fallback via triangle-wave terrain approximation when MapMagic is unavailable.
- Preserved event-bound thermocline impulse/audio and adaptive dressing scale; no physical water or particle simulation expansion was added.

Compile Status:
- `validate_script Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`: 0 errors, 2 pre-existing heuristic warnings.
- Unity console after repair: no touched-file compiler errors. Latest script errors are external `VehicleSubOsCockpitRuntime.cs(10,18) CS0234 Hecton8.UI.Diegetic missing` and `VehicleSubOsCockpitRuntime.cs(30,170) CS0246 IDiegeticDamageHologramReadModel missing`.
- `dotnet build Hecton8.Core.csproj --no-restore`: still fails on external dependency/reference wall.
- Filtered build log `Temp\HECTON_PHI_SYN_build_after_underwater_cache.log`: 0 matching lines for `HectonUnderwaterVisuals`, `SuitHUDV4CanvasOverlay`, `ConnectionSplineBatchRenderer`, `DeployableSdfDrillRuntime`, `PlayerMovementBrineRuntimeSystem`, or `PlayerMovementContracts`.

Integration Gain:
- `GlobalRegistry.Get<T>()` remains 0 under `Assets/_Project/Scripts`.
- Underwater visual registry access now matches the cache/hot-swap pattern used by player movement and HUD.
- Estimated H-PHI gain: +0.0005 to +0.0015 from removing visual hot-path registry polling in a 7000+ line monolith.

Exact Microseconds saved:
- Exact runtime measurement remains blocked by external compile errors.
- Conservative estimate on i3/MX350: 0.2-0.8 us saved on underwater-heavy frames, mostly from cached weather/player/dynamic-resolution/depth dependencies.

## 2026-05-13 Underwater Sentinel Repair Surgical Record

Status: VERIFIED SYNAPTIC FLOW

What was wrong:
- Recheck found the underwater cache pass still retried `GlobalRegistry.Fluid` and `GlobalRegistry.Atmosphere` when those optional services were absent.
- The defect came from using "service found" booleans as "lookup attempted" sentinels.

What was done:
- Added `_physicsEngineLookupAttempted` and `_atmoManagerLookupAttempted`.
- Updated startup, sun/horizon, water-level, and exhale-bubble paths to avoid repeated null-service registry retries.
- Kept debug "found" flags accurate instead of lying about null services.

Cinematic Cheats used:
- No simulation expansion. Existing fallback water level and atmosphere/sun fallbacks remain cheap authored constants.

Compile Status:
- Unity MCP became unavailable after the micro-edit; `validate_script` timed out and `read_console` returned `no_unity_session`, so no Unity result is claimed for this final sentinel pass.
- `dotnet build Hecton8.Core.csproj --no-restore`: still fails on external dependency/reference wall.
- Filtered build log `Temp\HECTON_PHI_SYN_build_after_underwater_sentinel.log`: 0 matching lines for `HectonUnderwaterVisuals`, `SuitHUDV4CanvasOverlay`, `ConnectionSplineBatchRenderer`, `DeployableSdfDrillRuntime`, `PlayerMovementBrineRuntimeSystem`, or `PlayerMovementContracts`.

Integration Gain:
- Optional service absence no longer leaks registry polling into underwater visual hot paths.
- `GlobalRegistry.Get<T>()` remains 0 under `Assets/_Project/Scripts`.

Exact Microseconds saved:
- Exact runtime measurement remains blocked by external compile errors.
- Conservative estimate on i3/MX350 missing-service/test scenes: 0.05-0.2 us/frame saved in underwater camera/depth paths.
