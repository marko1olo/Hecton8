# PROJECT_AUDIT Unity Time Triage

Date: 2026-05-21
Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, compile, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or device proof was executed.

## Source

- Tool: `Tools/PolishMandateStaticAudit.py`
- JSON artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_risk_buckets.json`
- Markdown artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_risk_buckets.md`
- Command: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.md`

## Raw Count Preservation

The raw Unity time warning class is:

- `unityTimeCritical`: 962 matches / 261 files

Additive time-kind buckets:

- `unityTimeFrameCount`: 842
- `unityTimeWallClock`: 118
- `unityTimeDelta`: 2
- Sum: 962

Additive build-surface buckets:

- `unityTimeBuildPlayerRuntime`: 925
- `unityTimeBuildEditorOnly`: 14
- `unityTimeBuildQaDevProof`: 23
- Sum: 962

Additive primary-risk buckets:

- `unityTimeRiskFrameStampOrTelemetry`: 806
- `unityTimeRiskGameplayWallClock`: 80
- `unityTimeRiskCooldownOrPerfLog`: 38
- `unityTimeRiskEditorOrProof`: 37
- `unityTimeRiskGameplayDelta`: 1
- Sum: 962

## Interpretation

The previous `unityTimeCritical=964` number was too blunt. Most hits are `Time.frameCount`, usually frame stamps, signal payload stamps, blackbox entries, warning cooldowns, or telemetry descriptors. They still need owner-phase route review, but they are not the same risk as simulation integration using `Time.deltaTime`.

The serious current gameplay-delta debt is now isolated to one player-runtime row:

| File | Line | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs` | 616 | Visual shoreline foam decay uses `Time.deltaTime`; likely presentation-only, but still not dispatcher-owned. |

Two previous gameplay-delta rows were removed:

- `FaunaBrain.TryResolvePredatorLungeCcdPosition()` now uses the last dispatcher `FixedTick(float fdt)` value instead of `Time.fixedDeltaTime`.
- `SubmarineFluidDynamics.UpdateBrineHullBreachState()` now uses `_currentFixedDeltaTime`, already assigned from dispatcher `FixedTick(float fixedDeltaTime)`.

The remaining high-volume risk is not `deltaTime`; it is player-runtime wall-clock ownership:

| File | Wall-clock rows | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 20 | Destruction/regrowth/fade timers appear to use `Time.time`; needs owner-tick conversion or proof that they are presentation-only. |
| `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs` | 4 | Timeline seconds derive from `Time.time`; needs simulation clock route review. |
| `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | 4 | Some combat/death presentation timers still use wall clock. |
| `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | 4 | Presentation/UI timing likely, but should be isolated from gameplay truth. |
| `Assets/_Project/Scripts/HectonBoidController.cs` | 3 | Acoustic ping/presentation timing uses wall clock; needs gameplay vs visual separation. |

## Safe Next Actions

1. Do not mass-replace `Time.frameCount`; first classify whether the value is a blackbox stamp, signal frame, dispatcher frame mirror, or gameplay authority.
2. Convert remaining `Time.deltaTime` only when the caller has a dispatcher `dt` route. For presentation-only VFX, document it as non-authoritative or route through visual frame timing.
3. For `Time.time`, split presentation cooldowns from gameplay truth. Gameplay timers should use owner-local accumulated dispatcher seconds or lockstep tick counters.
4. `DestructibleOrganicManager` is the next real wall-clock owner to inspect because it owns destruction/regrowth facts and also appears in private-native ownership debt.

## 2026-05-22 Organic Clock Follow-Up

`DestructibleOrganicManager` has now been migrated off `Time.time` for owner-state timing. It uses a local organic clock advanced through dispatcher `Tick(float deltaTime)` and feeds that value into corpse expiry, decomposition, wilt, touch, overgrowth, mature spore cadence, damage visuals, and Dear Lie regeneration.

Focused proof:

