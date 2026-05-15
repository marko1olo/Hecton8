# LOG_SHADER_OVERKILL_ARCHITECT

## 2026-05-15 02:40:07 +04:00 - SHADERS CRYSTALLIZED / VISUAL ORGASM READY
What was wrong:
- Material behavior was fragmented across separate caustics/rust/deformation concepts, which risks SetPass multiplication and SRP Batcher damage.
- The active dependency rationale files requested by the prompt were missing: `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md`.
- `Docs/Tasks/CURRENT_BATCH.md` does not contain this agent XML or a `<POLISH_MANDATE>` tag.
- Unity batchmode cannot complete because `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` references missing World/GPR symbols: `Hecton8.World.GPR`, `GroundRadarTelemetryEntry`, and `GroundRadarConstants`.

What was done:
- Created `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl` as the single UberNoir URP HLSL core.
- Enforced one `CBUFFER_START(UnityPerMaterial)` for per-material data.
- Applied `_TotalUniverseOffset.xyz` before world-position matrix multiplication for AUP precision.
- Added `StructuredBuffer<H8UberNoirInstanceData>` for GraphicsBuffer/Resident Drawer compatible matrices and seed/fade/flags.
- Integrated analytical caustics, dynamic pressure bending, 16-tap rust POM, spectral biolum emission, branchless attenuation, and blue-noise cutout.
- Added `_MATH_LOD_LOW` stripping for albedo/roughness-only low-tier output.
- Added NaN guards for all owned `pow()` and `rsqrt()` use.
- Created `Assets/_Project/Scripts/Graphics/Materials/H8ShaderIDs.cs` for zero-GC property ID caching.
- Ran Unity 6000.4.1f1 batchmode and static audits. Owned shader/C# names do not appear in the compiler error scan; the compile wall is outside this rendering domain.

Cinematic Cheats used:
- Caustics are analytical wave interference plus optional lookup texture, not physical photon simulation.
- Hull bending is shader vertex bowing from stress fields, not CPU mesh deformation.
- Rust depth is high-tier POM only, not geometry displacement or decal stacks.
- Bioluminescence is phase-driven spectral emission, not script-updated material state.
- Noir fog uses blue-noise cutout, not full transparent sorting.

Exact Microseconds saved:
- Measured: 0 us. No clean compile/runtime capture is available because Unity exits on the external World/GPR compile blocker.
- Estimated CPU SetPass/pass savings from unified shader path: 30-120 us.
- Estimated CPU savings from GraphicsBuffer/resident instance path: 20-80 us in dense draws.
- Estimated CPU savings from shader-side hull bending versus CPU mesh mutation: 60-300 us.
- Estimated CPU savings from static property IDs versus hot string lookup bursts: 5-40 us.
- Estimated GPU savings from low-tier stripping: 80-500 us in material-heavy low-end views.
- Estimated GPU texture savings from single packed ORM sample: 10-60 us.

Verification:
- Static audit: one `UnityPerMaterial` CBUFFER, one `_MaskMap` sample, guarded `pow()`/`rsqrt()`, balanced braces.
- `git diff --check`: no whitespace errors; PowerShell reports LF-to-CRLF warnings for updated markdown only.
- Unity batchmode: blocked by `GroundPenetratingRadarRuntime.cs` World/GPR missing references, not owned rendering files.
- Frame Debugger/RenderDoc/Profiler: not run because the project does not reach a clean compile.

## 2026-05-15 03:19:35 +04:00 - Follow-Up No-Rebuild Rendering/H-Phi Pass
What was wrong:
- `_MATH_LOD_LOW` still paid for normal-map sampling and unused specular/shadow setup.
- Dithered transparency evaluated blue-noise even when the dither feature was disabled.
- Clean materials sampled `_RustDetailMap` before proving rust was active.
- Optional caustic texture sampling was compiled into every non-low UberNoir variant.
- `Hecton8.Graphics.Materials.asmdef` carried an unused `Hecton8.World.Contracts` reference.

