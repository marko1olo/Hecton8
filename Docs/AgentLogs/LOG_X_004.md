# LOG_X_004

## 2026-05-23 Session Start

What was wrong -> No X_004 state files existed for this active batch.
What was done -> Created task status, rationale, and log files for file-backed memory.
Cinematic Cheats used -> None yet; scanner and mapping phase pending.
Exact Microseconds saved -> 0 us measured; no runtime code changed.

## 2026-05-23 Presentation Decoupling Pass

What was wrong -> Presentation APIs were present in pre-visual or simulation-adjacent lanes, and no reproducible X_004 proof artifact existed.
What was done -> Added `Tools/PresentationDecouplingAudit` and generated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`. Last completed run scanned 2373 files, 843 runtime files, 615 simulation files, 229 presentation files, with 0 parser failures. Last completed report hash: `3e0b88b501559b0c883073b544abf1439811d551e89d0f7995b0ef7fe74a3153`.
Cinematic Cheats used -> Static Dear Lie mapping: shader/constant-buffer visual fakes, SPSC audio route, zero-GC UI buffer route, and VISUAL_SYNC ownership route per finding.
Exact Microseconds saved -> 0 us measured; static proof only. Estimated review time saved: 20-45 us per finding cluster.

What was wrong -> `HabitatFluidIncursionDirector.PostFixedTick` wrote a global shader scalar from the fluid post-fixed phase.
What was done -> Replaced direct shader write with `_pendingFloodScalar` plus `_floodScalarDirty`; `Render` now commits `_H8GlobalFloodScalar`.
Cinematic Cheats used -> Flood/muffle presentation remains a shader-side scalar lie; simulation keeps only fluid summary truth.
Exact Microseconds saved -> 0 us measured. Estimate: 2-5 us on dirty flood frames on i3/MX350 class hardware.

What was wrong -> `SoundscapeSystem.SlowTick` wrote `_SoundscapeDepthTier` to shaders in the gameplay/audio slow lane.
What was done -> Added `ILateFrameTickable`, queued `_pendingShaderTier`, registered/unregistered the late-frame route, and moved the shader write to `LateFrameTick`.
Cinematic Cheats used -> Depth-tier visual coloration is a presentation snapshot; audio/gameplay tier truth remains separate.
Exact Microseconds saved -> 0 us measured. Estimate: 1-3 us per dirty tier change on low-end hardware.

What was wrong -> `FloraInteractionManager.Tick` uploaded prop-wash globals and interaction buffers directly while processing flora interaction state.
What was done -> Added a fixed pending visual snapshot and moved prop-wash, interaction buffer, and interaction count shader writes to `LateFrameTick`.
Cinematic Cheats used -> Flora bending/pushback presentation is a GPU interaction-buffer lie; gameplay density math remains the producer.
Exact Microseconds saved -> 0 us measured. Estimate: 6-8 us per active flora interaction frame on i3/MX350 class hardware.

What was wrong -> Final analyzer rerun and full compile were unsafe under active external C# workload.
What was done -> Checked process/CPU state: `dotnet.exe` and `csc.exe` were active, CPU probe returned 100.0%. Later CPU was 24.1%, but multiple `dotnet.exe` nodes still remained. Compile and analyzer rerun after final patches were not launched.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured; no false compile claim recorded.

## 2026-05-23 X_004 Proof Closure

What was wrong -> Five presentation-owned renderer classes still pushed shader/material state from `Tick`, leaving visual work in the wrong phase even after simulation-owned hot paths were clean.
What was done -> Added `ILateFrameTickable` and late-frame registration to `HectonBiolumDiffusionVolume`, `GPUScatterDirector`, `HectonDistantLandmarkRenderer`, `HectonHLODRenderer`, and `HectonOctahedralImpostorRenderer`; their `Tick` methods are now no-op compatibility stubs and GPU writes execute from `LateFrameTick`.
Cinematic Cheats used -> Biolum volume, scatter, landmark silhouettes, HLOD matrices, and impostor animation remain GPU-side lies fed by immutable buffers/scalars after pre-visual phases finish.
Exact Microseconds saved -> 0 us measured. Estimate: 18 us pre-visual contention removed on i3/MX350.

What was wrong -> `PRESENTATION_MUTABLE_TRUTH_ACCESS` proof was noisy because generic `TryResolveHandle` read consumers were counted as mutable writers.
What was done -> Tightened the Roslyn scanner to exact mutable/write member names. Latest report: `fatalHotPath=0`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `b8b9e08b96aa4c9e5530cbc737b88a765a54d6f195f1777bbf400aed9ab6a10c`.
Cinematic Cheats used -> None; proof-only fix.
Exact Microseconds saved -> 0 us runtime; 262 false-positive review entries removed.

What was wrong -> Compile proof was pending because external compiler workloads were active earlier.
What was done -> Waited until no `dotnet.exe`/`csc.exe` were present and CPU was under threshold. Ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Build result: 0 warnings, 0 errors, 00:01:06.72 elapsed. Unity runtime/profiler proof still not run.

## 2026-05-23 APEX Re-Audit Pass

What was wrong -> Direct-only scanning was insufficient; hidden helper chains from `Tick`/`SlowTick`/`FrostTick` still pushed shader globals in weather, seismic, ocean, and flora paths. PDA hot refresh paths could also allocate lore-surface strings through `string.Concat`.
What was done -> Extended `Tools/PresentationDecouplingAudit` with same-type helper closure scanning and stricter UI string checks. Moved `GlobalWeatherDirector`, `HectonSeismicTideDirector`, `HectonSurfaceWeatherDirector`, and `ShinobuOceanSurfaceAtmosphereRuntime` shader publication to `LateFrameTick` via dirty scalar/vector/buffer state. Split ocean wave buffer upload from shader binding. Added PDA cold caches for lore keys, author lines, summary lines, and unknown lines. Replaced hull breach jet per-material property mutation with global shader state in the visual sync path.
Cinematic Cheats used -> Weather fog/rain/godray, seismic shake, ocean wave globals, flood waterlines, hull dent/breach buffers, visor/PDA corruption, and flora wake/interaction visuals are presentation lies fed by DTOs and dirty scalar snapshots after simulation truth is complete.
Exact Microseconds saved -> 0 us measured. Estimates: weather/seismic/ocean 12-20 us on dirty frames, PDA hot refresh 2-6 us and 1-3 short-lived strings, hull breach material churn 3-5 us, selected flora visual globals 8-12 us.

What was wrong -> The strict project-wide audit still finds legacy hot-path presentation coupling outside the targeted X_004 paths.
What was done -> Reran analyzer on `Assets/_Project/Scripts`: `files=2373`, `runtimeFiles=863`, `simulationFiles=589`, `presentationFiles=274`, `fatalHotPath=334`, `boundaryLeaks=128`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `bb52bbb5df934a51c017469c2987c20ce9410067d3f05d984afc64ae3f4f2302`. Targeted filter for flooding, hull deformation, weather, seismic, ocean, helmet/PDA, and patched flora paths reports 0 fatal/UI-string hits.
Cinematic Cheats used -> None; proof boundary recorded honestly.
Exact Microseconds saved -> 0 us measured. Residual global findings are not counted as solved.

What was wrong -> Compile proof was stale after the APEX patches.
What was done -> Waited for dotnet/csc and CPU gate, then ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Build result: 0 warnings, 0 errors, 00:00:59.21 elapsed. Unity runtime/profiler proof still not run.

## 2026-05-23 APEX Re-Audit Pass 02

What was wrong -> The second-pass helper-chain audit found real presentation leaks still reachable from hot world/ecology paths: `SargassumCutManager` compute/RT clear/debris particle paths, `SargassumGlobalDragManager` scavenger render-resource path, `FloraInteractionManager` submarine wash/wake/flow/sediment/global shader paths, and `CreatureDamageManager` wound shader globals.
What was done -> Moved sargassum cut mask, terrain damage volume, debris particles, cave SDF upload, thermal smoke, ocean wave readback dispatch, sargassum nested/scavenger rendering, flora wake/flow/sediment/globals, and creature wound globals into late-frame or render-phase sync. Added A/B `GraphicsBuffer` write promotion for PDA spectrogram segments/args, VR brownout constants, and half-res particle constants.
Cinematic Cheats used -> Cut scars, terrain damage, submarine wash, ecology wakes, sediment bursts, cave AO, thermal smoke, visor brownout, PDA glitch/spectrogram, and half-res particles are now visual lies fed by scalar/DTO/buffer snapshots after simulation writes finish.
Exact Microseconds saved -> 0 us measured. Estimates: sargassum cut/damage/debris 8-14 us, sargassum scavenger/nesting 3-6 us, flora ecology visual cluster 31 us, creature wound globals 2-4 us, GPU double-buffer stall risk 4-9 us on i3/MX350-class dirty frames.

What was wrong -> The user specifically challenged hull deformation/flooding/PDA/helmet paths and GraphicsBuffer double buffering.
What was done -> Targeted APEX route filter now reports 0 fatal findings for sargassum cut/drag/crest/microfauna, cave voxel lighting, abyssal thermal, ocean atmosphere, flooding/hull/cavitation, PDA spectrogram, visor brownout, and half-res particles. Latest UI gate reports `uiStringGcRisks=0`.
Cinematic Cheats used -> Flood waterlines, hull dents/breach jets, cavitation, PDA spectrogram/glitch, visor brownout, and half-res particles consume read buffers/constants only; no hot UI string formatting is accepted by the scanner.
Exact Microseconds saved -> 0 us measured. UI hot-path GC risk count is 0 by static AST proof; Unity GC profiler proof was not executed.

What was wrong -> A global clean claim would be false.
What was done -> Regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`: `files=2374`, `runtimeFiles=863`, `simulationFiles=589`, `fatalHotPath=94`, `boundaryLeaks=128`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `e56d0b3a65123d9172c473324458fac3f330ec17e42a962945b117052c4a6bdb`. Top remaining strict findings are outside the closed targeted set: `FaunaBrain` 14, `HectonSubmarineOS` 11, `VRSomaticProvider` 8, `RandomEventSystem` 8, `MantaScooter` 7, `HectonPlayerCameraRig` 6, `EcosystemDirector` 5.
Cinematic Cheats used -> None; this is the honesty boundary.
Exact Microseconds saved -> 0 us measured. Residual findings are not counted as solved.

What was wrong -> Compile proof was required after the late-frame migrations and double-buffer changes.
What was done -> Waited until CPU/dotnet/csc gate allowed a single build. Ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Build result: 0 warnings, 0 errors, 00:01:18.92 elapsed. Unity runtime/profiler proof still not run.

## 2026-05-23 APEX Re-Audit Pass 03 - Global Fatal Closure

