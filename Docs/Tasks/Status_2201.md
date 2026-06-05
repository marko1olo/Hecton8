# Status 2201 - Foam/Caustics/Underwater Activation Auditor

ID: 2201
Role: FOAM_CAUSTICS_UNDERWATER_ACTIVATION_AUDITOR
Mode: STATIC AUDIT ONLY
Unity slot: NOT TAKEN
Build/import/play mode: NOT RUN

## Mandates Loaded
- Fake-first is valid only when the fake remains premium: foam, caustics, haze, and particulates may be shader/decal/LUT/projected/GPU fakes, not flat filler.
- Surface, shoreline, shallow water, sky, Aegir, coastline, ocean surface, and photic shallows must remain bright/readable/premium; darkness belongs to depth/caves/storms/eclipses only.
- Static audit cannot claim runtime acceptance. Runtime acceptance requires screenshots plus Frame Debugger/profiler proof.
- Renderer feature or script existence is not activation proof. Runtime owner, serialized bindings, buffers, and camera route must be proven.
- Continuous `GlobalQualityWeight` governs fidelity/cadence/capacity, not gameplay truth or binary low/high switches.
- VFX must be bounded, pooled/GPU-owned where relevant, zero hot allocations, and no scene search/hot polling.
- Anything over 0.1 ms is suspicious without profiler proof; no fake metrics are allowed.
- Compact tier still needs water identity, visible foam/caustic/haze cues, and readable silhouettes.

## Static Verified Inventory
- `Assets/_Project/Data/*Renderer.asset` contain active `HectonDeferredCausticsFeature` in PC, PC_High, Mobile, and Quest_VR renderer assets.
- Static GUID search found no serialized `AbyssalDeferredCausticsRuntime`, `WaterOpticsRuntime`, `JacobianFoamGpuRuntime`, `HectonMarineSnowRenderer`, `HectonUnderwaterVisuals`, `HectonJacobianFoamRenderFeature`, or `HectonWaterOpticsTelemetryFeature` in searched scene/prefab/data assets.
- Crest `UnderwaterRenderer` is serialized and enabled in `Assets/_Project/Scenes/02_HECTON_WORLD.unity` with `_depthFogDensityFactor: 0.92`.
- Crest foam sim/input route is partially present: `_createFoamSim: 1`, active `Crest.RegisterFoamInput`, `_disableRenderer: 1`, mesh renderer disabled as expected for simulation input.
- Many authored foam/caustic/haze/speck mesh routes are inactive by GameObject or renderer state in `02_HECTON_WORLD.unity`.
- `MAT_H8_SurfaceCrestOcean_1428.mat` has `_Foam: 1`, `_FoamTexture` assigned, `_Caustics: 1`, and `_CausticsTexture` assigned; old Batch21 unresolved/default-slot findings remain risk, not current runtime proof.

## Status
STATIC VERIFIED. No runtime acceptance claim. No Unity/editor action performed.