What was done:
- Low-tier UberNoir now returns from base+packed ORM surface sampling and skips normal/rust/POM/biolum sampling.
- Low-tier lighting uses `GetMainLight()` without `TransformWorldToShadowCoord`, specular half-vector, caustics, or discarded view math.
- Blue-noise dither is skipped under `_MATH_LOD_LOW` and only sampled when the dither feature flag is enabled.
- Rust detail sampling now returns early when resolved rust is effectively zero.
- Caustic map sampling is now behind `H8_UBERNOIR_CAUSTICS_TEXTURED`.
- Removed the unused World contracts dependency from `Hecton8.Graphics.Materials.asmdef`.

Cinematic Cheats used:
- Low-tier normals degrade to dominant-axis safe normals instead of exact normalization.
- Low-tier lighting keeps ambient + main diffuse only; visual belief is preserved by fog/ORM while expensive depth/specular detail is shed.
- Procedural caustics remain the default; texture caustics are opt-in visual overkill.

Exact Microseconds saved:
- Measured: 0 us. User forbade rebuilds, and Unity/runtime capture remains blocked by World/GPR compile errors.
- Estimated low-tier surface-sample savings: 20-120 us GPU in dense material views.
- Estimated low-tier lighting savings: 10-80 us GPU in forward-lit batches.
- Estimated clean-material rust gate savings: 10-90 us GPU when rust is zero.
- Estimated caustic texture variant/sample savings: 5-40 us GPU plus lower variant pressure when procedural caustics are enough.
- Asmdef cleanup runtime gain: 0 us; static architecture debt reduced.

Verification:
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010534799`, `RuntimeHPhiRisk=0.000573240`.
- Scoped HLSL scan: braces `40/40`, one `UnityPerMaterial` CBUFFER, one `_MaskMap` sample, caustic texture sample guarded by `H8_UBERNOIR_CAUSTICS_TEXTURED`.
- Scoped asmdef scan: no `Hecton8.World` / `World.Contracts` reference remains in `Assets/_Project/Scripts/Graphics/Materials`.
- `git diff --check` on touched files: no whitespace errors; LF-to-CRLF warnings only.
- No `dotnet build`, no `dotnet rebuild`, no Unity rebuild.

## 2026-05-15 03:34:52 +04:00 - Follow-Up No-Rebuild Rendering/H-Phi Pass 2
What was wrong:
- Low-tier caustic compute shutdown did not fully guarantee global caustic consumers were dark; `_HectonProjectedCausticsParams.x` could remain nonzero.
- Caustic GPU upload data, caustic black-box telemetry, and AUP culling job payloads relied on implicit layout in code crossing GPU/Burst/native boundaries.
- Disposed NativeArray scratch fields were released but not default-reset, making long-session state inspection less deterministic.

What was done:
- `AnalyticalCausticsService` now passes `lowTier` into `PublishShaderGlobals` and forces caustic intensity to zero for low-tier/depth-disabled modes.
- `CausticsWaveGpuData` and `CausticTelemetryEntry` now declare explicit sequential pack/size layout.
- `ApplyAupShiftJob` now declares explicit sequential layout.
- Disposed caustic black-box and wave-upload scratch NativeArrays are reset to default after release.

Cinematic Cheats used:
- Caustics remain fake-first analytical light contribution, and low-tier now kills the entire global contribution instead of paying for invisible ocean optics.
- Rust, POM, biolum, caustics, and bending stay tier-gated: toaster path keeps material identity; high-end path keeps overkill.

Exact Microseconds saved:
- 15-80 us estimated GPU saved on low-tier caustic receiver views by forcing global intensity to zero. Pending real Profiler/GPU capture.
- 0 us claimed for layout/default-reset changes; these are binary safety and black-box determinism improvements, not runtime speed claims.

Verification:
- No dotnet rebuild was executed.
- `git diff --check` reported no whitespace errors for owned files.
- Static brace scan passed for `Hecton8_UberNoir.hlsl`, `AnalyticalCausticsService.cs`, `InstanceCullingService.cs`, and `Hecton8.Graphics.Materials.asmdef`.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` completed with `RuntimeHPhiNarrow=0.010496041`, `RuntimeHPhiRisk=0.000571225`, `ArchitecturalPurity=0.996460177`, `MemoryAlignment=0.503703704`, `UnityUpdateMethods=2`, `AupPrecisionRisk=0`.