What was wrong -> The strict helper-chain audit still reported 28 fatal hot-path presentation routes after the prior pass. Residual chains included docking/airlock non-rigidbody transforms, eclipse and prologue shader globals, floater/plant/swim renderer enables, spark/decal draws, voxel chunk material fade writes, oxygen bubble transforms, resource-node deactivate fallbacks, drill terrain snap transforms, outpost graphics resource creation, and world chunk fade shader globals.
What was done -> Converted the residual routes to dirty scalar/DTO/presentation queues and VISUAL_SYNC flushes. `EclipseGameplaySystem`, `LifePodTactilePrologueController`, `PlayerSwimPresentationController`, `WorldChunkResidencyManager`, `LifePodDamageSystem`, `AbyssalFluidDecalManager`, `HectonVoxelStreamingBridge`, `VehicleDockingModule`, `MountablePlayerTransport`, `Floater`, `OxygenBubble`, `HarvestablePlant`, `ResourceDistributionDirector`, `MantaEmergencyWreck`, `DeployableSdfDrillRuntime`, `PlayerSwimBlockoutRig`, and `MarauderOutpostGenerationService` now route the checked shader/material/draw/transform/renderer/SetActive commits through `LateFrameTick` or render-phase ownership. `BaseAirlock.TeleportBody` uses `Rigidbody` pose publication instead of transform presentation mutation.
Cinematic Cheats used -> Docking snap visuals, chunk dissolve, outpost shell draw setup, spark quads, pressure decals, swim blockout renderer visibility, plant regrowth visibility, drill snap pose, and emergency wreck deactivation are now presentation lies applied after simulation state is resolved.
Exact Microseconds saved -> 0 us measured. Estimate: 22-41 us of pre-visual contention/allocation risk removed across dirty gameplay/world frames on i3/MX350-class hardware.

What was wrong -> The final residual fatal was `MarauderOutpostGenerationService.Tick -> EnsureGraphicsResources`, which could allocate `GraphicsBuffer`, `MaterialPropertyBlock`, mesh, or fallback material from the hot generation request chain.
What was done -> Removed `EnsureGraphicsResources()` from `TryRequestGeneration`; `LateFrameTick()` now performs graphics resource assurance before extraction upload/render readiness. Generation requests stay data/job-owned, while render resources wake only in VISUAL_SYNC.
Cinematic Cheats used -> The outpost shell remains an indirect GPU draw driven by extracted matrices/cell-type buffers; simulation does not create material resources during sector hydration.
Exact Microseconds saved -> 0 us measured. Estimate: 4-9 us stall/allocation risk avoided on generation handoff frames.

What was wrong -> The user demanded exact proof that double-buffered `GraphicsBuffer` routes move Vault/DTO state into shaders without blocking the main thread.
What was done -> Verified A/B buffer promotion in `PDADecryptionSpectrogramPanel` (`_segmentBufferA/B`, `_argsBufferA/B`), `HectonVRBrownoutFeature` (`_brownoutGlobalsBufferA/B`), `HectonHalfResParticlesFeature` (`_halfResParticlesGlobalsBufferA/B`), and `DiegeticVisorLensRuntime` (`_gpuGlobalsBufferA/B`). Each route writes the inactive buffer with `LockBufferForWrite`, unlocks it, promotes it as active, and binds it from VISUAL_SYNC/render-feature code. No same-frame `GetData`, readback wait, or hot `SetData` loop was found in those checked paths.
Cinematic Cheats used -> PDA wave/spectrogram, visor brownout, lens distortion/helmet scalar state, and half-res particle composite are shader-driven masks/constant-buffer lies fed by flat DTOs.
Exact Microseconds saved -> 0 us measured. Estimate: 4-9 us of CPU/GPU overlap stall risk removed on dirty frames.

What was wrong -> Helmet/PDA UI hot loops could not be accepted if any `.ToString()`, concat, `string.Concat`, `new string`, `AppendFormat`, or `TMP_Text.text` mutation survived in frame paths.
What was done -> Reran `Tools/PresentationDecouplingAudit`; latest report has `uiStringGcRisks=0` and `presentationMutableTruthFindings=0`. The checked UI/visor/PDA routes read flat DTOs/cached values and drive GPU buffers/masks in late-frame/render phases.
Cinematic Cheats used -> PDA glitch, spectrogram, helmet brownout, visor lens distortion, and half-res overlays are GPU masks, not CPU string/animation work.
Exact Microseconds saved -> 0 us measured. Static UI hot-path GC risk is 0; Unity GC profiler proof not executed.

What was wrong -> A global clean claim was previously false; the strict report still had 94 fatal findings before this pass.
What was done -> Regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`: `files=2375`, `runtimeFiles=863`, `simulationFiles=589`, `presentationFiles=274`, `fatalHotPath=0`, `boundaryLeaks=128`, `namespaceLeaks=51`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `9f57d2c490c62ed07f0fadcf640ce7dd0b7ed319e732761e2f1a4ef41583eb97`.
Cinematic Cheats used -> None; proof artifact only.
Exact Microseconds saved -> 0 us measured. Residual boundary debt is cold type/namespace ownership debt, not a hot presentation call in the current scanner.

What was wrong -> Compile proof after the final fatal closure could not be launched safely.
What was done -> Checked the gate repeatedly. CPU probes reached 100%, and external `dotnet.exe`/`csc.exe` processes were active. Per project rule, no new `dotnet build` was launched. Last previous CLI build before the final outpost move was clean: 0 warnings, 0 errors, 00:01:18.92.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Latest compile proof remains pending; this is a gate block, not a reported compiler failure.

## 2026-05-23 APEX Re-Audit Pass 04 - Paranoid Source Recheck

What was wrong -> User challenged whether hull deformation and flooding still directly touch presentation from simulation/jobs.
What was done -> Re-read owner methods and line routes. `HullIntegrityRuntime.Tick` schedules DTO jobs only; `LateFrameTick` finalizes the fence and owns `UploadDentsToGpu`, `UploadDeformationsToGpu`, `UploadBreachJetsToGpu`, and `RenderBreachJets`. `HabitatFluidIncursionDirector.FixedTick/PostFixedTick` schedules/finalizes fluid DTO jobs and only marks `_pendingFloodScalar`/waterline dirty; `Render` owns `Shader.SetGlobalFloat`, A/B waterline `GraphicsBuffer.LockBufferForWrite`, and shader binding. `StructuralIntegrityCalculatorRuntime.Tick` schedules solver work; `LateFrameTick -> AfterSolverComplete -> UploadStatesToGpu` owns the structural shader buffer bind.
Cinematic Cheats used -> Hull dents, deformation states, breach jets, flood scalar, and waterline visuals are shader/indirect-draw lies fed by DTO buffers after simulation fences complete.
Exact Microseconds saved -> 0 us measured. This pass is proof/readback; previous estimates remain 22-41 us pre-visual contention removed across dirty frames.

What was wrong -> Flora/environment visuals still had raw `ParticleSystem`/`Shader.Set*` tokens in mixed world files.
What was done -> Re-read call ownership. `FloraInteractionManager.Tick` queues environment, wash, damage, flow, wake, sediment, and interaction data. `LateFrameTick` owns `FlushQueuedTickVisualWork`, `FlushQueuedVisualGlobals`, `FlushSubmarineWashGlobals`, `FlushDamageReactionGlobal`, `FlushFlowFieldGlobals`, `FlushProceduralWakeBuffer`, `FlushFloraSwayFieldGlobals`, `FlushPlayerRuntimePosition`, and `FlushInteractionVisualSync`. Sediment `ParticleSystem.Emit` remains presentation-only under `LateFrameTick -> FlushQueuedTickVisualWork -> TryEmitSedimentBursts -> EmitSedimentBurst`.
Cinematic Cheats used -> Vegetation bend, flow, submarine wash, wake, sediment, and interaction visuals are queued scalar/DTO presentation lies.
Exact Microseconds saved -> 0 us measured. Previous flora cluster estimate remains 31 us shifted out of hot ecology `Tick` on dirty frames.

What was wrong -> PDA spectrogram passed the analyzer but still built visual sine segments on CPU/Burst and uploaded a structured segment buffer.
What was done -> Removed the PDA visual segment Vault/buffer route from `PDADecryptionSpectrogramPanel`. The CPU job now computes only gameplay error truth. `RenderWaveMesh` passes `_targetFrequency`, `_targetAmplitude`, `_playerFrequency`, `_playerAmplitude`, segment count, and surface layout. `Hecton_PDA_FrequencyTuningWave.shader` reconstructs both target/player tubes in the vertex shader with `sin` from those scalars. Indirect draw args remain A/B `GraphicsBuffer` using `LockBufferForWrite`; no segment buffer upload remains.
Cinematic Cheats used -> PDA waveform is now a GPU lie driven by scalar truth; CPU no longer builds the visual representation.
Exact Microseconds saved -> 0 us measured. Estimate: 3-7 us avoided on active PDA tuning frames on i3/MX350 from deleting segment DTO writes and structured-buffer upload.

What was wrong -> Helmet/PDA UI hot paths cannot allocate strings or mutate TMP text in frame loops.
What was done -> Re-confirmed last analyzer artifact summary: `fatalHotPath=0`, `presentationMutableTruthFindings=0`, `uiStringGcRisks=0`, `parseFailures=0`. The analyzer gate scans `.ToString`, concat, `string.Concat`, `new string`, `AppendFormat`, and `TMP_Text.text` in hot UI/presentation methods. Broad `rg` still finds cold/editor/runtime helper strings, but the hot-path AST gate reports zero frame-loop risks.
Cinematic Cheats used -> Helmet/PDA data reads remain flat DTO/scalar/buffer routes; visual richness is shader masks/constants, not managed strings.
Exact Microseconds saved -> 0 us measured. Static UI hot-path GC risk remains 0 by the last analyzer artifact; Unity GC profiler proof not executed.

What was wrong -> Fresh post-PDA analyzer/build proof is required but the build gate is closed.
What was done -> Checked the gate again. CPU was 99.81-100%; one probe saw `dotnet.exe`/`csc.exe`, a later probe saw no compilers but CPU still exceeded 50%, and final probe saw 8 active `dotnet.exe` processes. Per project rule, no `dotnet run` analyzer regeneration and no `dotnet build` were launched after the PDA shader-side patch.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Latest analyzer/build proof remains pending because of CPU gate, not because of a known compile failure.

## 2026-05-23 APEX Re-Audit Pass 05 - PDA No-CPU-Trig Closure

What was wrong -> PDA spectrogram visual geometry had been moved to the vertex shader, but C# still sampled sine waves in Burst jobs for minigame error truth. That was not a rendering leak into simulation, but it violated the stricter user request that PDA effects not burn CPU trigonometry in frame paths.
What was done -> Removed PDA target/player wave NativeArrays, error-output NativeArray, `JobHandle`, Burst jobs, CPU `math.sin`, and `noise.cnoise` drift from `PDADecryptionSpectrogramPanel`. C# now computes a flat scalar error from frequency/amplitude deltas and queues it from `Tick`; `LateFrameTick` consumes the scalar for unlock/feedback and calls `RenderWaveMesh`. Visual sine tubes are generated only in `Hecton_PDA_FrequencyTuningWave.shader` from `_HectonFrequencyTuningWaveScalars` and `_HectonFrequencyTuningWaveLayout`.
Cinematic Cheats used -> PDA waveform/glitch is now a pure shader lie from scalar truth; C# owns only flat DTO-style scalar state.
Exact Microseconds saved -> 0 us measured. Estimate: 2-5 us CPU math/job/fence overhead removed on active PDA tuning frames; combined PDA estimate after segment-buffer and CPU-trig removal is 5-12 us on i3/MX350-class hardware.

What was wrong -> Fresh analyzer/build proof is still required after the no-trig PDA patch.
What was done -> Ran `rg` checks: no PDA `math.sin`, `noise.cnoise`, `FrequencyWave*Job`, wave NativeArray handles, or visual segment-buffer symbols remain in C#; only shader `sin` remains. Ran `git diff --check` for touched files: clean. CPU stayed at 99.81%, so no `dotnet run` analyzer or `dotnet build` was launched.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Fresh Roslyn/build proof remains CPU-gated, not failed.

## 2026-05-23 APEX Re-Audit Pass 06 - Fresh Analyzer and Compile Boundary

What was wrong -> The PDA no-CPU-trig patch needed fresh Roslyn proof after the source changed.
What was done -> Waited for the gate, then regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`. Result: `files=2379`, `runtimeFiles=864`, `simulationFiles=590`, `presentationFiles=274`, `fatalHotPath=0`, `boundaryLeaks=128`, `namespaceLeaks=51`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `2daf5ce9562f97e394be02ec72b26e03f113b8d0940b2d8219f9a2db1dc809f6`.
Cinematic Cheats used -> None; proof artifact only.
Exact Microseconds saved -> 0 us measured. Static hot-path/UI-string/mutable-truth gates are clean; boundary/namespace debt remains cold ownership debt.