- `rg -n "Time\.time" Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` returns no rows.
- `rg -n "Time\.fixedDeltaTime|Time\.deltaTime" Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_organic_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 940
- `unityTimeWallClock`: 97
- `unityTimeRiskGameplayWallClock`: 60
- `unityTimeRiskGameplayDelta`: 1

Interpretation: the highest-priority wall-clock owner from the first time triage is no longer on Unity wall clock. Remaining wall-clock work should move to `MigrationDirector`, `FaunaBrain`, `SpectrumSystem`, and `HectonBoidController` after local owner-route inspection.

## 2026-05-22 Migration Timeline Follow-Up

`MigrationDirector` no longer uses `Time.time` as the absent-celestial fallback for migration field game time. `CelestialEngine.GameTime` remains the authority path. When celestial time is unavailable, `_fallbackTimelineGameSeconds` advances from the bounded cold-tick delta and feeds POI expiry, swarm state timestamps, and seasonal field phase.

Focused proof:

- `rg -n "Time\.time" Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_migration_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 936
- `unityTimeWallClock`: 93
- `unityTimeRiskGameplayWallClock`: 56
- `unityTimeRiskGameplayDelta`: 1

Residual note: `MigrationDirector` still uses `Time.unscaledTime` for cold-tick cadence because `ISlowTickable` currently does not pass a delta. That is not a `Time.time` authority stamp anymore, but it remains a dispatcher-contract cleanup candidate.

## 2026-05-22 FaunaBrain Time Follow-Up

`FaunaBrain` no longer uses Unity wall clock for combat mobility, hibernation sleep-start records, or dev watchdog throttling. Combat mobility now uses `_cognitionTimeSeconds`, hibernation sleep-start uses dispatcher `DilatedTimeSeconds`, and watchdog logging is frame-gated.

Focused proof:

- `rg -n "Time\.time|Time\.deltaTime|Time\.fixedDeltaTime" Assets/_Project/Scripts/Fauna/FaunaBrain.cs` leaves one row: `ArmCorpseBloatShaderTimer()`.
- The remaining row feeds `_CorpseBloatStartTime`, and `Hecton_LeviathanOrganic.shader` computes bloat age from Unity `_Time.y`. This is presentation shader time, not gameplay authority.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_fauna_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 932
- `unityTimeWallClock`: 88
- `unityTimeRiskGameplayWallClock`: 53
- `unityTimeRiskGameplayDelta`: 1

Residual note: replacing the corpse-bloat row requires a shader-side migration to a project-owned visual time global or a material property that is advanced by visual dispatcher time. A blind C# replacement would break the GPU fake.

## 2026-05-22 Spectrum Sonar Shader-Time Follow-Up

`SpectrumSystem` no longer uses direct `Time.time` for active sonar pulse, echo, reveal, or active geo timing. Those rows now flow through `ResolveUnityShaderTimeSeconds()` and use `Time.timeSinceLevelLoad`, which matches Unity shader `_Time.y`. This was required because `SonarGridOverlay`, `SuitVisor`, and `Hecton_CoreLit` compute sonar wave age from `_Time.y - pulseTime`.

Two reveal consumers were also aligned to the same clock:

- `HectonSonarPointCloudFeature` now retains screen/world history against `Time.timeSinceLevelLoad` instead of `Time.unscaledTime`.
- `HectonMarineSnowRenderer` now compares sonar glow lifetime against `Time.timeSinceLevelLoad` instead of `Time.time`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Visor/SpectrumSystem.cs Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_spectrum_shader_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 927
- `unityTimeWallClock`: 83
- `unityTimeRiskGameplayWallClock`: 48
- `unityTimeRiskGameplayDelta`: 1

Residual note: this is not a project-owned visual-time migration. `Time.timeSinceLevelLoad` remains a Unity presentation clock, and the current static metric does not count it because the detector targets `Time.time\b`. The gain is removing direct gameplay-wall-clock classification and preventing mixed scaled/unscaled reveal clocks; a full cleanup would introduce a shared visual-time global consumed by the shaders.

