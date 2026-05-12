# Rationale_RENDER_FILLRATE

Status: PENDING VERIFICATION

## Intake Decision
Problem: User requested `CURRENT_BATCH.md`, but the active batch source in the repository is `Docs/Tasks/CURRENT_BATCH.txt`; `CURRENT_BATCH.md` was not present under `C:\hades\Hecton8`.
Solution: Extracted only `<AGENT_PROMPT id="RENDER_FILLRATE">` via PowerShell raw read and singleline regex, then discarded neighboring agent prompts from working context.
Rejected Alternatives: Trusting the chat prompt only was rejected because the batch protocol requires CLI extraction. Searching only for `.md` was rejected because the repository uses `.txt` for the current batch.
Scalability potential: Low uses opaque/cutout/dither and half-res buffers; Middle keeps dither and limited fog jitter; High/Ultra can spend saved fill-rate on richer noir lighting, higher VFX density, and stronger post detail.
Hardware Impact: On i3/MX350, expected gain is lower transparent fragment cost and lower overdraw pressure; numeric gain is PENDING VERIFICATION until Unity profiling.

## Mandate Selection Decision
Problem: Fill-rate work touches shaders, renderer features, post/VFX, and editor build gates; using unrelated mandates would inflate scope.
Solution: Loaded seven render/perf/GC mandates: URP hotpath, noir shader aesthetics, performance budgets, zero GC, cinematic fake first, GPU sovereignty, and descriptor binding reality check.
Rejected Alternatives: Loading all `.agents-skills` files was rejected as prompt contamination and time waste. Loading only shader aesthetics was rejected because stencil, RenderGraph, and build-gate work cross into C# render infrastructure.
Scalability potential: Low/MX350 gets alpha-test/dither and half-res paths; High/Ultra can keep expensive visual overkill behind shader quality branches.
Hardware Impact: Expected low-end benefit is reduced GPU pixel cost and fewer state/material variants; exact microseconds saved are PENDING VERIFICATION.