What was wrong -> Compile proof after the fresh analyzer did not pass.
What was done -> Ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` after CPU/dotnet/csc gate opened. Build failed in untracked `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs`: unresolved `AupPreShiftSignal`, `AupShiftSignal`, `TimeDilationSignal`, `SimulationPauseSignal`, `BulletTimeVisualSignal`, `CraftingCompletedSignal`, and `SurvivalVitalsChangedSignal`. Inspection found those payload definitions in `GlobalSignals.cs` and `HectonSignalLaneContract.cs`; this is a Core signal-route/project-state dependency, not an X_004 PDA/visor/hull presentation compile error. Later rerun is currently forbidden by CPU >50% or active `dotnet`/`csc`.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Compile proof remains blocked by external Core dependency; no runtime profiler/GC capture was executed.

## 2026-05-23 APEX Re-Audit Pass 07 - Root Coverage and Direct Hot-Lane Purge

What was wrong -> The root-level runtime classification in `Tools/PresentationDecouplingAudit` was too narrow. Root files under `Assets/_Project/Scripts/*.cs` could escape the simulation scan, and a fallback direct scan found real visual work still inside hot lanes.
What was done -> Expanded analyzer roots to include `Assets/_Project/Scripts/` and added PDA/Visor/Lens presentation-name classification. Moved direct hot-lane presentation calls out of `HectonBoidController.Tick`, `MeteorSplashQuadVfx.Tick`, `SeamGapDitherRenderer.Tick`, `PDAInventoryTab.Tick`, and `SargassumMicroFaunaBoids.Tick` into `LateFrameTick`/visual-sync helpers. Direct scanner now reports 0 forbidden presentation calls in `Update`, `FixedUpdate`, tick lanes, or Burst `Execute` bodies.
Cinematic Cheats used -> Boid schools, meteor splash quads, seam dither motes, PDA parallax, and microfauna flocking are treated as visual lies executed after simulation truth.
Exact Microseconds saved -> 0 us measured. Estimate: 6-14 us of pre-visual contention shifted on dirty visual frames.

What was wrong -> Legacy hull/fluid roots still had fixed/post-fixed routes to shader globals, compute dispatch, and particles.
What was done -> `SubmarineStructuralGrid` now queues leak plume, fake crush depth, and hull spark requests from post-fixed paths and flushes them from `LateFrameTick`. `HectonFluidEngine` now queues current water level, ocean wave globals, abyssal flow publication, and cavitation particles; `LateFrameTick` owns shader/UI publication, compute/global binding, and particle emission. Physical cavitation shockwave remains in post-fixed.
Cinematic Cheats used -> Leak plume, crush distortion, cavitation bubbles, ocean wave uniforms, and abyssal flow textures are presentation lies fed by fixed-step scalar/DTO state.
Exact Microseconds saved -> 0 us measured. Estimate: 10-22 us of dirty fixed/post-fixed presentation work shifted to VISUAL_SYNC.

What was wrong -> Several PDA/visor/helmet-adjacent visual paths still used explicit CPU trigonometry/noise.
What was done -> Replaced checked CPU `math.sin`, `math.cos`, `math.sincos`, and `noise.cnoise` in visor mock data, glitch surgeon mock/radar, wrist HUD mock vitals/taps/projector, terminal gaze mock, dynamic decals, and gyro noise with triangle scalars or bounded random vectors. Remaining explicit CPU trig is not hidden: `TopographicalSonarSynthesizer` still has CPU sine/cosine synthesis, and `TerminalOsRuntime` has editor-gizmo sine.
Cinematic Cheats used -> Mock helmet/PDA/visor motion uses cheap scalar waves; shader routes can spend saved CPU budget on richer masks.
Exact Microseconds saved -> 0 us measured. Estimate: 1-4 us on active mock/UI visual frames.

What was wrong -> Fresh Roslyn/helper-chain and compile proof is required after current source edits.
What was done -> `git diff --check` is clean except CRLF warnings. Direct UI/PDA/Visor string scan reports 0 direct `.ToString()`, `string.Concat`, `new string`, `AppendFormat`, `.text =`, or `TMP_Text` local declarations in checked frame/job bodies. Build gate remains closed: CPU >50% or active `dotnet/csc` on the latest probes, so no fresh analyzer/build was launched. Last analyzer artifact is now stale relative to this pass.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Fresh analyzer/build proof is pending, not passed.

## 2026-05-23 APEX Re-Audit Pass 08 - Fresh Root Analyzer Result

What was wrong -> The first broad-root analyzer rerun returned `fatalHotPath=413` because presentation roots were still counted as simulation through `Assets/_Project/Scripts/`.
What was done -> Fixed classifier precedence in `Tools/PresentationDecouplingAudit`: presentation root/name match now excludes simulation classification. Reran the analyzer. Corrected result: `files=2390`, `runtimeFiles=1757`, `simulationFiles=1434`, `presentationFiles=323`, `fatalHotPath=291`, `boundaryLeaks=290`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `43db130df09a25c728557798606a0ccb84bf5a51a74060c4e73c924a8943f3a0`.
Cinematic Cheats used -> None; proof artifact only.
Exact Microseconds saved -> 0 us measured. Global purity is false; residuals are not counted as solved.

What was wrong -> The corrected helper-chain artifact still has 291 global residual fatal routes. Top clusters: `HectonCelestialEngine` 105, `BaseModule` 18, `MapMagicRuntimeBridge` 17, `OrbitalRelativityDirector` 15, `WorldGenerativeGeologySeamExecutionDirector` 15, `HectonPlayerMovement` 15, `WorldProceduralScatterDirector` 10, `SpatialAudioManager` 10, `WorldCaveDirector` 10.
What was done -> Did not suppress or claim clean. Recorded the residual owner queue in `Status_X_004.md` and `Rationale_X_004.md`.
Cinematic Cheats used -> Future cleanup must convert each residual to DTO/signal truth plus VISUAL_SYNC presentation.
Exact Microseconds saved -> 0 us measured. Residual owners need separate route work.

What was wrong -> One remaining X_004-adjacent route was real: `HectonFluidEngine.FixedTick -> QueueSplashdownBubbleRing -> EnsureFluidAdvectionState -> EnsureEmptyFluidAdvectionTexture`, which could create/apply a fallback `Texture3D` in fixed lane.
What was done -> Split fluid advection storage readiness from visual readiness. Fixed/signal queue paths now ensure only NativeArray/GraphicsBuffer storage. `LateFrameTick`/cold init owns fallback `Texture3D.SetPixel`, `Texture3D.Apply`, and RTHandle allocation through `EnsureFluidAdvectionVisualState`.
Cinematic Cheats used -> Fluid advection visual fallback texture is a render-graph lie, not fixed simulation state.
Exact Microseconds saved -> 0 us measured. Estimate: 2-4 us first-use fixed-frame allocation/apply risk shifted to VISUAL_SYNC.

What was wrong -> Fresh analyzer/build proof after the fluid split is required.
What was done -> Gate closed again: CPU 100% with active `dotnet/csc`. No rerun/build launched.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 us measured. Latest artifact is stale by the fluid split; compile remains unverified after this pass.

## 2026-05-23 APEX Re-Audit Pass 09 - Status Truth Correction

What was wrong -> `Status_X_004.md` still contained one stale green summary line from the earlier narrow analyzer run, while the corrected root-coverage helper-chain analyzer already disproved global purity with `fatalHotPath=291`.
What was done -> Re-read status/rationale, checked the current gate, and corrected the task/verification status text. Current gate remains closed: CPU 73% with active `dotnet`, so no analyzer or build was launched. The direct hot-method body scan remains clean, but the global helper-chain gate is explicitly not clean.
Cinematic Cheats used -> None; documentation truth correction only.
Exact Microseconds saved -> 0 us measured. Prevents false green reporting; runtime proof remains blocked by the build gate.

## 2026-05-23 APEX Re-Audit Pass 10 - Mixed Root Visual-Sync Rework

What was wrong -> Stale helper-chain evidence and source inspection showed real mixed-owner leaks after the root scan: `BaseModule` hot lanes queued water/flood/leak/hum visuals too late in the chain, `AcousticZoneController.Tick` still reached ambient `AudioSource` state, `HectonPlayerMovement` fixed/tick chains touched brine/VR shader globals plus particles/audio/camera/silt feedback, and `Fabricator.SlowTick` reached renderer/material/audio/particle/error feedback helpers.
What was done -> Added or extended `ILateFrameTickable` queues. `BaseModule` queues leak, flood, oxygen hum, and module water shader uploads; `AcousticZoneController` queues ambient loop and source graph state; `HectonPlayerMovement` queues brine shader globals, VR comfort globals, water/splash/bubble particles, one-shot audio, camera impulse scalars, and silt burst intensity; `Fabricator` queues assembly preview commands, sparks/welding loop state, error feedback material blocks, and craft one-shot audio. Hot paths now stage data; VISUAL_SYNC flushes Unity presentation APIs.
Cinematic Cheats used -> Physical truth remains scalar/DTO-driven. Brine fog, VR comfort, sparks, welding, bubbles, splash rings, camera shake, and error flashes are visual/audio lies applied after simulation.
Exact Microseconds saved -> 0 us measured. Estimate: 24-49 us of dirty-frame/slow-frame presentation contention shifted from patched mixed roots on i3/MX350-class hardware.

What was wrong -> Fresh Roslyn/helper-chain and compile proof are still required, but the project gate is closed.
What was done -> Ran source-only direct gates. Patched-file hot-method scan reports `TARGET_DIRECT_HOT_PRESENTATION_COUNT=0`. Project candidate direct scan reports `DIRECT_HOT_PRESENTATION_COUNT=0` after excluding `PerformanceMonitor.Tick -> Stopwatch.Stop()` as a non-presentation false positive. UI/PDA/visor string scan remains 0 for direct `.ToString()`, concat, `new string`, `AppendFormat`, `.text =`, and `TMP_Text` in checked frame/job bodies. `git diff --check` is clean except CRLF normalization warnings. Analyzer/build not launched because CPU=100 with active `dotnet` and `csc`.
Cinematic Cheats used -> None; proof-gate work only.
Exact Microseconds saved -> 0 us measured. The last Roslyn artifact remains stale and globally not green until the CPU/compiler gate opens.

## 2026-05-23 APEX Re-Audit Pass 11 - Current-Source Helper-Chain Tightening

What was wrong -> Current source still had verified helper-chain leaks after the previous mixed-root pass: atmosphere sun/cycle shader commits from slow timeline helpers, BaseModule pressure/reef presentation from slow/fixed helpers, AcousticZoneController mixer routing plus diagnostic `string.Concat`, ScanMarker Tick draw/material commits, MapMagic terrain fade/shadow shader publishing and runtime MapMagic fencing from SlowTick, and terrain seam shader clear from SlowTick.

What was done -> Rewired these routes to VISUAL_SYNC. `HectonAtmosphereManager` now queues sun pose, sun-direction shader, cycle globals, and giant abyss light for `LateFrameTick`. `BaseModule` queues pressure visual scale/rotation and reef proxy activation for `LateFrameTick`. `AcousticZoneController` queues ambient mixer routing and uses constant diagnostic strings. `HectonScanMarkerSystem` keeps Tick for marker timer aging only; material updates and `Graphics.DrawMeshInstanced` run in `LateFrameTick`. `MapMagicRuntimeBridge` queues planetary canvas shader globals and MapMagic generation fencing for `LateFrameTick`. `WorldGenerativeGeologyTerrainSeamApplier` queues blend-mask shader clear for `LateFrameTick`.

Cinematic Cheats used -> Sky, terrain fade/shadow, scanner markers, pressure squeeze, reef infestation visuals, and acoustic routing are presentation lies staged from scalar/DTO state. No gameplay truth was moved into shaders or read back from GPU.

Exact Microseconds saved -> 0 us measured. Estimate: 22-52 us shifted out of dirty slow/tick frames on i3/MX350-class hardware.

What was wrong -> Fresh proof is still bounded. The stale Roslyn artifact remains globally non-green (`fatalHotPath=291`), and subagent evidence still lists residual owners outside this patch set.

What was done -> Ran targeted helper-chain source scan: `HectonAtmosphereManager.cs`, `BaseModule.cs`, `AcousticZoneController.cs`, `HectonScanMarkerSystem.cs`, and `MapMagicRuntimeBridge.cs` report `HELPER_HOT_PRESENTATION_METHOD_COUNT=0`; `WorldGenerativeGeologyTerrainSeamApplier.cs` has one conservative scanner hit in an editor-only upload branch after the runtime early return, while the runtime clear leak is fixed. `git diff --check` is clean except CRLF normalization warnings. Analyzer/build were not launched because CPU=99%, above the explicit gate.

Cinematic Cheats used -> None; verification/status work only.

Exact Microseconds saved -> 0 us measured. Remaining project-wide purity is not proven.

## 2026-05-24 APEX Re-Audit Pass 12 - Current-Source 25-File VISUAL_SYNC Sweep

What was wrong -> The old report was not enough. Current source still had helper-chain routes from hot lanes to presentation APIs across audio, UI, world generation, survival/inventory shader scalars, interaction transforms, cave/scatter/seam systems, and fluid GPU dispatch.

What was done -> Reworked 25 current presentation-boundary files. Hot lanes now queue scalar/DTO/pose requests; `LateFrameTick` flushes Unity presentation work. Key moves: `SpatialAudioManager` drains audio source changes in late frame; `HectonFabricatorUI` hides recipe list in late frame and keeps runtime UI string gate clean; `HectonFluidEngine` queues GPU buoyancy dispatch out of fixed tick; `BaseModule` queues interior light toggles; `PlayerToolManager`, `PhysicalSnapSwitch`, `PhysicalBatteryCompartment`, `SkySystemFollowCamera`, and `DemoFirstPersonController` queue transform/UI poses; `HectonSurvivalSystem` and `PlayerInventory` queue shader scalars; `WorldCaveDirector`, `WorldGenerativeGeologyTerrainSeamApplier`, `WorldProceduralScatterDirector`, and `HectonWorldGenerator` now defer cave dressing, seam blend uploads, scatter reconcile/create/apply, and renderer disables to VISUAL_SYNC.

Cinematic Cheats used -> Cave dressing, terrain seam blend masks, scatter placement visuals, tool/cockpit poses, UI visibility, narcosis/rust shader scalars, GPU buoyancy sampling, and audio pitch/source updates are all treated as presentation lies fed by CPU-owned truth. No shader readback or animation state owns health, flooding, inventory, or physics truth.

Exact Microseconds saved -> 0 us measured. Estimate: 60-125 us of dirty-frame presentation contention shifted out of update/fixed/slow lanes on i3/MX350-class hardware. Runtime profiler not executed.

What was wrong -> Fresh proof before the second world patch still reported global helper-chain debt.

What was done -> Ran `PresentationDecouplingAudit.exe`: `files=2394`, `runtimeFiles=1761`, `simulationFiles=1437`, `presentationFiles=324`, `fatalHotPath=32`, `boundaryLeaks=291`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `59718adf36b7ce49672f59f3557419ba26e0b851907a53a2c4a3a71b29967927`. Then patched the cave/terrain/scatter/world-generator residuals covered by X_004 and ran source helper-chain proof for 15 touched files: `PATCHED_SOURCE_HELPER_HITS=0`. Runtime UI string scanner excluding editor windows: `RUNTIME_UI_STRING_FRAME_HITS=0`.

Cinematic Cheats used -> None; proof and routing work only.

Exact Microseconds saved -> 0 us measured. Final project-wide Roslyn rerun and compile are pending because active `dotnet/VBCSCompiler` processes blocked the explicit build gate.

## 2026-05-24 APEX Re-Audit Pass 13 - Dispatcher/Foveated/Origin Residual Purge

What was wrong -> The post-32 residual source still had phase leaks outside the world patch set: `FoveatedSimulationManager` scheduled visual transform interpolation from `ScheduleFrameJobs`, foveated doppler protection wrote `AudioSource` pitch/doppler during `BeginDispatcherFrame`, `SystemDispatcher.PublishSimulationBucketSync` wrote a shader global before VISUAL_SYNC, `RequestVisualStaticGlitch` wrote shader globals from public request sites, `GlobalPhysicsStateManager` redundantly wrote `body.transform.SetPositionAndRotation`, and `HectonFloatingOrigin` wrote scene root transforms from `IJobParallelForTransform.Execute`.

What was done -> Added `IFoveatedDispatcher.VisualSyncTick` and moved foveated pose interpolation plus audio pitch/doppler to the dispatcher late-frame path. Staged simulation-bucket interpolation alpha and visual-static glitch requests as scalars, with shader writes flushed in VISUAL_SYNC. Removed the redundant physics transform write while preserving rigidbody position/rotation and `PublishTransform`. Reworked floating-origin shift so `Tick` queues the immediate shift and `LateFrameTick` starts the frame-locked AUP/vault/root-transform rebase; removed the root transform access job.

Cinematic Cheats used -> Foveated visual interpolation, doppler pitch protection, pause/static shader scalars, and origin-shift scene presentation are late-frame lies fed by immutable simulation/world state. Physics truth remains rigidbody/vault-owned; no shader or animation readback drives gameplay.

Exact Microseconds saved -> 0 us measured. Estimate: 7-16 us shifted or removed on dirty foveated/origin/simulation-bucket frames on i3/MX350-class hardware. `git diff --check` is clean except CRLF warnings. Fresh Roslyn/build proof was not launched because latest CPU probe was 99.81%, above the explicit project gate.

## 2026-05-24 APEX Re-Audit Pass 14 - Structural/Encounter/XR/WFC/Thruster Closure

What was wrong -> Current source still had real hot presentation routes after the previous residual purge. `ConstructionManager.SlowTick -> HabitatGraphManager.ApplyHydrodynamicStress` reached habitat stress shader globals and module stress buffer upload. `HectonDirectorAI.Tick -> EncounterDirector.Advance` reached predator AUP `GraphicsBuffer` upload and shader binding. `SystemDispatcher.RunDispatcherUpdate -> HectonXRRuntimeState.RefreshFrameState` wrote XR shader globals before simulation. `LaserCutter.ToolTick -> WfcLaserCutRuntime.TryApplyDoorCut` wrote WFC cut shader globals while changing sealed-door truth. `PlayerThrusterAudio.Tick` wrote `AudioSource.volume/pitch`.

What was done -> Converted each route to pending scalar/DTO state and VISUAL_SYNC flush. `HabitatGraphManager.FlushVisualSync` owns vibration, emergency, analytical stress, and module stress GPU publication from `ConstructionManager.LateFrameTick`. `EncounterDirector.FlushPredatorAupVisualSync` owns A/B predator AUP buffer upload/bind from `HectonDirectorAI.LateFrameTick`. `HectonXRRuntimeState` now queues foveation/origin/cadence/pose vectors and `SystemDispatcher.RunDispatcherLateFrame` flushes them. `WfcLaserCutRuntime.FlushVisualSync` commits laser cut globals from dispatcher late frame. `PlayerThrusterAudio` now implements `ILateFrameTickable`; tick computes audio scalars only, late frame applies source volume/pitch.

Cinematic Cheats used -> Habitat deformation, predator pressure masks, XR comfort/foveation state, WFC cut sphere/molten heat, and thruster mix state are presentation lies fed by scalar/DTO truth. Simulation owns health, flooding, encounter state, sealed-door progress, and movement; GPU/audio sinks only consume.

Exact Microseconds saved -> 0 us measured. Estimate: 16-34 us shifted out of dirty structural/AI/XR/tool/audio frames on i3/MX350-class hardware. Direct project hot-lane scanner now reports `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`; runtime UI/PDA/Visor string scanner reports `RUNTIME_UI_STRING_FRAME_HITS=0`; `git diff --check` is clean except CRLF normalization warnings. Fresh Roslyn/build proof was not launched because the gate reclosed at CPU 96% with active `dotnet`, `csc`, and `VBCSCompiler`.

## 2026-05-24 APEX Re-Audit Pass 15 - Bridge/Ecology/Fluid Helper Closure

What was wrong -> Direct hot-lane scans were clean, but source inspection still found helper-chain leaks. `HectonShaderGlobalDataVaultBridge` could write shader globals from dispatcher-inactive publish fallbacks, `EcosystemDirector` could publish biolum flash and biomass overgrowth globals from ecology/service helpers, and `HectonFluidEngine` still had fixed/storage paths that could complete splashdown GPU uploads, create advection graphics buffers, or upload advection particle buffers before VISUAL_SYNC.

What was done -> Rewired the routes to late-frame ownership. Bridge publish methods now update Vault/read mirrors and set a fallback dirty bit; `SystemDispatcher.RunDispatcherLateFrame` calls `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync`. `EcosystemDirector` stages biolum/biomass scalar packets and flushes them from `FlushQueuedEcosystemVisuals`. `HectonFluidEngine` now keeps fixed/storage routes native-only; `LateFrameTick` owns splashdown impulse completion/upload, `EnsureFluidAdvectionVisualState`, and `FlushFluidAdvectionGpuUploads` into A/B buffers.

Cinematic Cheats used -> Biolum flash, biomass overgrowth, shader fallback state, splashdown impulse field, bubbles, debris, and silt advection are GPU/audio/visual lies fed by scalar/native DTO truth. Simulation remains blind to shader state and never reads back GPU presentation.

Exact Microseconds saved -> 0 us measured. Estimate: 12-27 us shifted out of dirty bridge/ecology/fluid frames on i3/MX350-class hardware. Current gates: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`, `UI_STRING_CANDIDATE_FILES=60`, `RUNTIME_UI_STRING_FRAME_HITS=0`, `TARGETED_HELPER_FORBIDDEN_HITS=0`; `git diff --check` is clean except CRLF warnings. Fresh Roslyn/build proof remains blocked by CPU=100%; Unity runtime/profiler proof not executed.

## 2026-05-24 APEX Re-Audit Pass 16 - UI/Ocean/Fluid Residual Slice

What was wrong -> The wide source-only helper callgraph still found real current-source routes after Pass 15. `HectonFluidEngine.FixedTick` could reach GPU buffer creation through native reallocation. `ShinobuOceanSurfaceAtmosphereRuntime.SlowTick` could create wave graphics/readback buffers and upload wave payloads. PDA inventory/loadout/data-log ticks could reach TMP/char-array refresh helpers. Vehicle cockpit tick could retry radar graphics creation, upload/dispatch sonar, update offscreen TMP metrics, and apply material/camera presentation state.

What was done -> Moved the slice to VISUAL_SYNC. HectonFluid native reallocation no longer creates GPU buffers. Ocean SlowTick no longer ensures graphics/readback buffers or uploads wave payloads; LateFrame owns that work. PDA inventory/loadout/data-log ticks now stage dirty flags, timers, and deltas only; LateFrame owns text, hologram, and visual refresh. Vehicle cockpit tick now computes state and schedules jobs only; LateFrame owns radar graphics retry, sonar upload/dispatch, offscreen text, material state, and offscreen camera state.

Cinematic Cheats used -> Ocean waves, PDA text/holograms, loadout mass pulse, cockpit sonar/radar/offscreen metrics, and fluid GPU buffer readiness are presentation lies fed by scalar/native DTO truth. Fixed/update lanes keep physics, inventory, power, and weather truth; GPU/UI sinks consume after simulation.

Exact Microseconds saved -> 0 us measured. Estimate: 18-39 us shifted out of dirty fixed/slow/UI frames on i3/MX350-class hardware. Current gates: `TARGETED_UI_ENV_CALLGRAPH_HOT_PRESENTATION_COUNT=0`, `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`, `UI_STRING_CANDIDATE_FILES=60`, `RUNTIME_UI_STRING_FRAME_HITS=0`; wide source-only helper callgraph remains non-green at `SOURCE_HELPER_HOT_PRESENTATION_COUNT=139`; `git diff --check` is clean except CRLF warnings. Fresh Roslyn/build proof remains blocked by CPU=79%; Unity runtime/profiler proof not executed.
## X_004 Pass 17 - Loop 28 Audio/Visor/Scatter VISUAL_SYNC Cleanup

What was wrong:
- Acoustic tick routes still queued/played transition, storm, vegetation, sonar, manta, and fatal-pressure audio through helpers; player lookup could resolve `AudioSource`/`AudioListener` from Tick.
- Adaptive stem audio finalized jobs and wrote Unity `AudioSource`/filter/playback state from Tick.
- Dynamic resolution Tick committed runtime render scale, shader globals, and buffer scale.
- Spectrum sonar, underwater visuals, marine snow, GPU scatter, flora tint, VR focus, material decay, stress VFX, and camera speed lines still had helper chains from update/slow lanes to shader/material/compute/audio/draw sinks.
- `PlayerThrusterAudio.Tick` still matched direct hot-lane audio because it referenced audio-source identifiers and clip creation.

What was done:
- Moved acoustic one-shots, ambient source routing/defaults, and fatal-pressure audio event emission to `AcousticZoneController.LateFrameTick`.
- Changed `AdaptiveStemAudioMixer` job completion to copy a pending mix DTO; Unity audio writes now flush from late frame.
- Changed `ThermalDynamicResolutionAdapter` Tick to queue render-scale/runtime/global commits; late frame owns `DynamicResolutionHandler`, `ScalableBufferManager`, shader globals, and telemetry publication.
- Converted `SpectrumSystem`, `HectonUnderwaterVisuals`, `HectonMarineSnowRenderer`, and `GpuScatterLodManager` Tick into delta accumulators with their former visual work executed from `LateFrameTick`.
- Moved flora tint, VR focus, material-decay globals, player stress globals/audio, and camera speed-line particle updates to late frame queues.
- Removed direct audio-source references and clip creation from `PlayerThrusterAudio.Tick`.

Cinematic Cheats used:
- Audio and visual systems now consume flat scalar/DTO snapshots and dirty bits. Simulation/update lanes do timing/math only; GPU/audio presentation lies happen after truth has settled.
- Marine snow, scatter, spectrum, underwater fog/sky/material work stay GPU-owned and cadence-scalable by continuous quality state instead of mutating simulation ownership.

Exact Microseconds saved:
- Estimated 38-86 us shifted out of dirty update/slow frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `TARGETED_EXPANDED_CALLGRAPH_HOT_PRESENTATION_COUNT=0`; `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`; `UI_STRING_CANDIDATE_FILES=262`; `RUNTIME_UI_STRING_FRAME_HITS=0`; `git diff --check` clean except CRLF normalization warnings.
- Roslyn/build proof not rerun: CPU gate closed at 73%, no active compiler rows.

## X_004 Pass 18 - Loop 29 Music Director VISUAL_SYNC Closure

What was wrong:
- `HectonMusicDirector.Tick/SlowTick` reached bed and stinger `AudioSource` operations through helper chains: configure, stop, play, volume, clip, loop, and mixer routing.
- Direct body scan could miss this because the Unity audio calls were behind music-state helper methods.

What was done:
- Added late-frame ownership to `HectonMusicDirector`.
- Converted Tick into a pending music delta accumulator.
- Converted SlowTick into a pending context-reevaluation flag.
- Existing fade, selection, override, bed, and stinger logic now runs from `LateFrameTick`, so Unity audio writes no longer originate from update/slow lanes.

Cinematic Cheats used:
- Music truth is contextual scalar/state routing, not simulation authority. Late-frame audio presentation consumes the same context and can scale layering by quality without touching gameplay state.

Exact Microseconds saved:
- Estimated 6-14 us shifted out of dirty music update/slow frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `TARGETED_AUDIO_VISUAL_CALLGRAPH_HOT_PRESENTATION_COUNT=0`; `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`; `git diff --check` clean except CRLF normalization warnings.
- Roslyn/build proof not rerun: CPU gate closed at 100% with active `dotnet` and `csc`.

## X_004 Pass 19 - Loop 30 Tool/Audio/Debris VISUAL_SYNC Closure

What was wrong:
- `BuilderTool`, `FlashlightTool`, and `ScannerTool` still had tool-frame helper paths to material-property-block reads/writes.
- `LaserCutter.UsePrimary` still played the overheat cue through `PlayStatic2D`; laser line/audio/decal/heat sinks needed late-frame ownership.
- `RepairTool.UsePrimary` still reached beam line/light/audio/particle helpers through tool-use paths.
- `PlayerBuilder.ToolTick` could route rotate/snap/build/error cues to `PlayStatic2D`.
- `AudioLogSystem.SlowTick` could start queued playback through direct audio/narrative-radio calls.
- `DynamicMusicGranularSynthesizer.SlowTick` configured the Unity host `AudioSource`.
- `CarveDebrisComputeRenderer.Tick` ran compute dispatch and `RenderMeshIndirect`.

What was done:
- Added or extended `ILateFrameTickable` queues across the tool slice; hot paths now write pending scalar/clip/pose state only.
- Moved builder one-shots into fixed pending clip slots drained by `PlayerBuilder.LateFrameTick`.
- Moved audio-log playback clip, volume, bit-crush preference, and interference scalar into a late-frame playback packet.
- Changed DynamicMusic SlowTick to dirty host-source config only; LateFrame applies Unity host source state.
- Changed CarveDebris Tick to update Vault/mirror/black-box state and queue dt/quality/capacity; LateFrame ensures GPU state, dispatches compute over A/B buffers, binds buffers, and renders indirect debris.

Cinematic Cheats used:
- Tool beams, heat, decals, repair sparks, audio logs, dynamic music host state, and carve debris are presentation lies fed by scalar/DTO state. Simulation/tool frames no longer touch Unity render/audio sinks in the checked routes.
- Carve debris now keeps the A/B `GraphicsBuffer` flip inside VISUAL_SYNC: Tick produces CPU/native truth; LateFrame binds read/write buffers, dispatches clear/advect/cull, toggles parity, and renders the promoted read buffer without CPU readback.

Exact Microseconds saved:
- Estimated 34-73 us shifted out of dirty tool/audio/debris frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `CURRENT_AUDIO_TOOL_DEBRIS_CALLGRAPH_HOT_PRESENTATION_COUNT=0`; `git diff --check` clean except CRLF normalization warnings.
- Roslyn/build proof not rerun: CPU gate closed at 90% despite no active compiler rows.

## X_004 Pass 20 - Loop 31 Diegetic Panel Direct-Frame Cleanup

What was wrong:
- Project direct hot-lane scan still caught `DiegeticPanelController.Tick` touching UI presentation state through panel-camera enable and cursor visibility/pose routes.
- This is not simulation truth, but it was still executing from an `ITickable` lane instead of the visual sync lane.

What was done:
- Added pending state for cursor visibility, cursor pose, and panel-camera enabled state.
- `Tick` now computes interaction state and queues cursor/camera presentation changes.
- `LateFrameTick` applies `cursorTransform.SetPositionAndRotation`, cursor `Graphic/Renderer/Collider.enabled`, panel-camera enable changes, and then runs the existing phosphor/material refresh.

Cinematic Cheats used:
- The panel cursor and camera state are now visual output only. Interaction math still produces flat panel DTO/input events; the visible cursor and render-camera toggles are late-frame lies over that truth.

Exact Microseconds saved:
- Estimated 3-8 us shifted out of active diegetic-panel tick frames. Not profiler-measured.
- Proof: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0` after excluding `PerformanceMonitor.Tick -> Stopwatch.Stop()` as non-presentation; `git diff --check` clean except CRLF normalization warnings.
- Roslyn/build proof not rerun: CPU gate closed at 68%.

## X_004 Pass 21 - Loop 32 Subagent Residual Closure

What was wrong:
- `HectonBiolumManager.Tick` still reached biolum master/ripple/flora shader publication and `GraphicsBuffer` upload helpers.
- `AbyssalThermalManager.SlowTick -> RebuildVentField` still called vent-buffer upload and smoke particle reset.
- `InternalFloodWaterlineRuntime.FastTick` wrote internal-waterline shader globals.
- `SubmarineStructuralGrid.PostFixedTick` could route breach updates to `AbyssalFluidDecals.RegisterPressureSpray`.
- `HectonSurfaceWeatherDirector.Tick` drove surface splash and weather VFX rig state.
- `FaunaBrain.SlowTick` could write infection material vectors.
- `PDAInventoryTab` used `ToLowerInvariant()` and dynamic char-buffer growth in refresh paths; active visor/HUD/compositor copy helpers could grow lists; `VisorHUDController.Tick` still owned BIOS font scan/material commit.

What was done:
- Added/extended `LateFrameTick` ownership for biolum shader globals/ripple A/B buffer upload, thermal smoke topology upload/reset, internal flood-waterline globals, breach pressure-spray decals, surface-weather VFX, fauna infection visuals, and visor BIOS/material commits.
- Replaced PDA lowercase matching with ordinal-ignore-case search and replaced dynamic char growth with a preallocated fallback buffer plus bounded description copy.
- Added capacity guards before active HUD/controller/compositor `List.Add` calls.

Cinematic Cheats used:
- Flood, weather, fauna infection, biolum ripples, and breach sprays now remain scalar/DTO truth in simulation lanes; GPU/material/decal presentation lies are consumed only in VISUAL_SYNC.

Exact Microseconds saved:
- Estimated 29-67 us shifted/avoided on dirty weather/flood/fauna/visor/PDA frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; `TARGET_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; PDA/visor targeted allocation grep has no `ToLowerInvariant()` or dynamic `new char[capacity]`; active HUD/controller/compositor `results.Add(...)` sites now have capacity guards; `git diff --check` clean except CRLF normalization warnings.
- Roslyn/build proof not rerun: CPU 85% and active `dotnet`/`csc` rows.

## X_004 Pass 22 - Loop 33 Async Buoyancy Readback VISUAL_SYNC Closure

What was wrong:
- `AsyncBuoyancyReadbackRuntime.PreSimulationTick` still submitted GPU readback work directly.
- The path uploaded request/wave DTOs into `GraphicsBuffer`, read shader globals, configured and dispatched the wave-height compute shader, and issued `AsyncGPUReadback.Request` before the simulation phase.

What was done:
- `PreSimulationTick` now only prepares request DTOs, tuning/counters, mock eligibility, and a visual-sync dispatch flag.
- `VisualSyncTick` now calls `FlushQueuedGpuReadbackDispatch`, which owns the `GraphicsBuffer` upload, shader-global reads, compute dispatch, and async readback request.
- Simulation now consumes previous completed snapshots with `ConsumeGpuReadbacksNoWait` or the existing mock path when the previous visual-sync dispatch proved GPU unavailable.

Cinematic Cheats used:
- Buoyancy truth remains CPU/Vault snapshot state. The GPU samples waves asynchronously as a visual-sync service, and the physics lane never blocks on the current-frame GPU result.
- The existing triple-buffer request/wave buffers remain the handoff mechanism; no readback fence or same-frame wait was introduced.

Exact Microseconds saved:
- Estimated 8-19 us shifted out of pre-simulation GPU API contention on i3/MX350-class hardware. Not profiler-measured.
- Proof: `ASYNC_BUOYANCY_DIRECT_HOT_PRESENTATION_COUNT=0`; project direct scan with `PreSimulationTick` included reports `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`.
- Roslyn/build proof not rerun: CPU gate remains closed at 71% despite no active compiler rows.

## X_004 Pass 23 - Loops 34-35 Current-Source Presentation Boundary Closure

What was wrong:
- PDA/helmet text buffers still had rare dynamic fallback growth paths.
- `ShinobuEcosystemBalancer.EnsureVaultState` could force GPU upload-capacity allocation outside VISUAL_SYNC.
- `DebrisManager.Tick` still rendered active chunks.
- Flora, GPR, sargassum collapse/debris, cave voxel lighting, crest damping, wreck material registry, indirect vegetation renderer, and cut manager still had tick/slow helper routes to material/particle/GPU resource work.
- `SargassumMicroFaunaBoids.SlowTick` reached `EnsureBuffers`, threat-grid `GraphicsBuffer` upload, kernel validation, and origin-shift compute dispatch through helpers.
- `HectonMapMagicVegetationBridge.Tick/SlowTick` reached terrain `AsyncGPUReadback`, active vegetation `GraphicsBuffer` upload/bind, vegetation-audio shader globals, and audio mixer writes.
- `DepthZoneDirector.SlowTick` pushed depth-zone events, narrative discovery, and HUD notifications directly.

What was done:
- Replaced PDA/HUD dynamic char growth with bounded preallocated fallback buffers.
- Moved ecosystem GPU upload capacity, debris render, flora wake/cascade uploads, GPR rebind, sargassum particles/silt, cave resources, crest facade refresh, wreck visibility upload, indirect vegetation visual tick, and cut-mask resource refresh into `LateFrameTick`.
- Split MicroFauna hot lanes into DTO/dirty flags; `LateFrameTick` owns `EnsureBuffers`, threat-grid upload, kernel validation, A/B boid uploads, origin-shift dispatch, and render.
- Split MapMagic tick/slow into request flags; `LateFrameTick` owns deferred tile cache disposal, startup tile processing, terrain cache validation/readback, active vegetation buffer upload/bind, vegetation-audio shader globals, and mixer commits.
- Converted DepthZone to `ILateFrameTickable`; slow lane owns zone truth/cooldown, late frame owns events/notifications/logging.

Cinematic Cheats used:
- Terrain cache, vegetation audio, MicroFauna, depth-zone HUD, and sargassum/cave/wreck visuals now consume scalar/native DTO truth in VISUAL_SYNC. Simulation and slow lanes no longer present the scene directly.
- MicroFauna uses queued A/B buffer publication; gameplay truth remains in Vault/native arrays and does not wait on same-frame GPU output.

Exact Microseconds saved:
- Estimated 75-173 us shifted/avoided across dirty PDA/helmet/ecology/debris/flora/GPR/sargassum/cave/crest/wreck/indirect/MapMagic/MicroFauna/DepthZone frames. Not profiler-measured.
- Proof: `MICROFAUNA_HELPER_HOT_PRESENTATION_COUNT=0`; `TARGETED_WORLD_HELPER_HOT_PRESENTATION_COUNT=0`; `TARGET_PATCHED_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`.
- Scoped `git diff --check` for 17 X_004 source files is clean except CRLF normalization warnings. Whole-repo diff check is contaminated by unrelated dirty `.meta`/docs trailing whitespace.
- Build/Roslyn not rerun: CPU gate stayed closed after wait (`CPU=100`; no active compiler rows returned).

## X_004 Pass 24 - Loop 36 Compile-Wall Triage

What was wrong:
- After the CPU gate opened, `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` failed in `MathLodApproximation.cs`.
- The job wrote `result.WorstOutput`, but `MathLodTortureResult` had no `WorstOutput` field; offset 56 was padding.

What was done:
- Restored `MathLodTortureResult.WorstOutput` as a `float` at `[FieldOffset(56)]`.
- Kept the struct at the existing 64-byte size; offset 60 remains padding.

Cinematic Cheats used:
- None. This was a compile-wall dependency fix, not a presentation behavior change.

Exact Microseconds saved:
- 0 us claimed. This unblocks telemetry compile compatibility only.
- Scoped `git diff --check` for `MathLodApproximation.cs` is clean except CRLF warning.
- Build rerun not launched: active compiler rows remained (`dotnet` pid 34348, `csc` pid 37920), and WMI CPU query returned `Access denied`; the required CPU/compiler gate was not provably clean.

## X_004 Pass 25 - Loops 37-38 Resource Interaction and Project Direct Gate

What was wrong:
- `SealedDoor`, `ScannableFragment`, `InteractionHighlighter`, `HarvestablePlant`, `HarvestableOutcrop`, `OxygenBubble`, `Floater`, and `OxygenPlant` still had current-source routes where hot interaction/tick helpers could reach MPB, renderer state, audio, particle, component-disable, or pooled scene release work.
- `OxygenPlant.Tick/ForceRelease` directly sampled `spawnPoint.position` and called `ObjectPoolService.Spawn`, so even a pooled gameplay object release was still scene/pool work in the tick lane.
- A broad direct scan of all runtime C# files exceeded 120 seconds, so the proof needed a narrower candidate-first pass instead of a partial timeout.

What was done:
- Added or extended `ILateFrameTickable` queues for sealed doors, scannable fragments, interaction highlights, harvestables, oxygen bubbles, floaters, and oxygen plants.
- Hot methods now write scalar/DTO/pending bits only. `LateFrameTick` owns renderer/MPB commits, scan render registry sync, audio, particles, debris VFX, fragment disable/despawn, and oxygen bubble pooled release.
- `OxygenPlant` now stores a bounded release count (`MaxPendingBubbleReleases=4`); late frame samples the spawn transform, calls the pool, and plays release audio.
- Removed a dead `Floater` comment containing a forbidden `renderer.enabled` sample because it polluted source proof without executable value.

Cinematic Cheats used:
- Interaction truth stays in health/progress/timer/state fields. Visual/auditory feedback is a late-frame lie driven by queued scalars and fixed pending bits.
- Oxygen plant release is delayed by at most one frame to move scene/pool presentation work out of the tick phase.

Exact Microseconds saved:
- Estimated 18-44 us shifted out of dirty interaction/resource frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `TARGET_RESOURCE_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; `RESOURCE_HELPER_HOT_PRESENTATION_COUNT=0`; `PROJECT_FORBIDDEN_TOKEN_CANDIDATE_FILES=437`; `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; `RUNTIME_UI_STRING_FRAME_HITS=0`; `HULL_FLOOD_DEFORM_DIRECT_HOT_PRESENTATION_COUNT=0`.
- Scoped `git diff --check` for the latest 8 resource files is clean except CRLF normalization warnings.
- Build/Roslyn not launched: latest gate check is `CPU=100` with active `dotnet` pid `18664`; this violates the repository gate.

## X_004 Pass 26 - Loops 39-40 Fluid, Atmosphere, UI, World Visual-Sync Closure

What was wrong:
- `HectonFluidEngine.FixedTick` still had helper reachability to GPU buoyancy readback consume and graphics-buffer release decisions. That is a physics/fixed phase touching GPU readback state.
- `AsyncBuoyancyReadbackRuntime.ScheduleSimulation` still consumed GPU readbacks from the simulation scheduling path.
- `SubmarineAtmosphereSystem.PostFixedTick` still reached room oxygen HUD, smoke overlay, visor pulse/distortion, audio log, pressure screech, and base-module overheat visual state.
- `HectonSurfaceWeatherDirector` still allowed thunder playback and storm visor/flashlight pulse queuing before the VISUAL_SYNC sink.
- PDA shell and suit HUD still had rare dynamic text-buffer growth paths; beacon light, hazard indicators, hostile flora aim/audio, culling renderer state, impostor scene state, voxel stale-volume despawn, world-generator material restore, destructible organic audio, and carve-debris GPU uploads still needed explicit late-frame ownership.

What was done:
- Fluid GPU releases now accumulate a release bit-mask and drain from `LateFrameTick`; buoyancy readback consume is queued in fixed/storage lanes and flushed in late frame. Async buoyancy now consumes completed GPU snapshots only in `VisualSyncTick`, then dispatches new readback work.
- Atmosphere now registers as `ILateFrameTickable`; post-fixed paths store scalar/pending state and `LateFrameTick` owns UI/audio/visor/smoke/base visual writes.
- Weather thunder and storm-equipment pulses now queue scalar state and flush in `LateFrameTick`.
- PDA chrome buffers are fixed capacity and truncate instead of allocating; suit HUD runtime gauge values use preallocated buffers and late-frame refresh.
- Beacon light writes, hazard MPB commits, hostile flora bone rotation/audio, culling renderer flags, impostor material/object toggles, voxel stale despawn, destructible organic audio, and carve-debris buffer uploads/dispatch/render moved to late-frame presentation drains.
- `HostileFlora` attack truth now uses logical rotation, not `aimingBone.forward`; animation is output only.

Cinematic Cheats used:
- Simulation/fixed/pre-sim paths now publish scalar/native DTO intent; VISUAL_SYNC turns that intent into shader globals, A/B `GraphicsBuffer` uploads, audio one-shots, renderer flags, and object-pool presentation.
- GPU readbacks are previous-frame snapshots only. No fixed/pre-sim path blocks for same-frame visual data.
- UI text refresh uses bounded character buffers and truncation rather than dynamic string growth.

Exact Microseconds saved:
- Estimated 73-167 us shifted or avoided across dirty fluid/atmosphere/weather/UI/world/resource frames on i3/MX350-class hardware. Not profiler-measured.
- Proof: `PATCHED_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`, `PATCHED_GPU_HOT_COUNT=0`, `PATCHED_UI_STRING_FRAME_COUNT=0` across 82 parsed bodies in the latest 18 edited files.
- Hull/flood/deformation spot-check: `HullIntegrityRuntime.Tick` schedules pure jobs; `LateFrameTick` owns `LockBufferForWrite`, shader globals, and breach-jet render. `InternalFloodWaterlineRuntime.FastTick` queues shader scalar state; `LateFrameTick` owns shader globals.
- Scoped `git diff --check` for the latest 18 edited files is clean except CRLF normalization warnings.
- Build/Roslyn not launched: latest gate has low CPU (`CPU=4`) but still active `dotnet` pids `20408`, `20944`, `36532`, `44188`, `46516`, and `46648`; the repository rule forbids launching another `dotnet` while any such row exists. A wide PowerShell project candidate scan timed out at 180 seconds, so no project-wide green claim is made from that run.

## 2026-05-24 - APEX Current-Source Helper-Chain Closure

What was wrong:
- `DeepPsychosisController.Tick`, `PlayerFootstepAudio.Tick`, `DeployableBeacon.Tick`, and `BioReactor.Tick` could reach Unity audio or MPB presentation sinks through helpers.
- `ShinobuOceanSurfaceAtmosphereRuntime.Tick`, `HectonGIRelaySystem.SlowTick`, `RenderTextureLifecycleTracker.SlowTick`, `ObjectPoolManager.DespawnTimer.Tick`, `DiegeticVisorHudMesh.Tick`, `DiegeticGyroCompassRuntime.SlowTick`, `PDADecryptionSpectrogramPanel.Tick`, `ToolDiegeticDisplayController.Tick`, `WristHologramHudRuntime.Tick`, `LifePodTactilePrologueController.Tick`, and `SargassumCutManager.Tick/SlowTick` still had helper routes into readback consumption, shader/material state, graphics-resource creation, leak reporting, scene despawn, BIOS/haptic UI, or visual dependency lookup.
- `HeadlessSimulationRunner.GhostAupJob.Execute` still used CPU noise; `NarrativeProgressionSmokeTester.Execute` had a direct `.ToString()` candidate.

What was done:
- Patched 17 runtime/source files in the current slice. Hot methods now stage dirty flags, pending audio bits, fixed counters, and scalar DTOs only.
- Moved psychosis/footstep/reactor/beacon audio, delayed pool despawn, RT leak checks, GI water cubemap binding, ocean readback consumption, visor/PDA/tool/wrist graphics resource creation, LifePod BIOS/haptic writes, and sargassum visual dependency lookup to `LateFrameTick`/VISUAL_SYNC.
- Replaced headless ghost CPU noise with deterministic integer hashing and replaced the dev smoke direct `.ToString()` with culture-explicit formatting outside gameplay runtime.

Cinematic Cheats used:
- Audio and UI are treated as visual lies: simulation/hot lanes emit pending bits; VISUAL_SYNC plays the cue or writes the display.
- PDA/visor/tool effects stay scalar-driven; checked routes have no CPU trigonometry/noise hits. Shader/GPU paths own the wave/glitch/fog detail.
- GraphicsBuffer/resource work remains late-frame-owned; hot lanes do not lock/write buffers or create GPU resources in the checked source gates.

Exact Microseconds saved:
- Estimated 39-94 us shifted or avoided on dirty frames on i3/MX350-class hardware. This is not profiler-measured.
- Proof: `FAST2_CURRENT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0` over 488 candidate files and 656 hot bodies.
- Proof: `TARGET_HELPER_HOT_PRESENTATION_COUNT=0` over 242 patched-file methods.
- Proof: `BROAD_HELPER_HOT_PRESENTATION_COUNT=0` over 13,852 runtime candidate methods.
- Proof: `UI_STRING_FRAME_HITS=0` over 169 UI/Visor/PDA files and 29 frame bodies.
- Proof: checked UI/Gameplay/Visor/VFX/CameraJuice/glitch/QA routes returned no CPU `math.sin`, `math.cos`, `math.sincos`, `Mathf.Sin`, `Mathf.Cos`, `System.Math.Sin`, `System.Math.Cos`, or `noise.cnoise` hits.
- Scoped `git diff --check` for the 17 current X_004 runtime files is clean except CRLF normalization warnings.
- Build/Roslyn not launched: CPU gate is closed (`CPU=100`) and active `dotnet` pids `10076`, `39296` are present. Repository rule forbids launching `dotnet` while CPU >50 or compiler processes exist.
## 2026-05-24 - Addendum 28 - Phase-Edge UI/World Closure

What was wrong:
- `ToolDiegeticDisplayController.Tick` still reached TMP/MPB via `RefreshTextAndShaderState`.
- `PDADecryptionSpectrogramPanel.Tick` could recreate native resources.
- `WristHologramHudRuntime.Tick` could refresh MPB state, poll editor CSV strings, and schedule the visual glyph job.
- `VehicleSubOsCockpitRuntime.Tick` could acquire/release external render textures.
- `SuitHUDPresentationController.Tick` still resolved hierarchy/components and applied presentation.
- `MantaEmergencyWreck` residency update and fixed lanes could spawn/despawn pooled scene objects or toggle pickup/highlighter behaviours.
- `HectonBiolumZone.Tick` drove pooled Light GameObject presentation.
- `PlayerPDA.Tick` and `PDADiagnosticTerminal.SlowTick` could apply canvas fade, tab visibility, audio, low-battery close, `TryGetComponent`, and TMP diagnostic text.

What was done:
- Patched 8 runtime files: `ToolDiegeticDisplayController.cs`, `PDADecryptionSpectrogramPanel.cs`, `WristHologramHudRuntime.cs`, `VehicleSubOsCockpitRuntime.cs`, `SuitHUDPresentationController.cs`, `MantaEmergencyWreck.cs`, `HectonBiolumZone.cs`, `PlayerPDA.cs`.
- Hot lanes now stage dirty bits/scalars/pending clips/request flags only.
- `LateFrameTick` now owns TMP writes, MPB commits, native/graphics resource refresh, glyph job scheduling, editor CSV polling, external RT acquire/release, HUD apply, Manta residency hydration/despawn, biolum light state, PDA canvas/audio/diagnostics.

Cinematic Cheats used:
- One-frame visual latency for PDA/tool/HUD/cockpit/biolum presentation.
- Dirty scalar/DTO queues instead of direct Unity object mutation from simulation/update lanes.
- Fixed char buffers and queued audio clips instead of hot string/audio commits.

Exact Microseconds saved:
- Estimated 36-82 us shifted or avoided on dirty UI/world frames on i3/MX350-class hardware.
- Not profiler-measured. Verification: `TARGET_HELPER_COUNT=0`; project direct gate `PROJECT_DIRECT_CANDIDATE_FILES=825`, `HOT_BODIES=1029`, `PROJECT_DIRECT_HITS=2` with both hits classified as non-presentation false positives. Build/Roslyn blocked by CPU/compiler gate (`CPU=100`, active `dotnet` pids `19092`, `32556`).

## 2026-05-24 - Addendum 29 - GraphicsBuffer, Upload, and Hull Feedback Closure

What was wrong:
- Acoustic radar, base module water/flicker, GPU scatter, HLOD, distant landmarks, diegetic tooltip glyphs, vegetation telemetry, abyssal thermal vents/smoke, seam-dither args, cockpit damage fallback, and thermal-grid visual upload still had single-buffer or SetData-style CPU upload residues.
- Crest depth-cache forensic PNG still used blocking `ReadPixels`.
- Physics collision resolution still called a decal-named hull visual route, and structural grid converted local hull impact points to world/scene presentation before the late-frame visual sink.
- The existing Roslyn audit did not explicitly classify `SetData`, `LockBufferForWrite`, `ReadPixels`, or `ComputeShader.Dispatch` as forbidden when reached from simulation hot lanes.

What was done:
- Patched 20 tracked runtime files in the latest slice.
- Converted acoustic radar to A/B `GraphicsBuffer` promotion: hot/audio accumulation writes native/CPU scalar state, VISUAL_SYNC writes the inactive buffer with `GraphicsBufferUploadUtility.UploadArray`, publishes it as the active GPU read buffer, then flips.
- Converted base module water/flicker buffers to A/B lock-write `GraphicsBuffer`s and global binding after upload.
- Converted GPU scatter, HLOD, distant landmark, thermal grid, abyssal thermal, vegetation telemetry, seam args, cockpit fallback, and tooltip glyph/UV/args upload paths away from `SetData`.
- Removed the unused debris matrix `GraphicsBuffer` upload.
- Replaced Crest debug PNG readback with dev-only async GPU readback; release builds return false before any readback path.
- Moved hull impact decal/spark/camera feedback from physics-facing calls into structural-grid `LateFrameTick`; `PhysicsApplySystem` now calls `QueueHullImpactFeedbackLocal`, which stores a DTO only.
- Tightened `PresentationDecouplingAudit` detection for `SetData`, `UploadArraySetData`, `ReadPixels`, `LockBufferForWrite`, and compute dispatch reachability.

Cinematic Cheats used:
- One-frame visual latency is accepted for hull feedback and GPU buffer publication.
- Simulation emits scalar/native DTOs; VISUAL_SYNC performs shader buffer binding, GPU upload, particles, decal signal, and camera shake.
- Tooltip and cockpit visuals are GPU glyph/point payloads, not managed UI strings in frame loops.
- Debug PNG export is async/dev-only and cannot block release runtime.

Exact Microseconds saved:
- Estimated 55-126 us stall/API contention risk shifted or avoided on dirty presentation frames on i3/MX350-class hardware.
- No profiler capture was run.
- Proof: `20 files changed` in the scoped tracked X_004 slice.
- Proof: `rg "SetData\\(|UploadArraySetData\\(|ReadPixels\\(" Assets/_Project/Scripts -g "*.cs" -g "!**/Editor/**" -g "!**/Dev/**"` returns no runtime hits.
- Proof: touched hot-method scanner reports `PHYSICS_HULL_UI_AUDIO_TOUCHED_HOT_HITS=0`; earlier full touched scan reports `TOUCHED_HOT_PRESENTATION_OR_GC_COUNT=0`.
- Proof: UI/PDA/Visor string frame scan reports `UI_STRING_FRAME_FILES=169`, `UI_STRING_FRAME_BODIES=136`, `UI_STRING_FRAME_HITS=0`.
- Proof: scoped `git diff --check` is clean except CRLF normalization warnings.
- Build/Roslyn not launched: CPU/compiler gate is closed (`CPU=73-88`; active `dotnet` rows present and earlier `VBCSCompiler` present). Repository rule forbids launching `dotnet` while CPU >50 or compiler rows exist.

## 2026-05-24 - Addendum 30 - Buffer Sovereignty Re-Sweep

What was wrong:
- The prior pass moved many routes to VISUAL_SYNC, but several VISUAL_SYNC consumers still wrote CPU data into a single GPU-read `GraphicsBuffer`.
- The concrete current-source risks were PDA sonar constants/HLOD AUP, seam-dither matrices/colors/args, scatter flora age/phase/payload/frame constants, cockpit button/damage buffers, sargassum scavenger matrices, sargassum cut/damage stamp commands, marauder outpost shell matrix/cell/args buffers, and ecosystem flora predator AUP.

What was done:
- Patched the current 20-file X_004 runtime slice.
- Finished `PDAMapTab` A/B promotion for sonar map constant buffer and HLOD impostor AUP buffer.
- Converted `SeamGapDitherRenderer` matrix/color/args uploads to write-inactive, bind-active buffers.
- Converted `GpuScatterLodManager` flora age/phase/visual payload lanes and frame constant buffer to A/B promotion.
- Converted `VehicleSubOsCockpitRuntime` button matrix, damage proxy vertices, and room flood levels to A/B promotion.
- Converted `SargassumGlobalDragManager` scavenger BRG matrix upload to A/B promotion.
- Converted `SargassumCutManager` cut-mask and damage-volume stamp command uploads to A/B promotion.
- Converted `MarauderOutpostGenerationService` shell matrix, cell type, and indirect args buffers to A/B promotion.
- Converted `EcosystemDirector` flora predator AUP buffer to A/B promotion.

Cinematic Cheats used:
- One-frame visual latency is accepted for buffer promotion.
- Simulation/Vault data stays scalar/native; GPU-facing detail is published only as visual buffers.
- PDA, cockpit, sargassum, scatter, outpost, and ecosystem presentation bind active GPU buffers and never feed render state back into simulation truth.

Exact Microseconds saved:
- Estimated 68-154 us stall/API contention risk shifted or avoided on dirty presentation frames on i3/MX350-class hardware.
- No profiler capture was run.
- Proof: scoped `git diff --stat` over the X_004 runtime slice reports `20 files changed`, `5698 insertions`, `2000 deletions`.
- Proof: runtime `SetData`, `UploadArraySetData`, and `ReadPixels` search returns no hits in `Assets/_Project/Scripts`.
- Proof: converted old single-buffer symbol scan returns no hits for the 8 newly touched files.
- Proof: `TARGET_20_HOT_PRESENTATION_DIRECT_HITS=0`.
- Proof: `TARGET_UI_STRING_FRAME_HITS=0`.
- Proof: runtime UI/PDA/Rendering CPU trig scan returns no hits excluding Editor/Dev.
- Proof: scoped `git diff --check` is clean except CRLF normalization warnings.
- Build/Roslyn not launched: CPU/compiler gate is closed (`CPU=100`; active `csc` pid `45568`, active `dotnet` pid `48620`).

## 2026-05-24 - Addendum 31 - ProceduralOreSpawner Fatal Hot-Path Closure

What was wrong:
- Fresh Roslyn audit after the clean build reported `fatalHotPath=2`.
- Both fatal paths were in `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`: `SlowTick -> WriteIndirectArgsGpu` reached `GraphicsBuffer.LockBufferForWrite` and `GraphicsBuffer.UnlockBufferAfterWrite`.

What was done:
- Replaced hot-lane GPU args writes with queued `GeologyIndirectArgsDTO` state.
- `UpdateIndirectArgsBuffer` now updates Vault args and calls `QueueIndirectArgsGpu`.
- Rebind/clear/discard paths now queue a zero-args DTO instead of touching the GPU.
- `LateFrameTick` now calls `FlushPendingIndirectArgsGpu` before `RenderDormantOres`, so the indirect args lock-write runs only in VISUAL_SYNC.

Cinematic Cheats used:
- One-frame delayed dormant ore draw args are acceptable presentation state.
- Ore truth remains in Vault/native state; draw args are only a GPU lie for presentation.

Exact Microseconds saved:
- Estimated 2-5 us GPU API contention shifted out of dirty `SlowTick` frames on i3/MX350-class hardware.
- No profiler capture was run.
- Proof: `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings / 0 errors in 00:00:58.50 before this final patch.
- Proof: fresh Roslyn audit before this final patch reported only the two `ProceduralOreSpawner` fatal findings.
- Proof: source gate after the patch reports `ORE_HOT_PRESENTATION_DIRECT_HITS=0`.
- Roslyn rerun after this patch was not launched: active Unity VBCSCompiler remains as `dotnet` pid `48968`, and CPU stayed above 50 on follow-up checks.

## 2026-05-25 - Addendum 32 - Phase-Edge UI/Audio/Visor Closure

What was wrong:
- Helper-chain scan still found `Tick`/`SlowTick` routes into presentation sinks across UI, audio, PDA, visor, and relay visuals.
- Offenders included HUD notification/quickbar text and canvas alpha, Manta HUD, airlock audio transitions, cave reverb mixer/filter writes, relay cable visuals, PDA barter/construction text, marker/beacon/AR waypoint UI, subtitles, UI scaler commits, acoustic reverb snapshots, diegetic PDA shell, tool screen render-texture decisions, visor lens GPU state, screen compositor, and visor HUD projection/material state.

What was done:
- Rewrote 20 runtime files in the current X_004 slice.
- Hot lanes now queue scalar/DTO/dirty state only.
- `LateFrameTick` now owns TMP/CanvasGroup/MPB/audio/snapshot/RT/GPU/compositor/projection commits for the touched routes.
- Presentation-only controllers that did not need update-lane ownership now register as `ILateFrameTickable` instead of `IUpdatable`.

Cinematic Cheats used:
- One-frame visual latency is accepted for HUD/audio/visor presentation.
- UI and visor detail remains a visual lie driven by DTOs and shader/material state after simulation.
- Airlock and reverb truth stays in timers/read models; audio snapshots and filters are late-frame presentation only.

Exact Microseconds saved:
- Estimated 47-112 us shifted or avoided on dirty UI/audio/visor frames on i3/MX350-class hardware.
- No profiler capture was run.
- Proof: scoped `git diff --stat` reports `20 files changed`, `2632 insertions`, `1535 deletions`.
- Proof: runtime upload/readback search returns no `SetData`, `UploadArraySetData`, `ReadPixels`, `LockBufferForWrite`, or `UnlockBufferAfterWrite` hits in `Assets/_Project/Scripts`.
- Proof: `TARGET20_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`.
- Proof: `TARGET20_HELPER_HOT_PRESENTATION_OR_GC_COUNT=0`.
- Proof: `RUNTIME_UI_STRING_FRAME_HITS=0`.
- Proof: scoped `git diff --check` is clean except CRLF normalization warnings.
- Build/Roslyn not launched: CPU gate is closed (`CPU=100`; no active compiler rows).