## 2026-05-22 Boid Acoustic Panic Clock Follow-Up

`HectonBoidController` no longer uses direct Unity wall-clock time for acoustic ping panic. The controller now owns `_boidClockSeconds`, advances it from dispatcher `Tick(float deltaTime)`, and uses that value for acoustic ping registration and expiry.

Why this was safe: `BoidSimulation.compute` reads `_AcousticPingParams.w` as an active flag. The compute shader does not compare `_AcousticPingParams.z` against Unity `_Time`; C# owns the lifetime check. That makes this unlike the sonar overlay shader path, where pulse age is calculated in HLSL.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/HectonBoidController.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_boid_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 926
- `unityTimeWallClock`: 80
- `unityTimeRiskGameplayWallClock`: 45
- `unityTimeRiskGameplayDelta`: 1

Residual note: this did not change boid GPU buffers, `BoidData` stride, compute shader ABI, acoustic signal DTOs, or the SignalBus route. The remaining wall-clock owners should be inspected one at a time because some are presentation shader clocks and some are real owner-state timers.

## 2026-05-22 Topographical Sonar Follow-Up

`TopographicalSonarSynthesizer` no longer uses direct Unity wall-clock time for ping cadence or point-cloud ping age. The synthesizer now owns `_sonarClockSeconds`, advances it from `Render(float deltaTime)`, and writes shader `PingSignal.x` as `ResolveSonarClockSeconds() - _lastPingTimeSeconds`.

Why this was safe: `Hecton_SonarPoint.shader` reads `_PingSignal.x` directly as `pingAge`. It does not compute `Unity _Time.y - pingStart`, so replacing the C# source clock does not desync a shader-time subtraction.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_topographical_sonar_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 923
- `unityTimeWallClock`: 77
- `unityTimeRiskGameplayWallClock`: 42
- `unityTimeRiskGameplayDelta`: 1

Residual note: the sonar owner clock currently advances in `Render(float deltaTime)`, because the class implements `IRenderable` and `ILateFrameTickable` but not `ITickable`. Late-frame ping cadence can therefore observe the previous render tick by one frame. That is acceptable for the UI point-cloud fake, but a future dispatcher API that passes delta to late-frame tickables would make the route cleaner.

## 2026-05-22 PlayerPDA Follow-Up

`PlayerPDA` no longer uses direct Unity wall-clock time for open duration. It owns `_pdaClockSeconds`, advances it from dispatcher `Tick(float deltaTime)`, and uses the local clock for open start, normal close duration, force-close duration, and `_debugOpenDuration`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/PlayerPDA.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_player_pda_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 918
- `unityTimeWallClock`: 73
- `unityTimeRiskGameplayWallClock`: 39
- `unityTimeRiskGameplayDelta`: 1

Residual note: PDA clock starts at zero and advances only while dispatcher ticks the PDA. External `Open()` calls before the first tick will produce a zero start timestamp until the next dispatcher tick, which is acceptable for UI duration and avoids Unity wall-clock dependency.

## 2026-05-22 HabitatGraphManager Follow-Up

`HabitatGraphManager` no longer uses direct Unity wall-clock time for analytical stress behavior. It owns `_habitatClockSeconds`, advances it from `ApplyHydrodynamicStress(float deltaTime)`, and uses the local clock for low-tier analytical feedback cooldown and breach-gate `timeSeconds`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_habitat_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 915
- `unityTimeWallClock`: 71
- `unityTimeRiskGameplayWallClock`: 37
- `unityTimeRiskGameplayDelta`: 1

Residual note: `ApplyHydrodynamicStress` currently hardcodes `HectonQualityTier.Ultra` for module stress, but `binaryHardwareSwitch=0` remains green because this is not a low/high branch. The quality ownership of habitat analytical stress is separate debt from wall-clock ownership.

## 2026-05-22 FoveatedSimulationManager Follow-Up

