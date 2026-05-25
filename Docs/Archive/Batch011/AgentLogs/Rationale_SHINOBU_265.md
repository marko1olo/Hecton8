# Rationale_SHINOBU_265

Status: PENDING VERIFICATION

## Decision 000 - State Files

Problem: Agent prompt requires disk-backed state before work, but status and rationale files were absent.
Solution: Created explicit status and rationale files under `Docs/Tasks` and `Docs/AgentLogs`.
Rejected Alternatives: Chat-only memory; it violates anti-amnesia and CTO log requirements.
Scalability potential: No runtime impact. Enables bounded task execution instead of one large speculative change.
Hardware Impact: 0 us/frame on i3/MX350; documentation-only.

## Decision 001 - Mandate Selection

Problem: Water extinction touches shader math, GPU binding, AUP precision, native DTO layout, dispatcher phase ownership, and crash telemetry.
Solution: Selected and read 8 mandates: Zero-GC, ARM64 struct layout, AUP floating origin, execution phases, noir shader/fog, descriptor binding, URP hot path, and debug telemetry.
Rejected Alternatives: Reading unrelated AI/physics/save mandates; too broad and increases risk of cross-domain edits.
Scalability potential: Low tier uses monochrome ALU collapse and shader fakes; middle tier restores spectral extinction; high tier adds stronger scattering; ultra spends saved cost on visual overkill without changing gameplay truth.
Hardware Impact: Static planning only. Runtime target remains <= 0.1 ms suspicious threshold with no managed allocations in hot paths.

## Decision 002 - Crest Math Source

Problem: Prompt names `Assets/~Quarantine_Crest5/`, but that folder is absent in this workspace.
Solution: Used the installed Crest package source under `Packages/com.waveharmonic.crest`, specifically underwater and volume-lighting HLSL. Extracted only Beer-Lambert `exp(-(absorption + scattering) * distance)`, scatter ratio `scattering / extinction`, and phase-scattered in-light concepts.
Rejected Alternatives: Importing or depending on Crest runtime symbols; that would create an external shader dependency and variant risk.
Scalability potential: Low uses a scalar monochrome extinction fake; mid/high restores spectral correction; ultra increases in-scatter weight without changing DTO shape.
Hardware Impact: No CPU cost. Shader low path targets one exponential-equivalent evaluation instead of three channel exponentials.

## Decision 003 - Global Water Optics Route

Problem: UberNoir, volumetric fog, and Dear Lie composite need one shared optics fact without hot `Shader.SetGlobalVector` writes.
Solution: Added `WaterOpticsDTO` as a 64-byte explicit layout row and routed it through DataVault lanes `71129`, `71135..71139`, then bound `_GlobalWaterOptics` in `VISUAL_SYNC` via double-buffered `GraphicsBuffer.Target.Constant`.
Rejected Alternatives: Material property blocks, global vectors, and direct Biome Manager references; each either breaks batching, fragments authority, or introduces sibling coupling.
Scalability potential: `QualityAndDepthLimits.x` carries continuous `GlobalQualityWeight`; shader math compresses ALU continuously instead of toggling features.
Hardware Impact: CPU upload is one 64-byte copy plus one constant-buffer bind. Estimated CPU-side cost is under 10 us on i3/MX350 absent Unity driver variance.

## Decision 004 - Dispatcher And Assembly Correction

Problem: The first runtime draft used `job.Run()` for parameter generation and lived under a broad VFX/Core compile surface.
Solution: Moved the runtime to scoped `Hecton8.Rendering.WaterOptics.asmdef`, changed parameter generation to scheduled `GenerateMockWaterOpticsJob`, and left only the mapped-buffer copy as a Burst `Run()` because scheduling a mapped buffer would require an immediate completion before unlock.
Rejected Alternatives: Same-frame scheduled GPU-copy job with hidden `Complete()`; that violates the dispatcher completion rule and adds overhead for a single 64-byte copy.
Scalability potential: Assembly isolation reduces compile-wall blast radius; scheduled parameter generation can absorb future biome profile reads without changing shader upload identity.
Hardware Impact: Removed avoidable main-thread math from PreSimulation. Mapped-buffer copy remains 64 bytes, bounded and phase-local.

## Decision 005 - Shader ALU Compression

Problem: Exact RGB Beer-Lambert extinction needs three exponentials per affected opaque pixel, expensive on mobile/TBDR GPUs.
Solution: Implemented a single scalar transmittance plus continuous rational spectral correction controlled by `GlobalQualityWeight`. Low quality collapses toward monochrome darkness; high quality restores RGB water-color separation.
Rejected Alternatives: Binary low/high shader keyword or exact three-channel exp at all quality levels; both violate continuous scalability or mobile ALU budget.
Scalability potential: Low, middle, high, ultra are points on one curve: scalar extinction, partial spectral correction, strong spectral correction, stronger directional in-scatter.
Hardware Impact: Low-tier saves roughly 2 channel-exp equivalents per affected pixel versus exact RGB exp; expected opaque pass saving depends on coverage.

## Decision 006 - Generic Fog Handling

Problem: The task demands eradication of generic fog, but raw scene/prefab YAML mutation outside ownership would be unsafe with concurrent agents.
Solution: Added `PostProcess_Fog_Scanner` and generated `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`. Current static scan found no `m_Fog: 1` tokens and three generic volume/profile tokens in scene/profile assets.
Rejected Alternatives: Blind deletion of `Volume` components from scenes; scene ownership and volume intent are not proven from text tokens.
Scalability potential: Scanner prevents reintroduction of camera-distance fog while UberNoir and compute fog carry continuous water optics.
Hardware Impact: Runtime 0 us. Editor scan only.

## Decision 007 - Human Control Facade

Problem: Artists need coefficient tuning and extinction preview without recompiling C# or flying a camera through a 100 km world.
Solution: Added `AbyssalOpticsTunerWindow` with UI Toolkit sliders/color field and a 64-swatch Beer-Lambert preview. The window writes through `WaterOpticsRuntime.ApplyEditorTuning`, which updates the Vault-backed tuning DTO.
Rejected Alternatives: ScriptableObject-only authoring or Gradient allocation; both bypass live Vault state or create unnecessary managed objects.
Scalability potential: Preview shows the same coefficient curve used by runtime; high-tier visual overkill can be tuned by increasing scattering/light intensity without changing authority.
Hardware Impact: Editor-only. Runtime path unchanged.