## 2026-05-15 03:48:00 +04:00 - Follow-Up No-Rebuild Shader Safety Pass
What was wrong:
- `H8UberNoirLoadInstance` could index `_H8UberNoirInstanceData[bufferOffset]` when the instance-buffer keyword was compiled but the runtime count was zero or the use flag was disabled.

What was done:
- Added `H8UberNoirBuildDefaultInstance`.
- Changed `H8UberNoirLoadInstance` to use Unity object/world matrices by default and only read the `StructuredBuffer` when `_UberNoirInstanceParams.z >= 0.5` and `_UberNoirInstanceParams.y > 0`.

Cinematic Cheats used:
- None. This was a deterministic safety fix for Resident Drawer fallback behavior.

Exact Microseconds saved:
- 0 us measured. Estimated 0-2 us vertex branch cost in fallback cases; undefined GPU buffer reads removed.

Verification:
- No dotnet rebuild was executed.
- Static HLSL review confirms the buffer read is now count/use gated before indexing.
- First H-Phi static audit attempt timed out at 120 seconds; second no-rebuild static audit completed at 300-second timeout.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` completed with `RuntimeHPhiNarrow=0.010497120`, `RuntimeHPhiRisk=0.000573792`, `ArchitecturalPurity=0.996460177`, `MemoryAlignment=0.503966155`, `UnityUpdateMethods=2`, `StructLayoutAttributes=953`, `AupPrecisionRisk=0`.

## 2026-05-15 04:12:00 +04:00 - Follow-Up No-Rebuild DRS/Shader Global Safety Pass
What was wrong:
- Dynamic resolution scale could respond to a one-frame pressure spike and then recover immediately, violating the hysteresis mandate and causing presentation instability.
- The fallback path wrote directly to the active URP asset render scale, and initialization wrote the upscaling filter. That is project asset mutation risk from a runtime rendering service.
- Procedural flora tint publishing trusted serialized floats and could publish NaN/Inf into global shader state.

What was done:
- Added 3-frame pressure hysteresis and 15-frame recovery hysteresis to `ThermalDynamicResolutionAdapter`.
- Packed DRS hysteresis counters into the existing telemetry `Reserved` field without changing the 32-byte black-box entry size.
- Removed the runtime upscaling-filter mutation method and stopped writing `UniversalRenderPipelineAsset.renderScale` from the direct fallback path.
- Added finite guards for procedural flora tint and tint strength before `Shader.SetGlobalVector`.
- Guarded procedural flora tint tick registration so it only registers with `GlobalRegistry` in play mode.

Cinematic Cheats used:
- Resolution scaling remains a controlled presentation fake: stable scale changes buy frame time without changing simulation truth.
- Flora biome color remains a deterministic global shader tint, not per-renderer material mutation.

Exact Microseconds saved:
- 5-40 us estimated jitter/state-churn reduction during unstable frame-time windows. Pending profiler capture.
- 0 us claimed for finite tint guard; correctness only.

Verification:
- No dotnet rebuild was executed.
- Static review only; runtime/Unity import remains blocked by the external World/GPR compile dependency.

## 2026-05-15 04:24:58 +04:00 - Post-Follow-Up Static Verification
What was wrong:
- The DRS/flora safety patch changed source state after the prior H-Phi reading.
- Reusing the older `03:57:16` H-Phi numbers would be stale evidence.

What was done:
- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` with a 300s timeout only.
- Ran no `dotnet build`, no `dotnet rebuild`, and no Unity rebuild.
- Recorded latest metrics in status and rationale.

Cinematic Cheats used:
- None added in this verification pass.
- Existing low-tier shader/DRS cheats remain: base+ORM-only low path, procedural caustic gate, rust texture stall gate, and hysteretic render-scale control.

Exact Microseconds saved:
- Audit execution itself saves 0 us at runtime.
- DRS hysteresis still estimates 5-40 us of avoided state churn/jitter during unstable pressure windows pending profiler capture.
- Flora finite guard saves 0 us; it prevents shader global poisoning.

Verification:
- `RuntimeHPhiNarrow=0.010750800`
- `RuntimeHPhiRisk=0.000587147`
- `AllSourceHPhiNarrow=0.009572479`
- `AllSourceHPhiRisk=0.000482295`
- `ArchitecturalPurity=0.996447602`
- `MemoryAlignment=0.504761905`
- `UnityUpdateMethods=2`
- `StructLayoutAttributes=954`
- `AupPrecisionRisk=0`