`FoveatedSimulationManager` no longer has working-tree `Time.time` rows for tier0 combat lock expiry. The manager uses `ResolveFoveatedClockSeconds()` advanced from `BeginDispatcherFrame(float frameDeltaTime)`, and runtime reset now clears `_foveatedClockSeconds`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_foveated_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 913
- `unityTimeWallClock`: 69
- `unityTimeRiskGameplayWallClock`: 35
- `unityTimeRiskGameplayDelta`: 1

Residual note: `git show HEAD` already contained the foveated clock route for lock/importance comparison; the working tree had regressed to `Time.time` at inspection time. The net diff against `HEAD` is the runtime-reset clock clear.

## 2026-05-22 PersistentWorldRegistry Follow-Up

`PersistentWorldRegistry` no longer uses direct `Time.time` for fauna egg hatch restore or hibernation state creation. It owns `_worldClockSeconds`, advances it from dispatcher `Tick(float dt)`, and uses that value for the fauna record path.

Focused proof:

- `rg -n "Time\.time\b" Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_persistent_world_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 910
- `unityTimeWallClock`: 66
- `unityTimeRiskGameplayWallClock`: 32
- `unityTimeRiskGameplayDelta`: 1

Residual note: `PersistentWorldRegistry` still has `Time.unscaledTime` in sector override unload/commit and tombstone sweep cadence. That should be handled as a cold IO/paging scheduler route, not mixed into fauna state timing.

## 2026-05-22 SargassumCutManager Follow-Up

`SargassumCutManager` no longer uses direct `Time.time` for recent cut heat stamp registration or shader-global pruning. This was intentionally handled as shader-clock alignment, not owner-clock migration: `Hecton_ScooterVolumetricShafts.shader` computes heat age from `HectonShaftAnimationTime() - strengthTime.y`, and `HectonShaftAnimationTime()` is based on Unity `_Time.y`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/SargassumCutManager.cs` returns no direct wall-clock rows; only `Time.timeSinceLevelLoad` remains inside `ResolveThermalShaderClockSeconds()`.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_sargassum_shader_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 906
- `unityTimeWallClock`: 64
- `unityTimeRiskGameplayWallClock`: 30
- `unityTimeRiskGameplayDelta`: 1

Residual note: `Time.timeSinceLevelLoad` is still a Unity presentation clock. This is acceptable here because the payload is a shader `_Time` bridge for thermal haze, not rollback/gameplay authority.

## 2026-05-22 VegetationFlowFieldIntegrator Follow-Up

`VegetationFlowFieldIntegrator` no longer uses direct Unity wall-clock time for threat propagation delta or swarm wake impulse expiry. `HectonMapMagicVegetationBridge` now owns `_vegetationRuntimeSeconds`, advances it from dispatcher `Tick(float dt)`, and the flow-field partial reads it through `ResolveVegetationRuntimeSeconds()`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_vegetation_flow_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 902
- `unityTimeWallClock`: 62
- `unityTimeRiskGameplayWallClock`: 28
- `unityTimeRiskGameplayDelta`: 1

Residual note: `HectonMapMagicVegetationBridge.cs` still has `Time.unscaledTime` for native-pool fragmentation log cadence and camera-resolve retry cadence. Those are cooldown/diagnostic routes and were not mixed into flow-field simulation timing.

## 2026-05-22 WorldChunkResidencyManager Follow-Up

