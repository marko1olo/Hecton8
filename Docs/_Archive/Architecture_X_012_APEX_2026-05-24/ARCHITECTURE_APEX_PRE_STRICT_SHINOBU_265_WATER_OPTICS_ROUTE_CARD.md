# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_265_WATER_OPTICS_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_265 Water Optics Route Card

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_265 / UBERNOIR_WATER_EXTINCTION_GRAFTER
Evidence class: STATIC_SOURCE / STATIC_DOC only

## Route ID

`SHINOBU_265_GLOBAL_WATER_OPTICS`

## Owner Domain

Echelon 7 Graphics and Rendering / Water Optics.

## Problem

UberNoir solid surfaces and volumetric fog need the same water absorption/scattering facts without per-frame managed globals, material mutation, or Unity post-processing fog ownership.

## Instrument

- `GlobalDataVault / IDataVault`
- `SystemDispatcher` producer in `PRE_SIMULATION`
- `SystemDispatcher` consumer in `VISUAL_SYNC`
- Global shader constant buffer `_GlobalWaterOptics`
- Cold-owned double `GraphicsBuffer.Target.Constant` pair for the 64-byte upload row
- 300-frame black-box telemetry ring
- URP RenderGraph raster marker feature `HectonWaterOpticsTelemetryFeature`
- Editor/build installer `WaterOpticsRendererFeatureInstaller`
- Stable Unity `.meta` identity for WaterOptics folders, asmdefs, C# sources, shader asset, and warmup variant collection

## Producer And Consumer Phase

- Cold bootstrap: `WaterOpticsRuntime` must be authored or explicitly bootstrapped by the owning scene/bootstrap composition. `WaterOpticsRuntimeOwnerInstaller` provides the manual editor route `Hecton8/Rendering/Water Optics/Install Runtime Owner In Bootstrap Scene`, which attaches the component to the existing `[BOOTSTRAPPER]` root in `Assets/_Project/Scenes/00_BOOTSTRAP.unity` only when deliberately invoked. The runtime caches `IDataVault` from `Awake/OnEnable/Start`, allocates generation handles, cold-acquires the double `_GlobalWaterOptics` constant-buffer pair when supported, registers the pre-simulation owner and visual-sync child dispatcher systems, and listens for `GlobalRegistryServiceSlot.DataVault` replacement to release/rebind fixed Vault handles without hot polling. There is no hidden runtime-load self-spawn or scene-load GameObject creation route.
- `PRE_SIMULATION`: writes owner tuning row into `ShinobuWaterOpticsTuning` only when tuning/profile/editor state is dirty, then writes the current fallback/mock `WaterOpticsDTO` row directly through `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef<T>`. Missing Vault/handle state fails closed and records telemetry; this phase does not call grow-capable buffer acquisition and does not schedule a one-row job.
- `VISUAL_SYNC`: verifies the already cold-owned `GraphicsBuffer.Target.Constant` pair, maps one buffer, copies one 64-byte DTO through direct `UnsafeUtility.MemCpy`, binds `_GlobalWaterOptics`, and records `TelemetryFlagUploadSkipped` instead of allocating or repairing GPU buffers if the pair is missing/invalid. Invalid numeric state or estimated budget breach requests a black-box dump; the file write is flushed from the owner phase instead of VisualSync.
- RenderGraph: `HectonWaterOpticsTelemetryFeature` injects a marker-only raster pass after opaques by default, binds active color as `AccessFlags.ReadWrite`, and does not call `WaterOpticsRuntime`, `GlobalRegistry`, or a static owner mutator from `RecordRenderGraph`. The render func only emits `BeginSample`/`EndSample`.
- Editor/build: `WaterOpticsRendererFeatureInstaller` ensures the feature exists in PC, PC_High, Mobile, and Quest renderer assets through Unity serialized object APIs only on explicit menu action or build preprocessor validation. It also fails validation when no authored `WaterOpticsRuntime` owner is serialized in `_Project` scenes/prefabs, so the explicit-owner requirement cannot silently regress. Current static GUID scan finds no authored owner, and scene/bootstrap placement remains an owner-review blocker. Domain reload no longer mutates renderer assets.
- Editor/development profile bridge: `Docs/water_optics_profiles.csv` is parsed into Vault profiles under `UNITY_EDITOR` during cold bootstrap or via the Abyssal Optics Tuner reload action. The file path resolves through a project-root guard that accepts either the Unity project current directory or a `Hecton8` child containing `Assets` and `ProjectSettings`. Player runtime text loading from `StreamingAssets` is not part of this route; production profile payloads must come from Data Monolith/Vault when that contract exists.

## Buffer IDs

- `71129` `ShinobuWaterOpticsTuning`
- `71135` `ShinobuWaterOpticsParams`
- `71136` `ShinobuWaterOpticsProfiles`
- `71137` `ShinobuWaterOpticsTelemetryRing`
- `71138` `ShinobuWaterOpticsTelemetryCursor`
- `71139` `ShinobuWaterOpticsCsvScratch`

All are owned by `SystemID.Vfx`. They are presentation/proof lanes, not rollback, save, or gameplay authority.

## Unity Asset Identity

Deterministic `.meta` files are present for the new WaterOptics runtime/editor folders, runtime/editor asmdefs, new C# source files, `Hecton_VolumetricFog_DearLie.shader`, and the UberNoir warmup variant collection. This prevents local GUID generation drift before Unity import. Unity import proof remains pending.