## 2026-05-15 04:46:29 +04:00 - Follow-Up No-Rebuild Underwater Visuals Lookup Hygiene
What was wrong:
- `HectonUnderwaterVisuals` still carried runtime `GetComponent<T>` / `GetComponentInParent<T>` lookup debt in camera recovery paths.
- The file is a fragile presentation hub with Crest ownership, editor preview, gameplay camera composition, and underwater pass control, so broad refactoring would be riskier than the debt being removed.

What was done:
- Replaced runtime camera/component probes with `TryGetComponent(out T)`.
- Replaced `GetComponentInParent<Camera>()` with a zero-allocation parent `Transform` walk that preserves first-parent-camera semantics.
- Left `UNITY_EDITOR` fallback discovery code intact.

Cinematic Cheats used:
- None added. This was rendering hot-path hygiene and static coupling cleanup.

Exact Microseconds saved:
- Estimated 0-5 us CPU on rare camera recovery frames.
- No steady-state frame-time savings claimed; this is primarily H-Phi/zero-GC hygiene.

Verification:
- No dotnet rebuild was executed.
- `git diff --check` on `HectonUnderwaterVisuals.cs`: no whitespace errors; LF-to-CRLF warning only.
- Brace scan: `564/564`.
- Runtime lookup scan: no runtime `GetComponent<T>` / `GetComponentInParent<T>` left in `HectonUnderwaterVisuals`; remaining lookup patterns are `UNITY_EDITOR` fallback discovery.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010750370`, `RuntimeHPhiRisk=0.000590952`, `AllSourceHPhiNarrow=0.009572568`, `AllSourceHPhiRisk=0.000485398`, `ArchitecturalPurity=0.996447602`, `MemoryAlignment=0.505023797`, `GetComponentCalls=532`, `StructLayoutAttributes=955`, `AupPrecisionRisk=0`.

## 2026-05-15 05:12:40 +04:00 - Follow-Up No-Rebuild Flashlight Voxel Shadow Provider Hygiene
What was wrong:
- `HectonFlashlightVoxelShadowProvider` still used `GetComponent<PlayerFlashlight>()` in cold setup/retry paths.
- Disposed native SDF staging buffers were unregistered and disposed but not reset to default, which weakens long-session state inspection.

What was done:
- Replaced both flashlight component lookups with `TryGetComponent(out _flashlight)`.
- Reset `_occupancyVolume` and `_sdfVolume` to `default` immediately after dispose.
- Kept voxel resolution clamp, incremental slice refresh, SDF encoding, and shader global publication behavior unchanged.

Cinematic Cheats used:
- Existing flashlight shadow remains a bounded voxel SDF visual fake instead of allocating shadow-map VRAM or doing physical light transport.

Exact Microseconds saved:
- Estimated 0-2 us CPU on rare flashlight component recovery frames.
- No steady-state Tick saving claimed; the main gain is static lookup debt and native handle hygiene.

Verification:
- No dotnet rebuild was executed.
- `git diff --check` on `HectonFlashlightVoxelShadowProvider.cs`: no whitespace errors; LF-to-CRLF warning only.
- Brace scan: `66/66`.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010762392`, `RuntimeHPhiRisk=0.000592295`, `AllSourceHPhiNarrow=0.009582622`, `AllSourceHPhiRisk=0.000486054`, `ArchitecturalPurity=0.996447602`, `DataSovereignty=0.021386637`, `MemoryAlignment=0.505023797`, `GetComponentCalls=530`, `StructLayoutAttributes=955`, `AupPrecisionRisk=0`.

## 2026-05-15 12:38:17 +04:00 - Follow-Up No-Rebuild Presentation UI Lookup Hygiene
What was wrong:
- Clean Echelon 8 UI/presentation setup scripts still carried local `GetComponent<T>`, `GetComponentInParent<T>`, and `GetComponentInChildren<T>` lookup debt.
- The debt was mostly cold-path, but it inflated H-Phi and left inconsistent zero-GC lookup style across tooltip, PDA, visor, localization, loading, pause, and HUD setup.