`WorldChunkResidencyManager` no longer uses direct `Time.time` for HLOD impostor spawn/fade timestamps. It owns `_chunkResidencyRuntimeSeconds`, advances it from dispatcher `Tick(float deltaTime)`, and routes HLOD swap/fade cull jobs through `ResolveChunkResidencyRuntimeSeconds()`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` leaves only adrenaline purge `Time.unscaledTime` rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_chunk_residency_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 898
- `unityTimeWallClock`: 60
- `unityTimeRiskGameplayWallClock`: 26
- `unityTimeRiskGameplayDelta`: 1

Residual note: memory-pressure adrenaline purge remains on `Time.unscaledTime`. That path is load-shed/backpressure scheduling; it needs an explicit unscaled scheduler clock before migration.

## 2026-05-22 WorldCaveDirector Follow-Up

`WorldCaveDirector` no longer uses direct Unity wall-clock time for cave spawn evaluation throttle. It now reads bounded dispatcher `DilatedTimeSeconds` through `ResolveCaveEvaluationTimeSeconds()`.

Focused proof:

- `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/WorldCaveDirector.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_world_cave_dispatcher_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 894
- `unityTimeWallClock`: 58
- `unityTimeRiskGameplayWallClock`: 24
- `unityTimeRiskGameplayDelta`: 1

Residual note: `WorldCaveDirector` still has larger non-time debt: managed dictionaries/lists and async cave spawn lifecycle remain owner-local managed state. This pass only removed direct wall-clock authority.

## 2026-05-22 Surface/Biome/Pipe/Drone Follow-Up

Four direct runtime wall-clock rows were removed or rerouted:

- `SurfaceWeatherVfxRig`: splash impulse timestamp now uses `ResolveWeatherShaderClockSeconds()` because `Hecton_OceanRainRippleDecal.shader` computes `_Time.y - impulse.z`.
- `BiomeMatrixDirector`: seismic dust entry cooldown now uses bounded dispatcher `DilatedTimeSeconds`.
- `ConnectionSplineBatchRenderer`: pipe rupture start timestamp now uses `ResolvePipeShaderClockSeconds()` because `Hecton_FlexiblePipe.shader` computes `_Time.y - ruptureStartTime`.
- `DroneFleetManager`: `DroneCognitionJob.PhantomFlowTime` now uses `s_HeadlessSimulationClockSeconds`, advanced from sanitized headless `Tick(deltaTime)`.

Focused proof:

- Focused scan over the four touched files shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, or `Time.fixedDeltaTime`; `Time.timeSinceLevelLoad` remains only in shader-clock helpers.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_surface_biome_pipe_drone_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 890
- `unityTimeWallClock`: 54
- `unityTimeRiskGameplayWallClock`: 20
- `unityTimeRiskGameplayDelta`: 1

Residual examples now include `CurrentVolume`, `FaunaBrain` corpse-bloat shader timer, player fixed/render interpolation, random event shockwave shader timestamp, atmosphere/celestial/fluid clocks, RT lifecycle diagnostics, footstep audio cadence, leak plume shader time, voxel cut heat shader time, abyssal fluid decal time, LOD cadence, micro-fauna hit flash, and scatter candidate acceptance time.

## 2026-05-22 Current/Atmosphere/Celestial/Decal/Shader Follow-Up

Six direct runtime time rows were removed or made explicit:

- `CurrentVolume`: shared current sample time now uses bounded dispatcher `DilatedTimeSeconds`.
- `AbyssalFluidDecalManager`: decal advection now uses `_fluidDecalClockSeconds`, advanced from sanitized dispatcher tick delta.
- `HectonAtmosphereManager`: atmosphere timeline and procedural biome influence refresh now use bounded dispatcher timeline time.
- `HectonCelestialEngine`: celestial timeline accumulation now uses bounded dispatcher timeline time.
- `RandomEventSystem`: meteor water impact timestamp now uses `ResolveMeteorWaterImpactShaderClockSeconds()` because the payload is a shader age bridge.
- `VoxelDeltaProcessor`: recent laser cut heat timestamp now uses `ResolveLaserCutHeatShaderClockSeconds()` because the payload is a shader heat-age bridge.

Focused proof:

- Focused scan over the six touched files shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, or `Time.fixedDeltaTime`.
- `git diff --check --` on touched files reports only LF-to-CRLF warnings.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_current_atmo_celestial_decal_shader_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 883
- `unityTimeWallClock`: 48
- `unityTimeRiskGameplayWallClock`: 14
- `unityTimeRiskGameplayDelta`: 1