## Layout Proof

- `WaterOpticsDTO = 64`: `float4 AbsorptionCoefficientsRGB@0`, `float4 ScatteringCoefficientsRGB@16`, `float4 DirectionalLightColorAndIntensity@32`, `float4 QualityAndDepthLimits@48`.
- `WaterOpticsTuningDTO = 64`: three `float4` coefficient rows plus `float4 MaxDistanceQualityFlagsProfile@48`.
- `WaterOpticsProfileDTO = 64`.
- `WaterOpticsTelemetryEntry = 64`.

No `Pack=1`, no managed fields, no Unity object references.

## GlobalQualityWeight Behavior

`GlobalQualityWeight` is written into `QualityAndDepthLimits.x`. HLSL continuously blends from one scalar monochrome extinction approximation to spectral RGB extinction correction through `smooth01(saturate((quality - 0.28) * 1.3888889))`; below that admission floor, opaque, volumetric, and legacy UberNoir vertex/fog extinction lanes return mono transmittance before spectral correction ALU. The legacy extinction LUT path is no longer gated by removed math-LOD or platform shader macros; LUT influence is blended by a smooth quality curve that uses water-optics quality only when `_GlobalWaterOptics` is active, otherwise preserving legacy LUT admission and full spectral richness for editor/import previews. The LUT sampler now matches the actual `Data/Visuals/Water_Extinction_Matrix.bin` upload shape: 768x256 RHalf, `x = turbidityIndex * 3 + rgbChannel`, `y = depthIndex`, with `_ExtinctionLUT_TexelSize` dimension guards before sampling. UberNoir light-probe richness and screen refraction now use runtime quality/material gates instead of local binary shader variants, and the stale low-quality UberNoir warmup entry has been removed. DTO layout, BufferID ownership, and rollback/save authority do not change with quality.

## Accessor Purity

`TryReadLatestParams`, `TryReadLatestTuning`, and telemetry UI reads use `IDataVault.TryReadHandle` and copy a row out. They do not allocate, publish, search scenes, complete jobs, or mutate global state.

## Failure Route

Invalid values or estimated opaque budget breaches set telemetry fault flags. VisualSync sets a pending dump request, and `PostSimulationTick`/shutdown flushes a 32-byte unmanaged header plus fixed 64-byte telemetry rows to `Docs/AgentLogs/Dump_SHINOBU_265.bin` once per fault, oldest-to-newest from the circular cursor, using the same project-root guard as the CSV bridge. If Vault rows are unavailable, the request stays pending instead of being dropped. Exact runtime GPU timing proof remains pending.

## Rejected Alternatives

- `Shader.SetGlobalVector`: rejected for per-parameter global churn and missing 64-byte DTO proof.
- Unity post-processing fog volumes: rejected because camera-distance fog ignores water surface height and per-pixel travel distance.
- Direct Biome manager dependency: rejected until a contracts/signal route exists; mock/profile tuning owns current presentation data.
- Owner-local private `NativeArray`: rejected; persistent rows live in DataVault.
- Hot `GlobalRegistry.DataVault` polling from dispatcher phases: rejected; Vault acquisition is cold lifecycle/hot-swap only.
- Hot `EnsureVaultBuffers` repair from `PRE_SIMULATION`/`SIMULATION`: rejected; stale or missing handles fail closed until cold bootstrap or hot-swap replacement repairs ownership.
- Player runtime text CSV loading from `StreamingAssets`: rejected; `Docs/water_optics_profiles.csv` is an editor tuning bridge, not production payload authority.
- Removed math-LOD/platform shader split for water extinction LUT: rejected; quality selection is a runtime continuous scalar.
- Leaving UberNoir `input.extinctionColor` on the old RGB analytical exp path at low quality: rejected; legacy vertex/fog tint now uses mono-first spectral admission.
- UberNoir `_MATH_LOD_LOW` and `H8_UBERNOIR_SCREEN_REFRACTION` local variants: rejected; light-probe trilinear sampling and screen refraction now admit through runtime quality/material gates.
- Reload-time renderer feature auto-install: rejected under concurrent-agent asset safety; explicit menu/build guard remains.
- Synchronous 64-byte `IJob.Run()` upload wrapper: rejected as a non-mathematical tiny job; direct memcpy preserves the same 64-byte payload route.
- Runtime self-spawn through runtime-load hooks or scene-load callbacks: rejected; the water-optics owner must be explicit scene/bootstrap composition.
- Scheduled one-row mock optics job: rejected without profiler proof; the existing pre-simulation owner mutates the single 64-byte row directly.
- Hot `GraphicsBuffer` allocation/repair from `VISUAL_SYNC`: rejected; constant-buffer acquisition is cold lifecycle only and the upload phase fails closed on buffer loss.

## Proof Required Before GREEN

- Unity import / C# compile.
- Shader import.
- Frame Debugger or GPU capture showing `_GlobalWaterOptics` bound.
- GC/profiler proof for 0 B/frame on the upload path.
- Profiler proof that `H8 Water Optics Opaque Extinction` marker executes in the opaque lane.
- Unity asset proof that the installer serialized the feature into all target renderer assets.
- Unity import proof that the deterministic WaterOptics `.meta` GUIDs are accepted without asmdef/feature reference churn.
- Scanner artifact `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