What was done:
- Updated `ActionProgressHUD`, `UIFadeTransition`, `EngineHealthOverlay`, `HUDSaveNotificationLink`, `UITooltip`, `MainMenuAudioIntegration`, `HectonTextNode`, `RelayHUDElement`, `SaveSlotHoverPreview`, `LoadingScreenController`, `PDASpectrumTab`, `PauseMenuHost`, `DiegeticPdaFocusDistanceController`, `DiegeticVisorHudMesh`, `LocalizedTMPAutoSizer`, and `LocalizedLayoutMirror`.
- Replaced same-object component probes with `TryGetComponent(out T)`.
- Replaced parent/camera/canvas/volume discovery with zero-allocation `Transform` walks where behavior required hierarchy search.
- Preserved active-child semantics for PDA focus volume discovery and did not change raycast, DOF, visor mesh, localization, or generated UI behavior.

Cinematic Cheats used:
- None added. This pass was presentation-domain hygiene.
- Existing visual fake policy remains intact: local UI/visor setup stays cheap on low tier, while high tier retains shader/post visual overkill.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold UI setup/recovery frames.
- Estimated 0-5 us CPU on PDA focus/visor camera recovery frames.
- No steady-state Tick saving claimed; static H-Phi and lookup hygiene were the measurable outputs.

Verification:
- No dotnet rebuild was executed.
- `rg` over the 16 edited files found no remaining `GetComponent*<T>` matches.
- `git diff --check` on edited files: no whitespace errors; LF-to-CRLF warnings only.
- Brace scan: all edited files balanced.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.01082338`, `RuntimeHPhiRisk=0.00060621`, `AllSourceHPhiNarrow=0.009634899`, `AllSourceHPhiRisk=0.000495259`, `ArchitecturalPurity=1`, `DataSovereignty=0.021386637`, `MemoryAlignment=0.506081438`, `GetComponentCalls=503`, `UnityUpdateMethods=0`, `StructLayoutAttributes=957`, `AupPrecisionRisk=0`.

## 2026-05-15 12:55:12 +04:00 - Follow-Up No-Rebuild Diegetic UI Lookup Consolidation
What was wrong:
- Diegetic PDA/panel setup, relay HUD fail-safe construction, suit advisory binding, settings camera recovery, and UI particle setup still had local `GetComponent*<T>` hierarchy lookup debt.
- A first attempt at generic local descendant helpers reduced calls but added unnecessary local source debt.

What was done:
- Replaced local probes in `SuitAdvisoryController`, `UIParticleEffect`, `SettingsLivePreview`, `RelayHUDRuntimeBootstrap`, `DiegeticPDAController`, and `DiegeticPanelController`.
- Reused `ComponentReferenceUtility.ResolveOwnedComponent<T>` for descendant-owned component discovery instead of keeping duplicated helpers.
- Kept the generated UI hierarchy, PDA render texture path, cursor cache, relay marker layout, particle configuration, and advisory logic behavior-equivalent.

Cinematic Cheats used:
- None added. This pass removed presentation setup debt only.
- Existing PDA/visor visual fakes remain: cached pointer targets, RT-backed diegetic panel, bounded relay HUD marker, and no extra physical simulation.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold setup/recovery frames.
- No steady-state Tick saving claimed; the measured win is static lookup debt reduction.

Verification:
- No dotnet rebuild was executed.
- `rg` over the 22 edited files found no remaining `GetComponent*<T>` matches.
- `git diff --check` on edited files: no whitespace errors.
- Brace scan: all edited files balanced.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010821867`, `RuntimeHPhiRisk=0.000610985`, `AllSourceHPhiNarrow=0.009633634`, `AllSourceHPhiRisk=0.000498924`, `ArchitecturalPurity=1`, `DataSovereignty=0.021383648`, `MemoryAlignment=0.506081438`, `GetComponentCalls=481`, `UnityUpdateMethods=0`, `StructLayoutAttributes=957`, `AupPrecisionRisk=0`.

## 2026-05-15 13:27:58 +04:00 - Follow-Up No-Rebuild Procedural Overlay Lookup Consolidation
What was wrong:
- Hecton OS boot, death memory dump, subtitle, debug overlay, and builder status UI builders still used local `GetComponent<T>` probes during procedural construction and canvas fallback recovery.
- The debt was cold-path, but it inflated H-Phi and kept inconsistent component-lookup style in Presentation & UX.

