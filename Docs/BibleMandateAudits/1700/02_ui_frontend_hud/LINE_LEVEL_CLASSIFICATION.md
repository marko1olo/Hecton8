# UI / Menus / HUD / Terminals / Localization / Settings Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING
Date: 2026-06-02
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 241 static suspect lines from:

- `Docs/BibleMandateAudits/1700/02_ui_frontend_hud/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/02_ui_frontend_hud/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/02_ui_frontend_hud_runtime_risks.txt`

This is not UI profiler proof, Play Mode proof, player-build proof, GC proof, Canvas rebuild proof, Frame Debugger proof, localization expansion proof, input-remap IO proof, or compact/high device proof. The system remains yellow until runtime artifacts prove the static classifications.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 128 | The line is editor-only or a compile-stripped `H8Debug`/development diagnostic route. |
| `LEGAL_COLD_PATH` | 86 | The line is menu/HUD/PDA setup, user-action IO, owner-lifetime native storage, fault-dump payload, font recovery/bootstrap, or cached material assignment. |
| `FALSE_POSITIVE` | 24 | Static search matched existing material references, font material reads, or render-pass draw parameters rather than runtime material construction/mutation. |
| `RUNTIME_VIOLATION` | 3 registered | Runtime fallback UI mesh/material generation is reachable in player runtime and remains blocked by `RB-131`. |

## Existing Blockers Still Binding This Group