Residual gameplay-wall-clock examples now concentrate in `RenderTextureLifecycleTracker` diagnostics, `FaunaBrain` corpse-bloat shader timer, fixed/render interpolation in player camera/movement, `HectonFluidEngine` shader/presentation timing, footstep audio cadence, leak plume shader time, LOD cadence, micro-fauna hit flash, and scatter candidate acceptance time.

## 2026-05-22 Shader/Presentation Residual Follow-Up

Six residual rows were removed or made explicit:

- `FaunaBrain`: corpse-bloat start time now uses `ResolveCorpseBloatShaderClockSeconds()` because `Hecton_LeviathanOrganic.shader` computes `_Time.y - _CorpseBloatStartTime`.
- `SubmarineStructuralGrid`: leak plume compute now uses `_leakPlumeClockSeconds`, advanced from sanitized fixed-step delta.
- `FloraInteractionManager`: parasite pulse and wake-trail simulation time now use `GetCurrentSimulationTimeSeconds()`; fallback no longer uses `Time.realtimeSinceStartup`.
- `SargassumMicroFaunaBoids`: VAT hit flash start time now uses `ResolveHitFlashShaderClockSeconds()` because `BoidFishInstanced.shader` computes `_Time.y - _HitFlashStartTime`.
- `HectonFluidEngine`: weather-missing water/force fallback now uses bounded dispatcher time, and UI water value timestamps reuse water-level time.
- `ObserverRelativeCelestialBody`: runtime realtime mode now uses an explicit presentation clock helper.

Focused proof:

- Focused scan over these files shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, or `Time.realtimeSinceStartup` rows in the patched paths.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_shader_presentation_owner_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 877
- `unityTimeWallClock`: 42
- `unityTimeRiskGameplayWallClock`: 8
- `unityTimeRiskGameplayDelta`: 1

Residual gameplay-wall-clock examples are now: player fixed/render interpolation in `HectonPlayerCameraRig` and `HectonPlayerMovement`, `RenderTextureLifecycleTracker` diagnostics, `PlayerFootstepAudio` cadence, `LODSystemManager` cadence, and scatter candidate acceptance time. The single gameplay delta row remains `OceanSinglePass/ShorelineFoamGraftContracts.cs`.

## 2026-05-22 Residual Tick/Delta/Lifecycle Follow-Up

The residual gameplay-time bucket is now empty in the broad static audit.

Verified or current source routes:

- `HectonPlayerCameraRig`: late-frame KCC offset uses `HectonFloatingOrigin.CurrentFixedInterpolationAlpha`, not `Time.time - Time.fixedTime`.
- `HectonPlayerMovement`: render interpolation uses `HectonFloatingOrigin.CurrentFixedInterpolationAlpha`; sargassum entanglement audio cooldown uses `_sargassumInfluenceClockSeconds`, advanced by fixed-step delta.
- `PlayerFootstepAudio`: step cooldown uses `_footstepClockSeconds`, advanced by dispatcher update delta.
- `LODSystemManager`: null-registration cleanup cadence uses `_lodRuntimeClockSeconds`, advanced by tick delta.
- `WorldProceduralScatterDirectorCandidateAcceptance`: rescue placement registration uses `_samplingNow` with dispatcher `DilatedTimeSeconds` fallback.
- `ShorelineFoamGraftRuntime`: foam decay/mock generation delta comes from `OceanSinglePassRuntime.VisualSyncTick` `timing.FrameDelta`.
- `RenderTextureLifecycleTracker`: RT allocation/leak age now uses dispatcher `UnscaledTimeSeconds` through `ResolveLifecycleClockSeconds()`.

Focused proof:

- Focused scan over the residual files found no direct `Time.time`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, or `Time.realtimeSinceStartup`.
- `git diff --check -- Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs` reports only LF-to-CRLF warning.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_render_texture_lifecycle_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 868
- `unityTimeWallClock`: 34
- `unityTimeRiskGameplayWallClock`: 0
- `unityTimeRiskGameplayDelta`: 0
- `unityTimeDelta`: 1 (`Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs:30`)