What was done:
- Updated `BuilderStatusOverlay`, `HectonOSBootManager`, `PDADeathMemoryDump`, `SubtitleManager`, and `SubnauticaSystemsDebugUI`.
- Replaced same-object and freshly-created-object `GetComponent<T>` calls with `TryGetComponent(out T)`.
- Preserved generated hierarchy, TMP registration, canvas fallback behavior, overlay visibility, and tick registration cadence.

Cinematic Cheats used:
- None added. This pass removed UI setup debt only.
- Existing presentation cheats remain: Hecton-OS boot text, death-dump scroll, subtitle waveform fake, and debug overlay stay deterministic and cheap.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold UI construction/recovery frames.
- No steady-state Tick saving claimed; static H-Phi lookup debt reduction is the measured output.

Verification:
- No dotnet rebuild was executed.
- `rg` over the 5 edited files found no remaining `GetComponent*<T>` matches.
- `git diff --check` on edited files: no whitespace errors; LF-to-CRLF warnings only.
- Brace scan: `BuilderStatusOverlay 77/77`, `HectonOSBootManager 54/54`, `PDADeathMemoryDump 42/42`, `SubtitleManager 133/133`, `SubnauticaSystemsDebugUI 105/105`.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010671906`, `RuntimeHPhiRisk=0.000607563`, `AllSourceHPhiNarrow=0.009509931`, `AllSourceHPhiRisk=0.000496385`, `ArchitecturalPurity=1`, `DataSovereignty=0.021132597`, `MemoryAlignment=0.50499737`, `GetComponentCalls=448`, `UnityUpdateMethods=0`, `StructLayoutAttributes=960`, `AupPrecisionRisk=0`.

## 2026-05-15 13:32:15 +04:00 - Follow-Up No-Rebuild Pause And PDA Tab Lookup Consolidation
What was wrong:
- Pause controls and PDA atlas/data-log/barter/construction/controls/loadout tab builders still had local `GetComponent<T>` and `GetComponentInParent<T>` debt.
- The debt was cold-path, but it kept H-Phi lookup count high and mixed direct Unity hierarchy search with the newer zero-GC style.

What was done:
- Updated `PauseControlsPanel`, `PDAAtlasSignalTab`, `PDADataLogTab`, `PDABarterTab`, `PDAConstructionTab`, `PDAControlsRebindUI`, and `PDALoadoutTab`.
- Replaced same-object and generated-object component probes with `TryGetComponent(out T)`.
- Replaced parent PDA/pause-owner recovery with bounded `Transform` walks.
- Kept generated tab hierarchy, fonts, selection indicators, madness FX binding, and loadout/barter/construction behavior unchanged.

Cinematic Cheats used:
- None added. This was UI setup hygiene.
- Existing PDA and pause presentation remains deterministic and fake-first: generated panels, char-buffer text, cached indicators, and no physical simulation.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold tab construction/recovery frames.
- No steady-state Tick saving claimed. Static lookup debt reduction is the measured output.

Verification:
- No dotnet rebuild was executed.
- `rg` over the 7 edited files found no remaining `GetComponent*<T>` matches.
- `git diff --check` on edited files: no whitespace errors; LF-to-CRLF warnings only.
- Brace scan: `PauseControlsPanel 125/125`, `PDAAtlasSignalTab 84/84`, `PDADataLogTab 161/161`, `PDABarterTab 92/92`, `PDAConstructionTab 210/210`, `PDAControlsRebindUI 164/164`, `PDALoadoutTab 185/185`.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010671906`, `RuntimeHPhiRisk=0.000610856`, `AllSourceHPhiNarrow=0.009509931`, `AllSourceHPhiRisk=0.000498829`, `ArchitecturalPurity=1`, `DataSovereignty=0.021132597`, `MemoryAlignment=0.50499737`, `GetComponentCalls=416`, `UnityUpdateMethods=0`, `StructLayoutAttributes=960`, `AupPrecisionRisk=0`.