## Decision 008 - Biome Route Blocker

Problem: Task 09 requires biome-specific water color routing, but `Rendering/WaterOptics` cannot take a direct dependency on `Hecton8.World` or scrape shader globals without breaking one-owner/one-route authority.
Solution: Kept the water-optics profile hash API and CSV profile buffer, then blocked live biome routing pending a Core/Contracts payload. Evidence: `BiomeTransitionManagerRuntime` owns `CurrentAtmosphereDTO` and uploads absorption/hash into `H8BiomeTransitionPayload` plus legacy shader globals; `BiomeChangedSignal` and `BiomeGradientSignal` do not carry scattering or an explicit water-optics profile hash.
Rejected Alternatives: Direct `Hecton8.World` asmdef reference; reading `_H8BiomeTransitionAbsorption`/`_H8ExtinctionCoefficients` via `Shader.GetGlobalVector`; relying on undocumented shader-payload slot order from a route that is itself pending runtime proof.
Scalability potential: Once a contract exists, low tier can consume a dominant profile hash, middle tier can blend absorption/scattering, high tier can add directional light tint, and ultra can feed richer spectral/scattering coefficients without changing `WaterOpticsDTO`.
Hardware Impact: Current decision adds 0 us/frame. A future approved signal/DataVault route should cost one fixed payload read and one profile blend; direct shader-global reads were rejected because they would add hidden render-state coupling rather than deterministic data flow.

## Decision 009 - Verification Boundary

Problem: The prompt demands proof, but Unity import/shader import/profiler/GPU timestamp evidence is not available from static shell inspection, and premature `dotnet build` was explicitly forbidden unless needed.
Solution: Performed static structural verification only: targeted forbidden-token scans, brace/preprocessor balance, `git diff --check`, asmdef reference inspection, route-card/ledger updates, and self-audit log append. Kept compile/runtime checks marked pending instead of fabricating green proof.
Rejected Alternatives: Launching a guarded rebuild without a clear compile need; reporting runtime GPU microseconds from estimates as measured timestamps.
Scalability potential: Static source is ready for Unity import; runtime proof still must validate constant-buffer binding, shader import, GPU cost, and profiler allocation status across low/mid/high/ultra quality weights.
Hardware Impact: No frame cost. Avoided a speculative compile/load spike on the developer machine.

## Decision 010 - RenderGraph Telemetry Marker Hardening

Problem: The first Task 15 hardening draft used an unsafe RenderGraph pass and invoked the water-optics runtime from inside the render func. It also set the estimated GPU budget breach flag inside `RecordTelemetry` without returning it to the caller, so the dump gate could miss a budget fault.
Solution: Replaced the marker with a URP raster `AddRasterRenderPass` marker-only route, notified `WaterOpticsRuntime` before pass registration, and kept the render func to `BeginSample`/`EndSample` only. Added the missing `Unity.RenderPipelines.Core.Runtime` asmdef reference, returned final telemetry flags from `RecordTelemetry`, and guarded the editor telemetry graph before UI fields are built.
Rejected Alternatives: `AddUnsafePass`; storing a managed runtime reference in RenderGraph pass data; hot `GlobalRegistry` lookup from the renderer feature; claiming exact GPU timestamps from static source.
Scalability potential: Low quality records the same fixed 64-byte telemetry row while shader ALU collapses to scalar extinction. Middle/high/ultra quality records the continuously increased spectral weight without changing DTO layout, BufferID ownership, or render-pass identity.
Hardware Impact: Marker-only raster pass adds no draw and no per-frame managed allocation in the water-optics owner path. Telemetry write remains one 64-byte row/frame; estimated overhead target remains below the 0.1 ms suspicion threshold, but runtime profiler proof is still pending.

## Decision 011 - Renderer Feature Installer Instead Of YAML Surgery