Residual time debt is now mostly frame stamps/telemetry/log throttles and one dev debugger `Time.fixedDeltaTime` accessor. This is not runtime/profiler proof; it is static-source proof only.

## 2026-05-22 Native API Exposure Follow-Up

This follow-up shifted from Unity time debt to native collection API debt after the gameplay wall-clock buckets reached zero.

Changes:

- `HectonMapMagicVegetationBridge.TryGetActiveAbyssalAnchorPayload` now returns `NativeArray<Vector3>.ReadOnly`.
- `HectonMapMagicVegetationBridge.TryGetActiveAbyssalAnchorAupPayload` now returns `NativeArray<AbsoluteUniversePosition>.ReadOnly`.
- `HectonMapMagicVegetationBridge.TryGetEcosystemThreatGridPayload` now returns `NativeArray<float>.ReadOnly`.
- `HectonMapMagicVegetationBridge.TryGetCompressedEcosystemThreatGridPayload` now returns `NativeArray<byte>.ReadOnly`.
- `HectonMapMagicVegetationBridge.TryGetTerrainHoleStreamingPayload` now returns `NativeArray<TerrainHoleStreamingRecord>.ReadOnly`.

Call-site updates:

- `AcousticEcholocationTranslator`
- `ARWaypointOverlay`
- `SuitHUDV4CanvasOverlay`
- `SargassumMicroFaunaBoids`
- `HectonVoxelStreamingBridge`

Focused proof:

- Focused scans found no stale mutable call-site declarations for the converted APIs.
- Focused scans found no `.IsCreated` checks on the converted read-only locals in touched call sites.
- Runtime `.Complete()` read-only explorer grep found no executable direct `.Complete()` outside `DispatcherJobFence`; remaining runtime-scope hit was only a string literal in `SavePersistenceOmegaSmokeTester`.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_after_vegetation_readonly_payloads.json`.

Updated static counts from that artifact:

- `nativeCollectionPublicMutableApiExposure`: 236, down from 268 at the start of this native-exposure pass.
- `nativeApiExposureBuildPlayerRuntime`: 222, down from 254.
- `nativeApiExposureOutRefMutable`: 184, down from 189.
- `nativeApiRiskRuntimeOutRefMutableView`: 109, down from 114.
- `jobHandleComplete`: 112, unchanged; bounded runtime grep found no remaining executable runtime candidates outside the core fence helper.

This is static-source proof only. No Unity import, Console, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 Voxel Passability Native API Follow-Up

Additional native API narrowing after the vegetation snapshot pass:

- `VoxelDynamicNavGridRuntime.TryGetPassabilityPayload` now returns `NativeArray<byte>.ReadOnly`.
- `VoxelDynamicNavGridRuntime.TryGetContainingPassabilityPayload` now returns `NativeArray<byte>.ReadOnly`.
- `VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload` now returns `NativeArray<byte>.ReadOnly`.
- `VegetationNavGridSynchronizer` and `StringPullPathJob.NavPassabilityGrid` now consume the read-only view.

Rejected:

- PDA/cartography upload narrowing in this pass, because `GraphicsBufferUploadUtility.UploadNativeArray` currently accepts mutable `NativeArray<T>` and needs separate core/local unsafe upload proof.
- Voxel build buffers and pure-void scan buffers, because those are legitimate writer-side job buffers.

Focused proof:

- Focused scans found no stale `out NativeArray<byte>` passability declarations.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_after_voxel_passability_readonly.json`.

Updated static counts from that artifact:

- `nativeCollectionPublicMutableApiExposure`: 222, down from 225.
- `nativeApiExposureBuildPlayerRuntime`: 208, down from 211.
- `nativeApiExposureOutRefMutable`: 170, down from 173.
- `nativeApiRiskRuntimeOutRefMutableView`: 95, down from 98.

This is static-source proof only. No Unity import, Console, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.
