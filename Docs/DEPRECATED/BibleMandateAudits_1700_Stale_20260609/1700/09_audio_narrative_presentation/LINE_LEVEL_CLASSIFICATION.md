# Audio / Narrative / PDA / Cinematics / Public Text Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING  
Date: 2026-06-02  
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 149 static suspect lines from:

- `Docs/BibleMandateAudits/1700/09_audio_narrative_presentation/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/09_audio_narrative_presentation/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/09_audio_narrative_presentation_runtime_risks.txt`

This is not DSP profiler proof, Unity Audio Profiler proof, mixer proof, Frame Debugger proof, subtitle/accessibility proof, player-build proof, GC proof, Memory Profiler proof, device proof, or proof that authored quest/PDA/audio assets are wired in scenes. The system remains yellow until runtime artifacts prove the static classifications.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 89 | The line is editor-only, an editor tool/window allocation, `OnValidate`, or a compile-stripped `H8Debug`/development diagnostic route. |
| `LEGAL_COLD_PATH` | 34 | The line is boot/setup validation, cold component repair, owner-lifetime native storage, font/material cache resolution, black-box/fault dump payload, or explicit UI/audio/narrative setup. |
| `RUNTIME_VIOLATION` | 2 registered | `MissionMarkerSystem` creates quest marker mesh/material resources at runtime and `CarveDebrisComputeRenderer` creates fallback debris mesh geometry. Both are registered release blockers. |
| `FALSE_POSITIVE` | 24 | Static pattern matched material references/draw parameters or allocator constants, not mesh/material mutation or allocation callsites. |

## Existing Blockers Still Binding This Group