Problem: A `ScriptableRendererFeature` source file alone does not guarantee the pass is present in `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, or `Quest_VR_Renderer`. Hand-editing renderer YAML during concurrent agent work risks corrupting `m_RendererFeatures` / `m_RendererFeatureMap` local IDs.
Solution: Added an editor-only `WaterOpticsRendererFeatureInstaller` and build guard under the water-optics editor assembly. It uses Unity serialization APIs to create one `HectonWaterOpticsTelemetryFeature` sub-asset per renderer, normalize duplicate references, rebuild `m_RendererFeatureMap`, and verify `AfterRenderingOpaques` plus marker enabled.
Rejected Alternatives: Manual YAML mutation; relying on a developer to remember adding the feature; runtime scene search or hot registry polling from RenderGraph.
Scalability potential: The installer binds one fixed marker feature across mobile, Quest, mid PC, and high PC renderers. Quality remains controlled by the existing continuous `GlobalQualityWeight` DTO field, not renderer asset variants.
Hardware Impact: Editor/build-time only. Runtime impact remains the marker-only pass and 64-byte telemetry row; no additional gameplay memory or rollback data is introduced.

## Decision 012 - Stable Unity Meta Identity

Problem: New WaterOptics folders, asmdefs, and C# source files existed without `.meta` files. If Unity generates those GUIDs locally, assembly identity and editor/tooling references become machine-dependent during concurrent integration.
Solution: Added deterministic `.meta` files for `Assets/_Project/Scripts/Rendering/WaterOptics`, its `Editor` folder, runtime/editor asmdefs, all new WaterOptics C# files, the Dear Lie shader, and the UberNoir warmup collection.
Rejected Alternatives: Letting Unity auto-generate GUIDs on first import; hand-editing renderer assets to force references before import proof.
Scalability potential: No runtime quality effect. Stable asset identity protects the compile wall and renderer-feature installation path across mobile, Quest, mid PC, and high PC imports.
Hardware Impact: 0 us/frame. Import/build stability only.

## Decision 013 - Quality Bias And Raw Telemetry Dump

Problem: The editor facade exposed absorption/scattering/light controls but did not expose the existing tuning DTO quality-bias lane requested as a human quality scalar. The telemetry dump also wrote fields through `BinaryWriter`, which was less direct than the black-box raw-row mandate.
Solution: Added a signed `Quality Bias` slider, stored it in `WaterOpticsTuningDTO.MaxDistanceQualityFlagsProfile.y`, consumed it as `saturate(GlobalQualityWeight + bias)`, and loaded it from CSV profiles. Replaced `BinaryWriter` telemetry output with a 32-byte unmanaged header plus raw `WaterOpticsTelemetryEntry` rows written oldest-to-newest through `ReadOnlySpan<byte>` over the native ring. Extended the layout validator to check the 32-byte dump header.
Rejected Alternatives: Adding a separate DTO field or shader variable for quality; it would change the 64-byte ABI. Keeping field-by-field dump writes; it weakens forensic row-stride proof.
Scalability potential: Low, middle, high, and ultra remain a continuous curve. Artists can bias the curve per profile without changing DTO layout, BufferID ownership, or rollback/save identity.
Hardware Impact: Runtime shader upload remains one 64-byte row. Dump path is fault-only and writes contiguous native rows instead of per-field serialization.

## Decision 014 - Cold Vault Bootstrap And Hot-Swap Rebind

Problem: The runtime originally cached `GlobalRegistry.DataVault` once in `OnEnable`. If the registry published the Vault after this owner enabled, the water-optics route could register with the dispatcher but never acquire the Vault buffers.
Solution: Added `TryColdBootstrapVault` and invoked it only from `Awake`, `OnEnable`, and `Start`; registered `IGlobalRegistryHotSwapListener` so `GlobalRegistryServiceSlot.DataVault` replacement releases old handles and rebinds new handles once. `PreSimulationTick`, `ScheduleSimulation`, and `VisualSyncTick` still use the cached `_vault` field only.
Rejected Alternatives: Polling `GlobalRegistry.DataVault` from dispatcher phases; adding a private persistent `NativeArray` fallback; completing a job to force same-frame buffer readiness.
Scalability potential: Low, middle, high, and ultra use the same Vault-backed DTO route. Late bootstrap changes readiness only, not quality curves, payload layout, or shader authority.
Hardware Impact: 0 us hot registry lookup cost. Cold lifecycle calls may reacquire six fixed Vault handles only during enable/start/service replacement.

## Decision 015 - CSV Bridge Versus Player Data Monolith

Problem: A cross-agent validation artifact flagged the water-optics runtime for loading text profile data from `StreamingAssets`. That would compete with the Data Monolith doctrine and add a fragile player-runtime text IO route.
Solution: Kept the zero-GC `ReadOnlySpan<byte>` CSV parser and editor/development cold/reload bridge, but removed the `Application.streamingAssetsPath` player text load. The tuner now exposes an explicit editor reload button. In player, profile data must arrive through the Vault/Data Monolith route or defaults/mock optics remain active.
Rejected Alternatives: Keeping runtime text CSV fallback; adding a managed ScriptableObject profile list; silently reading shader globals from biome systems.
Scalability potential: Designers still tune low/mid/high/ultra curves from CSV in editor, while player builds preserve one binary/static payload path once Data Monolith ownership exists.
Hardware Impact: Removes runtime text file probing in player. Editor cold/reload bridge cost is unchanged and outside frame hot paths.

## Decision 016 - Editor Static Route Array Reduction

Problem: The editor installer and scanner carried static `string[]` tables for renderer assets and scan roots. This is not gameplay-hot, but it adds avoidable managed state on editor reload and weakens the zero-allocation audit story.
Solution: Replaced those route tables with const path fields plus index-based selector methods. `AssetDatabase.FindAssets` still receives a transient array only during an explicit scanner invocation because the Unity API requires it. Build-guard diagnostics now use explicit `string.Concat` instead of `+` path concatenation.
Rejected Alternatives: Leaving editor static arrays as harmless; moving scanner routes into a ScriptableObject; manual renderer YAML mutation.
Scalability potential: No visual quality effect. Keeps water-optics tooling deterministic across mobile, Quest, mid PC, and high PC renderer assets without expanding runtime payload shape.
Hardware Impact: 0 us/runtime. Reduces editor domain-reload managed surface and keeps allocations confined to explicit editor operations.

## Decision 017 - Dispatcher Frame Provenance

Problem: WaterOptics owner-phase telemetry used `Time.frameCount` in `PreSimulationTick` and `VisualSyncTick`, and the RenderGraph marker also carried a Unity frame stamp only to prove marker submission.
Solution: Replaced owner-phase telemetry frame writes with `DispatcherTimingDTO.FrameId`. Reduced the RenderGraph callback to a marker-presence notification with a saturating counter, so the renderer feature no longer reads a Unity frame counter.
Rejected Alternatives: Leaving owner telemetry on `Time.frameCount`; inventing a custom frame counter inside WaterOptics; pushing dispatcher timing into the renderer feature through a new cross-phase singleton.
Scalability potential: No visual change. Low, middle, high, and ultra quality telemetry now share dispatcher provenance for the same 64-byte row shape.
Hardware Impact: Removes all `Time.frameCount` reads from the WaterOptics source scope; expected gain is sub-microsecond but tightens deterministic forensic alignment.

## Decision 018 - Direct DTO Memory Mutation

Problem: `GenerateMockWaterOpticsJob` wrote the final `WaterOpticsDTO` through `Output[0] = dto`. The DTO itself was raw-field and blittable, but the hot mutation path was still an indexed `NativeArray` assignment rather than the mandated direct memory mutation route.
Solution: Converted the job to `unsafe`, read tuning through `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` plus `UnsafeUtility.AsRef<WaterOpticsTuningDTO>`, and wrote the output row through `UnsafeUtility.AsRef<WaterOpticsDTO>` on the resolved output pointer.
Rejected Alternatives: Keeping the indexer because it compiles; adding C# properties/accessors; introducing a managed profile object before the Burst job.
Scalability potential: No visual curve change. The same low-to-ultra DTO shape is mutated directly and remains compatible with the constant-buffer upload lane.
Hardware Impact: Removes the indexer write path from the hot Burst job. Estimated gain is tiny for one row but it closes the CS1612/hidden-copy audit gap before the lane grows to blended profile arrays.

## Decision 019 - Legacy LUT Continuum

Problem: `Hecton_WaterExtinction.hlsl` still contained `_MATH_LOD_LOW`/`SHADER_API_MOBILE` compile-time LUT gating. Even if inherited from the older extinction path, it violates the continuous quality doctrine inside the water optics shader surface.
Solution: Removed the hardware/LOD macro split, declared `_ExtinctionLUT` unconditionally, and replaced the old preprocessor path with `H8WaterExtinctionLutBlendWeight(active)`, a smooth polynomial blend derived from `GlobalQualityWeight`.
Rejected Alternatives: Keeping a platform macro because mobile needs cheap shaders; replacing the LUT with a second shader keyword; deleting the old LUT route entirely.
Scalability potential: Low quality falls through the analytical Beer-Lambert path without a LUT fetch once blend weight is zero. Middle quality blends analytical and LUT extinction. High/ultra can bias toward the LUT without changing shader variant, DTO layout, or CBUFFER identity.
Hardware Impact: Low-tier avoids the texture lookup through a uniform branch when quality is below the LUT admission curve. High-tier keeps richer LUT bias. No new shader keyword or material variant.

## Decision 020 - Dirty Tuning And Direct Owner Rows

Problem: `PreSimulationTick` wrote the tuning DTO every frame through a managed `NativeArray` indexer even when authoring controls were unchanged. VisualSync and telemetry also still read/write one-row DTO lanes through indexers.
Solution: Added `_tuningDirty`, editor `OnValidate`, and forced writes only on cold bootstrap/profile/editor changes. Replaced hot owner row reads/writes for params, tuning, telemetry cursor, and telemetry rows with `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef<T>` helpers.
Rejected Alternatives: Leaving one-row indexers because cost is small; caching private NativeArrays; introducing a managed tuning object.
Scalability potential: Quality control remains in the same 64-byte tuning/params DTOs. Low-to-ultra visual behavior is unchanged, but owner publication now scales by actual authoring/profile changes instead of frame count.
Hardware Impact: Removes steady unchanged tuning row writes from `PRE_SIMULATION` and removes indexer row access from the VisualSync/telemetry owner path. Estimated gain is sub-microsecond on current one-row data, but it closes the hot-path mutation audit before profile blending widens.

## Decision 021 - Concrete CSV Tuning Artifact

Problem: Task 17 had a byte-level parser and editor reload path, but `Docs/water_optics_profiles.csv` was absent. That leaves human-control proof incomplete because the parser has no concrete authoring input in the checkout.
Solution: Added `Docs/water_optics_profiles.csv` with four bounded profiles: abyssal noir, red silt, glacial blue, and sulfur vent. Rows cover absorption, extinction multiplier, scattering, anisotropy, directional light, max distance, and signed quality bias without changing the 64-byte profile DTO ABI.
Rejected Alternatives: Shipping parser-only infrastructure; restoring player `StreamingAssets` text load; creating managed ScriptableObject profile assets.
Scalability potential: Low quality consumes the same profile coefficients through scalar extinction collapse; middle quality restores partial spectral correction; high/ultra use stronger spectral/LUT/scattering response. Profile quality bias is continuous and never changes BufferID ownership or rollback/save identity.
Hardware Impact: 0 us/player runtime. Editor cold bootstrap or explicit tuner reload performs bounded file IO into Vault scratch; frame hot paths remain unchanged.

## Decision 022 - Project Root Artifact Resolver

Problem: CSV reload and telemetry dump used `Directory.GetCurrentDirectory()` directly. Unity usually runs from the project root, but external tools in this workspace may run from `C:\hades`, which would point artifact IO at the wrong `Docs` directory.
Solution: Added a cold `ResolveProjectRoot()` helper that accepts the current directory only when it contains `Assets` and `ProjectSettings`; otherwise it checks a `Hecton8` child with the same proof. CSV reload and black-box dump now use this resolver.
Rejected Alternatives: Hard-coded absolute path; `Application.streamingAssetsPath`; silently writing dumps under the process CWD.
Scalability potential: No visual curve change. The same low-to-ultra optics profiles and telemetry proof route survive editor tooling, shell-driven automation, and Unity runtime invocation without changing payload identity.
Hardware Impact: 0 us/frame. The resolver executes only on editor CSV reload/cold attempt or fault dump.

## Decision 023 - Spectral Admission ALU Collapse

Problem: The previous compressed transmittance path returned low-quality mono output, but still computed spectral delta and vector reciprocal correction before lerping it away. That weakens Task 10 because survival quality should actually shed ALU, not only hide spectral color.
Solution: Added `SpectralAdmissionWeight = smooth01(saturate((quality - 0.28) * 1.3888889))` in opaque HLSL, volumetric compute, and telemetry. When admission is below epsilon, the shader returns mono transmittance immediately after the single scalar exponential-equivalent path. Above the admission floor, spectral correction grows continuously.
Rejected Alternatives: Hardware/platform keywords; `IsLowEndHardware` branches; keeping always-computed spectral correction; exact RGB exponential at all quality weights.
Scalability potential: Low quality is mono Beer-Lambert darkening with no vector spectral correction. Middle quality admits spectral correction gradually. High/ultra use the full correction plus LUT bias/scattering without changing DTO layout, BufferID ownership, or shader variant identity.
Hardware Impact: Low-tier pixel path avoids spectral delta, vector reciprocal, and spectral blend after the mono exponential. GPU microseconds remain estimated until profiler proof, but the source-level instruction path now matches the intended low-quality collapse.

## Decision 024 - Legacy LUT Inactive Fallback

Problem: Removing the `_MATH_LOD_LOW`/`SHADER_API_MOBILE` gate made LUT admission depend on `H8WaterOpticsQualityWeight()`. If `_GlobalWaterOptics` is inactive or unbound during import/material preview, that quality lane defaults to zero and can suppress the older extinction LUT unexpectedly.
Solution: `H8WaterExtinctionLutBlendWeight` now blends quality as `lerp(1.0, H8WaterOpticsQualityWeight(), H8WaterOpticsActive())`. Runtime water-optics continues to drive LUT admission; inactive/unbound water-optics preserves legacy LUT behavior under `_ExtinctionLUTParams`.
Rejected Alternatives: Reintroducing a shader keyword/platform macro; requiring the runtime CBUFFER for editor/import preview; deleting the legacy LUT route.
Scalability potential: Runtime low-to-ultra remains continuous under the water-optics DTO. Offline/editor fallback stays visually rich instead of failing dark/flat because a presentation CBUFFER is absent.
Hardware Impact: One scalar lerp in the legacy resolver. No new texture fetch, shader variant, material clone, or CPU upload.

## Decision 025 - Proxy Fog And Dear Lie Water Gate

Problem: Static shader audit found that the low-quality volumetric proxy path multiplied water extinction by active state only, and the Dear Lie composite applied mono tint screen-wide whenever water optics was active.
Solution: Added `WaterOpticsCameraUnderwaterGate()` in the compute shader and used it for proxy water extinction. The Dear Lie shader now gates final tint by `max(waterlineWeight, cameraUnderwaterGate)`, preserving waterline fade while preventing dry-screen tint.
Rejected Alternatives: Leaving `_GlobalWaterOptics.w` as the only gate; adding a binary platform keyword; sampling more depth data in the Dear Lie pass.
Scalability potential: Low quality remains a cheap proxy with one scalar gate. Middle/high/ultra still use the spectral/scattering route where available; no DTO, BufferID, or variant changes.
Hardware Impact: Adds one scalar `step`/`max` style gate and prevents visually expensive false positives. No new texture fetch, pass, or CPU state upload.

## Decision 026 - RenderGraph Marker And Read Accessors

Problem: The marker-only raster pass bound active color as write-only, which can make RenderGraph treat the target as overwritten even though the pass only emits markers. Public `TryRead*` accessors also resolved mutable Vault views through `TryResolveHandle`.
Solution: Changed the marker attachment to `AccessFlags.ReadWrite`. Added `TryRead<T>` wrapping `IDataVault.TryReadHandle` and routed `TryReadLatestParams`, `TryReadLatestTuning`, `TryReadLatestTelemetry`, and `TryReadTelemetryEntry` through it.
Rejected Alternatives: Write-only marker attachment; mutable Vault resolves from editor/UI read accessors; render func runtime calls.
Scalability potential: No visual curve change. Low-to-ultra telemetry/readback remains proof-only and does not alter payload ownership or shader quality behavior.
Hardware Impact: No draw cost. Read accessor purity prevents accidental mutation lanes; CPU cost is equivalent handle validation.

## Decision 027 - VisualSync Tiny Job And Renderer Auto-Mutation

Problem: The 64-byte mapped-buffer upload used a synchronous Burst `IJob.Run()` wrapper for a non-mathematical memcpy, and the renderer installer mutated shared renderer assets on editor domain reload.
Solution: Replaced the copy job wrapper with direct `UnsafeUtility.MemCpy` inside `VISUAL_SYNC` after `LockBufferForWrite`. Removed `[InitializeOnLoadMethod]` auto-install; renderer feature binding is now explicit menu action plus build-guard validation/install.
Rejected Alternatives: Keeping a same-frame tiny job for a single cache-line copy; hidden reload-time renderer asset mutation while many agents are active.
Scalability potential: Upload payload remains the same 64-byte CBUFFER for low/mid/high/ultra. Renderer binding no longer changes assets outside explicit user/build phases.
Hardware Impact: Removes one job wrapper from the per-frame upload path. Editor reload no longer performs shared renderer asset IO.

## Decision 028 - Legacy Vertex Extinction ALU Closure

Problem: The new water-optics fragment and compute paths collapsed to mono at low `GlobalQualityWeight`, but UberNoir still populated `input.extinctionColor` in the vertex path through the legacy analytical RGB extinction. At low quality, `H8UberNoirResolveExtinctionColor` returned that supposedly cheap value, so the fog tint could still carry spectral RGB work/color shift.
Solution: Patched `H8WaterExtinctionAnalyticalRgbByDepthMeters` to use the same spectral admission curve, `smooth01(saturate((quality - 0.28) * 1.3888889))`. It computes mono extinction first and only evaluates spectral RGB when the admission curve is above epsilon. The quality source falls back to 1.0 when `_GlobalWaterOptics` is inactive, preserving legacy preview/import behavior.
Rejected Alternatives: Leaving vertex/fog extinction outside Task 10; adding a shader keyword for low quality; deleting the legacy extinction lane that UberNoir still uses for fog tint/fallback.
Scalability potential: Low quality now uses mono extinction consistently through opaque, volumetric, Dear Lie, and legacy fog lanes. Middle/high/ultra gradually recover RGB spectral attenuation and LUT bias without DTO, BufferID, or shader variant changes.
Hardware Impact: Low-tier vertex/fog lane avoids RGB spectral exponent work below the admission floor. Fragment/compute savings remain the larger win; exact GPU microseconds still need Unity profiler proof.

## Decision 029 - Simulation Reachability And Hot Vault Allocation Fence

Problem: `WaterOpticsRuntime.GetDispatcherPhase()` returns `PRE_SIMULATION`, but the SystemDispatcher only invokes `ScheduleSimulation` for simulation-phase systems. That made `GenerateMockWaterOpticsJob` structurally unreachable. The same owner phase also called `EnsureVaultBuffers(clearExisting:false)` from `PreSimulationTick` and `ScheduleSimulation`, which can fall through to `GetGenerationHandle` when handles are missing or stale.
Solution: Added a cold-allocated `SimulationMockSystem` child registered at `DispatcherPhase.Simulation`; it delegates `ScheduleSimulation` to the owner while the owner remains responsible for dirty tuning publication in `PRE_SIMULATION`. Changed dispatcher hot phases to fail closed on `_vaultBootstrapped` and cached handle resolves instead of invoking grow-capable buffer acquisition. Cold `Awake/OnEnable/Start`, hot-swap replacement, and explicit editor reload remain the only handle acquisition/repair surfaces.
Rejected Alternatives: Moving the whole runtime to `Simulation` and mixing tuning publication with parameter generation; calling `GetGenerationHandle` from frame phases as a self-heal path; scheduling the mock job from `PreSimulationTick` and forcing a same-frame completion.
Scalability potential: Low quality still gets the emergency scalar/mono extinction DTO because the mock job is actually scheduled. Middle/high/ultra recover spectral correction, LUT bias, and directional scatter from the same DTO lane without changing BufferID ownership or dispatcher identity.
Hardware Impact: Restores one scheduled 1-row Burst job and removes hot generation-handle repair from frame phases. Expected CPU impact remains under the 0.1 ms suspicion threshold; measured dispatcher/profiler proof is still pending.

## Decision 030 - UberNoir Variant Continuum Closure

Problem: Static shader audit found compile-time binary variants still affecting the UberNoir route: `_MATH_LOD_LOW` selected nearest-only light-probe sampling, and `H8_UBERNOIR_SCREEN_REFRACTION` wrapped screen refraction behind a local shader feature. Both contradict the continuous quality mandate and expand shader variant surface.
Solution: Removed `_MATH_LOD_LOW` pragmas from UberNoir passes, deleted the stale `_MATH_LOD_LOW` warmup entry, and deleted the preprocessor branch in `Hecton_CustomLightProbeGrid.hlsl`; the grid now always starts from nearest sampling and admits trilinear sampling by the existing smooth quality curve. Removed the local screen-refraction shader feature and made the refraction include/function always compile, with early return controlled by `_UberNoirRefractionParams`, cavitation state, `H8UberNoirHighCostAllowed()`, and `GlobalQualityWeight`.
Rejected Alternatives: Keeping `_MATH_LOD_LOW` as a mobile shortcut; replacing it with another keyword; leaving cavitation refraction free to sample opaque texture when material blend is zero; deleting refraction entirely.
Scalability potential: Low quality exits before trilinear probe sampling and before screen-refraction texture sampling. Middle quality admits probe interpolation and refraction gradually. High/ultra use the richer paths without shader keyword churn or material variant dependence.
Hardware Impact: Reduces UberNoir variant count by removing one `_MATH_LOD_LOW` multi_compile from three passes, one stale warmup variant, and one local screen-refraction feature from the forward pass. Low-tier runtime cost remains guarded by uniform quality/material gates; measured shader import/GPU timing remains pending.

## Decision 031 - Shared Rendering Report Restoration

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` is a shared reporting artifact and had been upserted by neighboring agents; the current file no longer contained SHINOBU_265 water-optics evidence despite Task 19 claiming a report artifact.
Solution: Added a scoped `shinobu_265_water_optics` object without rewriting existing agent entries. The object records the WaterOptics runtime route, shader route, Vault BufferIDs, DTO byte sizes, fog scan verdict, binary-variant patch, telemetry route, rollback exclusion, and pending Unity proof. Validated the JSON with `ConvertFrom-Json`.
Rejected Alternatives: Overwriting the shared report; moving proof only to `LOG_SHINOBU_265.md`; ignoring report drift because earlier status had been true.
Scalability potential: Documentation-only. The report now tracks that low/mid/high/ultra quality behavior is controlled by continuous runtime gates rather than shader variants.
Hardware Impact: 0 us/frame. Static evidence artifact only.