- `RB-017`: audio UI feedback and PDA/audio route proof where audio callbacks or mock banks cross UI.
- `RB-018`: quest/PDA mission marker runtime asset fallback.
- `RB-123`: cross-routed VFX fallback mesh/material/RT proof.
- `RB-125`: visor RenderFeature material/shader/hot-swap lifecycle proof.
- `RB-129`: core lazy first-use native initialization, including `UIStateStore`.
- `RB-131`: UI, localization, input-remap, and diegetic projection proof gates. This pass keeps `AcousticRadarSphereRenderer`, `DiegeticVisorHudMesh`, and `VehicleSubOsCockpitRuntime` under this blocker.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `AcousticRadarSphereRenderer.cs:531` | `RUNTIME_VIOLATION` registered | Method review shows `EnsureResources()` runs from `OnEnable()`/`Start()`. If `voxelMesh` is missing, `CreateVoxelMesh()` allocates a runtime cube mesh, vertex/index arrays, calls `mesh.RecalculateNormals()`, uploads the mesh, and `EnsureResources()` can create a `new Material(voxelShader)` with `HideFlags.DontSave`. | `RB-131`: authored voxel mesh/material proof, fallback release exclusion or recovery-only proof, 300-frame radar HUD profiler, and compact/high readability capture. |
| `DiegeticVisorHudMesh.cs:438` | `RUNTIME_VIOLATION` registered | `OnEnable()` calls `RebuildMesh()`, which creates a runtime `Mesh` for the physical visor projection surface. The same component creates runtime material instances in `EnsureRuntimeMaterial()`, and method review shows quality-policy changes set `_meshRebuildDirty` but `LateFrameTick()` clears the flag without rebuilding. | `RB-131`: authored/serialized projection mesh/material route or explicit bootstrap-only exception with profiler proof; fix or document the `GlobalQualityWeight` mesh rebuild behavior. |
| `VehicleSubOsCockpitRuntime.cs:2601` | `RUNTIME_VIOLATION` registered | `CreateDamageCubeMesh()` builds a runtime fallback damage hologram cube, calls `mesh.RecalculateNormals()`, recalculates bounds, and uploads the mesh when the LOD3 damage proxy mesh is missing. This hides missing authored cockpit damage proxy art. | `RB-131`: authored cockpit radar/damage meshes/materials, fallback release exclusion or bounded recovery proof, cockpit GPU/GC profiler capture, and compact/high cockpit readability capture. |
| `DiegeticMenuCanvasUtility.cs:74`, `:97` | `LEGAL_COLD_PATH` | `Camera.main` fallback happens only after preferred camera resolution during menu setup. `NormalizeReadableText()` scans TMP children through a static scratch list and is labeled main-menu setup only. | Injected camera proof, no menu-time hidden camera search during gameplay, localization/readability capture. |
| `MainMenuAtmosphereController.cs:189` | `LEGAL_COLD_PATH` | MeshRenderer lookup belongs to menu atmospheric quad setup, not gameplay HUD tick. | Menu bootstrap proof and no repeated hierarchy/resource repair. |
| `DiegeticPDAController.cs:652`, `:653`, `:654` | `LEGAL_COLD_PATH` | PDA tablet renderers/colliders/canvas groups are cached during `Awake()`/`OnEnable()`/root-change paths. This is cold/rebind-shaped, not open/close hot-path proof. | PDA enable/root-swap/language-swap stress with no repeated hierarchy scan and 0 B/frame after bootstrap. |
| `MenuVisualConceptDecorApplier.cs:80`, `:81`, `:90`, `:111`, `:112` | `LEGAL_COLD_PATH` | Menu concept decoration resolves root/slot components during menu visual setup. | Menu visual style switching proof, no repeated hierarchy rebuilds, and capture on compact/high settings. |
| `SettingsPanel.cs:604`, `:607`, `:611`, `:671`, `:674`, `:678`, `:738`, `:741`, `:745`, `:800`, `:803`, `:807`, `:863`, `:880`, `:896`, `:922`, `:941`, `:944`, `:969`, `:1001` | `LEGAL_COLD_PATH` | Settings rows/sliders/buttons/text/layout elements are resolved during settings-panel construction, not per-frame HUD updates. | Settings open/apply/remap profiler with no post-bootstrap hierarchy growth and explicit blocking/user-action IO proof. |
| `DiegeticTooltipSystem.cs:1572`, `DiegeticVisorHudMesh.cs:802`, `DiegeticGlitchSurgeonRuntime.cs:2430`, `DiegeticGyroCompassRuntime.cs:1796`, `PDAEncyclopediaStreamer.cs:3027`, `PDADecryptionSpectrogramPanel.cs:899`, `TopographicalSonarSynthesizer.cs:2008`, `VehicleSubOsCockpitRuntime.cs:3008`, `OpenXRManualOverrideLever.cs:685`, `DynamicDecalVaultRuntime.cs:2339`, `HectonVisorARStencilRendererFeature.cs:1378`, `HectonVisorFluidDistortionFeature.cs:1731`, `HectonVisorUberPostFeature.Noir.cs:1076`, `HectonVisorUberPostFeature.cs:1797`, `HectonVolumetricParticulateFogFeature.cs:1963`, `InternalFloodWaterlineRuntime.cs:789`, `SpectrumSystem.cs:3762` | `LEGAL_COLD_PATH` | These `NativeArray<byte>(Allocator.Temp/TempJob)` lines are black-box dump, fault/export, or explicit UI/visor snapshot payloads, not healthy-frame UI rendering. | Fault-trigger proof, no normal-frame dump spam, black-box artifact paths, and compact/high profiler captures. |
| `DiegeticGlitchSurgeonRuntime.cs:1301`, `:1331`, `TopographicalSonarSynthesizer.cs:542`, `:548`, `:554`, `:560`, `:566`, `:572`, `LocRegistry.cs:650`, `:1989` | `LEGAL_COLD_PATH` | Persistent UI/glitch/sonar/localization storage belongs to owners or bounded language-stage scratch, not per-label allocation. | `RB-129`/`RB-131`: boot prewarm counters, language-switch transaction proof, ping spam proof, no first-use HUD allocation. |
| `ControlRemapper.cs:132`, `:220`, `:346`, `:356` | `LEGAL_COLD_PATH` | Temp buffers are control-remap/settings IO transaction paths, not HUD tick. | Explicit user-action blocking proof, no silent gameplay freeze, settings roundtrip/rejection proof. |
| `LocalizedFontResolver.cs:189`, `:353`, `FontStreamingManager.cs:633`, `VisorHUDController.cs:2151` | `FALSE_POSITIVE` | These lines read existing font/material references or atlas textures. They do not construct or mutate materials at the flagged callsites. | Font/material assignment and localization expansion proof remains required under `RB-131`. |
| `FontAssetRecovery.cs:131`, `:168`, `:209`, `:210`, `:217`, `:218`, `:223`, `:224`, `:240`, `:241` | `LEGAL_COLD_PATH` | Runtime font recovery reads/assigns TMP font material/atlas data and can force text mesh refresh in a private bootstrap/recovery path. It is not normal text update proof. | Release font atlas/material prefab proof and callsite proof that recovery/mesh rebuild is bootstrap/recovery only. |
| `FontAssetRecovery.cs:347`, `:348`, `:423`, `:448`, `:508`, `:511`, `:512` | `LEGAL_EDITOR_OR_DEV_GUARDED` | These material repair and `EditorUtility.SetDirty` routes are inside `#if UNITY_EDITOR` asset repair/import code. | None for player runtime; keep editor repair out of player builds. |
| `PDAShellChrome.cs:1712`, `PDAMapTab.cs:483`, `ShaderCompassRibbon.cs:178`, `SuitHUDV4CanvasOverlay.cs:2725`, `:2756`, `:2760`, `:2777`, `:2783`, `:2813`, `:3594`, `:5491`, `:5812`, `:5853`, `:7347` | `LEGAL_COLD_PATH` | These are cached UI material assignment/nulling paths for PDA shell, PDA map, compass ribbon, and suit HUD state changes. They are not material construction at the flagged lines. | `RB-131`: material lifecycle counters, no repeated material creation, no canvas rebuild spikes, and 0 B/frame HUD/PDA/compass interaction proof. |
| `HectonBiolumSSGIFeature.cs:353`, `:365`, `:370`, `:398`, `:415`, `:427`, `HectonScooterVolumetricShaftsFeature.cs:639`, `:661`, `:687`, `HectonHolographicEdgeFeature.cs:94`, `:103`, `HectonSonarPointCloudFeature.cs:311`, `:329`, `:345`, `HectonVolumetricParticulateFogFeature.cs:1111`, `:1128`, `:1149`, `VolumetricLightFeature.cs:563`, `:586`, `:605` | `FALSE_POSITIVE` | These lines pass existing render-pass material references into draw/full-screen paths or null-check them. They are not material construction/mutation callsites. | `RB-125`: render feature material lifecycle, shader assignment, hot-swap counters, RenderGraph/Frame Debugger/GPU proof. |
| All executable `H8Debug` / `Hecton8.Core.H8Debug` lines in the raw UI scan | `LEGAL_EDITOR_OR_DEV_GUARDED` | `H8Debug` methods are conditionally compiled for editor/development builds. The listed UI, settings, input, terminal, visor, PDA, and runtime diagnostic callsites do not compile into non-development player logging. | Build-symbol proof that release player is non-development; underlying UI/input/settings/visor systems still need runtime proof. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 241 listed UI/menu/HUD/terminal/localization/settings static suspect lines are now classified. This does not clear UI for release. The remaining work is concrete: close `RB-131`, prove authored HUD/PDA/radar/visor/cockpit meshes/materials/fonts, close lazy UI/localization prewarm gaps, prove no post-bootstrap hierarchy/material/mesh creation, prove settings/control remap IO windows, and capture compact/high UI readability plus 300-frame 0 B/frame interaction profiler artifacts.