- `RB-017`: managed audio callback synthesis/decode and mock audio content.
- `RB-018`: quest mission marker runtime mesh/material fallback assets.
- `RB-123`: runtime VFX mesh/material/RT fallback assets, including `CarveDebrisComputeRenderer` fallback octahedron mesh.
- `RB-125`: visor RenderFeature material/shader/hot-swap lifecycle proof.
- `RB-131`: UI/localization/input-remap/diegetic projection proof gates, including PDA and relay marker authoring.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `MissionMarkerSystem.cs:705` | `RUNTIME_VIOLATION` registered | Method review shows `EnsureRuntimeResources()` runs from `Awake()`/`OnEnable()`, calls `CreateMarkerMesh()`, allocates a `new Mesh`, fills runtime vertex/index arrays, calls `mesh.RecalculateNormals()`, uploads the mesh, and creates `new Material(markerShader)` with `DontSave`. This is runtime visual asset authoring, not an authored/prefab asset route. | `RB-018`: serialize/authored quest marker mesh and material or prove this component is excluded/unreachable; provide prefab/build/player/profiler proof with no runtime mesh/material construction. |
| `CarveDebrisComputeRenderer.cs:2393` | `RUNTIME_VIOLATION` registered | Cross-routed rendering line already classified: `BuildOctahedronMesh()` calls `mesh.RecalculateNormals()` on fallback low-poly debris geometry in runtime VFX code. | `RB-123`: authored/default debris mesh/material proof or release exclusion of fallback geometry. |
| `HectonBiolumSSGIFeature.cs:353`, `:365`, `:370`, `:398`, `:415`, `:427` | `FALSE_POSITIVE` | These lines assign/read/pass an existing render-pass material reference and draw a full-screen pass. They are not material construction or mutation callsites. | `RB-125`: shader/material assignment, recreate counters, RenderGraph/GPU proof. |
| `HectonHolographicEdgeFeature.cs:94`, `:103` | `FALSE_POSITIVE` | These lines pass an existing material into a draw path. Static search matched `material`, not an allocation or mutation. | `RB-125`: material lifecycle and draw-count proof. |
| `VolumetricLightFeature.cs:563`, `:586`, `:605` | `FALSE_POSITIVE` | Existing proxy material reference/null-check/draw path, not runtime material construction. | `RB-125`: material lifecycle, shader assignment, and GPU proof. |
| `HectonSonarPointCloudFeature.cs:311`, `:329`, `:345` | `FALSE_POSITIVE` | Existing material reference/null-check/draw path, not runtime material construction. | `RB-005`/`RB-125`: sonar point data truth, material/shader, and GPU proof. |
| `HectonScooterVolumetricShaftsFeature.cs:639`, `:661`, `:687` | `FALSE_POSITIVE` | Existing material reference/null-check/draw path, not runtime material construction. | `RB-125`: shaft material lifecycle and compact/high GPU capture. |
| `HectonVolumetricParticulateFogFeature.cs:1111`, `:1128`, `:1149` | `FALSE_POSITIVE` | Existing material reference/null-check/draw path, not runtime material construction. | `RB-124`/`RB-125`: fallback texture/mock-light lifecycle and RenderGraph proof. |
| `AudioLogEvents.cs:80`, `QuestGraphEvaluator.cs:24`, `QuestEvents.cs:59`, `QuestStateManager.cs:46` | `FALSE_POSITIVE` | These are constant allocator selector definitions, not allocation callsites. | Signal-lane capacity proof remains under the actual owner storage lines. |
| `DynamicMusicGranularSynthesizer.cs:681` | `LEGAL_COLD_PATH` | Host validation uses `TryGetComponent<AudioListener>` during synth/component setup. The broader class remains blocked by managed `OnAudioFilterRead` under `RB-017`. | `RB-017`: native/DSPGraph/audio-kernel proof and no managed callback production path. |
| `VocalBankPlaybackRuntime.cs:306` | `LEGAL_COLD_PATH` | Host validation checks for an `AudioListener` during vocal playback setup. The stronger issue is managed callback decode, not this lookup. | `RB-017`: remove/restrict/prove callback decode route and authored vocal bank proof. |
| `PDARuntimeInstaller.cs:20`, `:23`, `:26`, `:29` | `LEGAL_COLD_PATH` | Cold fail-safe installer checks player components and adds missing PDA/exploration systems. It is not a gameplay tick lookup, but release scenes should author these components up front. | Authored player prefab/PDA component proof; no normal scene composition through runtime repair. |
| `PDAMarkerHUDElement.cs:573` | `LEGAL_COLD_PATH` | `GetComponentsInChildren(true, s_GraphicRaycastDisableScratch)` is used during marker icon prefab/display setup to disable raycasts, with a reusable scratch list. | UI/PDA prewarm proof; no repeated marker hierarchy repair during gameplay cadence. |
| `VisorHUDController.cs:2151` | `LEGAL_COLD_PATH` | BIOS/font material resolution reads `font.material` for a cache path. It is not material construction in the reviewed context. | `RB-131`: font/material assignment, prewarm, and no first-use UI material churn proof. |
| `NativeAudioFrameRingBuffer.cs:506`, `:515`, `:527`, `:539`, `:565`, `:571`, `:577`, `:583` | `LEGAL_COLD_PATH` | Persistent native bridge frames/shared-state/telemetry/dump buffers are owner-lifetime allocations/frees through H8Memory. | `RB-017`: native audio-kernel registration, underrun counters, disposal/leak proof, and compact DSP capture. |
| `MetaCampaignService.cs:1591`, `AwaitableDropSequenceDirector.cs:1226`, `CartographyGridJobs.cs:1155`, `InternalFloodWaterlineRuntime.cs:789`, `HectonVisorUberPostFeature.Noir.cs:1076`, `HectonVisorARStencilRendererFeature.cs:1378`, `HectonVisorFluidDistortionFeature.cs:1731`, `HectonVisorUberPostFeature.cs:1797`, `HectonVolumetricParticulateFogFeature.cs:1963`, `SpectrumSystem.cs:3762`, `ShinobuPlasmaBeamRuntime.cs:1486`, `DynamicDecalVaultRuntime.cs:2339` | `LEGAL_COLD_PATH` | These `NativeArray<byte>(Allocator.Temp/TempJob)` payloads are black-box/fault dump or explicit export payloads, not healthy-frame audio/narrative/UI work. | Fault-trigger proof, no normal-frame dump spam, dump artifact paths, and render/audio proof where applicable. |
| `QuestDagResolverRuntime.cs:569`, `QuestStateManager.cs:194`, `:410`, `:415` | `LEGAL_COLD_PATH` | Persistent quest DAG/state arrays/maps are owner storage built during service setup/compile, not per quest event. | Quest boot/compile/save-load proof; no first-use quest storage growth during gameplay. |
| `BiolumPulseSyncRuntime.cs:316`, `:318` | `LEGAL_COLD_PATH` | Persistent black-box snapshot ring allocation for biolum pulse telemetry. | Owner/disposal proof and no healthy-frame growth. |
| `VocalBankPlaybackRuntime.cs:1355`, `:1367` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Editor CSV scratch allocation/free belongs to editor/import tooling boundaries, not release playback. | None for player runtime; vocal playback remains blocked separately by `RB-017`. |
| `ShinobuVoxelSculptorWindow.cs:248`, `:249`, `:250`, `:251`, `:679`, `:680`, `:681`, `:682`, `:683`, `:684`, `:685`, `:686` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The file is an editor debris/voxel sculptor window; TempJob arrays are authoring/tool allocations. | None for player runtime; keep tool assemblies editor-only. |
| `NarrativeDagInspectorWindow.cs:62` | `LEGAL_EDITOR_OR_DEV_GUARDED` | File begins with `#if UNITY_EDITOR` and derives from `EditorWindow`. Direct `Debug.LogError` cannot enter a non-editor player build. | None for player runtime; keep inspector editor-only. |
| `AdaptiveStemAudioMixer.cs:1315`, `:1319`, `:1372`, `:1376` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Diagnostic warnings use the compile-stripped `H8Debug` facade. | Audio telemetry/dump proof remains required, but these lines are not release logging. |
| `ProceduralAudioEvents.cs:1267` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Exception logging uses `H8Debug.LogException`, stripped outside editor/development builds. | Event-lane proof separate. |
| `PlayerCriticalProceduralAudioRenderer.cs:3493`, `:4514`, `:4561`, `:4582`, `:8763`, `:8783` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Audio diagnostics use `H8Debug` and are release-stripped. | `RB-017`: native bridge, reverb/mixer assignment, zero underrun proof. |
| `HectonMusicDirector.cs:907`, `:917` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Missing config/prefab diagnostics use `H8Debug`. | Authored config/runtime director prefab proof remains required. |
| `AudioLogPickup.cs:241`, `:250`, `AudioLogSystem.cs:1681`, `:1689`, `:1697`, `:1705`, `AudioLogEvents.cs:579` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Audio-log diagnostics use `H8Debug`; release logging is stripped. | Audio-log save/playback/subtitle proof remains required. |
| `CorporateOrderSystem.cs:357`, `:363`, `MetaCampaignService.cs:1620`, `:1627`, `ProceduralLoreDirector.cs:159`, `LoreDatabaseManager.cs:101`, `:889`, `:1041`, `:1076`, `:1080`, `QuestManager.cs:525`, `:534`, `:605`, `QuestEvents.cs:506`, `PDALogbookManager.cs:488` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Narrative/quest/PDA diagnostics use `H8Debug`; release logging is stripped. | Narrative evidence, quest compile/save-load, PDA component proof, and black-box dump artifacts remain required. |
| `BiomeProfile.cs:68`, `CameraJuiceSystem_CameraJuiceBurst.cs:184`, `ShakeProfile.cs:52`, `CameraJuiceSystem.cs:861`, `:869`, `:877`, `:885`, `:893`, `:901`, `:909`, `:917`, `:925`, `:933`, `:941`, `:949`, `:957`, `:965`, `:973` | `LEGAL_EDITOR_OR_DEV_GUARDED` | VFX/camera-juice logs are `OnValidate`, profile validation, or compile-stripped `H8Debug` diagnostics. | Camera/VFX readability and profiler proof remains required. |
| `BiolumPulseSyncRuntime.cs:1472`, `HectonMarineSnowRenderer.cs:2845`, `:2853`, `:2861`, `:2869`, `:2877`, `:2885`, `ParasiteSwarmGpuRuntime.cs:130`, `DiegeticVisorLensRuntime.cs:871`, `DynamicDecalVaultRuntime.cs:3108`, `:3115`, `:3117`, `InternalFloodWaterlineRuntime.cs:811`, `:817`, `:823`, `:829`, `:835`, `:841`, `HectonVisorARStencilRendererFeature.cs:200`, `:204`, `:702` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Visor/VFX/ABI/fault diagnostics use `H8Debug` or editor/development guarded logging. | `RB-123`/`RB-125`/presentation proof remains required for resources and GPU cost, not for these log calls. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 149 listed audio/narrative/PDA/cinematics/public-text static suspect lines are now classified. This does not clear the group for release. The remaining work is concrete: close `RB-017` managed audio callback routes, close `RB-018` quest marker runtime asset authoring, close the cross-routed `RB-123` debris fallback, prove native/DSPGraph/audio-kernel output, prove authored banks/profiles/mixer/player components, prove PDA/player marker prefabs, run DSP/mix/soundscape/subtitle/accessibility/quest-save-load/public-capture proof, and collect compact/high player-build profiler artifacts.