## Loop 1 Decisions
Problem: Suit visor glass was a full transparent blended pass (`Queue=Transparent+10`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`), which shades helmet pixels even when most of the effect is low-opacity glass.
Solution: Converted the visor to `TransparentCutout`/`AlphaTest+20`, `Blend Off`, `ZWrite On`, `AlphaToMask On`, added stencil write ref 1, and clipped coverage with the existing Bayer dither threshold before returning opaque alpha.
Rejected Alternatives: Keeping alpha blend was the target defect. Runtime material override was rejected because third-party/runtime material mutation is forbidden and breaks batching discipline.
Scalability potential: Low uses sparse dither coverage; Middle keeps the same path with TAA smoothing; High/Ultra can increase HUD/glass grime intensity without returning to alpha blend.
Hardware Impact: Estimated 180 us saved on i3/MX350 during visor-heavy frames by reducing blended overdraw; PENDING VERIFICATION.

Problem: Acoustic radar overlay drew as a transparent blended full overlay and did not require visor stencil.
Solution: Converted it to `TransparentCutout`/`AlphaTest+90`, added stencil compare equal ref 1, removed blending, and used dithered coverage clips instead of returning transparent black pixels.
Rejected Alternatives: Canvas alpha masking was rejected because it still shades off-visor pixels. Keeping `return 0` with `Blend Off` was rejected because it would write black.
Scalability potential: Low uses hard Bayer coverage; Middle/High can bias coverage upward for denser radar pulses.
Hardware Impact: Estimated 70 us saved on hidden HUD pixels and alpha blend removal; PENDING VERIFICATION.

Problem: Water and opaque world depth were not force-written by a dedicated fill-rate prepass before transparent/refractive effects.
Solution: Added `HectonFillrateDepthPrepassFeature` using RenderGraph raster pass and a hidden depth-only override shader for Water/Terrain/VoxelCave layers.
Rejected Alternatives: Raw command buffer and `ScriptableRenderPass.Execute` were rejected by the Unity 6000 RenderGraph mandate. Editing URP asset settings directly was rejected as project-settings churn.
Scalability potential: Low runs a single cheap depth override. High/Ultra can keep the prepass and spend saved pixels on richer refractive/noir effects.
Hardware Impact: Estimated 110 us saved where water/silt pixels are behind terrain/voxels; PENDING VERIFICATION.

Problem: Half-res VFX feature filtered only `RenderQueueRange.transparent`, missing dithered AlphaTest smoke/plumes after alpha blending removal.
Solution: Expanded the renderer-list filter to `RenderQueueRange.all` while keeping the dedicated TransparentFX layer mask and existing bilateral depth-aware composite.
Rejected Alternatives: Moving smoke back to Transparent was rejected. Per-shader special cases were rejected because layer ownership already isolates VFX.
Scalability potential: Low keeps cutout FX at half resolution; High/Ultra can raise FX density while preserving bilateral resolve.
Hardware Impact: Estimated 220 us saved in dense smoke/plume scenes; PENDING VERIFICATION.

Problem: Noir fog did not have a dedicated black-crush curve in the shared core include.
Solution: Added `HectonCoreLitApplyNoirBlackCrush` and applied it after fog blending, crushing low-luminance deep pixels toward the authored abyss floor.
Rejected Alternatives: Pure black was rejected because shader aesthetics forbid pure `#000000` in scene geometry/post-stack. Extra LUT samples were rejected because MX350 is bandwidth-limited.
Scalability potential: Low gets ALU-only crush; High/Ultra can pair it with denser fog and richer emissive contrast.
Hardware Impact: Estimated visual gain at roughly 30 us ALU cost, no texture bandwidth; PENDING VERIFICATION.

## Loop 1 Compile Gate
Problem: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed before a green compile could be recorded.
Solution: Captured exact errors and isolated them to non-render files: `PlayerFootstepAudio.cs` references missing `_surfaceHits`; `SubmarineFluidDynamics.cs` references missing `ImpactSignal`.
Rejected Alternatives: Editing audio/physics files was rejected as outside the RENDER_FILLRATE domain and likely owned by concurrent agents.
Scalability potential: Not applicable to render scalability; compile verification remains blocked until dependency owners repair those files.
Hardware Impact: No render hardware impact from the compile blocker.

## Loop 2 Decisions
Problem: Main-light shadows were still consuming soft-shadow variants or visibly harsh 1-tap edges on low hardware.
Solution: Added `HectonCoreLitResolveMx350ShadowDither`, which converts low-tier shadow penumbra into screen-space IGN/TAA coverage, and routed DryZone, voxel rock, scatter, wreck, and leviathan main shadow attenuation through it.
Rejected Alternatives: Multi-tap PCF and extra shadow-map fetches were rejected because MX350 is bandwidth-bound. Global URP asset mutation was rejected because renderer settings are shared across agents.
Scalability potential: Low uses 1-tap jittered coverage; Middle keeps the same shader path with TAA smoothing; High/Ultra can enable soft-shadow variants and bypass the helper branch.
Hardware Impact: Estimated 95 us saved on i3/MX350 versus soft PCF in visible world geometry; PENDING VERIFICATION.

Problem: Volumetric fog/shaft banding needed cleanup without raising march cost.
Solution: Added temporal IGN phase offsets to `Hecton_VolumetricLight.compute` and `Hecton_ScooterVolumetricShafts.shader`, preserving the existing low tap counts and half-res resolve.
Rejected Alternatives: 3D blue-noise textures and extra raymarch steps were rejected; both spend bandwidth/fill-rate to hide a sampling problem.
Scalability potential: Low keeps stochastic 1-7 step fog; High/Ultra can spend saved budget on stronger shaft density and richer fog color.
Hardware Impact: Estimated 45 us saved versus increasing fog taps; PENDING VERIFICATION.

Problem: Caustics still used a ridge/hash helper instead of the demanded 3-sine ALU-only fake.
Solution: Replaced the caustic core with three overlapping sine waves in `HectonCoreLitEvaluateProceduralCaustics`; no texture fetch was introduced.
Rejected Alternatives: Projected caustic textures and decal caustics were rejected as bandwidth/overdraw costs.
Scalability potential: Low gets cheap ALU shimmer; High/Ultra can raise caustic strength/color grading without texture pressure.
Hardware Impact: Estimated 60 us bandwidth-equivalent savings in caustic-heavy views; PENDING VERIFICATION.

Problem: Cutout smoke/plumes/visor could create hard intersections after alpha blending was removed.
Solution: Dither coverage now multiplies by SceneDepth intersection fade before clip in `AbyssalBlackSmoke.shader`, `Hecton_LeakPlume.shader`, and `SuitVisor.shader`.
Rejected Alternatives: Soft-particle alpha blending was rejected because the whole mandate is to kill blended overdraw.
Scalability potential: Low uses depth-faded dither; High/Ultra can increase particle density while staying half-res/cutout.
Hardware Impact: Estimated 80 us saved versus transparent soft particles on MX350; PENDING VERIFICATION.

Problem: Vertex-displaced vegetation needs correct motion vectors or TAA smears kelp/fish-like swaying silhouettes.
Solution: Audited the existing `Hecton_IndirectVegetationMotionVectors.shader` path: it evaluates current and previous animated positions and `HectonIndirectVegetationRenderer` submits Object motion passes for near/far indirect vegetation.
Rejected Alternatives: Adding a second motion path to kelp materials was rejected as duplication and likely shader variant churn; camera-only motion was rejected because it ignores vertex sway.
Scalability potential: Low uses one dedicated motion pass for vegetation; High/Ultra can increase vertex displacement amplitude without TAA ghosting.
Hardware Impact: Estimated 35 us stability gain by avoiding broader TAA resolve artifacts; PENDING VERIFICATION.

## Loop 2 Compile Gate
Problem: Compile verification still cannot go green.
Solution: Re-ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`; current blockers are non-render files: `GlobalSignals.cs` missing signal structs, `FaunaBrain.cs` missing `FaunaTier1LodProxyEntry`, and `ConstructionManager.cs` missing the current `IOriginShiftListener.OnOriginShift` signature.
Rejected Alternatives: Editing core signals/fauna/construction was rejected as outside the render-fillrate domain and likely owned by other agents.
Scalability potential: Not applicable to render scalability; compile verification remains dependency-blocked.
Hardware Impact: No render hardware impact from the compile blocker.

## Loop 3 Decisions
Problem: Distant kelp still paid near-shader costs past the desired 20m flat-noir boundary.
Solution: Defaulted indirect vegetation near/far split to 20m and added `HectonCoreLitResolveFlatNoirLod`; kelp and GPUI kelp skip the normal-map sample and suppress specular/rim/transmission in flat-noir far mode.
Rejected Alternatives: Per-instance material swapping was rejected because it breaks batching and adds CPU churn. A new shader asset was rejected because the existing far pass already carries `_HectonLodPassMode`.
Scalability potential: Low switches at 20m to flat/no-spec; Middle keeps flat far cards; High/Ultra can spend saved cycles on denser near kelp and stronger biolum.
Hardware Impact: Estimated 140 us saved in distant kelp fields on i3/MX350; PENDING VERIFICATION.

Problem: Stencil visor overlay and opaque world prepass were repeated as explicit objectives after being implemented in Loop 1.
Solution: Re-audited the actual shader/prepass code: visor writes stencil ref 1, acoustic HUD compares Equal ref 1, and the RenderGraph fill-rate prepass writes Water/Terrain/VoxelCave depth before silt/refractive work.
Rejected Alternatives: Duplicating a second stencil system or a second depth prepass was rejected as redundant state churn.
Scalability potential: Low gets hard stencil/depth rejection; High/Ultra can spend rejected pixels on denser HUD/fog visuals.
Hardware Impact: Stencil remains estimated 70 us saved; depth prepass remains estimated 110 us saved; PENDING VERIFICATION.

Problem: Fauna lighting should not depend on real-time point lights.
Solution: Leviathan fauna now receives vertex-stage SH ambient through `ambientSH`, and the fauna shader skips point-light keywords. The existing shader path has no additional-light loop.
Rejected Alternatives: Runtime point lights attached to fauna were rejected because they add culling, shadow, and overdraw pressure. Per-pixel SH was rejected for low tier because vertex SH is sufficient under noir fog.
Scalability potential: Low uses vertex SH approximation; High/Ultra can use richer emissive/biolum pulses without spawning point lights.
Hardware Impact: Estimated 65 us saved for large fauna lighting on MX350; PENDING VERIFICATION.

Problem: MX350 builds must delete point-light variants.
Solution: Confirmed `HectonShaderVariantStripper` is an active `IPreprocessShaders`/`IPreprocessBuildWithReport` implementation and strips `POINT_LIGHTS`/point keywords by default unless `HECTON_MX350_SHADER_STRIP=0`.
Rejected Alternatives: Relying on material keyword usage alone was rejected because point-light variants can survive through URP/global keywords.
Scalability potential: Low strips point/soft variants; High/Ultra can disable the env-controlled strip only for expensive capture builds.
Hardware Impact: Runtime savings are indirect through smaller variant set and lower warmup pressure; exact microseconds PENDING VERIFICATION.

## Loop 3 Compile Gate
Problem: Compile verification remains blocked after Loop 3.
Solution: Re-ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`; current blockers are non-render save/construction code: `SaveBinaryPayloadCodec.cs`, `HabitatGraphManager.cs`, `SaveBinaryStorage.cs`, and `ConstructionManager.cs`.
Rejected Alternatives: Editing save/construction systems was rejected as outside render-fillrate ownership.
Scalability potential: Not applicable to render scalability; compile verification remains dependency-blocked.
Hardware Impact: No render hardware impact from the compile blocker.

## Loop 4 Decisions
Problem: Fluid/blood aftermath decals were still available as transparent mesh quads, keeping blended overdraw alive in rupture-fluid scenes.
Solution: Routed active `AbyssalFluidDecalManager` states into `DeferredDecalPass` as screen-space projected decal matrices/tints, skipped the mesh draw while screen-space mode is active, and converted the mesh fallback shader to cutout/dither with `Blend Off`.
Rejected Alternatives: Particle blood clouds and transparent quad decals were rejected because they stack overdraw exactly where the scene is already foggy. A new decal GameObject path was rejected because the existing fullscreen deferred pass already owns screen-space projection.
Scalability potential: Low uses capped 32 screen-space fluid decals with no transparent mesh draw; Middle can raise active rupture count within the same buffer; High/Ultra can spend the saved fill-rate on richer decal atlas art and stronger wet sheen.
Hardware Impact: Estimated 120 us saved on i3/MX350 during rupture-fluid visibility spikes; PENDING VERIFICATION.

Problem: Visor refraction always paid a similar scene-sample path regardless of quality tier.
Solution: Added a low-tier static hash UV offset path when `_HectonVisorRefractionScale` is near zero and a second scene-color tap only when the scale is high; the existing controller quality map remains the driver.
Rejected Alternatives: Always-on two-tap refraction was rejected because low-tier visor distortion must be cheap. Disabling refraction entirely was rejected because even low tier needs helmet-glass motion cues.
Scalability potential: Low uses static offset; Middle uses one dynamic scene sample; High/Ultra gets the second refraction tap for visual overkill.
Hardware Impact: Estimated 55 us saved on low visor quality while preserving a high-tier richer lens path; PENDING VERIFICATION.

Problem: Authored flora biolum pulses still depended on sampled material masks for glow identity.
Solution: Kelp and coral authored glow masks now use `_Time.y`, world position, UV/shape terms, and triangle waves; texture-mask contribution was removed from the authored biolum pulse mask.
Rejected Alternatives: Emissive textures and extra mask fetches were rejected because flora fields are dense and bandwidth-sensitive. Spawning point lights was already rejected by the fauna/flora lighting mandate.
Scalability potential: Low gets procedural glow with no emissive texture identity; High/Ultra can raise biolum strength and pulse density without extra texture samples.
Hardware Impact: Estimated 35 us saved in dense flora frames by shifting glow identity to ALU and existing geometry data; PENDING VERIFICATION.

Problem: Indirect vegetation carried color, biolum intensity, and damage in separate scalar lanes, increasing BRG payload pressure.
Solution: Packed RGB color plus 8-bit biolum intensity and 8-bit damage state into `HectonVegetationInstanceData.BioluminescenceColor`; the main indirect vegetation shader decodes the packed alpha and preserves the 64-byte stride.
Rejected Alternatives: Adding another BRG metadata buffer was rejected because it increases binding and memory-fetch pressure. Widening the struct was rejected because it risks all culling/motion/depth consumers.
Scalability potential: Low keeps the compact payload; High/Ultra can use the decoded damage lane for richer decay/emission response without changing the BRG contract.
Hardware Impact: Estimated 25 us saved in vegetation-heavy views through lower payload pressure and no added buffer fetch; PENDING VERIFICATION.

Problem: Transparent-overdraw regressions needed to fail before builds ship, not after manual frame-debugger inspection.
Solution: Added `HectonTransparentOverdrawBuildGuard`, an editor prebuild gate that scans `02_HECTON_WORLD` material dependencies, estimates transparent pixel overlap, writes `Library/Hecton8/transparent_overdraw_report.csv`, and fails above factor 2.5.
Rejected Alternatives: A profiler-only checklist was rejected because it is non-deterministic and easy to skip. Runtime sampling was rejected because this is a build hygiene gate, not a frame feature.
Scalability potential: Low/MX350 builds are blocked by transparent stack regressions; High/Ultra can still pass if expensive effects are cutout/stencil/screen-space instead of blended.
Hardware Impact: Runtime impact is 0 us; build-time guard prevents reintroducing multi-layer transparent overdraw.

## Loop 4 Compile Gate
Problem: The runtime project needed to prove the new render C# changes do not break compilation.
Solution: Re-ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`; it passed with 0 warnings and 0 errors.
Rejected Alternatives: Stopping at shader-only audit was rejected because `DeferredDecalPass`, `AbyssalFluidDecalManager`, and BRG packing changed C# contracts.
Scalability potential: Compile green means the runtime render/fill-rate code is integratable; GPU profiling remains pending.
Hardware Impact: No direct hardware impact from compilation.

Problem: Editor build verification for the new overdraw gate could not complete through the full editor project.
Solution: Ran `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`; it failed before editor validation in `CombatDamageRuntime.cs` due to duplicate `ResolveDominantAxisDirection` definitions.
Rejected Alternatives: Editing combat runtime was rejected as outside render-fillrate ownership. Removing the build gate was rejected because task 20 requires it.
Scalability potential: Not applicable to render scalability; editor verification remains dependency-blocked until combat code is repaired.
Hardware Impact: No render hardware impact from the editor compile blocker.

## OMEGA POLISH CHANGES
Problem: The Polish Mandate required a "dear lie" audit over the touched math and a hard check for divisions/normalization that can be cheaper.
Solution: Kept the already-cheated render math: 3-sine caustics instead of texture/projector caustics, static low-tier visor UV offset instead of full refraction, triangle-wave biolum pulses instead of emissive texture masks, screen-space fluid decals instead of transparent mesh/particle blood, and bit-packed vegetation presentation state instead of another BRG buffer. Replaced late divisions in `AbyssalFluidDecal.shader`, `SuitVisor.shader`, and `Hecton_HalfResParticleComposite.shader` with `rcp` multiplication.
Rejected Alternatives: More "honest" refraction, particle blood volumes, projected caustic textures, and wider BRG payloads were rejected because they spend pixels/bandwidth instead of buying visible noir contrast.
Scalability potential: Low uses cutout/dither, static offset refraction, one-tap dither shadows, vertex SH, flat-noir far kelp, and half-res VFX. Middle keeps the same cheap approximations with TAA smoothing. High/Ultra can spend the saved cycles on a second visor refraction tap, denser decals/fog, stronger biolum, and richer atlas art without reintroducing alpha blending.
Hardware Impact: Added polish saves are small but deterministic: rcp conversions avoid shader divides in repeated full/half-screen paths; final estimated aggregate remains roughly 1.3 ms saved on i3/MX350 in worst fill-rate scenes, PENDING GPU CAPTURE.

Problem: Runtime additions needed a zero-GC scan.
Solution: Audited new runtime C# paths. Runtime allocations are cold storage only: decal upload arrays, fluid decal matrix/color scratch arrays, and existing render pass material/buffer lifecycle. New per-frame loops use indexed `for` loops. The editor overdraw guard is wrapped in `#if UNITY_EDITOR`; its `foreach`, strings, `StringBuilder`, and `.ToString()` calls are build-time only.
Rejected Alternatives: Allocating decal lists per frame or adding LINQ/report strings to runtime was rejected. A runtime overdraw sampler was rejected because the task needs a build gate.
Scalability potential: Low gets predictable fixed-capacity buffers; High/Ultra can increase authored visual density without changing the runtime allocation model.
Hardware Impact: No managed allocations were added to runtime hot paths; GC impact is expected 0 B/frame, PENDING PROFILER CAPTURE.

Problem: Domain scope had to be justified after touching World/Optimization files.
Solution: Cross-domain edits are presentation interfaces only: `AbyssalFluidDecalManager` exposes screen-space decal data to the existing render pass, `HectonIndirectVegetationContracts` preserves BRG stride while packing presentation payload, and the Optimization editor guard is build-time render hygiene.
Rejected Alternatives: Moving rendering data through gameplay dependencies or adding new global services was rejected; existing `GlobalRegistry.AbyssalFluidDecals` and BRG payloads were used.
Scalability potential: The presentation layer remains decoupled; low/high visual policy stays in renderer/shader quality paths.
Hardware Impact: No gameplay simulation cost was introduced.

Problem: The mandated `Hecton8.Core.csproj` build initially failed after stale/missing NuGet assets and then transient external blockers.
Solution: Ran `dotnet build .\Hecton8.Core.csproj`; restore regenerated missing assets files. Final `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` passed with 0 warnings and 0 errors. `Hecton8.Editor.csproj` and `Assembly-CSharp.csproj` also passed with 0 warnings and 0 errors.
Rejected Alternatives: Reporting the first failing command was rejected after restore/build state changed; the final verified commands are recorded instead.
Scalability potential: Build health is now clean for runtime/core/editor C# verification; shader import/RenderDoc/Frame Debugger remain pending.
Hardware Impact: No direct hardware impact.

Final scoped diff owned by RENDER_FILLRATE:
- Docs: `Docs/Tasks/Status_RENDER_FILLRATE.md`, `Docs/AgentLogs/Rationale_RENDER_FILLRATE.md`, `Docs/AgentLogs/LOG_RENDER_FILLRATE.md`.
- Render features/C#: `Assets/_Project/Scripts/Visor/DeferredDecalPass.cs`, `Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs`, `Assets/_Project/Scripts/Visor/HectonFillrateDepthPrepassFeature.cs`, `Assets/_Project/Scripts/World/AbyssalFluidDecalManager.cs`, `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs`, `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`, `Assets/_Project/Scripts/Optimization/Editor/HectonTransparentOverdrawBuildGuard.cs`.
- Shaders: `SuitVisor.shader`, `Hecton_HUD_AcousticRadarOverlay.shader`, `Hecton_HalfResParticleComposite.shader`, `AbyssalFluidDecal.shader`, `Hecton_DeferredDecal.shader`, `Hecton_FillrateDepthOnly.shader`, `Hecton_CoreLit.hlsl`, `AbyssalBlackSmoke.shader`, `Hecton_LeakPlume.shader`, `Hecton_NoirDepthFog.shader`, `Hecton_DryZoneLit.shader`, `Hecton_AbyssalVoxelRock.shader`, `Hecton_ScatterIndirectLit.shader`, `Hecton_WreckIndirectLit.shader`, `Hecton_LeviathanOrganic.shader`, `Hecton_VolumetricLight.compute`, `Hecton_ScooterVolumetricShafts.shader`, `Hecton_KelpMaster.shader`, `Hecton_KelpMaster_GPUI.shader`, `Hecton_CoralMaster.shader`, `Hecton_CoralMaster_GPUI.shader`, `Hecton_IndirectVegetation.shader`.

## Loop 6 Additional Anti-Alpha Sweep
Problem: A post-compression static scan still found blended presentation shaders outside the first task list: blueprint/fabricator holograms, diegetic UI glyph/panel shaders, scanner/PDA overlays, tether/pipe visuals, rain/laser/silt effects, phantom drones, seam-gap masking, and the sun pass. Several used additive blending rather than `SrcAlpha`, but they still lived in `Transparent` queues and stacked pixels.
Solution: Converted the runtime HECTON presentation shaders to `TransparentCutout`/`AlphaTest`, `Blend Off`, dithered `clip(alpha - IGN)`, and opaque return alpha. World shell/pipe/phantom paths now write depth where safe; screen/HUD/decal-like paths stay depth-read-only where depth writes would corrupt overlay order. The overdraw build guard now treats `Blend Off` and the no-op `Blend One Zero` as safe, and flags other non-off blend states.
Rejected Alternatives: Keeping additive "because it is not alpha" was rejected for runtime HECTON presentation effects because the prompt bans transparent overdraw, not just one blend equation. Converting Crest damping passes was rejected because they are render-target damping operations, not scene alpha presentation. Converting hidden `Hecton_OverdrawHeatmap` was rejected because its additive pass is the diagnostic accumulation tool for detecting overdraw.
Scalability potential: Low/MX350 uses hard stochastic coverage with TAA accumulation and depth rejection. Middle keeps the same shader math with denser authored coverage. High/Ultra can increase hologram, silt, scanner, and celestial intensity without reintroducing transparent queues.
Hardware Impact: Broad sweep removes remaining HECTON runtime transparent/additive presentation queues from the static shader set. Additional low-end estimate is 180-320 us saved in worst UI+hologram+silt scenes beyond the initial 1,335 us estimate; exact GPU number remains PENDING RenderDoc/MX350 capture.

Problem: Unity batchmode was needed because generated `.csproj` files did not include the newly added overdraw guard source.
Solution: Ran Unity 6000.4.1f1 batchmode with log output to `Docs/AgentLogs/Unity_RENDER_FILLRATE.log`.
Rejected Alternatives: Claiming the guard compiled through stale generated `.csproj` files was rejected because `Select-String` showed no `HectonTransparentOverdrawBuildGuard` entry in any generated project file.
Scalability potential: Unity import remains the authoritative gate for shader/import validation once unrelated editor compile blockers are repaired.
Hardware Impact: No runtime hardware impact.

Problem: Unity batchmode compile stopped before shader import validation.
Solution: Recorded the exact external blocker: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs(745,34)` and `(746,34)` attempt implicit float-to-uint conversion. The Unity log shows `Hecton8.Optimization.Editor.dll` was compiled, IL-postprocessed, and copied before the later DataMonolith failure, so the overdraw guard assembly reached Unity compilation. The render-fillrate changes were not reverted because the runtime/editor `dotnet build` gates still pass and the Unity error is outside the assigned domain.
Rejected Alternatives: Editing DataMonolith compiler code was rejected as outside RENDER_FILLRATE ownership and not a presentation interface.
Scalability potential: None until dependency owner clears the editor compile wall.
Hardware Impact: No render hardware impact from the external compile wall.

Loop 6 scoped shader additions:
- `Hecton_BlueprintWireInstanced.shader`, `Hecton_DiegeticPanelUnlit.shader`, `Hecton_DiegeticTooltipGlyph.shader`, `Hecton_FabricatorHologram.shader`, `Hecton_FabricatorProgressBeam.shader`, `Hecton_FlexiblePipe.shader`, `Hecton_HolographicEdge.shader`.
- `Hecton_OceanRainRippleDecal.shader`, `Hecton_PDA_SonarPointCloud.shader`, `Hecton_ScannerPulseInstanced.shader`, `Hecton_ScannerMarkerInstanced.shader`, `Hecton_TetherLineStrip.shader`, `Hecton_UI_CompassRibbon.shader`, `Hecton_VoxelBakeGhost.shader`, `Hecton_PDA_SonarMap.shader`, `Hecton_RadarBlipInstanced.shader`.
- `Hecton_FlashlightConeSilt.shader`, `Hecton_LaserCutRadianceDecal.shader`, `Hecton_PhantomDrones.shader`, `Hecton_SeamGapDitherIndirect.shader`, `Sun.shader`.
- `Assets/_Project/Shaders/UI/Hecton_RetinaStressPulse.shader`, `Hecton_IGNDitherDissolve.shader`, `Hecton_DiegeticPanelDepthFade.shader`, `Hecton_DataRecPulse.shader`.
- `Assets/_Project/_Archive/HectonOcean.shader`.

Problem: A wider `_Project` scan found four live UI shaders outside `Art/Shaders` still using `Blend SrcAlpha`.
Solution: Converted `RetinaStressPulse`, `IGNDitherDissolve`, `DiegeticPanelDepthFade`, and `DataRecPulse` to cutout/dither coverage with `Blend Off`; depth-faded diegetic panel now writes depth, pure overlay pulses remain depth-read-only.
Rejected Alternatives: Leaving live overlay UI as alpha-blended was rejected because overlay queues are still full-screen fill-rate pressure. Keeping `_Archive/HectonOcean.shader` untouched was initially considered because it is archived, then rejected because the batch mandate is a static zero-alpha police pass and the file was cheap to make cutout/opaque.
Scalability potential: Low tier gets dithered screen overlays; high tier can raise pulse density/color response without returning to alpha blending.
Hardware Impact: Removes the last `Blend SrcAlpha` hits under `_Project`; residual non-off shader blend hits are non-alpha Crest damping and hidden editor overdraw diagnostics only.