## 2026-05-15 14:11:53 +04:00 - Follow-Up No-Rebuild Large UI Owner Lookup Consolidation
What was wrong:
- `PauseMenuController`, `PDAShellChrome`, and `SettingsManager` still carried the largest clean runtime UI lookup cluster after the smaller tab passes.
- The remaining non-edited lookup matches are editor-only fallback scans or outside the safe runtime UI cleanup slice.

What was done:
- Replaced pause menu canvas/button/panel/event-system probes with `TryGetComponent(out T)` and explicit parent canvas walks.
- Replaced PDA shell owner/intrusion binding probes with `TryGetComponent(out T)` and bounded parent walks.
- Replaced settings parent camera/volume fallback lookups with bounded `Transform` walks.
- Left generated menu hierarchy, PDA shell binding semantics, settings profile cache, and event-system fallback behavior unchanged.

Cinematic Cheats used:
- None added. This pass removed setup lookup debt only.
- Existing presentation cheats remain: generated pause/menu panels, PDA shell chrome, cached settings preview, and no physical UI simulation.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold menu/shell/settings recovery frames.
- No steady-state Tick saving claimed; static lookup count is the measured output.

Verification:
- No dotnet rebuild was executed.
- `rg` over runtime UI/visor/graphics scope now leaves only editor-only fallback scan matches and no remaining targeted runtime UI builder matches.
- `git diff --check` on changed files: no whitespace errors; LF-to-CRLF warnings only.
- Brace scan: `PauseMenuController 185/185`, `SettingsManager 194/194`; `PDAShellChrome` regex brace count is unchanged from HEAD baseline offset because strings contain braces.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010752435`, `RuntimeHPhiRisk=0.000618924`, `AllSourceHPhiNarrow=0.009581932`, `AllSourceHPhiRisk=0.00050517`, `ArchitecturalPurity=1`, `DataSovereignty=0.021258973`, `MemoryAlignment=0.505783386`, `GetComponentCalls=384`, `UnityUpdateMethods=0`, `StructLayoutAttributes=962`, `AupPrecisionRisk=0`.

## 2026-05-15 15:17:41 +04:00 - Follow-Up No-Rebuild Root Presentation Lookup Consolidation
What was wrong:
- Root PDA/menu/localization/save-thumbnail files and VFX/celestial presentation binders still had safe direct `GetComponent*` probes after the UI folder cleanup.
- Some remaining debt lived in runtime installers for PDA/progression/narrative presentation systems that attach player-owned UI services.

What was done:
- Updated root PDA/menu/save/localization files, PDA marker/runtime installer files, progression/narrative presentation installers, build watermark presenter, camera juice, marine snow, sky follow camera, observer-relative celestial body, and dry-volume stencil source.
- Replaced direct component probes with `TryGetComponent(out T)`.
- Replaced parent component searches with bounded `Transform` walks.
- Preserved generated UI, marker pools, save thumbnail camera fallback, localization runtime, VFX camera binding, and sky/celestial placement behavior.

Cinematic Cheats used:
- None added. This was lookup debt removal.
- Existing visual cheats remain deterministic: PDA chrome, marker HUD, marine snow, camera juice, sky dome follow, and observer-relative celestial placement.

Exact Microseconds saved:
- Estimated 0-10 us CPU on cold presentation setup/recovery frames.
- No steady-state Tick win claimed; static lookup count reduction is the measured output.

Verification:
- No dotnet rebuild was executed.
- Scoped runtime UI/PDA/VFX/Visor/Graphics lookup scan now leaves only the editor-only Crest fallback `GetComponents<MonoBehaviour>()` in `HectonUnderwaterVisuals`.
- `git diff --check` on edited files: no whitespace errors; LF-to-CRLF warnings only.
- Brace scan on edited files: balanced.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010755694`, `RuntimeHPhiRisk=0.000626365`, `AllSourceHPhiNarrow=0.009584727`, `AllSourceHPhiRisk=0.000510846`, `ArchitecturalPurity=1`, `DataSovereignty=0.021276596`, `MemoryAlignment=0.505517604`, `FindObjectCalls=0`, `GetComponentCalls=321`, `UnityUpdateMethods=0`, `StructLayoutAttributes=962`, `AupPrecisionRisk=0`.