## Decision 032 - Build Guard Boundary

Problem: The work now includes new asmdefs/C# source, but the generated solution/project files do not contain `Hecton8.Rendering.WaterOptics` or `Hecton8.Rendering.WaterOptics.Editor`. A `dotnet build Hecton8.slnx` before Unity import/project regeneration would not compile the new assembly surface and could still burn IO across the massive dirty workspace.
Solution: Checked process and CPU guard only: no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` processes were running, CPU averaged 29%, and `Hecton8.Rendering.WaterOptics*.csproj` was absent. Kept compile checks pending and did not launch build.
Rejected Alternatives: Running a misleading full solution build; manually editing generated csproj/slnx; invoking Unity import from shell without an explicit need and owner-safe window.
Scalability potential: No runtime effect. Protects iteration speed and compile-wall discipline while preserving honest pending verification.
Hardware Impact: Avoided a potentially broad solution build with no coverage of the new asmdefs.

## Decision 033 - Fog Scanner Shared Report Upsert

Problem: `PostProcess_Fog_Scanner` still wrote a standalone JSON object directly to `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`. Running the scanner would erase neighboring agent sections and also erase SHINOBU_265's richer runtime/shader/Vault evidence restored in the shared report.
Solution: Changed the scanner to build a scoped `shinobu_265_water_optics` section and upsert that section into the shared report. The upsert preserves existing root properties, handles replacement in the middle of the file by restoring a consumed trailing comma, and resolves the Unity project root before writing.
Rejected Alternatives: Full-file overwrite from the scanner; writing only a sidecar report; relying on the manually restored report object while leaving the editor menu command destructive.
Scalability potential: Documentation/editor-only. The report now preserves the low/mid/high/ultra continuous quality evidence after every scanner run instead of degrading to a narrow fog-token report.
Hardware Impact: 0 us/frame. The scanner runs only from editor menu/build tooling and does not touch runtime shader upload or dispatcher phases.

## Decision 034 - VisualSync Shader Buffer Allocation Fence

Problem: `VisualSyncTick` called `EnsureShaderParamsBuffers()`. If the constant-buffer pair was missing, invalidated, or released, the hot upload phase could allocate two `GraphicsBuffer.Target.Constant` objects during gameplay.
Solution: Renamed the route to `TryColdBootstrapShaderParamsBuffers()` and invoked it from `Awake`, `OnEnable`, and `Start`. `VisualSyncTick` now only checks `HasValidShaderParamsBuffers()` and records `TelemetryFlagUploadSkipped` when buffers are absent, without allocating or repairing GPU buffers in-frame.
Rejected Alternatives: Keeping hot self-heal allocation; releasing/reallocating buffers from VisualSync when `SystemInfo.supportsSetConstantBuffer` changes; adding a private managed fallback upload route.
Scalability potential: Low/mid/high/ultra still share the same 64-byte `_GlobalWaterOptics` CBUFFER and continuous quality curve. The change affects lifecycle ownership only, not shader math, DTO layout, or rollback/save identity.
Hardware Impact: Removes worst-case two `GraphicsBuffer` allocations from the VisualSync hot path. Normal steady-state upload remains one mapped 64-byte copy and one constant-buffer bind; runtime profiler proof remains pending.

## Decision 035 - Forbidden Token Source-Scan Hygiene

Problem: The scoped forbidden-token scan still matched the WaterOptics editor scanner because its report text mentioned the old removed keyword names as prose. That weakens automated absence proof even though the shader variants were gone.
Solution: Reworded the scanner output and current shared report to say `local binary variants` without embedding the removed token names in source or JSON evidence.
Rejected Alternatives: Maintaining an exception list for the scanner source; leaving static proof dependent on human interpretation of false-positive matches.
Scalability potential: Documentation/editor-only. The actual low/mid/high/ultra shader path remains controlled by continuous `GlobalQualityWeight`.
Hardware Impact: 0 us/frame. Verification hygiene only.

## Decision 036 - VisualSync Bandwidth Discipline

Problem: `VisualSyncTick` still called `LockBufferForWrite`, copied 64 bytes, and rebound `_GlobalWaterOptics` every visual-sync frame even when the DataVault `WaterOpticsDTO` was byte-equivalent to the last uploaded payload. That violates the project bandwidth discipline rule: do not upload unchanged GPU data.
Solution: Added `_lastUploadedDto` and `_hasUploadedDto` as value-type owner state, compared all four `float4` lanes with `math.all`, and skipped both mapped-buffer write and `Shader.SetGlobalConstantBuffer` when the DTO is unchanged and a valid active buffer already exists. Telemetry still records the frame with `TelemetryFlagUploadUnchanged`. Invalid numeric DTOs now dump telemetry and fail closed before GPU upload, preserving the last valid constant buffer.
Rejected Alternatives: Always rebinding because the row is only 64 bytes; adding a managed checksum/string route; repairing or rebinding GPU buffers from VisualSync when state is unchanged.
Scalability potential: Low, middle, high, and ultra quality keep the same DTO ABI and shader math. Static scenes or stable camera/quality periods now avoid redundant GPU traffic, while moving camera/quality/tuning frames still upload immediately without hysteresis or binary quality switches.
Hardware Impact: Unchanged frames avoid one mapped 64-byte write, one buffer unlock, and one global constant-buffer bind. Exact CPU/driver microseconds remain pending Unity profiler proof.

## Decision 037 - Shader CBUFFER ABI Order Validation

Problem: `WaterOpticsLayoutValidator` verified the 64-byte C# DTO layout and checked that shader graft tokens existed, but it did not prove that each direct shader consumer declared `_GlobalWaterOptics` in the same lane order as `WaterOpticsDTO`. A reordered shader lane would compile while silently swapping absorption, scattering, light, or quality data.
Solution: Added editor/static CBUFFER order validation for `Hecton_WaterExtinction.hlsl`, `Hecton_VolumetricFog.compute`, and `Hecton_VolumetricFog_DearLie.shader`. The validator now requires the exact lane order: absorption, scattering, directional light, quality/depth, bounded by `CBUFFER_START(_GlobalWaterOptics)` and `CBUFFER_END`.
Rejected Alternatives: Token-existence validation only; runtime shader-global probing; duplicating DTO fields into separate shader globals.
Scalability potential: Low, middle, high, and ultra all consume the same ABI. The check prevents quality and active-state lanes from being interpreted as optical coefficients, which would break continuous scalability without changing C# layout.
Hardware Impact: 0 us/frame. Editor/static validation only; it prevents a future runtime GPU-debug session caused by silent CBUFFER drift.

## Decision 038 - Profile Row Direct Memory

Problem: The hot params/tuning/telemetry paths used pointer row helpers, but the CSV profile route still wrote parsed `WaterOpticsProfileDTO` rows through `profiles[count++] = profile` and read them back through `profiles[i]` when applying a profile hash. That is cold/editor-facing today, but it is exactly the row-mutation pattern that becomes a hidden-copy risk when biome profile blending is unblocked.
Solution: Changed profile ingestion to resolve the native base pointer once and write each row with `UnsafeUtility.AsRef<WaterOpticsProfileDTO>(base + index * 64)`. Added `ReadProfileAt` for profile hash application so profile reads use the same fixed 64-byte stride.
Rejected Alternatives: Leaving cold profile indexers as acceptable; moving profile rows into managed objects; waiting for biome routing before fixing the profile lane.
Scalability potential: Low, middle, high, and ultra all still consume the same profile DTO shape. Future biome/profile blending can grow on a direct-memory row pattern instead of reintroducing indexer mutation.
Hardware Impact: 0 us/frame in the current mock/profile route. Editor/cold profile reload avoids managed row-indexer ambiguity and keeps the profile lane ready for Burst/data-local blending.

## Decision 039 - Meta Identity Documentation Reconciliation

Problem: The route card and binary payload ledger still described the Dear Lie shader meta as an older retained asset. Current filesystem evidence shows `Hecton_VolumetricFog_DearLie.shader` and its `.meta` are part of the new deterministic WaterOptics asset set, so the docs contradicted the repo state.
Solution: Rewrote both architecture statements to name deterministic metas for the WaterOptics folders, asmdefs, C# files, Dear Lie shader, and UberNoir warmup variant collection, with Unity import proof still pending.
Rejected Alternatives: Leaving stale wording because status/rationale were already correct; that would poison future audit reads after context compression.
Scalability potential: Documentation-only. Low, middle, high, and ultra shader behavior remains unchanged; proof text now matches the asset identity route that protects imports on every tier.
Hardware Impact: 0 us/frame. Prevents integration churn from false meta provenance, no runtime code or shader code changed.

## Decision 040 - RenderGraph Mutable Owner Leak Closure

Problem: `HectonWaterOpticsTelemetryFeature.RecordRenderGraph` pulled a mutable `WaterOpticsRuntime` reference through public `TryGetRuntimeInstance` to mark opaque-lane telemetry. The call was pure enough in practice, but it exposed an owner object to runtime render code and looked like a singleton read path rather than a narrow marker route.
Solution: Replaced the runtime feature call with `WaterOpticsRuntime.TryMarkRenderGraphTelemetrySubmitted()`, which gates the marker pass and mutates only the owner marker counter if a scene-local owner already exists. Wrapped `TryGetRuntimeInstance` in `UNITY_EDITOR` so the mutable instance facade is available only to the Abyssal Optics Tuner and editor diagnostics. Superseded by Decision 043, which removed this static marker mutator and the sticky runtime counter entirely.
Rejected Alternatives: Hot `GlobalRegistry` lookup from `RecordRenderGraph`; keeping a public mutable owner getter in player builds; adding another global service slot; passing a managed runtime reference into RenderGraph pass data.
Scalability potential: Low, middle, high, and ultra shader behavior is unchanged. The fixed RenderGraph marker remains proof-only while water optics quality still rides the 64-byte DTO and continuous `GlobalQualityWeight`.
Hardware Impact: No measurable frame-time change expected; the change removes one runtime mutable-owner read surface and keeps render code on a single narrow marker call.

## Decision 041 - Explicit Owner, No Tiny Job, Shader Bounds

Problem: Static audit found two architectural faults in the WaterOptics runtime: a hidden runtime-load path could create a scene GameObject owner, and the fallback/mock optics route scheduled one Burst job to write a single 64-byte DTO every simulation phase. Shader audit also found dry-screen Dear Lie tint and unbounded custom light-probe StructuredBuffer indexing from stale or invalid probe globals.
Solution: Removed the runtime-load/scene-load self-install path entirely; `WaterOpticsRuntime` must now be authored or explicitly bootstrapped by owner composition, and the editor/build guard fails if no authored runtime owner is serialized in `_Project` scenes/prefabs. Static GUID scan currently finds no owner placement, so scene/bootstrap authoring is an explicit blocker instead of a hidden runtime fallback. Replaced the one-row scheduled mock job with direct `PRE_SIMULATION` row mutation through `NativeArrayUnsafeUtility` and `UnsafeUtility.AsRef<T>`. Gated Dear Lie waterline tint/opacity by camera-underwater state and added finite scalar/vector checks plus `activeCount >= resolution^3` fail-closed validation before light-probe grid reads.
Rejected Alternatives: Keeping hidden scene owner creation as a cold convenience; keeping the one-row job because it was Burst-compiled; treating light-probe globals as trustworthy after variant cleanup; adding another shader keyword for above/below water.
Scalability potential: Low devices avoid job scheduling for one cache-line update and avoid invalid or dry-screen shader work. Middle/high/ultra still use the same DTO, CBUFFER, light-probe quality curve, and Dear Lie waterline curve without binary hardware branches.
Hardware Impact: Removes one per-frame job schedule for the fallback/mock route and one possible cold `GameObject` allocation path. Build-time owner validation is editor IO only. Shader changes add scalar finite/count gates before buffer reads and prevent undefined StructuredBuffer fetches; measured GPU timing remains pending Unity import/profiler proof.

## Decision 042 - Explicit Runtime Owner Authoring Menu

Problem: After removing runtime self-spawn, static GUID scan proved no `WaterOpticsRuntime` owner is serialized in `_Project` scenes/prefabs. Leaving only a build failure would be precise but not actionable for the scene owner.
Solution: Added `WaterOpticsRuntimeOwnerInstaller`, an editor-only manual menu route that opens `Assets/_Project/Scenes/00_BOOTSTRAP.unity`, resolves the existing `[BOOTSTRAPPER]` root without referencing a bootstrap assembly type, and attaches `WaterOpticsRuntime` through `Undo.AddComponent` only when deliberately invoked. The build guard failure now points to that exact menu.
Rejected Alternatives: Reintroducing runtime `GameObject` creation; shell-editing scene YAML; adding a direct dependency on bootstrap scene component types; silently auto-installing during editor domain reload or build verification.
Scalability potential: Runtime payload, shader CBUFFER, and quality curve are unchanged. Low devices still pay no owner-spawn cost, mid/high/ultra still receive the same authored owner path once scene composition is serialized.
Hardware Impact: 0 us/frame. Editor-only scene authoring route; no hot allocation, no dispatcher work, no shader work.

## Decision 043 - Shader OOB Guard And RenderGraph Mutation Removal

Problem: Follow-up shader/runtime audit found three current-source hazards: `H8WaterExtinctionSamplePackedRaw` addressed a non-existent 256^3 LUT using a 4096-wide atlas, while the actual `Data/Visuals/Water_Extinction_Matrix.bin` route is a 768x256 RHalf depth/turbidity/rgb matrix; `HectonWaterOpticsTelemetryFeature` still mutated WaterOptics runtime state through a static marker counter from `RecordRenderGraph`; and UberNoir/custom-light-probe StructuredBuffer reads trusted counts without a capacity proof. VisualSync also triggered fault dumps directly from the render-upload phase.
Solution: Corrected the LUT sampler to `texel = turbidityIndex * 3 + channel, depthIndex`, added `_ExtinctionLUT_TexelSize` dimension guards, kept the old wavelength wrapper as a channel mapper, removed `TryMarkRenderGraphTelemetrySubmitted`, removed the marker telemetry flag/counter, and left the RenderGraph pass as marker-only with no runtime owner call. Added `_UberNoirInstanceCapacity` so the instance-buffer variant reads only when offset/count fit inside a published capacity. Reinterpreted `_H8CustomLightProbeGridState.z` as published capacity/count and updated `InteriorGIProbeVolumeRuntime` to publish active count instead of DTO stride. Fault dumps are now requested in VisualSync and flushed in `PostSimulationTick` or shutdown, retrying if Vault rows are unavailable.
Rejected Alternatives: Keeping the 4096-wide LUT math because it happened to compile; querying texture size through C# every frame; reusing `_UberNoirInstanceParams.w` and destroying seed-bias semantics; leaving the static runtime marker as a "narrow" mutation; doing synchronous fault file IO inside VisualSync.
Scalability potential: Low quality still avoids the LUT fetch through the existing smooth quality admission, middle quality blends analytical/LUT extinction once texture dimensions prove valid, and high/ultra can use the richer LUT without undefined texture reads. Probe and instance-buffer richness remains controlled by runtime quality/material gates and now fails closed on missing capacity instead of reading arbitrary GPU rows.
Hardware Impact: Prevents OOB texture/StructuredBuffer reads on mobile and desktop GPUs. Normal-frame CPU cost adds one pending-dump bool check in post-simulation and no extra draw. Fault IO is moved out of VisualSync; exact Unity profiler proof remains pending.