## 2026-05-15 20:04:54 +04:00 - Follow-Up No-Rebuild Diegetic Panel Phosphor Tier Gate
What was wrong:
- `DiegeticPanelController` had a blit-backed phosphor persistence fake that could stay active on Unknown/Low/Mx350/low-memory profiles.
- The high-tier CRT persistence effect is visually useful, but on low tier it competes with terminal legibility and RT bandwidth.

What was done:
- Added `ShouldUsePhosphorDecay()` and `IsLowTierPhosphorProfile()` gates.
- Low/Unknown/Mx350/low-memory profiles now release phosphor history textures and use the direct panel render texture.
- Late-frame registration, resource allocation, composite execution, and material texture selection now respect the tier gate.
- The high-tier phosphor persistence path remains unchanged; the remaining `Graphics.Blit` is explicitly recorded as feature-level RenderGraph migration debt.

Cinematic Cheats used:
- Kept the CRT phosphor persistence as a high-tier visual fake.
- Low tier gets the cheaper readable terminal surface instead of simulating phosphor history.

Exact Microseconds saved:
- Estimated 20-120 us GPU/RT bandwidth avoided in active low-tier terminal views pending capture.
- Static H-Phi cannot prove GPU time; runtime profiler/Frame Debugger proof remains blocked by the external compile issue.

Verification:
- No dotnet rebuild was executed.
- `git diff --check` on `DiegeticPanelController.cs`: no whitespace errors; LF-to-CRLF warning only.
- Brace scan: `DiegeticPanelController 201/201`.
- Render-debt scan still shows high-tier `Graphics.Blit` in `DiegeticPanelController` and multiple existing RenderGraph `AddUnsafePass` debts in visor features; not claimed fixed.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010787439`, `RuntimeHPhiRisk=0.000634336`, `AllSourceHPhiNarrow=0.009611624`, `AllSourceHPhiRisk=0.00051719`, `ArchitecturalPurity=1`, `DataSovereignty=0.021306032`, `MemoryAlignment=0.506309148`, `FindObjectCalls=0`, `GetComponentCalls=321`, `LinqSurface=3`, `ManagedFormatSurface=564`, `PrimaryManagedRuntimeRisk=177`, `UnityUpdateMethods=0`, `StructLayoutAttributes=963`, `AupPrecisionRisk=0`.

## 2026-05-15 21:16:28 +04:00 - Follow-Up No-Rebuild Visor RenderGraph Blit Migration
What was wrong:
- Visor fullscreen post chains still had first-party `AddUnsafePass` wrappers whose render funcs only unwrapped native command buffers to call `Blitter.BlitCameraTexture` / `Blitter.BlitTexture`.
- That is legacy RenderGraph surface for passes that are otherwise ordinary material blits.

What was done:
- Migrated the simple material-blit passes to Unity 6 `RenderGraphUtils.AddBlitPass` across atmosphere soot, VR brownout, retina distortion, BIOS diagnostic, scanner projection, noir depth fog, visor fluid distortion, deferred decals, reflection sheen, biolum SSGI composite, half-res particle composite, sonar history/composite, abyssal SSDO, and underwater noir shafts.
- Kept explicit graph reads for depth/history/occlusion/half-res/exposure resources through returned builders.
- Left 4 unsafe passes documented because they are stencil/custom-draw/compute bridges: `HectonDryVolumeFeature` x2, `HectonHolographicEdgeFeature`, and `HectonFluidAdvectionRenderFeature`.

Cinematic Cheats used:
- No visual algorithm changed. The same noir fog, CRT diagnostics, scanner projection, sonar memory, SSDO, and shaft fakes now use graph-visible blit plumbing.

Exact Microseconds saved:
- Estimated 5-60 us CPU/render-graph scheduling hygiene in heavy visor stacks pending Frame Debugger capture.
- Scoped Visor `AddUnsafePass` count reduced from 28 to 4.
- Static H-Phi: `RuntimeHPhiNarrow=0.010787439`, `RuntimeHPhiRisk=0.000636091`, `AllSourceHPhiRisk=0.000518488`, `ManagedFormatSurface=534`, `PrimaryManagedRuntimeRisk=147`, `FindObjectCalls=0`, `GetComponentCalls=321`, `AupPrecisionRisk=0`.
- No dotnet rebuild or Unity rebuild was run.
